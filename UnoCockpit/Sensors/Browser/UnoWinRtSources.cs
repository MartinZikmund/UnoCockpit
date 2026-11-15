using UnoCockpit.Core.Geo;
using UnoCockpit.Core.Sensors;
using Windows.Devices.Geolocation;
using Windows.Gaming.Input;

namespace UnoCockpit.Sensors.Browser;

/// <summary>Location through the WinRT <see cref="Geolocator"/> that Uno implements on WebAssembly.</summary>
internal sealed class UnoGeolocatorSource : ILocationSource
{
    private readonly Geolocator _locator = new() { DesiredAccuracyInMeters = 10 };

    public bool IsDemo => false;

    public async Task<LocationResult> GetAsync(CancellationToken ct)
    {
        try
        {
            Geoposition p = await _locator.GetGeopositionAsync(TimeSpan.Zero, TimeSpan.FromSeconds(5)).AsTask(ct);
            Geocoordinate c = p.Coordinate;
            return new(LocationStatus.Ok, new GeoSample(
                c.Timestamp,
                c.Point.Position.Latitude,
                c.Point.Position.Longitude,
                c.Point.Position.Altitude,
                c.Accuracy,
                c.Speed is { } s && !double.IsNaN(s) ? s : null,
                c.Heading is { } h && !double.IsNaN(h) ? h : null));
        }
        catch (UnauthorizedAccessException)
        {
            return LocationResult.Failed(LocationStatus.Denied);
        }
        catch (Exception)
        {
            return LocationResult.Failed(LocationStatus.Unavailable);
        }
    }
}

/// <summary>The first connected controller, through the WinRT <see cref="Gamepad"/> API that Uno implements on WebAssembly.</summary>
internal sealed class UnoGamepadSource : IGamepadSource
{
    private const double DeadZone = 0.15;

    public GamepadState Read()
    {
        if (Gamepad.Gamepads is not { Count: > 0 } pads)
        {
            return default;
        }

        GamepadReading r = pads[0].GetCurrentReading();
        double x = Math.Abs(r.LeftThumbstickX) > DeadZone ? r.LeftThumbstickX : 0;
        if (r.Buttons.HasFlag(GamepadButtons.DPadLeft))
        {
            x = -1;
        }
        else if (r.Buttons.HasFlag(GamepadButtons.DPadRight))
        {
            x = 1;
        }

        return new GamepadState(
            Connected: true,
            X: x,
            Y: r.LeftThumbstickY,
            A: r.Buttons.HasFlag(GamepadButtons.A),
            B: r.Buttons.HasFlag(GamepadButtons.B),
            Start: r.Buttons.HasFlag(GamepadButtons.Menu));
    }
}
