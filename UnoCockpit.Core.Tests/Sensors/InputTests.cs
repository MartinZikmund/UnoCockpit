using UnoCockpit.Core.Demo;
using UnoCockpit.Core.Sensors;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Core.Tests.Sensors;

[TestClass]
public sealed class InputTests
{
    [TestMethod]
    [DataRow("open arcade", AppId.Arcade)]
    [DataRow("Let's play a game", AppId.Arcade)]
    [DataRow("open the trip", AppId.Trip)]
    [DataRow("take a selfie", AppId.Photo)]
    [DataRow("photo booth", AppId.Photo)]
    [DataRow("clap meter", AppId.Sound)]
    [DataRow("show sensor lab", AppId.Lab)]
    public void Voice_Parses(string phrase, AppId expected) => Assert.AreEqual(expected, VoiceCommands.TryParse(phrase));

    [TestMethod]
    public void Voice_Unknown_IsNull() => Assert.IsNull(VoiceCommands.TryParse("what's the weather"));

    [TestMethod]
    public void Voice_FirstMatchingAlternativeWins() =>
        Assert.AreEqual(AppId.Lab, VoiceCommands.TryParse("blah\nopen lab\nopen arcade"));

    [TestMethod]
    public void Composite_PrefersConnectedPad()
    {
        SimulatedGamepadSource real = new() { State = new(true, 0.5, 0, true, false, false) };
        SimulatedGamepadSource fallback = new() { State = new(false, -1, 0, false, false, true) };

        Assert.AreEqual(real.State, new CompositeGamepadSource(real, fallback).Read());
    }

    [TestMethod]
    public void Composite_FallsBackWhenPadMissing()
    {
        SimulatedGamepadSource real = new();
        SimulatedGamepadSource fallback = new() { State = new(false, -1, 0, false, false, true) };

        Assert.AreEqual(fallback.State, new CompositeGamepadSource(real, fallback).Read());
    }
}
