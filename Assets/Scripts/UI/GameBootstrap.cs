using System;
using System.Text;
using LastTown.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastTown.UI
{
    /// <summary>Builds the whole prototype UI at runtime so it works in an empty scene.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            new GameObject("LastTown").AddComponent<GameBootstrap>();
        }

        GameState g;
        Font font;
        Text resText, logText, troopText, stageText;
        readonly Button[] upBtns = new Button[5];
        readonly Text[] upTxt = new Text[5];
        readonly Button[] trainBtns = new Button[3];
        Text trainStatus;
        float saveTimer;
        static readonly string[] BName = { "성(Town Hall)", "농장", "벌목장", "채석장", "병영" };
        static readonly string[] TName = { "보병", "기병", "궁병" };

        void Start()
        {
            Application.targetFrameRate = 60;
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Noto Sans CJK KR", "NanumGothic", "Arial" }, 32);
            g = SaveStore.Load();
            double before = g.lastTickAt, now = Clock.Now;
            g.Tick(now);
            BuildUI();
            if (now - before > 60) Log($"오프라인 {(int)Math.Min(now - before, Balance.OfflineCapSeconds) / 60}분 동안 자원이 쌓였습니다.");
        }

        void Update()
        {
            g.Tick(Clock.Now);
            Refresh();
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer > 5f) { saveTimer = 0; SaveStore.Save(g); }
        }

        void OnApplicationPause(bool paused) { if (paused && g != null) { g.Tick(Clock.Now); SaveStore.Save(g); } }
        void OnApplicationQuit() { if (g != null) SaveStore.Save(g); }

        // ---------------- logic glue ----------------
        void Log(string s) { logText.text = s; }

        void OnUpgrade(BuildingType t)
        {
            if (!g.StartUpgrade(t, Clock.Now)) { g.CanUpgrade(t, out var why); Log("건설 불가: " + why); }
            else Log(BName[(int)t] + " 건설 시작");
        }

        void OnTrain(TroopType t)
        {
            int n = Balance.BarracksCapacity(g.Get(BuildingType.Barracks).level);
            if (!g.StartTraining(t, n, Clock.Now)) Log("훈련 불가 (병영 필요/자원 부족/훈련 중)");
            else Log($"{TName[(int)t]} {n}명 훈련 시작");
        }

        void OnAttack()
        {
            int total = g.troops[0] + g.troops[1] + g.troops[2];
            if (total == 0) { Log("출전할 병력이 없습니다."); return; }
            var r = g.Attack(out var st);
            Log(r.win ? $"스테이지 {st.id} 승리! ({r.rounds}라운드) 보상 획득" : $"스테이지 {st.id} 패배… 병력을 보강하세요");
            SaveStore.Save(g);
        }

        // ---------------- refresh ----------------
        void Refresh()
        {
            double cap = g.Cap;
            resText.text = $"식량 {F(g.res.food)}  나무 {F(g.res.wood)}  돌 {F(g.res.stone)}  금 {F(g.res.gold)}\n" +
                           $"(+{g.ProdPerSec(Res.Food):0.0}/s, +{g.ProdPerSec(Res.Wood):0.0}/s, +{g.ProdPerSec(Res.Stone):0.0}/s)  창고 {F(cap)}";
            for (int i = 0; i < 5; i++)
            {
                var t = (BuildingType)i;
                var b = g.Get(t);
                var sb = new StringBuilder();
                sb.Append($"{BName[i]} Lv.{b.level}");
                if (b.Upgrading) sb.Append($"  ⏳{Math.Max(0, (int)Math.Ceiling(b.upgradeEndsAt - Clock.Now))}s");
                else if (b.level < Balance.MaxLevel)
                {
                    var c = Balance.UpgradeCost(t, b.level + 1);
                    sb.Append($"\n→Lv.{b.level + 1}: 식{F(c.food)} 목{F(c.wood)} 돌{F(c.stone)}  {Balance.UpgradeSeconds(t, b.level + 1)}s");
                }
                upTxt[i].text = sb.ToString();
                bool ok = g.CanUpgrade(t, out _);
                upBtns[i].interactable = ok;
                upBtns[i].GetComponent<Image>().color = ok ? new Color(0.25f, 0.6f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
            }
            troopText.text = $"병력  보병 {g.troops[0]} / 기병 {g.troops[1]} / 궁병 {g.troops[2]}\n상성: 보병>궁병>기병>보병";
            trainStatus.text = g.order == null ? "훈련 대기" : $"훈련 중: {TName[(int)g.order.type]} 남은 {g.order.remaining}명";
            int bl = g.Get(BuildingType.Barracks).level;
            for (int i = 0; i < 3; i++) trainBtns[i].interactable = bl > 0 && g.order == null;
            var st = Stages.All[Math.Min(g.nextStage, Stages.All.Count) - 1];
            stageText.text = $"스테이지 {st.id}  적: 보병 {st.infantry} 기병 {st.cavalry} 궁병 {st.archer}";
        }

        static string F(double v) => v >= 1e6 ? (v / 1e6).ToString("0.0") + "M" : v >= 1e3 ? (v / 1e3).ToString("0.0") + "K" : ((int)v).ToString();

        // ---------------- UI construction ----------------
        void BuildUI()
        {
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080, 1920);
            sc.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Camera.main?.gameObject.SetActive(true);
            var bg = Panel(cgo.transform, "BG", new Color(0.1f, 0.14f, 0.2f));
            Stretch(bg, 0, 0, 0, 0);

            float y = -40;
            resText = Label(bg, y, 150, 36, TextAnchor.UpperLeft); y -= 170;

            for (int i = 0; i < 5; i++)
            {
                var t = (BuildingType)i;
                var btn = MakeButton(bg, "", y, 150);
                upBtns[i] = btn; upTxt[i] = btn.GetComponentInChildren<Text>();
                upTxt[i].fontSize = 32;
                btn.onClick.AddListener(() => OnUpgrade(t));
                y -= 165;
            }

            troopText = Label(bg, y, 110, 34, TextAnchor.UpperLeft); y -= 120;
            trainStatus = Label(bg, y, 60, 34, TextAnchor.UpperLeft); y -= 70;
            for (int i = 0; i < 3; i++)
            {
                var t = (TroopType)i;
                var b = MakeButton(bg, TName[i] + " 훈련", y, 110, 0.05f + i * 0.31f, 0.26f);
                trainBtns[i] = b;
                b.onClick.AddListener(() => OnTrain(t));
            }
            y -= 130;

            stageText = Label(bg, y, 70, 34, TextAnchor.UpperLeft); y -= 80;
            var atk = MakeButton(bg, "출전!", y, 130);
            atk.GetComponent<Image>().color = new Color(0.75f, 0.2f, 0.2f);
            atk.onClick.AddListener(OnAttack); y -= 150;
            logText = Label(bg, y, 160, 34, TextAnchor.UpperLeft);
            logText.color = new Color(1f, 0.9f, 0.5f);

            var reset = MakeButton(bg, "데이터 초기화", -1840, 70, 0.7f, 0.26f);
            reset.GetComponent<Image>().color = new Color(0.35f, 0.15f, 0.15f);
            reset.GetComponentInChildren<Text>().fontSize = 26;
            reset.onClick.AddListener(() => { SaveStore.Reset(); g = GameState.NewGame(Clock.Now); Log("초기화 완료"); });
        }

        static GameObject Panel(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = c;
            return go;
        }
        static void Stretch(GameObject go, float l, float b, float r, float t)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        Text Label(GameObject parent, float y, float h, int size, TextAnchor anchor)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 1); rt.anchorMax = new Vector2(0.96f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(0, h);
            var tx = go.GetComponent<Text>();
            tx.font = font; tx.fontSize = size; tx.alignment = anchor; tx.color = Color.white;
            tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Overflow;
            return tx;
        }

        Button MakeButton(GameObject parent, string label, float y, float h, float x0 = 0.04f, float w = 0.92f)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, 1); rt.anchorMax = new Vector2(x0 + w, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(0, h);
            go.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.7f);
            var tx = Label(go, 0, h, 34, TextAnchor.MiddleCenter);
            var trt = tx.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            tx.text = label;
            return go.GetComponent<Button>();
        }
    }
}
