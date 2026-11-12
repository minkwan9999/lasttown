using System;

namespace LastTown.Core
{
    public class BattleResult
    {
        public bool win;
        public int[] survivors = new int[3]; // indexed by TroopType
        public int rounds;
    }

    /// <summary>Deterministic round-based battle. Each side's troop group deals damage to the
    /// enemy group it is strong against (x1.5), weak against (x0.67), else x1.</summary>
    public static class BattleSim
    {
        const int MaxRounds = 50;

        public static double Mult(TroopType atk, TroopType def)
        {
            if (Balance.Beats(atk) == def) return 1.5;
            if (Balance.Beats(def) == atk) return 0.67;
            return 1.0;
        }

        public static BattleResult Run(int[] player, int[] enemy)
        {
            var p = (int[])player.Clone();
            var e = (int[])enemy.Clone();
            var res = new BattleResult();
            for (int r = 0; r < MaxRounds && Sum(p) > 0 && Sum(e) > 0; r++)
            {
                res.rounds = r + 1;
                var dmgToE = new double[3];
                var dmgToP = new double[3];
                for (int a = 0; a < 3; a++)
                {
                    Distribute((TroopType)a, p[a], e, dmgToE);
                    Distribute((TroopType)a, e[a], p, dmgToP);
                }
                Apply(e, dmgToE);
                Apply(p, dmgToP);
            }
            res.win = Sum(p) > 0 && Sum(e) == 0;
            res.survivors = p;
            return res;
        }

        // Spread an attacker group's damage evenly over living enemy groups.
        static void Distribute(TroopType atk, int count, int[] enemy, double[] dmg)
        {
            if (count <= 0) return;
            int groups = 0;
            for (int i = 0; i < 3; i++) if (enemy[i] > 0) groups++;
            if (groups == 0) return;
            double per = count * Balance.TroopAtk(atk) / (double)groups;
            for (int i = 0; i < 3; i++)
                if (enemy[i] > 0) dmg[i] += per * Mult(atk, (TroopType)i);
        }

        static void Apply(int[] side, double[] dmg)
        {
            for (int i = 0; i < 3; i++)
            {
                if (side[i] <= 0) continue;
                int killed = (int)Math.Floor(dmg[i] / Balance.TroopHp((TroopType)i));
                side[i] = Math.Max(0, side[i] - killed);
            }
        }

        static int Sum(int[] a) => a[0] + a[1] + a[2];
    }
}
