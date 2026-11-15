using UnoCockpit.Core.Game;

namespace UnoCockpit.Core.Tests.Game;

[TestClass]
public sealed class ChargeRunGameTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(100);

    private static ChargeRunGame Started(int seed = 1)
    {
        ChargeRunGame g = new(seed);
        g.Update(TimeSpan.Zero, new GameInput(0, Start: true));
        return g;
    }

    private static void Run(ChargeRunGame g, TimeSpan duration, double steer = 0)
    {
        for (TimeSpan t = TimeSpan.Zero; t < duration; t += Frame)
        {
            g.Update(Frame, new GameInput(steer, false));
        }
    }

    [TestMethod]
    public void NotRunning_UntilStart()
    {
        ChargeRunGame g = new(1);
        g.Update(TimeSpan.FromSeconds(1), new GameInput(0, false));

        Assert.IsFalse(g.IsRunning);
        Assert.AreEqual(100, g.Battery);

        g.Update(TimeSpan.Zero, new GameInput(0, true));
        Assert.IsTrue(g.IsRunning);
    }

    [TestMethod]
    public void Battery_Drains3PerSecond()
    {
        ChargeRunGame g = Started();
        g.SpawningEnabled = false;
        Run(g, TimeSpan.FromSeconds(1));

        Assert.AreEqual(97, g.Battery, 0.1);
    }

    [TestMethod]
    public void Bolt_Adds20_Capped()
    {
        ChargeRunGame g = Started();
        g.SpawningEnabled = false;
        Run(g, TimeSpan.FromSeconds(10));
        double before = g.Battery;
        List<EntityKind> collected = new();
        g.Collected += (_, k) => collected.Add(k);

        g.Spawn(new Entity(EntityKind.Bolt, g.CarX, ChargeRunGame.CarY));
        g.Update(TimeSpan.FromMilliseconds(1), new GameInput(0, false));

        Assert.AreEqual(Math.Min(100, before + 20), g.Battery, 0.1);
        CollectionAssert.AreEqual(new[] { EntityKind.Bolt }, collected);

        g.Spawn(new Entity(EntityKind.Bolt, g.CarX, ChargeRunGame.CarY));
        g.Spawn(new Entity(EntityKind.Bolt, g.CarX, ChargeRunGame.CarY));
        g.Update(TimeSpan.FromMilliseconds(1), new GameInput(0, false));
        Assert.AreEqual(100, g.Battery, 0.01);
    }

    [TestMethod]
    public void Bolt_AddsScore()
    {
        ChargeRunGame g = Started();
        g.SpawningEnabled = false;
        int before = g.Score;
        g.Spawn(new Entity(EntityKind.Bolt, g.CarX, ChargeRunGame.CarY));
        g.Update(TimeSpan.FromMilliseconds(1), new GameInput(0, false));

        Assert.IsGreaterThanOrEqualTo(before + 50, g.Score);
    }

    [TestMethod]
    public void Cone_Subtracts25_ThenInvulnerable1s()
    {
        ChargeRunGame g = Started();
        g.SpawningEnabled = false;
        g.Spawn(new Entity(EntityKind.Cone, g.CarX, ChargeRunGame.CarY));
        g.Update(TimeSpan.FromMilliseconds(1), new GameInput(0, false));
        Assert.AreEqual(75, g.Battery, 0.1);

        g.Spawn(new Entity(EntityKind.Cone, g.CarX, ChargeRunGame.CarY));
        g.Update(TimeSpan.FromMilliseconds(1), new GameInput(0, false));
        Assert.AreEqual(75, g.Battery, 0.1);
    }

    [TestMethod]
    public void BatteryZero_IsOver()
    {
        ChargeRunGame g = Started();
        g.SpawningEnabled = false;
        Run(g, TimeSpan.FromSeconds(40));

        Assert.IsTrue(g.IsOver);
        Assert.IsFalse(g.IsRunning);
        Assert.AreEqual(0, g.Battery);
    }

    [TestMethod]
    public void Start_AfterOver_Restarts()
    {
        ChargeRunGame g = Started();
        g.SpawningEnabled = false;
        Run(g, TimeSpan.FromSeconds(40));
        g.Update(TimeSpan.Zero, new GameInput(0, true));

        Assert.IsTrue(g.IsRunning);
        Assert.AreEqual(100, g.Battery);
        Assert.AreEqual(0, g.Score);
    }

    [TestMethod]
    public void Steer_ClampsToRoad()
    {
        ChargeRunGame g = Started();
        g.SpawningEnabled = false;
        Run(g, TimeSpan.FromSeconds(3), steer: 1);
        Assert.AreEqual(ChargeRunGame.RoadMaxX, g.CarX, 1e-9);

        Run(g, TimeSpan.FromSeconds(3), steer: -1);
        Assert.AreEqual(ChargeRunGame.RoadMinX, g.CarX, 1e-9);
    }

    [TestMethod]
    public void SameSeed_SameEntities()
    {
        ChargeRunGame a = Started(7);
        ChargeRunGame b = Started(7);
        Run(a, TimeSpan.FromSeconds(5));
        Run(b, TimeSpan.FromSeconds(5));

        CollectionAssert.AreEqual(a.Entities.ToList(), b.Entities.ToList());
        Assert.IsNotEmpty(a.Entities);
    }

    [TestMethod]
    public void SpeedFactor_Grows2PercentEvery10s()
    {
        ChargeRunGame g = Started();
        g.SpawningEnabled = false;
        g.Spawn(new Entity(EntityKind.Bolt, 0.5, -10));
        Run(g, TimeSpan.FromSeconds(20));

        Assert.AreEqual(1.04, g.SpeedFactor, 0.005);
    }
}
