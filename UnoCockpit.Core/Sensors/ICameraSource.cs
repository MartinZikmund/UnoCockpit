namespace UnoCockpit.Core.Sensors;

public interface ICameraSource
{
    bool IsActive { get; }

    Task<CapabilityStatus> StartAsync(string videoElementId);

    void Stop();

    Task<byte[]?> SnapJpegAsync();
}
