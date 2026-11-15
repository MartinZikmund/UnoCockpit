using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using UnoCockpit.Core.Game;
using UnoCockpit.Core.Motion;
using UnoCockpit.Core.Storage;

namespace UnoCockpit.Core.ViewModels;

public sealed partial class ArcadeViewModel : ObservableObject
{
    public const string HighScoreKey = "arcade.highscore";

    private readonly IKeyValueStore _store;
    private bool _wasConnected;

    public ArcadeViewModel(Func<int, ChargeRunGame> factory, IKeyValueStore store)
    {
        _store = store;
        Game = factory(Environment.TickCount);
        HighScore = int.TryParse(store.Get(HighScoreKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out int saved) ? saved : 0;
    }

    public ChargeRunGame Game { get; }

    [ObservableProperty]
    public partial int Score { get; set; }

    [ObservableProperty]
    public partial double Battery { get; set; } = 100;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool IsOver { get; set; }

    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    public partial int HighScore { get; set; }

    public void Tick(TimeSpan dt, GameInput input, bool gamepadConnected)
    {
        if (_wasConnected && !gamepadConnected && Game.IsRunning)
        {
            IsPaused = true;
        }

        _wasConnected = gamepadConnected;

        if (IsPaused)
        {
            if (input.Start)
            {
                IsPaused = false;
            }

            return;
        }

        Game.Update(dt, input);
        Score = Game.Score;
        Battery = Game.Battery;
        IsRunning = Game.IsRunning;
        IsOver = Game.IsOver;

        if (Game.IsOver && Game.Score > HighScore)
        {
            HighScore = Game.Score;
            _store.Set(HighScoreKey, HighScore.ToString(CultureInfo.InvariantCulture));
        }
    }

    public void OnParkStateChanged(ParkState state)
    {
        if (state != ParkState.Parked && Game.IsRunning)
        {
            IsPaused = true;
        }
    }
}
