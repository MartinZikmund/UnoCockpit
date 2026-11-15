namespace UnoCockpit.Core.Sensors;

public enum CapabilityStatus
{
    Unknown,
    Available,
    Active,
    Denied,
    Unsupported,
    RequiresPark,
}

public sealed record CapabilityReport(string Id, string Name, CapabilityStatus Status, string Detail);

public interface ICapabilityProbe
{
    Task<IReadOnlyList<CapabilityReport>> ProbeAsync();
}
