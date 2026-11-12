using LastTown.Core;
using NUnit.Framework;

public class CoreTests
{
    [Test]
    public void ProductionAccumulates()
    {
        var g = GameState.NewGame(0);
        g.Get(BuildingType.Farm).level = 1;
        double f0 = g.res.food;
        g.Tick(10);
        Assert.AreEqual(f0 + 20, g.res.food, 0.01);
    }

    [Test]
    public void UpgradeCompletesAndRaisesLevel()
    {
        var g = GameState.NewGame(0);
        Assert.IsTrue(g.StartUpgrade(BuildingType.Farm, 0));
        g.Tick(Balance.UpgradeSeconds(BuildingType.Farm, 1) + 1);
        Assert.AreEqual(1, g.Get(BuildingType.Farm).level);
        Assert.IsFalse(g.Get(BuildingType.Farm).Upgrading);
    }

    [Test]
    public void OnlyOneBuilderAtATime()
    {
        var g = GameState.NewGame(0);
        Assert.IsTrue(g.StartUpgrade(BuildingType.Farm, 0));
        Assert.IsFalse(g.StartUpgrade(BuildingType.Lumberyard, 0));
    }

    [Test]
    public void ResourcesCappedByStorage()
    {
        var g = GameState.NewGame(0);
        g.Get(BuildingType.Farm).level = 1;
        g.Tick(Balance.OfflineCapSeconds);
        Assert.LessOrEqual(g.res.food, g.Cap + 0.01);
    }

    [Test]
    public void TrainingProducesTroops()
    {
        var g = GameState.NewGame(0);
        g.Get(BuildingType.Barracks).level = 1;
        g.res.food = 1000; g.res.wood = 1000;
        Assert.IsTrue(g.StartTraining(TroopType.Infantry, 5, 0));
        g.Tick(20);
        Assert.AreEqual(5, g.troops[0]);
        Assert.IsNull(g.order);
    }

    [Test]
    public void TriangleAdvantageWins()
    {
        // equal numbers: infantry should beat archers
        var r = BattleSim.Run(new[] { 20, 0, 0 }, new[] { 0, 0, 20 });
        Assert.IsTrue(r.win);
    }
}
