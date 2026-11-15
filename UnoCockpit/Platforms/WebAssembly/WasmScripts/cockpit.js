// Browser sensor bridge for Uno Cockpit. C# binds to these via [JSImport("globalThis.Cockpit.<name>")].
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
            cameraStream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: "user", width: { ideal: 1280 }, height: { ideal: 720 } },
                audio: false
            });
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
        if (cameraStream) {
            cameraStream.getTracks().forEach(function (t) { t.stop(); });
            cameraStream = null;
        }
        globalThis.__cockpitCameraActive = false;
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
            speech: !!(globalThis.SpeechRecognition || globalThis.webkitSpeechRecognition),
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

    function locationSearch() {
        return globalThis.location ? globalThis.location.search : "";
    }

    return {
        geoGetCurrentJson: geoGetCurrentJson,
        mediaStartCamera: mediaStartCamera,
        mediaStopCamera: mediaStopCamera,
        probeJson: probeJson,
        locationSearch: locationSearch
    };
})();
