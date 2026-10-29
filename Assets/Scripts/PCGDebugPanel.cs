using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Isi panel lab: kontrol generation (kiri), laporan hasil (kanan), status + riwayat seed (bawah).
/// Parameter slider diterapkan saat Generate (R / tombol), bukan setiap kali digeser.
/// </summary>
public class PCGDebugPanel : MonoBehaviour
{
    private static readonly string[] PresetNames = { "Default", "Modul 60:30:10", "Merata", "Dominan Red", "Langka" };

    private static readonly int[][] PresetWeights =
    {
        new[] { 60, 30, 10, 5 },
        new[] { 60, 30, 10, 0 },
        new[] { 33, 33, 34, 0 },
        new[] { 80, 15, 5, 0 },
        new[] { 10, 20, 70, 0 }
    };

    private static readonly int[] PresetSeeds = { 100, 200, 300, 500, 12345 };

    private static readonly Color SafeColor = new Color(0.3f, 1f, 0.5f);
    private static readonly Color ObstacleColor = new Color(1f, 0.3f, 0.35f);
    private static readonly Color SpacingColor = new Color(1f, 0.85f, 0.2f);

    private class CompositionRow
    {
        public PCGSpawner.SpawnEntry entry;
        public TextMeshProUGUI count;
        public RectTransform fill;
    }

    private GameManager gm;
    private PCGSpawner spawner;
    private PCGOverlay overlay;
    private GameHUD hud;

    private TMP_InputField seedInput;
    private Button modeFixedButton;
    private Button modeProceduralButton;
    private Slider enemySlider, itemSlider, obstacleSlider, safeSlider, spacingSlider, attemptsSlider;
    private TextMeshProUGUI enemyValue, itemValue, obstacleValue, safeValue, spacingValue, attemptsValue;
    private Button weightButton;
    private TextMeshProUGUI weightLabel;
    private Toggle validationToggle, rejectedToggle, areaToggle;

    private TextMeshProUGUI seedBig, fingerprint, enemyStat, itemStat, obstacleStat;
    private TextMeshProUGUI attemptsStat, rejectedStat, safeStat, obstacleRejectStat, spacingStat;
    private readonly List<CompositionRow> composition = new List<CompositionRow>();

    private TextMeshProUGUI statusMain;
    private TextMeshProUGUI statusSub;
    private RectTransform historyRow;

    private bool refreshing;

