using System.Globalization;
using UnoCockpit.Core.Motion;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

/// <summary>Glanceable trip view: the only mini-app that stays usable while driving.</summary>
public sealed partial class TripHudView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(TripViewModel), typeof(TripHudView), new PropertyMetadata(null, (d, e) => ((TripHudView)d).OnViewModelChanged(e.OldValue as TripViewModel, e.NewValue as TripViewModel)));

    private static readonly string[] Cardinals = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];

    public TripHudView() => InitializeComponent();

    public TripViewModel? ViewModel
    {
        get => (TripViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static string Cardinal(double deg) => Cardinals[(int)Math.Round(((deg % 360) + 360) % 360 / 45) % 8];

    public static string Degrees(double deg) => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(((deg % 360) + 360) % 360):0}°");

    public static string GText(GForce g) => string.Create(CultureInfo.InvariantCulture, $"{Math.Sqrt(g.LongitudinalG * g.LongitudinalG + g.LateralG * g.LateralG):0.00} g");

    public static string AccuracyText(double accuracyM) => accuracyM > 0
        ? string.Create(CultureInfo.InvariantCulture, $"GPS ±{accuracyM:0} m")
        : "Waiting for GPS";

    private void OnViewModelChanged(TripViewModel? oldVm, TripViewModel? newVm)
    {
        if (oldVm is not null)
        {
            oldVm.Refreshed -= OnRefreshed;
        }

        if (newVm is not null)
        {
            newVm.Refreshed += OnRefreshed;
        }
    }

    private void OnRefreshed(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm && Visibility == Visibility.Visible)
        {
            Breadcrumbs.Update(vm.Breadcrumbs, vm.HeadingDeg);
        }
    }
}
