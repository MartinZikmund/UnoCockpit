namespace UnoCockpit.Core.Sensors;

/// <summary>Uses the real gamepad when one is connected, otherwise the keyboard / on-screen fallback.</summary>
public sealed class CompositeGamepadSource(IGamepadSource real, IGamepadSource fallback) : IGamepadSource
{
    public GamepadState Read() => real.Read() is { Connected: true } pad ? pad : fallback.Read();
}
