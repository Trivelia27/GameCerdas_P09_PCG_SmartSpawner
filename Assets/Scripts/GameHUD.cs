using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Layout dashboard: panel kontrol (kiri), viewport arena (tengah, dirender ke RenderTexture),
/// laporan (kanan) dan bar riwayat (bawah). HUD game, layar judul, menang/kalah berada di dalam viewport.
/// TAB mengganti ke mode fokus (viewport memenuhi layar).
/// </summary>
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
    private RenderTexture renderTexture;

    private RectTransform chrome;
    private RectTransform frame;
    private RawImage view;
    private RectTransform hudRoot;
    private RectTransform titlePanel;
    private RectTransform levelPanel;
    private RectTransform overPanel;
    private RectTransform pausePanel;
    private TextMeshProUGUI focusHint;

    private TextMeshProUGUI levelText;
    private TextMeshProUGUI scoreText;
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
    private bool focusMode;
    private float displayScore;
    private float toastTimer;
    private Color toastColor;
    private float flashAlpha;

    public RectTransform Root { get; private set; }
    public RectTransform LeftPanel { get; private set; }
    public RectTransform RightPanel { get; private set; }
    public RectTransform BottomBar { get; private set; }
    public RectTransform Viewport => view.rectTransform;

    private void Awake()
    {
        UIFactory.EnsureEventSystem();
        BuildDisplayCamera();
        BuildCanvas();
        BuildChrome();
        BuildViewport();
        BuildHud();
        BuildTitle();
        BuildLevelComplete();
        BuildGameOver();
        BuildPause();
        ApplyLayout();
    }

    private void Start()
    {
        gm = GameManager.Instance;
        cam = Camera.main;
        playerController = gm.Player.GetComponent<SimplePlayerController>();

        gm.Toast += ShowToast;
        gm.Popup += SpawnPopup;
        gm.Damaged += OnDamaged;

        Canvas.ForceUpdateCanvases();
        EnsureRenderTexture();
    }

    private void OnDestroy()
    {
        if (gm != null)
        {
            gm.Toast -= ShowToast;
            gm.Popup -= SpawnPopup;
            gm.Damaged -= OnDamaged;
        }

        if (cam != null)
        {
            cam.targetTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
        }
    }

    // ------------------------------------------------------------------ kerangka dashboard

    /// <summary>Kamera utama merender ke RenderTexture; kamera kosong ini mencegah peringatan "No cameras rendering".</summary>
    private void BuildDisplayCamera()
    {
        GameObject go = new GameObject("DisplayCamera");
        go.transform.SetParent(transform, false);

        Camera display = go.AddComponent<Camera>();
        display.clearFlags = CameraClearFlags.SolidColor;
        display.backgroundColor = new Color(0.016f, 0.024f, 0.05f);
        display.cullingMask = 0;
        display.depth = -50f;
        display.allowHDR = false;
        display.allowMSAA = false;
    }

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

        Image background = UIFactory.MakeImage(Root, "Background", new Color(0.016f, 0.024f, 0.05f, 1f));
        UIFactory.Stretch(background.rectTransform);
    }

    private void BuildChrome()
    {
        chrome = UIFactory.Node(Root, "Chrome");
        UIFactory.Stretch(chrome);

        // ----- label di atas viewport
        TextMeshProUGUI arenaLabel = UIFactory.MakeText(chrome,
            "<color=#FF4DB8>02</color>   ARENA  /  LIVE GENERATION",
            20f, UIFactory.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        arenaLabel.characterSpacing = 4f;
        UIFactory.Place(arenaLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(492f, -26f), new Vector2(980f, 28f));

        TextMeshProUGUI legend = UIFactory.MakeText(chrome,
            "Pipeline: Obstacle → Enemy → Item → Exit    ·    garis cyan = area spawn    ·    lingkaran hijau = safe zone",
            16f, UIFactory.Dim, TextAlignmentOptions.TopLeft);
        UIFactory.Place(legend.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(494f, -54f), new Vector2(980f, 24f));

        // ----- tips di bawah viewport
        TextMeshProUGUI tips = UIFactory.MakeText(chrome,
            "WASD / panah gerak  ·  SPACE dash  ·  R generate  ·  N seed baru  ·  TAB mode fokus  ·  F2 autopilot  ·  P pause\n" +
            "Tanda X: <color=#4DFF80>hijau</color> = safe zone,  <color=#FF4D59>merah</color> = obstacle,  <color=#FFD633>kuning</color> = spacing (maks. 300)",
            15f, UIFactory.Dim, TextAlignmentOptions.TopLeft, FontStyles.Normal, true);
        UIFactory.Place(tips.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(494f, 122f), new Vector2(980f, 44f));

        // ----- kartu kiri / kanan
        LeftPanel = UIFactory.MakeCard(chrome, "01", "GENERATION CONTROLS", UIFactory.Cyan);
        LeftPanel.anchorMin = new Vector2(0f, 0f);
        LeftPanel.anchorMax = new Vector2(0f, 1f);
        LeftPanel.offsetMin = new Vector2(24f, 132f);
        LeftPanel.offsetMax = new Vector2(474f, -24f);

        RightPanel = UIFactory.MakeCard(chrome, "03", "GENERATION REPORT", UIFactory.Magenta);
        RightPanel.anchorMin = new Vector2(1f, 0f);
        RightPanel.anchorMax = new Vector2(1f, 1f);
        RightPanel.offsetMin = new Vector2(-424f, 132f);
        RightPanel.offsetMax = new Vector2(-24f, -24f);

        // ----- bar bawah
        BottomBar = UIFactory.Node(chrome, "BottomBar");
        BottomBar.anchorMin = new Vector2(0f, 0f);
        BottomBar.anchorMax = new Vector2(1f, 0f);
        BottomBar.offsetMin = new Vector2(24f, 20f);
        BottomBar.offsetMax = new Vector2(-24f, 112f);

        Image barBackground = BottomBar.gameObject.AddComponent<Image>();
        barBackground.sprite = UIFactory.Rounded;
        barBackground.type = Image.Type.Sliced;
        barBackground.color = UIFactory.PanelColor;

        Image barBorder = UIFactory.MakeImage(BottomBar, "Border", new Color(UIFactory.Cyan.r, UIFactory.Cyan.g, UIFactory.Cyan.b, 0.4f), UIFactory.Outline, true);
        UIFactory.Stretch(barBorder.rectTransform);
        barBorder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        HorizontalLayoutGroup barLayout = UIFactory.Horizontal(BottomBar.gameObject, 14f);
        barLayout.padding = new RectOffset(24, 24, 12, 12);
        barLayout.childAlignment = TextAnchor.MiddleLeft;
    }

    private void BuildViewport()
    {
        frame = UIFactory.Node(Root, "ViewportFrame");

        Image frameBackground = frame.gameObject.AddComponent<Image>();
        frameBackground.color = Color.black;
        frameBackground.raycastTarget = false;

        view = UIFactory.MakeRawImage(frame, "Viewport");
        view.color = Color.white;
        UIFactory.Stretch(view.rectTransform, 3f);
        view.gameObject.AddComponent<RectMask2D>();

        Image border = UIFactory.MakeImage(frame, "Border", new Color(UIFactory.Cyan.r, UIFactory.Cyan.g, UIFactory.Cyan.b, 0.7f), UIFactory.Outline, true);
        UIFactory.Stretch(border.rectTransform);
    }

    private void ApplyLayout()
    {
        chrome.gameObject.SetActive(!focusMode);

        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;

        if (focusMode)
        {
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;
        }
        else
        {
            frame.offsetMin = new Vector2(490f, 168f);
            frame.offsetMax = new Vector2(-440f, -82f);
        }

        if (focusHint != null)
        {
            focusHint.gameObject.SetActive(focusMode);
        }
    }

    private void EnsureRenderTexture()
    {
        if (cam == null || view == null)
        {
            return;
        }

        Vector2 size = view.rectTransform.rect.size * canvas.scaleFactor;
        int width = Mathf.Max(64, Mathf.RoundToInt(size.x));
        int height = Mathf.Max(64, Mathf.RoundToInt(size.y));

        if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
        {
            return;
        }

        cam.targetTexture = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB)
        {
            name = "ArenaViewport",
            antiAliasing = 1
        };

        renderTexture.Create();

        cam.targetTexture = renderTexture;
        view.texture = renderTexture;
    }

    // ------------------------------------------------------------------ HUD dalam viewport

    private void BuildHud()
    {
        hudRoot = UIFactory.Node(view.transform, "HUD");
        UIFactory.Stretch(hudRoot);

        Image vignette = UIFactory.MakeImage(hudRoot, "DamageFlash", new Color(1f, 0.1f, 0.15f, 0f), UIFactory.Vignette);
        UIFactory.Stretch(vignette.rectTransform);
        flash = vignette;

        // kiri atas
        levelText = UIFactory.MakeText(hudRoot, "LEVEL 1", 22f, UIFactory.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        UIFactory.Place(levelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -16f), new Vector2(300f, 30f));
        levelText.characterSpacing = 5f;

        scoreText = UIFactory.MakeText(hudRoot, "000000", 50f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        UIFactory.Place(scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -42f), new Vector2(360f, 60f));

        // tengah atas
        itemsText = UIFactory.MakeText(hudRoot, "ITEMS 0 / 0", 22f, Color.white, TextAlignmentOptions.Top, FontStyles.Bold);
        UIFactory.Place(itemsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(420f, 30f));
        itemsText.characterSpacing = 3f;

        Image barBackground = UIFactory.MakeImage(hudRoot, "ItemsBar", new Color(1f, 1f, 1f, 0.16f), UIFactory.Rounded, true);
        UIFactory.Place(barBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(340f, 11f));

        Image barFill = UIFactory.MakeImage(barBackground.transform, "Fill", UIFactory.Gold, UIFactory.Rounded, true);
        itemsFill = barFill.rectTransform;
        itemsFill.anchorMin = Vector2.zero;
        itemsFill.anchorMax = new Vector2(0f, 1f);
        itemsFill.offsetMin = Vector2.zero;
        itemsFill.offsetMax = Vector2.zero;

        exitText = UIFactory.MakeText(hudRoot, "EXIT LOCKED", 18f, UIFactory.Red, TextAlignmentOptions.Top, FontStyles.Bold);
        UIFactory.Place(exitText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(420f, 26f));
        exitText.characterSpacing = 5f;

        autoText = UIFactory.MakeText(hudRoot, "AUTOPILOT ON  (F2)", 16f, UIFactory.Gold, TextAlignmentOptions.Top, FontStyles.Bold);
        UIFactory.Place(autoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(420f, 24f));

        // kanan atas
        int maxHealth = 5;
        hpPips = new Image[maxHealth];

        for (int i = 0; i < maxHealth; i++)
        {
            Image pip = UIFactory.MakeImage(hudRoot, "HP" + i, UIFactory.Red, UIFactory.Rounded, true);
            UIFactory.Place(pip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f - i * 40f, -22f), new Vector2(34f, 14f));
            hpPips[maxHealth - 1 - i] = pip;
        }

        TextMeshProUGUI dashLabel = UIFactory.MakeText(hudRoot, "DASH  [SPACE]", 15f, UIFactory.Dim, TextAlignmentOptions.TopRight, FontStyles.Bold);
        UIFactory.Place(dashLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -46f), new Vector2(260f, 22f));

        Image dashBackground = UIFactory.MakeImage(hudRoot, "DashBar", new Color(1f, 1f, 1f, 0.16f), UIFactory.Rounded, true);
        UIFactory.Place(dashBackground.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -72f), new Vector2(200f, 9f));

        dashFillImage = UIFactory.MakeImage(dashBackground.transform, "Fill", UIFactory.Cyan, UIFactory.Rounded, true);
        dashFill = dashFillImage.rectTransform;
        dashFill.anchorMin = Vector2.zero;
        dashFill.anchorMax = new Vector2(1f, 1f);
        dashFill.offsetMin = Vector2.zero;
        dashFill.offsetMax = Vector2.zero;

        timerText = UIFactory.MakeText(hudRoot, "00:00.0", 24f, Color.white, TextAlignmentOptions.TopRight, FontStyles.Bold);
        UIFactory.Place(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -88f), new Vector2(260f, 32f));

        // toast
        toastText = UIFactory.MakeText(hudRoot, "", 44f, UIFactory.Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(toastText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(900f, 70f));
        toastText.characterSpacing = 6f;

        // petunjuk mode fokus
        focusHint = UIFactory.MakeText(hudRoot, "TAB: tampilkan panel lab   ·   WASD gerak   ·   SPACE dash", 15f, new Color(0.7f, 0.8f, 0.9f, 0.7f), TextAlignmentOptions.Bottom);
        UIFactory.Place(focusHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(900f, 24f));
    }

    private void BuildTitle()
    {
        titlePanel = UIFactory.Node(view.transform, "TitlePanel");
        UIFactory.Stretch(titlePanel);

        Image dim = UIFactory.MakeImage(titlePanel, "Dim", new Color(0.01f, 0.02f, 0.05f, 0.55f));
        UIFactory.Stretch(dim.rectTransform);

        TextMeshProUGUI title = UIFactory.MakeText(titlePanel, "SMART SPAWNER", 72f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(940f, 110f));
        title.characterSpacing = 10f;

        TextMeshProUGUI subtitle = UIFactory.MakeText(titlePanel, "PROCEDURAL CONTENT GENERATION", 22f, UIFactory.Cyan, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 98f), new Vector2(940f, 32f));
        subtitle.characterSpacing = 8f;

        TextMeshProUGUI mission = UIFactory.MakeText(titlePanel,
            "Setiap arena dibangun oleh algoritma PCG: <b>seed</b>, <b>weighted random</b>, dan <b>validasi posisi</b>.\n" +
            "Kumpulkan item bercahaya, hindari atau <b>dash</b> menembus enemy, lalu capai <b>EXIT</b>.",
            22f, new Color(0.85f, 0.92f, 1f), TextAlignmentOptions.Center, FontStyles.Normal, true);
        UIFactory.Place(mission.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(820f, 90f));

        Button start = UIFactory.MakeButton(titlePanel, "START  [ENTER]", 28f, () => gm.StartGame(), new Color(0.1f, 0.55f, 0.65f));
        UIFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(360f, 74f));

        titleSeedText = UIFactory.MakeText(titlePanel, "", 20f, UIFactory.Dim, TextAlignmentOptions.Center);
        UIFactory.Place(titleSeedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(900f, 30f));
    }

    private void BuildLevelComplete()
    {
        levelPanel = UIFactory.Node(view.transform, "LevelCompletePanel");
        UIFactory.Stretch(levelPanel);

        Image dim = UIFactory.MakeImage(levelPanel, "Dim", new Color(0f, 0.04f, 0.02f, 0.45f));
        UIFactory.Stretch(dim.rectTransform);

        Image card = UIFactory.MakeImage(levelPanel, "Card", UIFactory.PanelColor, UIFactory.Rounded, true);
        UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(660f, 380f));

        levelTitle = UIFactory.MakeText(card.transform, "LEVEL 1 COMPLETE", 46f, UIFactory.Green, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(levelTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(620f, 66f));
        levelTitle.characterSpacing = 5f;

        levelStats = UIFactory.MakeText(card.transform, "", 25f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal, true);
        levelStats.lineSpacing = 26f;
        UIFactory.Place(levelStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(600f, 150f));

        Button next = UIFactory.MakeButton(card.transform, "NEXT LEVEL  [ENTER]", 26f, () => gm.NextLevel(), new Color(0.12f, 0.6f, 0.35f));
        UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(400f, 70f));
    }

    private void BuildGameOver()
    {
        overPanel = UIFactory.Node(view.transform, "GameOverPanel");
        UIFactory.Stretch(overPanel);

        Image dim = UIFactory.MakeImage(overPanel, "Dim", new Color(0.08f, 0f, 0.02f, 0.55f));
        UIFactory.Stretch(dim.rectTransform);

        Image card = UIFactory.MakeImage(overPanel, "Card", UIFactory.PanelColor, UIFactory.Rounded, true);
        UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(660f, 380f));

        TextMeshProUGUI title = UIFactory.MakeText(card.transform, "GAME OVER", 58f, UIFactory.Red, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(620f, 80f));
        title.characterSpacing = 8f;

        overStats = UIFactory.MakeText(card.transform, "", 25f, Color.white, TextAlignmentOptions.Center, FontStyles.Normal, true);
        overStats.lineSpacing = 26f;
        UIFactory.Place(overStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(600f, 150f));

        Button retry = UIFactory.MakeButton(card.transform, "RETRY  [R]", 24f, () => gm.RestartLevel(), new Color(0.65f, 0.2f, 0.25f));
        UIFactory.Place((RectTransform)retry.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 30f), new Vector2(270f, 70f));

        Button fresh = UIFactory.MakeButton(card.transform, "NEW SEED  [N]", 24f, () => gm.NewRun(), new Color(0.1f, 0.5f, 0.6f));
        UIFactory.Place((RectTransform)fresh.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 30f), new Vector2(270f, 70f));
    }

    private void BuildPause()
    {
        pausePanel = UIFactory.Node(view.transform, "PausePanel");
        UIFactory.Stretch(pausePanel);

        Image dim = UIFactory.MakeImage(pausePanel, "Dim", new Color(0f, 0.02f, 0.05f, 0.6f));
        UIFactory.Stretch(dim.rectTransform);

        TextMeshProUGUI text = UIFactory.MakeText(pausePanel, "PAUSED", 80f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 120f));
        text.characterSpacing = 16f;
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
        TextMeshProUGUI text = UIFactory.MakeText(view.transform, message, 34f, color, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Place(text.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 50f));

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

        if (Input.GetKeyDown(KeyCode.Tab) && !UIFactory.TextInputFocused())
        {
            focusMode = !focusMode;
            ApplyLayout();
        }

        // Setelah klik slider/tombol, lepas fokus supaya WASD/Space tidak mengubah kontrol UI.
        if (Input.GetMouseButtonUp(0) && !UIFactory.TextInputFocused())
        {
            UIFactory.ClearSelection();
        }

        ApplyState(gm.State);
        UpdateHud(dt);
    }

    private void LateUpdate()
    {
        if (gm == null)
        {
            return;
        }

        EnsureRenderTexture();
        UpdateFloating(Time.unscaledDeltaTime);
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
        Vector2 size = view.rectTransform.rect.size;

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

            Vector3 viewport = cam.WorldToViewportPoint(f.world);
            float k = f.age / PopupLife;

            f.text.rectTransform.anchoredPosition = new Vector2(viewport.x * size.x, viewport.y * size.y);
            f.text.alpha = viewport.z > 0f ? 1f - k * k : 0f;
            f.text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(k * 5f));
        }
    }
}
