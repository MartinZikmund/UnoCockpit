using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml.Media.Imaging;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

/// <summary>Cabin selfie: native camera preview, a 3-2-1 countdown, then a postcard stamped with real trip data.</summary>
public sealed partial class PhotoBoothView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(PhotoBoothViewModel), typeof(PhotoBoothView), new PropertyMetadata(null, (d, e) => ((PhotoBoothView)d).OnViewModelChanged(e.OldValue as PhotoBoothViewModel, e.NewValue as PhotoBoothViewModel)));

    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _remaining;

    public PhotoBoothView()
    {
        InitializeComponent();
        _countdown.Tick += OnCountdownTick;
    }

    public PhotoBoothViewModel? ViewModel
    {
        get => (PhotoBoothViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static Visibility Show(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ShowIdle(bool previewing, bool postcard) => Show(!previewing && !postcard);

    public static string StatusText(CapabilityStatus status) => status switch
    {
        CapabilityStatus.Denied => "Camera access was denied. Allow the camera for this site in the browser, then try again.",
        CapabilityStatus.Unsupported => BrowserJson.CameraRequirement + ".",
        CapabilityStatus.RequiresPark => "Park the car to use the camera.",
        _ => "Snap a selfie with the cabin camera and get a postcard stamped with where you are, the time and the weather outside.",
    };

    public static string Place(PostcardData? data) => data?.PlaceText ?? "";

    public static string Stamp(PostcardData? data) => data is null
        ? ""
        : string.Join("  ·  ", new[] { data.LocalTime.ToString("HH:mm · d MMM yyyy", CultureInfo.InvariantCulture), data.TemperatureText }.Where(s => s.Length > 0));

    private void OnViewModelChanged(PhotoBoothViewModel? oldVm, PhotoBoothViewModel? newVm)
    {
        if (oldVm is not null)
        {
            oldVm.PropertyChanged -= OnVmPropertyChanged;
        }

        if (newVm is not null)
        {
            newVm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PhotoBoothViewModel.LastSnapJpeg) && ViewModel?.LastSnapJpeg is { } jpeg)
        {
            BitmapImage bitmap = new();
            using MemoryStream stream = new(jpeg);
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            PostcardImage.Source = bitmap;
        }
        else if (e.PropertyName == nameof(PhotoBoothViewModel.IsPreviewing) && ViewModel?.IsPreviewing == false)
        {
            StopCountdown();
        }
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        _ = ViewModel?.StartCommand.ExecuteAsync(Camera.VideoElementId ?? "");
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        StopCountdown();
        ViewModel?.StopCommand.Execute(null);
    }

    private void OnShutter(object sender, RoutedEventArgs e)
    {
        if (_countdown.IsEnabled)
        {
            return;
        }

        _remaining = 3;
        CountdownText.Text = "3";
        _countdown.Start();
    }

    private void OnCountdownTick(object? sender, object e)
    {
        _remaining--;
        if (_remaining > 0)
        {
            CountdownText.Text = _remaining.ToString(CultureInfo.InvariantCulture);
            return;
        }

        StopCountdown();
        _ = ViewModel?.SnapCommand.ExecuteAsync(null);
    }

    private void StopCountdown()
    {
        _countdown.Stop();
        CountdownText.Text = "";
    }
}
