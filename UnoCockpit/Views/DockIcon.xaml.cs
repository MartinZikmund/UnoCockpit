namespace UnoCockpit.Views;

/// <summary>A dock glyph with an accent underline under the active app.</summary>
public sealed partial class DockIcon : UserControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(DockIcon), new PropertyMetadata("", (d, e) => ((DockIcon)d).Icon.Glyph = (string)e.NewValue));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(DockIcon), new PropertyMetadata(false, (d, _) => ((DockIcon)d).UpdateActive()));

    public DockIcon()
    {
        InitializeComponent();
        UpdateActive();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private void UpdateActive()
    {
        Icon.Opacity = IsActive ? 1 : 0.55;
        Underline.Opacity = IsActive ? 1 : 0;
    }
}
