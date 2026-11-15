using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.ViewModels;
using UnoCockpit.Sensors.Browser;

namespace UnoCockpit.Views.Apps;

/// <summary>Diagnostics: every capability the browser exposes, with live values.</summary>
public sealed partial class SensorLabView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(SensorLabViewModel), typeof(SensorLabView), new PropertyMetadata(null));

    public SensorLabView() => InitializeComponent();

    public SensorLabViewModel? ViewModel
    {
        get => (SensorLabViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static Visibility Is(CapabilityStatus status, string expected) =>
        status == Enum.Parse<CapabilityStatus>(expected) ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility IsBlocked(CapabilityStatus status) =>
        status is CapabilityStatus.Denied or CapabilityStatus.RequiresPark ? Visibility.Visible : Visibility.Collapsed;

    public static string BlockedText(CapabilityStatus status) => status == CapabilityStatus.Denied ? "Denied" : "Park first";

    private async void OnCopy(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || !OperatingSystem.IsBrowser())
        {
            return;
        }

        bool copied = await Interop.CopyText(ViewModel.ToPlainText());
        CopyButton.Content = copied ? "Copied" : "Copy failed";
    }
}
