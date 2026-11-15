namespace UnoCockpit.Core.Motion;

public enum ParkState
{
    Unknown,
    Parked,
    Moving,
}

/// <summary>Decides parked vs moving from GPS speed, with hysteresis so jitter doesn't flap the state.</summary>
public sealed class ParkDetector
{
    public const double ParkedBelowMps = 2 / 3.6;
    public const double MovingAboveMps = 5 / 3.6;
    public static readonly TimeSpan ParkedAfter = TimeSpan.FromSeconds(5);

    /// <summary>Consecutive fast samples needed to leave Parked; one GPS speed spike isn't driving away.</summary>
    public const int FastSamplesToLeaveParked = 2;

    private DateTimeOffset? _slowSince;
    private int _fastCount;

    public ParkState State { get; private set; }

    public event EventHandler<ParkState>? StateChanged;

    public void Update(DateTimeOffset t, double? speedMps)
    {
        if (speedMps is not { } speed)
        {
            return;
        }

        if (speed > MovingAboveMps)
        {
            _slowSince = null;
            _fastCount++;
            if (State != ParkState.Parked || _fastCount >= FastSamplesToLeaveParked)
            {
                SetState(ParkState.Moving);
            }

            return;
        }

        _fastCount = 0;
        if (speed < ParkedBelowMps)
        {
            _slowSince ??= t;
            if (t - _slowSince.Value >= ParkedAfter)
            {
                SetState(ParkState.Parked);
            }
        }
        else
        {
            _slowSince = null;
        }
    }

    public void ForceState(ParkState state)
    {
        _slowSince = null;
        _fastCount = 0;
        SetState(state);
    }

    private void SetState(ParkState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
    }
}
