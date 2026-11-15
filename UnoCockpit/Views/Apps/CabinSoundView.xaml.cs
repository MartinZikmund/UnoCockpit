using System.Globalization;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

/// <summary>Microphone visualizer and the Clap-o-meter. Audio never leaves the browser.</summary>
public sealed partial class CabinSoundView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(CabinSoundViewModel), typeof(CabinSoundView), new PropertyMetadata(null, (d, e) => ((CabinSoundView)d).OnViewModelChanged(e.OldValue as CabinSoundViewModel, e.NewValue as CabinSoundViewModel)));

    // -60 dBFS and below reads as 0, full scale as 100: a friendlier number than raw decibels.
    private const float ScoreFloorDb = -60;

    public CabinSoundView() => InitializeComponent();

    public CabinSoundViewModel? ViewModel
    {
        get => (CabinSoundViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static Visibility Show(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Hide(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static bool Not(bool value) => !value;

    public static string Score(float db) => Math.Round(Math.Clamp((db - ScoreFloorDb) / -ScoreFloorDb * 100, 0, 100)).ToString("0", CultureInfo.InvariantCulture);

    public static string Db(float db) => string.Create(CultureInfo.InvariantCulture, $"{db:0} dBFS");

    public static string Best(float db) => db > CabinSoundViewModel.SilenceDb ? $"Best {Score(db)}" : "";

    public static string StatusText(CapabilityStatus status) => status switch
    {
        CapabilityStatus.Denied => "Microphone access was denied. Allow the microphone for this site in the browser, then try again.",
        CapabilityStatus.Unsupported => BrowserJson.CameraRequirement + ".",
        CapabilityStatus.RequiresPark => "Park the car to use the microphone.",
        _ => "See the cabin's sound as a live spectrum, then see who can max out the Clap-o-meter. Nothing is recorded or sent anywhere.",
    };

    private void OnViewModelChanged(CabinSoundViewModel? oldVm, CabinSoundViewModel? newVm)
    {
        if (oldVm is not null)
        {
            oldVm.BandsUpdated -= OnBands;
        }

        if (newVm is not null)
        {
            newVm.BandsUpdated += OnBands;
        }
    }

    private void OnBands(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm)
        {
            Spectrum.Update(vm.Bands);
        }
    }
}
