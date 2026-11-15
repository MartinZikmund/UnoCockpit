using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnoCockpit.Core.Motion;
using UnoCockpit.Core.Sensors;

namespace UnoCockpit.Core.ViewModels;

public sealed record PostcardData(string PlaceText, DateTimeOffset LocalTime, string TemperatureText);

public sealed partial class PhotoBoothViewModel(ICameraSource camera, Func<PostcardData> postcardData) : ObservableObject
{
    private ParkState _park;

    [ObservableProperty]
    public partial CapabilityStatus Status { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewing { get; set; }

    [ObservableProperty]
    public partial bool HasPostcard { get; set; }

    [ObservableProperty]
    public partial byte[]? LastSnapJpeg { get; set; }

    [ObservableProperty]
    public partial PostcardData? LastPostcard { get; set; }

    [RelayCommand]
    private async Task StartAsync(string videoElementId)
    {
        if (_park != ParkState.Parked)
        {
            Status = CapabilityStatus.RequiresPark;
            return;
        }

        HasPostcard = false;
        CapabilityStatus status = await camera.StartAsync(videoElementId);
        if (_park != ParkState.Parked)
        {
            // The permission prompt was answered after the car pulled away.
            camera.Stop();
            Status = CapabilityStatus.RequiresPark;
            return;
        }

        Status = status;
        IsPreviewing = Status == CapabilityStatus.Active;
    }

    [RelayCommand]
    private async Task SnapAsync()
    {
        if (!IsPreviewing)
        {
            return;
        }

        byte[]? jpeg = await camera.SnapJpegAsync();
        if (jpeg is null)
        {
            return;
        }

        LastSnapJpeg = jpeg;
        LastPostcard = postcardData();
        HasPostcard = true;
        Stop();
    }

    [RelayCommand]
    private void Stop()
    {
        camera.Stop();
        IsPreviewing = false;
    }

    public void OnParkStateChanged(ParkState state)
    {
        _park = state;
        if (state == ParkState.Parked)
        {
            if (Status == CapabilityStatus.RequiresPark)
            {
                Status = CapabilityStatus.Unknown;
            }

            return;
        }

        if (camera.IsActive || IsPreviewing)
        {
            Stop();
        }

        Status = CapabilityStatus.RequiresPark;
    }
}
