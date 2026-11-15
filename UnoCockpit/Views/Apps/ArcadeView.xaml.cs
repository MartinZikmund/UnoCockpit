using UnoCockpit.Core.Demo;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

public sealed partial class ArcadeView : UserControl
{
    public static readonly DependencyProperty ShellProperty = DependencyProperty.Register(
        nameof(Shell), typeof(ShellViewModel), typeof(ArcadeView), new PropertyMetadata(null));

    public ShellViewModel? Shell
    {
        get => (ShellViewModel?)GetValue(ShellProperty);
        set => SetValue(ShellProperty, value);
    }

    public static readonly DependencyProperty VirtualGamepadProperty = DependencyProperty.Register(
        nameof(VirtualGamepad), typeof(SimulatedGamepadSource), typeof(ArcadeView), new PropertyMetadata(null));

    public SimulatedGamepadSource? VirtualGamepad
    {
        get => (SimulatedGamepadSource?)GetValue(VirtualGamepadProperty);
        set => SetValue(VirtualGamepadProperty, value);
    }

    public ArcadeView() => InitializeComponent();
}
