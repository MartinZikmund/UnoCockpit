using Uno.UI.NativeElementHosting;

namespace UnoCockpit.Views;

/// <summary>Hosts a native HTML <c>&lt;video&gt;</c> element for the camera preview (WebAssembly only).</summary>
public sealed partial class CameraHost : ContentControl
{
    private readonly BrowserHtmlElement? _video;

    public CameraHost()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        if (OperatingSystem.IsBrowser())
        {
            _video = BrowserHtmlElement.CreateHtmlElement("video");
            _video.SetHtmlAttribute(("autoplay", ""), ("playsinline", ""), ("muted", ""));
            _video.SetCssStyle(
                ("width", "100%"),
                ("height", "100%"),
                ("object-fit", "cover"),
                ("transform", "scaleX(-1)"),
                ("border-radius", "16px"),
                ("background", "#000"));
            Content = _video;
        }
    }

    public string? VideoElementId => _video?.ElementId;
}
