using UnoCockpit.Core.Sensors;

namespace UnoCockpit.Core.Demo;

/// <summary>A gamepad whose state is set by other input (keyboard, on-screen pads).</summary>
public sealed class SimulatedGamepadSource : IGamepadSource
{
    public GamepadState State { get; set; }

    public GamepadState Read() => State;
}
