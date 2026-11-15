using System.ComponentModel;
using UnoCockpit.Core.Motion;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.ViewModels;
using UnoCockpit.Sensors.Browser;
using UnoCockpit.Services;
using Windows.System;

namespace UnoCockpit;

public sealed partial class ShellPage : Page
{
    private const double InactiveOpacity = 0.3;

    private readonly HashSet<VirtualKey> _pressed = new();

    public ShellPage(ShellViewModel viewModel, Sources sources)
    {
        ViewModel = viewModel;
        Sources = sources;
        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Trip.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TripViewModel.HeadingDeg))
            {
                Car.HeadingDeg = ViewModel.Trip.HeadingDeg;
            }
        };

        KeyDown += OnKeyChanged;
        KeyUp += OnKeyChanged;
        Loaded += (_, _) => ApplyTheme();
    }

    public ShellViewModel ViewModel { get; }

    public Sources Sources { get; }

    public static Visibility Show(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static double OnOff(bool value) => value ? 1 : InactiveOpacity;

    public static bool IsActive(AppId active, string app) => active == Enum.Parse<AppId>(app);

    public static Visibility IsApp(AppId active, string app) => Show(IsActive(active, app));

    public static bool IsParked(ParkState state) => state != ParkState.Moving;

    public static double GearOpacity(ParkState state, string gear) =>
        (gear == "D") == (state == ParkState.Moving) ? 1 : InactiveOpacity;

    public static string ParkText(ParkState state) => state switch
    {
        ParkState.Moving => "Driving · games and camera are paused",
        ParkState.Parked => "Parked",
        _ => "Waiting for location…",
    };

    public static string AppTitle(AppId app) => app switch
    {
        AppId.Trip => "Trip",
        AppId.Arcade => "Charge Run",
        AppId.Photo => "Photo Booth",
        AppId.Sound => "Cabin Sound",
        AppId.Lab => "Sensor Lab",
        _ => "",
    };

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsDark))
        {
            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        RequestedTheme = ViewModel.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        if (OperatingSystem.IsBrowser())
        {
            Interop.SetPageBackground(ViewModel.IsDark ? "#0E0F11" : "#F4F4F4");
        }
    }

    private void OnDock(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string app })
        {
            ViewModel.OpenAppCommand.Execute(Enum.Parse<AppId>(app));
            Sources.Audio.Play("tap");
        }
    }

    private void OnThemeAuto(object sender, RoutedEventArgs e) => ViewModel.ThemeOverrideDark = null;

    private void OnThemeLight(object sender, RoutedEventArgs e) => ViewModel.ThemeOverrideDark = false;

    private void OnThemeDark(object sender, RoutedEventArgs e) => ViewModel.ThemeOverrideDark = true;

    private void OnToggleDemo(object sender, RoutedEventArgs e) => Sources.Location.ForceDemo = DemoItem.IsChecked;

    private void OnResetTrip(object sender, RoutedEventArgs e) => ViewModel.ResetTrip();

    private void OnVoice(object sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsBrowser())
        {
            Interop.SpeechStart();
        }
    }

    private void OnFullscreen(object sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsBrowser())
        {
            Interop.FullscreenToggle();
        }
    }

    // Arrow keys / A-D steer, Space or Enter is the A button: lets the arcade run without a controller.
    private void OnKeyChanged(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox)
        {
            return;
        }

        bool down = e.KeyStatus.IsKeyReleased is false;
        if (down)
        {
            _pressed.Add(e.Key);
        }
        else
        {
            _pressed.Remove(e.Key);
        }

        GamepadState state = Sources.VirtualGamepad.State;
        if (e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.A or VirtualKey.D)
        {
            bool left = _pressed.Contains(VirtualKey.Left) || _pressed.Contains(VirtualKey.A);
            bool right = _pressed.Contains(VirtualKey.Right) || _pressed.Contains(VirtualKey.D);
            state = state with { X = left == right ? 0 : left ? -1 : 1 };
        }
        else if (e.Key is VirtualKey.Space or VirtualKey.Enter)
        {
            state = state with { A = down };
        }

        Sources.VirtualGamepad.State = state;
    }
}
