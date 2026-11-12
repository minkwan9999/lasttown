using System;
using System.Collections.Generic;

namespace LastTown.Core
{
    public enum Res { Food, Wood, Stone, Gold }
    public enum BuildingType { TownHall, Farm, Lumberyard, Quarry, Barracks }
    // Kingshot-style triangle: Infantry > Archer > Cavalry > Infantry
    public enum TroopType { Infantry, Cavalry, Archer }

    [Serializable]
    public class ResourceBag
    {
        public double food, wood, stone, gold;
        public double Get(Res r) => r switch { Res.Food => food, Res.Wood => wood, Res.Stone => stone, _ => gold };
        public void Add(Res r, double v)
        {
            switch (r)
            {
                case Res.Food: food += v; break;
                case Res.Wood: wood += v; break;
                case Res.Stone: stone += v; break;
                default: gold += v; break;
            }
        }
    }

    public struct Cost
    {
        public double food, wood, stone, gold;
        public Cost(double f, double w, double s, double g) { food = f; wood = w; stone = s; gold = g; }
        public Cost Scaled(double k) => new Cost(Math.Ceiling(food * k), Math.Ceiling(wood * k), Math.Ceiling(stone * k), Math.Ceiling(gold * k));
        public bool CanPay(ResourceBag b) => b.food >= food && b.wood >= wood && b.stone >= stone && b.gold >= gold;
        public void Pay(ResourceBag b) { b.food -= food; b.wood -= wood; b.stone -= stone; b.gold -= gold; }
    }

    public static class Balance
    {
        public const int MaxLevel = 10;
        public const double OfflineCapSeconds = 8 * 3600;
        public const double CostGrowth = 1.6;
        public const double TimeGrowth = 1.7;
        public const double ProdGrowth = 1.45;

        public static Cost BaseBuildCost(BuildingType t) => t switch
        {
            BuildingType.TownHall => new Cost(100, 100, 50, 0),
            BuildingType.Farm => new Cost(0, 60, 20, 0),
            BuildingType.Lumberyard => new Cost(40, 0, 20, 0),
            BuildingType.Quarry => new Cost(40, 60, 0, 0),
            _ => new Cost(80, 80, 40, 0),
        };
        public static double BaseBuildSeconds(BuildingType t) => t == BuildingType.TownHall ? 20 : 8;

        // production per second at level 1
        public static double BaseProd(BuildingType t) => t switch
        {
            BuildingType.Farm => 2.0,
            BuildingType.Lumberyard => 1.5,
            BuildingType.Quarry => 1.0,
            _ => 0,
        };
        public static Res? Produces(BuildingType t) => t switch
        {
            BuildingType.Farm => Res.Food,
            BuildingType.Lumberyard => Res.Wood,
            BuildingType.Quarry => Res.Stone,
            _ => (Res?)null,
        };

        public static Cost UpgradeCost(BuildingType t, int toLevel) => BaseBuildCost(t).Scaled(Math.Pow(CostGrowth, toLevel - 1));
        public static double UpgradeSeconds(BuildingType t, int toLevel) => Math.Ceiling(BaseBuildSeconds(t) * Math.Pow(TimeGrowth, toLevel - 1));
        public static double ProdPerSec(BuildingType t, int level) => level <= 0 ? 0 : BaseProd(t) * Math.Pow(ProdGrowth, level - 1);
        // Storage cap grows with Town Hall level
        public static double StorageCap(int townHallLevel) => 1000 * Math.Pow(1.8, townHallLevel - 1);
        // Other buildings cannot exceed Town Hall level
        public static int LevelCapFor(BuildingType t, int townHallLevel) => t == BuildingType.TownHall ? MaxLevel : townHallLevel;

        public static Cost TroopCost(TroopType t) => t switch
        {
            TroopType.Infantry => new Cost(30, 10, 0, 0),
            TroopType.Cavalry => new Cost(30, 0, 10, 0),
            _ => new Cost(20, 20, 0, 0),
        };
        public static double TroopSeconds(TroopType t) => 2.0;
        public static int TroopAtk(TroopType t) => t switch { TroopType.Infantry => 8, TroopType.Cavalry => 12, _ => 10 };
        public static int TroopHp(TroopType t) => t switch { TroopType.Infantry => 60, TroopType.Cavalry => 40, _ => 30 };
        public static int BarracksCapacity(int barracksLevel) => 10 * barracksLevel; // max troops per training order

        public static TroopType Beats(TroopType t) => t switch
        {
            TroopType.Infantry => TroopType.Archer,
            TroopType.Archer => TroopType.Cavalry,
            _ => TroopType.Infantry,
        };
    }

    [Serializable]
    public class Building
    {
        public BuildingType type;
        public int level;            // 0 = not built
        public double upgradeEndsAt; // unix seconds, 0 = idle
        public bool Upgrading => upgradeEndsAt > 0;
    }

    [Serializable]
    public class TrainOrder
    {
        public TroopType type;
        public int remaining;
        public double nextAt;
    }

    [Serializable]
    public class PveStage
    {
        public int id;
        public int infantry, cavalry, archer;
        public Cost reward;
    }

    public static class Stages
    {
        public static readonly List<PveStage> All = BuildStages();
        static List<PveStage> BuildStages()
        {
            var l = new List<PveStage>();
            for (int i = 1; i <= 20; i++)
            {
                int n = 4 + i * 3;
                l.Add(new PveStage
                {
                    id = i,
                    infantry = n / 2 + (i % 3 == 0 ? 3 : 0),
                    cavalry = n / 3 + (i % 3 == 1 ? 3 : 0),
                    archer = n / 3 + (i % 3 == 2 ? 3 : 0),
                    reward = new Cost(100 * i, 100 * i, 50 * i, 20 * i),
                });
            }
            return l;
        }
    }
}