    private void Start()
    {
        gm = GameManager.Instance;
        spawner = gm.Spawner;
        hud = FindFirstObjectByType<GameHUD>();
        overlay = FindFirstObjectByType<PCGOverlay>();

        BuildControls(hud.LeftPanel);
        BuildReport(hud.RightPanel);
        BuildBottom(hud.BottomBar);

        spawner.Generated += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.Generated -= Refresh;
        }
    }

    private void Update()
    {
        if (UIFactory.TextInputFocused())
        {
            return;
        }

        // F4: ganti mode obstacle (tetap / prosedural) lalu generate.
        if (Input.GetKeyDown(KeyCode.F4))
        {
            SetObstacleMode(!spawner.ProceduralObstacles);
            OnGenerate();
        }

        // Hotkey preset seed untuk eksperimen cepat.
        for (int i = 0; i < PresetSeeds.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.F5 + i))
            {
                gm.NewRun(PresetSeeds[i]);
            }
        }
    }

    // ------------------------------------------------------------------ kontrol (kiri)

    private void BuildControls(RectTransform card)
    {
        Label(card, "SEED / IDENTITAS LAYOUT");

        seedInput = UIFactory.MakeInput(card, "contoh: 12345", 26f);
        UIFactory.Size(seedInput.gameObject, -1f, 46f);
        seedInput.onSubmit.AddListener(_ => OnGenerate());

        RectTransform actions = Row(card, 44f);
        AddButton(actions, "GENERATE  [R]", 19f, OnGenerate, new Color(0.1f, 0.5f, 0.62f), 1f, 44f);
        AddButton(actions, "SEED BARU  [N]", 19f, () => gm.NewRun(), new Color(0.38f, 0.2f, 0.55f), 1f, 44f);

        Label(card, "PRESET SEED   (F5 - F9)");

        RectTransform presets = Row(card, 34f);

        foreach (int preset in PresetSeeds)
        {
            int value = preset;
            AddButton(presets, value.ToString(), 16f, () => gm.NewRun(value), new Color(0.12f, 0.3f, 0.4f), 1f, 34f);
        }

        Label(card, "LAYER PCG");

        RectTransform modes = Row(card, 40f);
        modeFixedButton = AddButton(modes, "ENEMY + ITEM", 17f, () => SetObstacleMode(false), Color.gray, 1f, 40f);
        modeProceduralButton = AddButton(modes, "+ OBSTACLE", 17f, () => SetObstacleMode(true), Color.gray, 1f, 40f);

        Label(card, "PARAMETER");

        enemySlider = AddSlider(card, "Enemy", 0f, 60f, true, v => $"{v:0}", out enemyValue, OnCountsChanged);
        itemSlider = AddSlider(card, "Item", 0f, 25f, true, v => $"{v:0}", out itemValue, OnCountsChanged);
        obstacleSlider = AddSlider(card, "Obstacle", 0f, 14f, true, v => $"{v:0}", out obstacleValue, v => spawner.ObstacleCount = Mathf.RoundToInt(v));
        safeSlider = AddSlider(card, "Safe radius", 0f, 10f, false, v => $"{v:0.0}", out safeValue, v => spawner.MinDistanceFromPlayer = Mathf.Round(v * 2f) / 2f);
        spacingSlider = AddSlider(card, "Min spacing", 0f, 5f, false, v => $"{v:0.0}", out spacingValue, v => spawner.MinimumSpacing = Mathf.Round(v * 2f) / 2f);
        attemptsSlider = AddSlider(card, "Max attempts", 1f, 100f, true, v => $"{v:0}", out attemptsValue, v => spawner.MaxAttempts = Mathf.RoundToInt(v));

        Label(card, "BOBOT ENEMY   RED : BLUE : YELLOW : GREEN");

        weightButton = AddButton(card, "", 18f, CycleWeights, new Color(0.14f, 0.24f, 0.34f), 1f, 42f);
        weightLabel = weightButton.GetComponentInChildren<TextMeshProUGUI>();

        validationToggle = UIFactory.MakeToggle(card, "Validasi enemy / item aktif", true, v => spawner.ValidationEnabled = v, 17f);
        rejectedToggle = UIFactory.MakeToggle(card, "Tampilkan rejected candidates (X)", true, v => overlay.ShowRejected = v, 17f);
        areaToggle = UIFactory.MakeToggle(card, "Tampilkan area spawn & safe zone", true, v => overlay.ShowArea = v, 17f);

        RectTransform tests = Row(card, 40f);
        AddButton(tests, "UJI SEED SAMA 3x", 16f, RunSeedTest, new Color(0.2f, 0.28f, 0.4f), 1f, 40f);

        RectTransform tools = Row(card, 40f);
        AddButton(tools, "TES 10K BOBOT", 16f, RunWeightTest, new Color(0.2f, 0.28f, 0.4f), 1f, 40f);
        AddButton(tools, "EKSPOR CSV", 16f, ExportCsv, new Color(0.2f, 0.28f, 0.4f), 1f, 40f);

        TextMeshProUGUI note = UIFactory.MakeText(card, "Parameter baru diterapkan saat Generate.", 15f, UIFactory.Dim, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
        UIFactory.Size(note.gameObject, -1f, 22f);
    }

    private RectTransform Row(Transform parent, float height)
    {
        RectTransform row = UIFactory.Node(parent, "Row");
        UIFactory.Horizontal(row.gameObject, 8f);
        UIFactory.Size(row.gameObject, -1f, height);
        return row;
    }

    private void Label(Transform parent, string text)
    {
        TextMeshProUGUI label = UIFactory.MakeText(parent, text, 14f, UIFactory.Dim, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        label.characterSpacing = 3f;
        UIFactory.Size(label.gameObject, -1f, 22f);
    }

    private Button AddButton(Transform parent, string text, float fontSize, UnityEngine.Events.UnityAction onClick, Color background, float flexible, float height)
    {
        Button button = UIFactory.MakeButton(parent, text, fontSize, onClick, background);
        UIFactory.Size(button.gameObject, -1f, height, flexible);
        return button;
    }

    private Slider AddSlider(Transform parent, string label, float min, float max, bool whole, System.Func<float, string> format,
        out TextMeshProUGUI valueText, UnityEngine.Events.UnityAction<float> apply)
    {
        RectTransform row = Row(parent, 28f);

        TextMeshProUGUI name = UIFactory.MakeText(row, label, 17f, Color.white, TextAlignmentOptions.MidlineLeft);
        UIFactory.Size(name.gameObject, 130f, 28f);

        TextMeshProUGUI valueLabel = UIFactory.MakeText(row, format(min), 17f, UIFactory.Gold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);

        Slider slider = UIFactory.MakeSlider(row, min, max, whole, min, v =>
        {
            valueLabel.text = format(v);

            if (!refreshing)
            {
                apply(v);
            }
        }, UIFactory.Cyan);

        UIFactory.Size(slider.gameObject, -1f, 28f, 1f);
        UIFactory.Size(valueLabel.gameObject, 52f, 28f);

        valueText = valueLabel;
        return slider;
    }

    private void OnCountsChanged(float _)
    {
        gm.SetBaseCounts(Mathf.RoundToInt(enemySlider.value), Mathf.RoundToInt(itemSlider.value));
    }

    private void OnGenerate()
    {
        if (int.TryParse(seedInput.text, out int parsed) && parsed != gm.BaseSeed)
        {
            gm.NewRun(parsed);
        }
        else
        {
            gm.RestartLevel();
        }
    }

    private void SetObstacleMode(bool procedural)
    {
        spawner.ProceduralObstacles = procedural;
        ColorModeButtons();

        SetStatus(
            procedural
                ? "Mode obstacle PROSEDURAL: obstacle ikut digenerate dari seed."
                : "Mode obstacle TETAP: memakai 4 obstacle bawaan scene.",
            "Tekan Generate untuk menerapkan.");
    }

    private void ColorModeButtons()
    {
        SetButtonColor(modeFixedButton, !spawner.ProceduralObstacles);
        SetButtonColor(modeProceduralButton, spawner.ProceduralObstacles);
    }

    private static void SetButtonColor(Button button, bool selected)
    {
        Color background = selected ? new Color(0.1f, 0.5f, 0.62f) : new Color(0.14f, 0.18f, 0.26f);
        ColorBlock colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = Color.Lerp(background, Color.white, 0.28f);
        colors.pressedColor = Color.Lerp(background, Color.black, 0.3f);
        colors.selectedColor = background;
        button.colors = colors;
    }

    private int currentPreset;

    private void CycleWeights()
    {
        currentPreset = (currentPreset + 1) % PresetWeights.Length;
        ApplyPreset(currentPreset);
        UpdateWeightLabel();
        SetStatus($"Bobot enemy diganti: {PresetNames[currentPreset]}.", "Tekan Generate untuk melihat komposisi baru.");
    }

    private void ApplyPreset(int index)
    {
        int[] weights = PresetWeights[index];

        for (int i = 0; i < spawner.EnemyEntryCount; i++)
        {
            spawner.SetEnemyWeight(i, i < weights.Length ? weights[i] : 0);
        }
    }

    private void UpdateWeightLabel()
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < spawner.EnemyEntryCount; i++)
        {
            sb.Append(i > 0 ? " : " : "");
            sb.Append(spawner.GetEnemyWeight(i));
        }

        int match = -1;

        for (int p = 0; p < PresetWeights.Length; p++)
        {
            bool same = true;

            for (int i = 0; i < spawner.EnemyEntryCount; i++)
            {
                int expected = i < PresetWeights[p].Length ? PresetWeights[p][i] : 0;
                same &= spawner.GetEnemyWeight(i) == expected;
            }

            if (same)
            {
                match = p;
                break;
            }
        }

        if (match >= 0)
        {
            currentPreset = match;
        }

        weightLabel.text = $"{sb}   ·   {(match >= 0 ? PresetNames[match] : "Kustom")}";
    }

    // ------------------------------------------------------------------ uji & ekspor

    private void RunSeedTest()
    {
        int seed = int.TryParse(seedInput.text, out int parsed) ? parsed : gm.BaseSeed;
        int first = 0;
        bool same = true;

        for (int i = 0; i < 3; i++)
        {
            gm.NewRun(seed);

            if (i == 0)
            {
                first = spawner.LayoutHash;
            }
            else if (spawner.LayoutHash != first)
            {
                same = false;
            }
        }

        SetStatus(
            same
                ? $"Uji seed sama 3x: LOLOS — fingerprint {first:X8} identik pada 3 generation."
                : "Uji seed sama 3x: GAGAL — fingerprint berbeda antar generation.",
            $"Seed {seed}  ·  reproducibility = seed + parameter + urutan Random call yang sama.");
    }

    private void RunWeightTest()
    {
        const int samples = 10000;
        Dictionary<string, int> counts = spawner.SampleEnemyWeights(samples);

        int total = 0;

        for (int i = 0; i < spawner.EnemyEntryCount; i++)
        {
            total += spawner.GetEnemyWeight(i);
        }

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < spawner.EnemyEntryCount; i++)
        {
            PCGSpawner.SpawnEntry entry = spawner.GetEnemyEntry(i);
            counts.TryGetValue(entry.name, out int count);

            float actual = count * 100f / samples;
            float expected = total > 0 ? entry.weight * 100f / total : 0f;

            sb.Append($"{ShortName(entry.name)} {actual:0.0}% (target {expected:0.0}%)   ");
        }

        SetStatus($"Tes {samples:N0} sampel bobot enemy:  {sb}", "Semakin banyak sampel, hasil makin mendekati bobot relatif.");
    }

    private void ExportCsv()
    {
        try
        {
            string path = spawner.ExportCsv();
            SetStatus($"Ekspor CSV selesai: {spawner.History.Count} generation tercatat.", path);
        }
        catch (System.Exception exception)
        {
            SetStatus("Ekspor CSV gagal.", exception.Message);
        }
    }

    // ------------------------------------------------------------------ laporan (kanan)

    private void BuildReport(RectTransform card)
    {
        Label(card, "CURRENT SEED");

        seedBig = UIFactory.MakeText(card, "0", 46f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        UIFactory.Size(seedBig.gameObject, -1f, 56f);

        Label(card, "LAYOUT FINGERPRINT");

        fingerprint = UIFactory.MakeText(card, "--------", 26f, UIFactory.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        fingerprint.characterSpacing = 4f;
        UIFactory.Size(fingerprint.gameObject, -1f, 36f);

        Spacer(card, 6f);

        enemyStat = StatRow(card, "Enemy", 22f, Color.white);
        itemStat = StatRow(card, "Item", 22f, Color.white);
        obstacleStat = StatRow(card, "Obstacle", 22f, Color.white);

        Spacer(card, 4f);

        attemptsStat = StatRow(card, "Attempts", 17f, UIFactory.Dim);
        rejectedStat = StatRow(card, "Rejected  |  Gagal", 17f, UIFactory.Dim);
        safeStat = StatRow(card, "Safe zone", 17f, SafeColor);
        obstacleRejectStat = StatRow(card, "Obstacle", 17f, ObstacleColor);
        spacingStat = StatRow(card, "Spacing", 17f, SpacingColor);

        Label(card, "KOMPOSISI HASIL");

        for (int i = 0; i < spawner.EnemyEntryCount; i++)
        {
            AddCompositionRow(card, spawner.GetEnemyEntry(i));
        }

        for (int i = 0; i < spawner.ItemEntryCount; i++)
        {
            AddCompositionRow(card, spawner.GetItemEntry(i));
        }
    }

    private void Spacer(Transform parent, float height)
    {
        RectTransform spacer = UIFactory.Node(parent, "Spacer");
        UIFactory.Size(spacer.gameObject, -1f, height);
    }

    private TextMeshProUGUI StatRow(Transform parent, string label, float fontSize, Color color)
    {
        RectTransform row = Row(parent, fontSize + 10f);

        TextMeshProUGUI name = UIFactory.MakeText(row, label, fontSize, color, TextAlignmentOptions.MidlineLeft);
        UIFactory.Size(name.gameObject, -1f, fontSize + 10f, 1f);

        TextMeshProUGUI value = UIFactory.MakeText(row, "", fontSize, color, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        UIFactory.Size(value.gameObject, 150f, fontSize + 10f);

        return value;
    }

    private void AddCompositionRow(Transform parent, PCGSpawner.SpawnEntry entry)
    {
        RectTransform row = Row(parent, 24f);

        Image swatch = UIFactory.MakeImage(row, "Swatch", entry.color, UIFactory.Rounded, true);
        UIFactory.Size(swatch.gameObject, 14f, 14f);

        TextMeshProUGUI name = UIFactory.MakeText(row, ShortName(entry.name), 16f, Color.white, TextAlignmentOptions.MidlineLeft);
        UIFactory.Size(name.gameObject, 84f, 24f);

        Image barBackground = UIFactory.MakeImage(row, "Bar", new Color(1f, 1f, 1f, 0.1f), UIFactory.Rounded, true);
        UIFactory.Size(barBackground.gameObject, -1f, 8f, 1f);

        Image barFill = UIFactory.MakeImage(barBackground.transform, "Fill", entry.color, UIFactory.Rounded, true);
        barFill.rectTransform.anchorMin = Vector2.zero;
        barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        barFill.rectTransform.offsetMin = Vector2.zero;
        barFill.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI count = UIFactory.MakeText(row, "0", 17f, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        UIFactory.Size(count.gameObject, 36f, 24f);

        composition.Add(new CompositionRow { entry = entry, count = count, fill = barFill.rectTransform });
    }

    private static string ShortName(string name)
    {
        int space = name.LastIndexOf(' ');
        return space >= 0 ? name.Substring(space + 1) : name;
    }

    // ------------------------------------------------------------------ bawah (status + riwayat)

    private void BuildBottom(RectTransform bar)
    {
        RectTransform texts = UIFactory.Node(bar, "StatusBlock");
        UIFactory.Vertical(texts.gameObject, 2f, 0);
        UIFactory.Size(texts.gameObject, -1f, -1f, 1f);

        statusMain = UIFactory.MakeText(texts, "", 20f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, true);
        UIFactory.Size(statusMain.gameObject, -1f, 34f);

        statusSub = UIFactory.MakeText(texts, "", 16f, UIFactory.Dim, TextAlignmentOptions.MidlineLeft);
        UIFactory.Size(statusSub.gameObject, -1f, 26f);

        TextMeshProUGUI label = UIFactory.MakeText(bar, "RIWAYAT", 17f, UIFactory.Cyan, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        label.characterSpacing = 4f;
        UIFactory.Size(label.gameObject, 110f, -1f);

        historyRow = UIFactory.Node(bar, "History");
        UIFactory.Horizontal(historyRow.gameObject, 8f);
        UIFactory.Size(historyRow.gameObject, 720f, -1f);
    }

    private void SetStatus(string main, string sub)
    {
        statusMain.text = main;
        statusSub.text = sub;
    }

    private void RebuildHistory()
    {
        for (int i = historyRow.childCount - 1; i >= 0; i--)
        {
            Destroy(historyRow.GetChild(i).gameObject);
        }

        for (int i = spawner.SeedHistory.Count - 1; i >= 0; i--)
        {
            int value = spawner.SeedHistory[i];
            Button button = UIFactory.MakeButton(historyRow, value.ToString(), 17f, () => gm.NewRun(value), new Color(0.14f, 0.22f, 0.32f));
            UIFactory.Size(button.gameObject, -1f, -1f, 1f);
        }
    }

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        if (seedBig == null)
        {
            return;
        }

        refreshing = true;

        if (!seedInput.isFocused)
        {
            seedInput.SetTextWithoutNotify(gm.BaseSeed.ToString());
        }

        enemySlider.SetValueWithoutNotify(gm.BaseEnemyCount);
        itemSlider.SetValueWithoutNotify(gm.BaseItemCount);
        obstacleSlider.SetValueWithoutNotify(spawner.ObstacleCount);
        safeSlider.SetValueWithoutNotify(spawner.MinDistanceFromPlayer);
        spacingSlider.SetValueWithoutNotify(spawner.MinimumSpacing);
        attemptsSlider.SetValueWithoutNotify(spawner.MaxAttempts);

        enemyValue.text = $"{gm.BaseEnemyCount}";
        itemValue.text = $"{gm.BaseItemCount}";
        obstacleValue.text = $"{spawner.ObstacleCount}";
        safeValue.text = $"{spawner.MinDistanceFromPlayer:0.0}";
        spacingValue.text = $"{spawner.MinimumSpacing:0.0}";
        attemptsValue.text = $"{spawner.MaxAttempts}";

        validationToggle.SetIsOnWithoutNotify(spawner.ValidationEnabled);
        rejectedToggle.SetIsOnWithoutNotify(overlay.ShowRejected);
        areaToggle.SetIsOnWithoutNotify(overlay.ShowArea);

        ColorModeButtons();
        UpdateWeightLabel();

        refreshing = false;

        // laporan
        seedBig.text = spawner.CurrentSeed.ToString();
        fingerprint.text = spawner.Fingerprint;

        enemyStat.text = $"{spawner.SuccessfulEnemySpawns} / {spawner.EnemyCount}";
        itemStat.text = $"{spawner.SuccessfulItemSpawns} / {spawner.ItemCount}";
        obstacleStat.text = $"{spawner.ObstacleSpawned} / {spawner.ObstacleRequested}";

        attemptsStat.text = $"{spawner.AttemptsTotal}";
        rejectedStat.text = $"{spawner.RejectedPositions}  |  {spawner.FailedSpawns}";
        safeStat.text = $"{spawner.RejectedSafeZone}";
        obstacleRejectStat.text = $"{spawner.RejectedObstacle}";
        spacingStat.text = $"{spawner.RejectedSpacing}";

        int max = 1;

        foreach (CompositionRow row in composition)
        {
            max = Mathf.Max(max, spawner.GetStat(row.entry.name));
        }

        foreach (CompositionRow row in composition)
        {
            int value = spawner.GetStat(row.entry.name);
            row.count.text = value.ToString();
            row.fill.anchorMax = new Vector2(value / (float)max, 1f);
        }

        RebuildHistory();

        string mode = spawner.ProceduralObstacles ? "obstacle prosedural" : "obstacle tetap";
        string validation = spawner.ValidationEnabled ? "validasi aktif" : "validasi MATI";

        if (spawner.FailedSpawns > 0)
        {
            SetStatus(
                $"Ruang terbatas: {spawner.FailedSpawns} object gagal ditempatkan setelah batas percobaan.",
                $"Seed {spawner.CurrentSeed}  ·  {validation}  ·  {mode}  ·  {spawner.GenerationMs:0.0} ms");
        }
        else
        {
            SetStatus(
                $"Layout dibuat dari seed {spawner.CurrentSeed}: {spawner.RejectedPositions} kandidat ditolak dalam {spawner.AttemptsTotal} percobaan.",
                $"{validation}  ·  {mode}  ·  fingerprint {spawner.Fingerprint}  ·  {spawner.GenerationMs:0.0} ms");
        }
    }
}
