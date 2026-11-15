using System.ComponentModel;
using System.Globalization;
using UnoCockpit.Core.Demo;
using UnoCockpit.Core.Game;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

/// <summary>Charge Run: gamepad, keyboard or the on-screen pads steer; sound effects play through the car speakers.</summary>
public sealed partial class ArcadeView : UserControl
{
    public static readonly DependencyProperty ShellProperty = DependencyProperty.Register(
        nameof(Shell), typeof(ShellViewModel), typeof(ArcadeView), new PropertyMetadata(null, (d, e) => ((ArcadeView)d).OnShellChanged(e.OldValue as ShellViewModel, e.NewValue as ShellViewModel)));

    public static readonly DependencyProperty VirtualGamepadProperty = DependencyProperty.Register(
        nameof(VirtualGamepad), typeof(SimulatedGamepadSource), typeof(ArcadeView), new PropertyMetadata(null));

    private const double BatteryBarWidth = 240;

    public ArcadeView() => InitializeComponent();

    public ShellViewModel? Shell
    {
        get => (ShellViewModel?)GetValue(ShellProperty);
        set => SetValue(ShellProperty, value);
    }

    public SimulatedGamepadSource? VirtualGamepad
    {
        get => (SimulatedGamepadSource?)GetValue(VirtualGamepadProperty);
        set => SetValue(VirtualGamepadProperty, value);
    }

    public static double BatteryWidth(double battery) => Math.Max(28, BatteryBarWidth * battery / 100);

    public static string Percent(double battery) => string.Create(CultureInfo.InvariantCulture, $"{Math.Ceiling(battery):0}%");

    public static Visibility ShowMenu(bool running, bool paused) => !running || paused ? Visibility.Visible : Visibility.Collapsed;

    public static string MenuTitle(bool over, bool paused) => paused ? "Paused" : over ? "Out of charge" : "Charge Run";

    public static string MenuBody(bool over, bool paused, int score) =>
        paused ? "Reconnect the controller or press Start to carry on."
        : over ? $"You scored {score}. Grab the green bolts to keep your battery up and dodge the cones."
        : "Steer with a gamepad, the arrow keys or the pads below. Grab bolts, dodge cones, don't run out of charge.";

    public static string Best(int highScore) => highScore > 0 ? $"Best {highScore}" : "";

    public static string StartText(bool over, bool paused) => paused ? "Resume" : over ? "Play again" : "Start";

    private void OnShellChanged(ShellViewModel? oldShell, ShellViewModel? newShell)
    {
        if (oldShell is not null)
        {
            oldShell.Arcade.PropertyChanged -= OnArcadeChanged;
        }

        if (newShell is not null)
        {
            Canvas.Game = newShell.Arcade.Game;
            newShell.Arcade.PropertyChanged += OnArcadeChanged;
        }
    }

    // The hub ticks the game every frame; the score changes with it, so this redraws once per frame while playing.
    private void OnArcadeChanged(object? sender, PropertyChangedEventArgs e) => Canvas.Invalidate();

    private void OnStart(object sender, RoutedEventArgs e) => Shell?.Arcade.Tick(TimeSpan.Zero, new GameInput(0, true), gamepadConnected: false);

    private void OnLeftPressed(object sender, PointerRoutedEventArgs e) => Steer(-1, (UIElement)sender, e);

    private void OnRightPressed(object sender, PointerRoutedEventArgs e) => Steer(1, (UIElement)sender, e);

    private void Steer(double x, UIElement pad, PointerRoutedEventArgs e)
    {
        pad.CapturePointer(e.Pointer);
        if (VirtualGamepad is { } v)
        {
            v.State = v.State with { X = x };
        }
    }

    private void OnPadReleased(object sender, PointerRoutedEventArgs e)
    {
        if (VirtualGamepad is { } v)
        {
            v.State = v.State with { X = 0 };
        }
    }
}
