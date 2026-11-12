using LastTown.Core;
using UnityEngine;

namespace LastTown.UI
{
    public static class SaveStore
    {
        const string Key = "lasttown_save_v1";
        public static GameState Load()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                var g = JsonUtility.FromJson<GameState>(PlayerPrefs.GetString(Key));
                if (g != null && g.buildings != null && g.buildings.Count > 0)
                {
                    if (g.order != null && g.order.remaining <= 0) g.order = null; // JsonUtility has no null
                    if (g.troops == null || g.troops.Length != 3) g.troops = new int[3];
                    return g;
                }
            }
            return GameState.NewGame(Clock.Now);
        }
        public static void Save(GameState g)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(g));
            PlayerPrefs.Save();
        }
        public static void Reset() { PlayerPrefs.DeleteKey(Key); }
    }
}
