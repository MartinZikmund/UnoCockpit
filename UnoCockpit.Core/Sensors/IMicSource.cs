namespace UnoCockpit.Core.Sensors;

public interface IMicSource
{
    public const int BandCount = 32;

    bool IsActive { get; }

    Task<CapabilityStatus> StartAsync();

    void Stop();

    /// <summary>Fills <paramref name="bands"/> with <see cref="BandCount"/> levels in [0, 1].</summary>
    bool TryReadBands(Span<float> bands);

    /// <summary>Overall level in dBFS (0 is full scale, silence is about -100).</summary>
    float ReadLevelDb();
}
