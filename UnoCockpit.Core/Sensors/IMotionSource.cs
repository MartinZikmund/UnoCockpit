namespace UnoCockpit.Core.Sensors;

public readonly record struct MotionSample(double LongitudinalMps2, double LateralMps2);

public interface IMotionSource
{
    bool IsSupported { get; }

    MotionSample? Latest { get; }
}
