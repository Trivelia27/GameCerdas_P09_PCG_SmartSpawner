using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD uGUI: skor, level, HP, progres item, dash, toast, popup melayang, dan overlay judul/menang/kalah.</summary>
public class GameHUD : MonoBehaviour
{
    private class Floating
    {
        public TextMeshProUGUI text;
        public Vector3 world;
        public float age;
    }

    private const float PopupLife = 0.95f;

    private GameManager gm;
    private SimplePlayerController playerController;
    private Camera cam;
    private Canvas canvas;

    private RectTransform hudRoot;
    private RectTransform titlePanel;
    private RectTransform levelPanel;
    private RectTransform overPanel;
    private RectTransform pausePanel;

    private TextMeshProUGUI levelText;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI seedText;
    private TextMeshProUGUI itemsText;
    private TextMeshProUGUI exitText;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI toastText;
    private TextMeshProUGUI autoText;
    private TextMeshProUGUI titleSeedText;
    private TextMeshProUGUI levelTitle;
    private TextMeshProUGUI levelStats;
    private TextMeshProUGUI overStats;

    private RectTransform itemsFill;
    private RectTransform dashFill;
    private Image dashFillImage;
    private Image flash;
    private Image[] hpPips;

    private readonly List<Floating> floating = new List<Floating>();

    private GameState shownState = (GameState)(-1);
    private float displayScore;
    private float toastTimer;
    private Color toastColor;
    private float flashAlpha;

    public RectTransform Root { get; private set; }

    private void Awake()
    {
        UIFactory.EnsureEventSystem();
        BuildCanvas();
        BuildHud();
        BuildTitle();
        BuildLevelComplete();
        BuildGameOver();
        BuildPause();
    }

    private void Start()
    {
        gm = GameManager.Instance;
        cam = Camera.main;
        playerController = gm.Player.GetComponent<SimplePlayerController>();

        gm.Toast += ShowToast;
        gm.Popup += SpawnPopup;
        gm.Damaged += OnDamaged;
    }

    private void OnDestroy()
    {
        if (gm != null)
        {
            gm.Toast -= ShowToast;
            gm.Popup -= SpawnPopup;
            gm.Damaged -= OnDamaged;
        }
    }

    // ------------------------------------------------------------------ build

    private void BuildCanvas()
    {
        GameObject go = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(transform, false);

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Root = go.GetComponent<RectTransform>();
    }

    private void BuildHud()
    {
        hudRoot = UIFactory.Node(Root, "HUD");
        UIFactory.Stretch(hudRoot);

        Image vignette = UIFactory.MakeImage(hudRoot, "DamageFlash", new Color(1f, 0.1f, 0.15f, 0f), UIFactory.Vignette);
        UIFactory.Stretch(vignette.rectTransform);
        flash = vignette;

        // kiri atas
        levelText = UIFactory.MakeText(hudRoot, "LEVEL 1", 30f, UIFactory.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        UIFactory.Place(levelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(400f, 40f));
        levelText.characterSpacing = 6f;

        scoreText = UIFactory.MakeText(hudRoot, "000000", 66f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        UIFactory.Place(scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -62f), new Vector2(500f, 80f));

        seedText = UIFactory.MakeText(hudRoot, "SEED 0", 22f, UIFactory.Dim, TextAlignmentOptions.TopLeft);
        UIFactory.Place(seedText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -142f), new Vector2(500f, 30f));

        // tengah atas
        itemsText = UIFactory.MakeText(hudRoot, "ITEMS 0 / 0", 30f, Color.white, TextAlignmentOptions.Top, FontStyles.Bold);
        UIFactory.Place(itemsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(600f, 40f));
        itemsText.characterSpacing = 4f;

        Image barBackground = UIFactory.MakeImage(hudRoot, "ItemsBar", new Color(1f, 1f, 1f, 0.14f), UIFactory.Rounded, true);
        UIFactory.Place(barBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(560f, 14f));

        Image barFill = UIFactory.MakeImage(barBackground.transform, "Fill", UIFactory.Gold, UIFactory.Rounded, true);
        itemsFill = barFill.rectTransform;
        itemsFill.anchorMin = Vector2.zero;
        itemsFill.anchorMax = new Vector2(0f, 1f);
        itemsFill.offsetMin = Vector2.zero;
        itemsFill.offsetMax = Vector2.zero;

        exitText = UIFactory.MakeText(hudRoot, "EXIT LOCKED", 24f, UIFactory.Red, TextAlignmentOptions.Top, FontStyles.Bold);
        UIFactory.Place(exitText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(600f, 34f));
        exitText.characterSpacing = 6f;

        autoText = UIFactory.MakeText(hudRoot, "AUTOPILOT ON  (F2)", 22f, UIFactory.Gold, TextAlignmentOptions.Top, FontStyles.Bold);
        UIFactory.Place(autoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(600f, 30f));

        // kanan atas
        int maxHealth = 5;
        hpPips = new Image[maxHealth];

        for (int i = 0; i < maxHealth; i++)
        {
            Image pip = UIFactory.MakeImage(hudRoot, "HP" + i, UIFactory.Red, UIFactory.Rounded, true);
            UIFactory.Place(pip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f - i * 52f, -40f), new Vector2(44f, 18f));
            hpPips[maxHealth - 1 - i] = pip;
        }

