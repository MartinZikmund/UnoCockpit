namespace UnoCockpit.Core.Game;

/// <summary>Steer in [-1, 1]; Start begins or restarts a run.</summary>
public readonly record struct GameInput(double Steer, bool Start);

public enum EntityKind
{
    Bolt,
    Cone,
}

/// <summary>X runs across the road (0..1), Y down the screen (0 top, 1 bottom).</summary>
public readonly record struct Entity(EntityKind Kind, double X, double Y);
