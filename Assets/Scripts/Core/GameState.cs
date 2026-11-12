using System;
using System.Collections.Generic;

namespace LastTown.Core
{
    [Serializable]
    public class GameState
    {
        public ResourceBag res = new ResourceBag { food = 300, wood = 300, stone = 150, gold = 0 };
        public List<Building> buildings = new List<Building>();
        public int[] troops = new int[3];
        public TrainOrder order; // single queue in prototype
        public int nextStage = 1;
        public double lastTickAt;

        public static GameState NewGame(double now)
        {
            var g = new GameState { lastTickAt = now };
            foreach (BuildingType t in Enum.GetValues(typeof(BuildingType)))
                g.buildings.Add(new Building { type = t, level = t == BuildingType.TownHall ? 1 : 0 });
            return g;
        }

        public Building Get(BuildingType t) => buildings.Find(b => b.type == t);
        public int TownHallLevel => Get(BuildingType.TownHall).level;
        public double Cap => Balance.StorageCap(TownHallLevel);

        public double ProdPerSec(Res r)
        {
            double sum = 0;
            foreach (var b in buildings)
                if (Balance.Produces(b.type) == r) sum += Balance.ProdPerSec(b.type, b.level);
            return sum;
        }

        // ---------- building ----------
        public bool CanUpgrade(BuildingType t, out string reason)
        {
            var b = Get(t);
            reason = null;
            if (b.Upgrading) { reason = "건설 중"; return false; }
            if (b.level >= Balance.MaxLevel) { reason = "최대 레벨"; return false; }
            if (t != BuildingType.TownHall && b.level >= TownHallLevel) { reason = "성 레벨 부족"; return false; }
            // one builder at a time (like early-game Kingshot)
            foreach (var o in buildings) if (o.Upgrading) { reason = "건설 대기열 사용 중"; return false; }
            if (!Balance.UpgradeCost(t, b.level + 1).CanPay(res)) { reason = "자원 부족"; return false; }
            return true;
        }

        public bool StartUpgrade(BuildingType t, double now)
        {
            if (!CanUpgrade(t, out _)) return false;
            var b = Get(t);
            Balance.UpgradeCost(t, b.level + 1).Pay(res);
            b.upgradeEndsAt = now + Balance.UpgradeSeconds(t, b.level + 1);
            return true;
        }

        // ---------- troops ----------
        public bool StartTraining(TroopType t, int count, double now)
        {
            var bar = Get(BuildingType.Barracks);
            if (bar.level <= 0 || order != null || count <= 0) return false;
            if (count > Balance.BarracksCapacity(bar.level)) return false;
            var cost = Balance.TroopCost(t).Scaled(count);
            if (!cost.CanPay(res)) return false;
            cost.Pay(res);
            order = new TrainOrder { type = t, remaining = count, nextAt = now + Balance.TroopSeconds(t) };
            return true;
        }

        // ---------- PvE ----------
        public BattleResult Attack(out PveStage stage)
        {
            stage = Stages.All[Math.Min(nextStage, Stages.All.Count) - 1];
            var r = BattleSim.Run(troops, new[] { stage.infantry, stage.cavalry, stage.archer });
            troops = r.survivors;
            if (r.win)
            {
                res.food += stage.reward.food; res.wood += stage.reward.wood;
                res.stone += stage.reward.stone; res.gold += stage.reward.gold;
                if (nextStage < Stages.All.Count) nextStage++;
            }
            return r;
        }

        // ---------- time ----------
        /// <summary>Advance the simulation to <paramref name="now"/>. Offline gaps are capped.</summary>
        public void Tick(double now)
        {
            double elapsed = now - lastTickAt;
            if (elapsed <= 0) return;
            double effective = Math.Min(elapsed, Balance.OfflineCapSeconds);
            // Process in steps so upgrades that finish mid-gap change production correctly.
            double t = now - effective;
            while (t < now)
            {
                double next = now;
                foreach (var b in buildings)
                    if (b.Upgrading && b.upgradeEndsAt > t && b.upgradeEndsAt < next) next = b.upgradeEndsAt;
                if (order != null && order.nextAt > t && order.nextAt < next) next = order.nextAt;

                Produce(next - t);
                t = next;

                foreach (var b in buildings)
                    if (b.Upgrading && b.upgradeEndsAt <= t) { b.level++; b.upgradeEndsAt = 0; }
                if (order != null && order.nextAt <= t)
                {
                    troops[(int)order.type]++;
                    order.remaining--;
                    if (order.remaining <= 0) order = null;
                    else order.nextAt = t + Balance.TroopSeconds(order.type);
                }
            }
            lastTickAt = now;
        }

        void Produce(double dt)
        {
            double cap = Cap;
            foreach (Res r in new[] { Res.Food, Res.Wood, Res.Stone })
            {
                double cur = res.Get(r);
                if (cur >= cap) continue;
                double add = ProdPerSec(r) * dt;
                res.Add(r, Math.Min(add, cap - cur));
            }
        }
    }
}
