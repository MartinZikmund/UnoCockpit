namespace UnoCockpit.Core.Sensors;

/// <summary>Steering axis in [-1, 1] plus the buttons the arcade uses.</summary>
public readonly record struct GamepadState(bool Connected, double X, double Y, bool A, bool B, bool Start);

public interface IGamepadSource
{
    GamepadState Read();
}
