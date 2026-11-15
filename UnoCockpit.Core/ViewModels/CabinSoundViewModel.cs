using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnoCockpit.Core.Motion;
using UnoCockpit.Core.Sensors;

namespace UnoCockpit.Core.ViewModels;

public sealed partial class CabinSoundViewModel(IMicSource mic) : ObservableObject
{
    public const float SilenceDb = -100;
    public static readonly TimeSpan ChallengeLength = TimeSpan.FromSeconds(5);
    private const float PeakDecayDbPerSecond = 15;

    private ParkState _park;
    private TimeSpan _challengeRemaining;

    public float[] Bands { get; } = new float[IMicSource.BandCount];

    [ObservableProperty]
    public partial CapabilityStatus Status { get; set; }

    [ObservableProperty]
    public partial bool IsListening { get; set; }

    [ObservableProperty]
    public partial float LevelDb { get; set; } = SilenceDb;

    [ObservableProperty]
    public partial float PeakDb { get; set; } = SilenceDb;

    [ObservableProperty]
    public partial bool IsChallengeRunning { get; set; }

    [ObservableProperty]
    public partial double ChallengeProgress { get; set; }

    [ObservableProperty]
    public partial float ChallengeScoreDb { get; set; } = SilenceDb;

    [ObservableProperty]
    public partial float BestScoreDb { get; set; } = SilenceDb;

    /// <summary>Raised after each tick that read new bands.</summary>
    public event EventHandler? BandsUpdated;

    [RelayCommand]
    private async Task StartAsync()
    {
        if (_park != ParkState.Parked)
        {
            Status = CapabilityStatus.RequiresPark;
            return;
        }

        CapabilityStatus status = await mic.StartAsync();
        if (_park != ParkState.Parked)
        {
            // The permission prompt was answered after the car pulled away.
            mic.Stop();
            Status = CapabilityStatus.RequiresPark;
            return;
        }

        Status = status;
        IsListening = Status == CapabilityStatus.Active;
    }

    [RelayCommand]
    private void Stop()
    {
        mic.Stop();
        IsListening = false;
        IsChallengeRunning = false;
        Array.Clear(Bands);
        LevelDb = PeakDb = SilenceDb;
    }

    [RelayCommand]
    private void StartChallenge()
    {
        if (!IsListening)
        {
            return;
        }

        _challengeRemaining = ChallengeLength;
        ChallengeScoreDb = SilenceDb;
        ChallengeProgress = 0;
        IsChallengeRunning = true;
    }

    public void Tick(TimeSpan dt)
    {
        if (!IsListening)
        {
            return;
        }

        if (mic.TryReadBands(Bands))
        {
            BandsUpdated?.Invoke(this, EventArgs.Empty);
        }

        float level = mic.ReadLevelDb();
        LevelDb = level;
        PeakDb = Math.Max(level, PeakDb - PeakDecayDbPerSecond * (float)dt.TotalSeconds);

        if (!IsChallengeRunning)
        {
            return;
        }

        ChallengeScoreDb = Math.Max(ChallengeScoreDb, level);
        _challengeRemaining -= dt;
        ChallengeProgress = Math.Clamp(1 - _challengeRemaining / ChallengeLength, 0, 1);
        if (_challengeRemaining <= TimeSpan.Zero)
        {
            IsChallengeRunning = false;
            BestScoreDb = Math.Max(BestScoreDb, ChallengeScoreDb);
        }
    }

    public void OnParkStateChanged(ParkState state)
    {
        _park = state;
        if (state == ParkState.Parked)
        {
            if (Status == CapabilityStatus.RequiresPark)
            {
                Status = CapabilityStatus.Unknown;
            }

            return;
        }

        if (mic.IsActive || IsListening)
        {
            Stop();
        }

        Status = CapabilityStatus.RequiresPark;
    }
}
