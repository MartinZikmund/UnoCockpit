using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Core.Sensors;

/// <summary>Maps a spoken phrase (or several recognition alternatives, one per line) to a mini-app.</summary>
public static class VoiceCommands
{
    private static readonly (string Keyword, AppId App)[] Keywords =
    [
        ("arcade", AppId.Arcade), ("game", AppId.Arcade), ("play", AppId.Arcade),
        ("trip", AppId.Trip), ("hud", AppId.Trip), ("speed", AppId.Trip), ("drive", AppId.Trip),
        ("photo", AppId.Photo), ("selfie", AppId.Photo), ("camera", AppId.Photo),
        ("sound", AppId.Sound), ("clap", AppId.Sound), ("mic", AppId.Sound),
        ("lab", AppId.Lab), ("sensor", AppId.Lab),
    ];

    public static AppId? TryParse(string text)
    {
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach ((string keyword, AppId app) in Keywords)
            {
                if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return app;
                }
            }
        }

        return null;
    }
}