        TextMeshProUGUI dashLabel = UIFactory.MakeText(hudRoot, "DASH  [SPACE]", 20f, UIFactory.Dim, TextAlignmentOptions.TopRight, FontStyles.Bold);
        UIFactory.Place(dashLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -74f), new Vector2(300f, 28f));

        Image dashBackground = UIFactory.MakeImage(hudRoot, "DashBar", new Color(1f, 1f, 1f, 0.14f), UIFactory.Rounded, true);
        UIFactory.Place(dashBackground.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -106f), new Vector2(260f, 12f));

        dashFillImage = UIFactory.MakeImage(dashBackground.transform, "Fill", UIFactory.Cyan, UIFactory.Rounded, true);
        dashFill = dashFillImage.rectTransform;
        dashFill.anchorMin = Vector2.zero;
        dashFill.anchorMax = new Vector2(1f, 1f);
        dashFill.offsetMin = Vector2.zero;
        dashFill.offsetMax = Vector2.zero;

        timerText = UIFactory.MakeText(hudRoot, "00:00.0", 30f, Color.white, TextAlignmentOptions.TopRight, FontStyles.Bold);
        UIFactory.Place(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -128f), new Vector2(300f, 40f));

        // toast tengah
        toastText = UIFactory.MakeText(hudRoot, "", 58f, UIFactory.Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(toastText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1400f, 90f));
        toastText.characterSpacing = 8f;

        // petunjuk kontrol
        TextMeshProUGUI hint = UIFactory.MakeText(hudRoot,
            "WASD move   ·   SPACE dash (menghancurkan enemy)   ·   R restart   ·   N seed baru   ·   TAB panel PCG   ·   F2 autopilot   ·   P pause   ·   M mute",
            19f, new Color(0.6f, 0.7f, 0.8f, 0.8f), TextAlignmentOptions.Bottom);
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1800f, 30f));
    }

    private void BuildTitle()
    {
        titlePanel = UIFactory.Node(Root, "TitlePanel");
        UIFactory.Stretch(titlePanel);

        Image dim = UIFactory.MakeImage(titlePanel, "Dim", new Color(0.01f, 0.02f, 0.05f, 0.6f));
        UIFactory.Stretch(dim.rectTransform);

        // Konten digeser ke kanan supaya tidak menimpa panel debug PCG di kiri.
        RectTransform content = UIFactory.Node(titlePanel, "Content");
        UIFactory.Stretch(content);
        content.offsetMin = new Vector2(440f, 0f);

        TextMeshProUGUI title = UIFactory.MakeText(content, "SMART SPAWNER", 104f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1600f, 170f));
        title.characterSpacing = 14f;

        TextMeshProUGUI subtitle = UIFactory.MakeText(content, "PROCEDURAL CONTENT GENERATION  ·  GAME CERDAS P09", 30f, UIFactory.Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1600f, 44f));
        subtitle.characterSpacing = 10f;

        TextMeshProUGUI mission = UIFactory.MakeText(content,
            "Setiap arena dibangun oleh algoritma PCG: <b>seed</b>, <b>weighted random</b>, dan <b>validasi posisi</b>.\n" +
            "Kumpulkan item bercahaya, hindari atau <b>dash</b> menembus enemy, lalu capai <b>EXIT</b>.",
            30f, new Color(0.85f, 0.92f, 1f), TextAlignmentOptions.Center, FontStyles.Normal, true);
        UIFactory.Place(mission.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1200f, 120f));

        Button start = UIFactory.MakeButton(content, "START  [ENTER]", 36f, () => gm.StartGame(), new Color(0.1f, 0.55f, 0.65f));
        UIFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(460f, 96f));

        titleSeedText = UIFactory.MakeText(content, "", 26f, UIFactory.Dim, TextAlignmentOptions.Center);
        UIFactory.Place(titleSeedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(1200f, 40f));

        TextMeshProUGUI keys = UIFactory.MakeText(content,
            "WASD / Panah  gerak     ·     SPACE  dash     ·     TAB  panel debug PCG     ·     F2  autopilot",
            24f, new Color(0.7f, 0.8f, 0.9f, 0.85f), TextAlignmentOptions.Center);
        UIFactory.Place(keys.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(1600f, 36f));
    }

    private void BuildLevelComplete()
    {
        levelPanel = UIFactory.Node(Root, "LevelCompletePanel");
        UIFactory.Stretch(levelPanel);

        Image dim = UIFactory.MakeImage(levelPanel, "Dim", new Color(0f, 0.04f, 0.02f, 0.45f));
        UIFactory.Stretch(dim.rectTransform);

        Image card = UIFactory.MakeImage(levelPanel, "Card", UIFactory.PanelColor, UIFactory.Rounded, true);
        UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(860f, 470f));

        levelTitle = UIFactory.MakeText(card.transform, "LEVEL 1 COMPLETE", 66f, UIFactory.Green, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(levelTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 90f));
        levelTitle.characterSpacing = 6f;

        levelStats = UIFactory.MakeText(card.transform, "", 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal, true);
        levelStats.lineSpacing = 28f;
        UIFactory.Place(levelStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(780f, 170f));

        Button next = UIFactory.MakeButton(card.transform, "NEXT LEVEL  [ENTER]", 32f, () => gm.NextLevel(), new Color(0.12f, 0.6f, 0.35f));
        UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(480f, 86f));
    }

    private void BuildGameOver()
    {
        overPanel = UIFactory.Node(Root, "GameOverPanel");
        UIFactory.Stretch(overPanel);

        Image dim = UIFactory.MakeImage(overPanel, "Dim", new Color(0.08f, 0f, 0.02f, 0.55f));
        UIFactory.Stretch(dim.rectTransform);

        Image card = UIFactory.MakeImage(overPanel, "Card", UIFactory.PanelColor, UIFactory.Rounded, true);
        UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(860f, 470f));

        TextMeshProUGUI title = UIFactory.MakeText(card.transform, "GAME OVER", 80f, UIFactory.Red, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(820f, 100f));
        title.characterSpacing = 10f;

        overStats = UIFactory.MakeText(card.transform, "", 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal, true);
        overStats.lineSpacing = 28f;
        UIFactory.Place(overStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(780f, 170f));

        Button retry = UIFactory.MakeButton(card.transform, "RETRY  [R]", 30f, () => gm.RestartLevel(), new Color(0.65f, 0.2f, 0.25f));
        UIFactory.Place((RectTransform)retry.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 44f), new Vector2(340f, 86f));

        Button fresh = UIFactory.MakeButton(card.transform, "NEW SEED  [N]", 30f, () => gm.NewRun(), new Color(0.1f, 0.5f, 0.6f));
        UIFactory.Place((RectTransform)fresh.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(16f, 44f), new Vector2(340f, 86f));
    }

    private void BuildPause()
    {
        pausePanel = UIFactory.Node(Root, "PausePanel");
        UIFactory.Stretch(pausePanel);

        Image dim = UIFactory.MakeImage(pausePanel, "Dim", new Color(0f, 0.02f, 0.05f, 0.6f));
        UIFactory.Stretch(dim.rectTransform);

        TextMeshProUGUI text = UIFactory.MakeText(pausePanel, "PAUSED", 110f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 150f));
        text.characterSpacing = 24f;
    }

    // ------------------------------------------------------------------ event

    private void ShowToast(string message, Color color)
    {
        toastText.text = message;
        toastColor = color;
        toastTimer = 1.9f;
    }

    private void SpawnPopup(Vector3 world, string message, Color color)
    {
        TextMeshProUGUI text = UIFactory.MakeText(Root, message, 44f, color, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(text.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 60f));

        floating.Add(new Floating { text = text, world = world });
    }

    private void OnDamaged()
    {
        flashAlpha = 0.85f;
    }

    // ------------------------------------------------------------------ update

    private void Update()
    {
        if (gm == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;

        ApplyState(gm.State);
        UpdateHud(dt);
        UpdateFloating(dt);
    }

    private void ApplyState(GameState state)
    {
        if (state == shownState)
        {
            return;
        }

        shownState = state;

        hudRoot.gameObject.SetActive(state != GameState.Title);
        titlePanel.gameObject.SetActive(state == GameState.Title);
        levelPanel.gameObject.SetActive(state == GameState.LevelComplete);
        overPanel.gameObject.SetActive(state == GameState.GameOver);
        pausePanel.gameObject.SetActive(state == GameState.Paused);

        // Overlay selalu di bawah panel debug (yang ditambahkan terakhir), tetapi di atas HUD.
        if (state == GameState.LevelComplete)
        {
            levelTitle.text = $"LEVEL {gm.Level} COMPLETE";
            levelStats.text =
                $"Waktu  <color=#4DF2FF>{gm.LevelTime:F1}s</color>     Enemy hancur  <color=#4DF2FF>{gm.Kills}</color>\n" +
                $"Bonus  <color=#FFD14D>+{gm.LastBonus}</color>     Skor  <color=#FFFFFF>{gm.Score}</color>\n" +
                $"Level berikutnya memakai seed  <color=#4DF2FF>{gm.NextLevelSeed}</color>";
        }
        else if (state == GameState.GameOver)
        {
            string record = gm.Score >= gm.BestScore && gm.Score > 0 ? "  <color=#FFD14D>REKOR BARU!</color>" : "";

            overStats.text =
                $"Level  <color=#4DF2FF>{gm.Level}</color>     Item  <color=#4DF2FF>{gm.Collected}/{gm.Required}</color>     Enemy hancur  <color=#4DF2FF>{gm.Kills}</color>\n" +
                $"Skor  <color=#FFFFFF>{gm.Score}</color>     Terbaik  <color=#FFD14D>{gm.BestScore}</color>{record}\n" +
                $"Seed  <color=#4DF2FF>{gm.LevelSeed}</color>";
        }
    }

    private void UpdateHud(float dt)
    {
        if (gm.State == GameState.Title)
        {
            titleSeedText.text = $"SEED  {gm.CurrentSeed}     ·     TERBAIK  {gm.BestScore}";
            return;
        }

        levelText.text = $"LEVEL {gm.Level}";

        displayScore = Mathf.MoveTowards(displayScore, gm.Score, Mathf.Max(80f, Mathf.Abs(gm.Score - displayScore) * 5f) * dt);
        scoreText.text = Mathf.RoundToInt(displayScore).ToString("D6");
        seedText.text = $"SEED {gm.CurrentSeed}";

        int extra = Mathf.Max(0, gm.Collected - gm.Required);
        itemsText.text = $"ITEMS  {Mathf.Min(gm.Collected, gm.Required)} / {gm.Required}" + (extra > 0 ? $"  <color=#FFD14D>+{extra}</color>" : "");
        float fraction = gm.Required > 0 ? Mathf.Clamp01(gm.Collected / (float)gm.Required) : 1f;
        itemsFill.anchorMax = new Vector2(Mathf.Lerp(itemsFill.anchorMax.x, fraction, 1f - Mathf.Exp(-10f * dt)), 1f);

        if (gm.ExitUnlocked)
        {
            exitText.text = "EXIT OPEN";
            exitText.color = Color.Lerp(UIFactory.Green, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
        }
        else
        {
            exitText.text = "EXIT LOCKED";
            exitText.color = UIFactory.Red;
        }

        for (int i = 0; i < hpPips.Length; i++)
        {
            bool filled = i < gm.Health;
            bool critical = gm.Health <= 1 && filled;

            Color color = filled ? UIFactory.Red : new Color(1f, 1f, 1f, 0.12f);

            if (critical)
            {
                color = Color.Lerp(UIFactory.Red, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f));
            }

            hpPips[i].color = color;
        }

        float dash = playerController.DashReady01;
        dashFill.anchorMax = new Vector2(dash, 1f);
        dashFillImage.color = dash >= 1f ? UIFactory.Cyan : new Color(0.3f, 0.5f, 0.6f);

        int minutes = Mathf.FloorToInt(gm.LevelTime / 60f);
        timerText.text = $"{minutes:00}:{gm.LevelTime - minutes * 60f:00.0}";

        autoText.gameObject.SetActive(gm.AutoPilotComponent != null && gm.AutoPilotComponent.Active);

        toastTimer = Mathf.Max(0f, toastTimer - dt);
        float toastAlpha = Mathf.Clamp01(toastTimer / 0.5f);
        toastText.color = new Color(toastColor.r, toastColor.g, toastColor.b, toastAlpha);
        toastText.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, toastAlpha);

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.6f);
        flash.color = new Color(1f, 0.1f, 0.15f, flashAlpha);
    }

    private void UpdateFloating(float dt)
    {
        for (int i = floating.Count - 1; i >= 0; i--)
        {
            Floating f = floating[i];
            f.age += dt;

            if (f.age >= PopupLife)
            {
                Destroy(f.text.gameObject);
                floating.RemoveAt(i);
                continue;
            }

            f.world += Vector3.up * (1.3f * dt);

            Vector3 screen = cam.WorldToScreenPoint(f.world);
            float k = f.age / PopupLife;

            f.text.rectTransform.anchoredPosition = new Vector2(screen.x, screen.y) / canvas.scaleFactor;
            f.text.alpha = screen.z > 0f ? 1f - k * k : 0f;
            f.text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(k * 5f));
        }
    }
}
