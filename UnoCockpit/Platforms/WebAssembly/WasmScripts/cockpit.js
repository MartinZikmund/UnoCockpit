// Browser sensor bridge for Uno Cockpit. C# binds to these via [JSImport("globalThis.Cockpit.<name>")].
// Each section is self-contained so it's easy to read alongside its C# source in Sensors/Browser.
globalThis.Cockpit = (function () {
    "use strict";

    // ---------- Location ----------
    // Tesla's browser has historically failed watchPosition, so C# polls getCurrentPosition once a second.
    function geoGetCurrentJson(timeoutMs) {
        return new Promise(function (resolve) {
            if (!("geolocation" in navigator)) {
                resolve(JSON.stringify({ ok: false, code: "unsupported" }));
                return;
            }
            navigator.geolocation.getCurrentPosition(
                function (p) {
                    var c = p.coords;
                    resolve(JSON.stringify({
                        ok: true, lat: c.latitude, lon: c.longitude, alt: c.altitude, acc: c.accuracy,
                        speed: c.speed, heading: c.heading, ts: p.timestamp
                    }));
                },
                function (e) { resolve(JSON.stringify({ ok: false, code: e.code })); },
                { enableHighAccuracy: true, timeout: timeoutMs, maximumAge: 0 });
        });
    }

    // ---------- Camera ----------
    var cameraStream = null;
    var cameraGeneration = 0;
    globalThis.__cockpitCameraActive = false;

    function mediaErrorToStatus(e) {
        if (e && e.name === "NotAllowedError") { return "denied"; }
        if (e && (e.name === "NotFoundError" || e.name === "OverconstrainedError")) { return "unsupported"; }
        return "error:" + (e && e.message ? e.message : e);
    }

    async function mediaStartCamera(videoElementId) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { return "unsupported"; }
        try {
            mediaStopCamera();
            var generation = cameraGeneration;
            var stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: "user", width: { ideal: 1280 }, height: { ideal: 720 } },
                audio: false
            });
            // Stopped while the permission prompt was open: don't resurrect the stream.
            if (generation !== cameraGeneration) {
                stream.getTracks().forEach(function (t) { t.stop(); });
                return "stopped";
            }
            cameraStream = stream;
            var video = document.getElementById(videoElementId);
            if (video) {
                video.srcObject = cameraStream;
                await video.play().catch(function () { });
            }
            globalThis.__cockpitCameraActive = true;
            return "ok";
        } catch (e) {
            return mediaErrorToStatus(e);
        }
    }

    function mediaStopCamera() {
        cameraGeneration++;
        if (cameraStream) {
            cameraStream.getTracks().forEach(function (t) { t.stop(); });
            cameraStream = null;
        }
        globalThis.__cockpitCameraActive = false;
    }

    // Grabs the current video frame, mirrored like the preview, as base64 JPEG (max 1280 px wide).
    function snapJpegBase64(videoElementId) {
        var video = document.getElementById(videoElementId);
        if (!video || !video.videoWidth) { return Promise.resolve(""); }
        var scale = Math.min(1, 1280 / video.videoWidth);
        var canvas = document.createElement("canvas");
        canvas.width = Math.round(video.videoWidth * scale);
        canvas.height = Math.round(video.videoHeight * scale);
        var ctx = canvas.getContext("2d");
        ctx.translate(canvas.width, 0);
        ctx.scale(-1, 1);
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        return Promise.resolve(canvas.toDataURL("image/jpeg", 0.9).split(",")[1] || "");
    }

    // ---------- Microphone ----------
    var mic = null;
    var micGeneration = 0;
    globalThis.__cockpitMicActive = false;

    async function mediaStartMic() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { return "unsupported"; }
        try {
            mediaStopMic();
            var generation = micGeneration;
            var stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: false, noiseSuppression: false }, video: false });
            if (generation !== micGeneration) {
                stream.getTracks().forEach(function (t) { t.stop(); });
                return "stopped";
            }
            var context = new (globalThis.AudioContext || globalThis.webkitAudioContext)();
            var analyser = context.createAnalyser();
            analyser.fftSize = 2048;
            analyser.smoothingTimeConstant = 0.6;
            context.createMediaStreamSource(stream).connect(analyser);
            mic = {
                stream: stream, context: context, analyser: analyser,
                freq: new Float32Array(analyser.frequencyBinCount),
                time: new Float32Array(analyser.fftSize)
            };
            globalThis.__cockpitMicActive = true;
            return "ok";
        } catch (e) {
            return mediaErrorToStatus(e);
        }
    }

    function mediaStopMic() {
        micGeneration++;
        if (mic) {
            mic.stream.getTracks().forEach(function (t) { t.stop(); });
            mic.context.close();
            mic = null;
        }
        globalThis.__cockpitMicActive = false;
    }

    // 32 log-spaced bands from 60 Hz to 12 kHz, each mapped from -100..-20 dB to 0..1.
    function micBands() {
        var bands = new Array(32).fill(0);
        if (!mic) { return bands; }
        mic.analyser.getFloatFrequencyData(mic.freq);
        var hzPerBin = mic.context.sampleRate / mic.analyser.fftSize;
        for (var b = 0; b < 32; b++) {
            var lo = 60 * Math.pow(200, b / 32);
            var hi = 60 * Math.pow(200, (b + 1) / 32);
            var from = Math.max(1, Math.floor(lo / hzPerBin));
            var to = Math.max(from + 1, Math.ceil(hi / hzPerBin));
            var max = -140;
            for (var i = from; i < to && i < mic.freq.length; i++) { max = Math.max(max, mic.freq[i]); }
            bands[b] = Math.min(1, Math.max(0, (max + 100) / 80));
        }
        return bands;
    }

    function micLevelDb() {
        if (!mic) { return -100; }
        mic.analyser.getFloatTimeDomainData(mic.time);
        var sum = 0;
        for (var i = 0; i < mic.time.length; i++) { sum += mic.time[i] * mic.time[i]; }
        var rms = Math.sqrt(sum / mic.time.length);
        return rms > 0 ? Math.max(-100, 20 * Math.log10(rms)) : -100;
    }

    // ---------- Motion ----------
    // Only counts as supported once a real event with data arrives; desktop Chrome has the API but never fires.
    var motion = { seen: false, lon: 0, lat: 0, ts: 0 };
    if ("DeviceMotionEvent" in globalThis) {
        globalThis.addEventListener("devicemotion", function (e) {
            var a = e.acceleration;
            if (!a || a.x === null || a.z === null) { return; }
            // Screen faces the cabin: screen X is lateral, screen Z points back at the driver.
            motion = { seen: true, lon: -a.z, lat: a.x, ts: Date.now() };
        });
    }

    function motionLatestJson() {
        return JSON.stringify(motion);
    }

    // ---------- Sound effects (Web Audio, through the car speakers) ----------
    var audioContext = null;
    var cues = {
        bolt: [[880, 0.06], [1320, 0.09]],
        cone: [[160, 0.18]],
        start: [[440, 0.08], [660, 0.08], [880, 0.12]],
        over: [[440, 0.15], [330, 0.15], [220, 0.3]],
        tap: [[1200, 0.03]]
    };

    function audioPlay(cue) {
        var notes = cues[cue];
        if (!notes) { return; }
        try {
            audioContext = audioContext || new (globalThis.AudioContext || globalThis.webkitAudioContext)();
            var t = audioContext.currentTime;
            notes.forEach(function (n) {
                var osc = audioContext.createOscillator();
                var gain = audioContext.createGain();
                osc.type = cue === "cone" ? "sawtooth" : "triangle";
                osc.frequency.value = n[0];
                gain.gain.setValueAtTime(0.0001, t);
                gain.gain.exponentialRampToValueAtTime(0.25, t + 0.01);
                gain.gain.exponentialRampToValueAtTime(0.0001, t + n[1]);
                osc.connect(gain).connect(audioContext.destination);
                osc.start(t);
                osc.stop(t + n[1] + 0.02);
                t += n[1];
            });
        } catch (e) {
            // Audio is a nicety; never break the app over it.
        }
    }

    // ---------- Fullscreen, page chrome, clipboard ----------
    function fullscreenToggle() {
        if (document.fullscreenElement) {
            document.exitFullscreen().catch(function () { });
        } else if (document.documentElement.requestFullscreen) {
            document.documentElement.requestFullscreen().catch(function () { });
        }
    }

    function setPageBackground(color) {
        document.body.style.background = color;
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) { meta.setAttribute("content", color); }
    }

    function copyText(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text).then(function () { return true; }, function () { return false; });
        }
        return Promise.resolve(false);
    }

    function isOnline() {
        return navigator.onLine !== false;
    }

    // ---------- Voice commands (Web Speech API, if the browser has it) ----------
    var speechQueue = [];
    var recognition = null;

    function speechSupported() {
        return !!(globalThis.SpeechRecognition || globalThis.webkitSpeechRecognition);
    }

    function speechStart() {
        if (!speechSupported()) { return; }
        try {
            if (recognition) { recognition.abort(); }
            var Ctor = globalThis.SpeechRecognition || globalThis.webkitSpeechRecognition;
            recognition = new Ctor();
            recognition.lang = navigator.language || "en-US";
            recognition.maxAlternatives = 3;
            recognition.onresult = function (e) {
                for (var i = 0; i < e.results[0].length; i++) { speechQueue.push(e.results[0][i].transcript); }
            };
            recognition.start();
        } catch (e) {
            // Unsupported in this browser build.
        }
    }

    // C# polls this; returns "" when nothing new was heard.
    function speechTake() {
        return speechQueue.length ? speechQueue.splice(0, speechQueue.length).join("\n") : "";
    }

    // ---------- Capability probe ----------
    function webglRenderer() {
        try {
            var gl = document.createElement("canvas").getContext("webgl");
            if (!gl) { return null; }
            var ext = gl.getExtension("WEBGL_debug_renderer_info");
            return ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
        } catch (e) {
            return null;
        }
    }

    function probeJson() {
        var conn = navigator.connection;
        return JSON.stringify({
            userAgent: navigator.userAgent,
            secureContext: globalThis.isSecureContext === true,
            geolocation: "geolocation" in navigator,
            gamepad: typeof navigator.getGamepads === "function",
            mediaDevices: !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia),
            deviceMotion: "DeviceMotionEvent" in globalThis,
            deviceOrientation: "DeviceOrientationEvent" in globalThis,
            speech: speechSupported(),
            online: navigator.onLine,
            connectionType: conn ? conn.effectiveType : null,
            battery: typeof navigator.getBattery === "function",
            fullscreen: document.fullscreenEnabled === true,
            wakeLock: "wakeLock" in navigator,
            hardwareConcurrency: navigator.hardwareConcurrency || null,
            deviceMemory: navigator.deviceMemory || null,
            webgl: webglRenderer(),
            sinceNavigationMs: Math.round(performance.now())
        });
    }

    function browserLanguage() {
        return navigator.language || "";
    }

    function locationSearch() {
        return globalThis.location ? globalThis.location.search : "";
    }

    return {
        geoGetCurrentJson: geoGetCurrentJson,
        mediaStartCamera: mediaStartCamera,
        mediaStopCamera: mediaStopCamera,
        snapJpegBase64: snapJpegBase64,
        mediaStartMic: mediaStartMic,
        mediaStopMic: mediaStopMic,
        micBands: micBands,
        micLevelDb: micLevelDb,
        motionLatestJson: motionLatestJson,
        audioPlay: audioPlay,
        fullscreenToggle: fullscreenToggle,
        setPageBackground: setPageBackground,
        copyText: copyText,
        isOnline: isOnline,
        speechSupported: speechSupported,
        speechStart: speechStart,
        speechTake: speechTake,
        probeJson: probeJson,
        browserLanguage: browserLanguage,
        locationSearch: locationSearch
    };
})();
