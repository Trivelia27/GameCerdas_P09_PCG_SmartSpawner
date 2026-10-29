using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel debug PCG (TAB untuk sembunyikan): seed, jumlah spawn, rejected, statistik per jenis,
/// preset seed, riwayat seed, serta slider parameter PCG yang langsung me-regenerate arena.
/// </summary>
public class PCGDebugPanel : MonoBehaviour
{
    private const float RegenerateDelay = 0.35f;

    private static readonly int[] PresetSeeds = { 100, 200, 300, 500, 12345 };

    private GameManager gm;
    private PCGSpawner spawner;

    private RectTransform panel;
    private TextMeshProUGUI summary;
    private RectTransform historyRow;

    private Slider enemySlider;
    private Slider itemSlider;
    private Slider safeSlider;
    private Slider spacingSlider;
    private Slider attemptsSlider;
    private Slider[] weightSliders;

    private TextMeshProUGUI enemyValue;
    private TextMeshProUGUI itemValue;
    private TextMeshProUGUI safeValue;
    private TextMeshProUGUI spacingValue;
    private TextMeshProUGUI attemptsValue;
    private TextMeshProUGUI[] weightValues;

    private float regenerateTimer;
    private bool refreshing;

    private void Start()
    {
        gm = GameManager.Instance;
        spawner = gm.Spawner;

        Build(FindFirstObjectByType<GameHUD>().Root);

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
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            panel.gameObject.SetActive(!panel.gameObject.activeSelf);
        }

        if (regenerateTimer > 0f)
        {
            regenerateTimer -= Time.unscaledDeltaTime;

            if (regenerateTimer <= 0f)
            {
                gm.SetBaseCounts(Mathf.RoundToInt(enemySlider.value), Mathf.RoundToInt(itemSlider.value));
                gm.RestartLevel();
            }
        }
    }

    // ------------------------------------------------------------------ build

    private void Build(RectTransform parent)
    {
        panel = UIFactory.Node(parent, "PCGDebugPanel");
        UIFactory.Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -190f), new Vector2(410f, 100f));

        Image background = panel.gameObject.AddComponent<Image>();
        background.sprite = UIFactory.Rounded;
        background.type = Image.Type.Sliced;
        background.color = UIFactory.PanelColor;

        UIFactory.Vertical(panel.gameObject, 7f, 16);

        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform header = Row(panel, 30f);
        TextMeshProUGUI title = UIFactory.MakeText(header, "PCG DEBUG", 26f, UIFactory.Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
        title.characterSpacing = 4f;
        UIFactory.Size(title.gameObject, -1f, 30f, 1f);

        TextMeshProUGUI hint = UIFactory.MakeText(header, "TAB: sembunyikan", 18f, UIFactory.Dim, TextAlignmentOptions.Right);
        UIFactory.Size(hint.gameObject, 160f, 30f);

        summary = UIFactory.MakeText(panel, "", 20f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal, true);

        AddLabel("SEED PRESET");
        RectTransform presets = Row(panel, 36f);

        foreach (int preset in PresetSeeds)
        {
            int value = preset;
            Button button = UIFactory.MakeButton(presets, value.ToString(), 18f, () => gm.NewRun(value), new Color(0.12f, 0.3f, 0.4f));
            UIFactory.Size(button.gameObject, -1f, 36f, 1f);
        }

        Button random = UIFactory.MakeButton(presets, "ACAK", 18f, () => gm.NewRun(), new Color(0.35f, 0.2f, 0.5f));
        UIFactory.Size(random.gameObject, -1f, 36f, 1.2f);

        AddLabel("PARAMETER PCG");

        enemySlider = AddSlider("Enemy Count", 0f, 40f, true, gm.BaseEnemyCount, out enemyValue, v => $"{v:0}", OnParameterChanged);
        itemSlider = AddSlider("Item Count", 0f, 25f, true, gm.BaseItemCount, out itemValue, v => $"{v:0}", OnParameterChanged);
        safeSlider = AddSlider("Safe Distance", 0f, 10f, false, spawner.MinDistanceFromPlayer, out safeValue, v => $"{v:0.0}", OnParameterChanged);
        spacingSlider = AddSlider("Min Spacing", 0f, 5f, false, spawner.MinimumSpacing, out spacingValue, v => $"{v:0.0}", OnParameterChanged);
        attemptsSlider = AddSlider("Max Attempts", 1f, 100f, true, spawner.MaxAttempts, out attemptsValue, v => $"{v:0}", OnParameterChanged);

        AddLabel("BOBOT ENEMY (WEIGHTED RANDOM)");

        int entries = spawner.EnemyEntryCount;
        weightSliders = new Slider[entries];
        weightValues = new TextMeshProUGUI[entries];

        for (int i = 0; i < entries; i++)
        {
            int index = i;
            weightSliders[i] = AddSlider(spawner.GetEnemyName(i), 0f, 100f, true, spawner.GetEnemyWeight(i), out weightValues[i], v => $"{v:0}", OnParameterChanged,
                new Color(1f, 0.55f, 0.3f));
        }

        RectTransform actions = Row(panel, 46f);
        Button regenerate = UIFactory.MakeButton(actions, "REGENERATE [R]", 20f, () => gm.RestartLevel(), new Color(0.12f, 0.5f, 0.6f));
        UIFactory.Size(regenerate.gameObject, -1f, 46f, 1f);

        Button newSeed = UIFactory.MakeButton(actions, "SEED BARU [N]", 20f, () => gm.NewRun(), new Color(0.4f, 0.22f, 0.55f));
        UIFactory.Size(newSeed.gameObject, -1f, 46f, 1f);

        AddLabel("RIWAYAT SEED (KLIK UNTUK PAKAI LAGI)");
        historyRow = Row(panel, 34f);
    }

    private RectTransform Row(Transform parent, float height)
    {
        RectTransform row = UIFactory.Node(parent, "Row");
        UIFactory.Horizontal(row.gameObject, 8f);
        UIFactory.Size(row.gameObject, -1f, height);
        return row;
    }

    private void AddLabel(string text)
    {
        TextMeshProUGUI label = UIFactory.MakeText(panel, text, 17f, UIFactory.Dim, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        label.characterSpacing = 3f;
        UIFactory.Size(label.gameObject, -1f, 26f);
    }

    private Slider AddSlider(string label, float min, float max, bool whole, float value, out TextMeshProUGUI valueText,
        System.Func<float, string> format, UnityEngine.Events.UnityAction<float> onChange, Color? fill = null)
    {
        RectTransform row = Row(panel, 30f);

        TextMeshProUGUI name = UIFactory.MakeText(row, label, 19f, Color.white, TextAlignmentOptions.MidlineLeft);
        UIFactory.Size(name.gameObject, 150f, 30f);

        TextMeshProUGUI valueLabel = UIFactory.MakeText(row, format(value), 19f, UIFactory.Gold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);

        Slider slider = UIFactory.MakeSlider(row, min, max, whole, value, v =>
        {
            valueLabel.text = format(v);

            if (!refreshing)
            {
                onChange(v);
            }
        }, fill ?? UIFactory.Cyan);

        UIFactory.Size(slider.gameObject, -1f, 30f, 1f);
        UIFactory.Size(valueLabel.gameObject, 56f, 30f);

        valueText = valueLabel;
        return slider;
    }

    // ------------------------------------------------------------------ logika

    private void OnParameterChanged(float _)
    {
        spawner.MinDistanceFromPlayer = Mathf.Round(safeSlider.value * 2f) / 2f;
        spawner.MinimumSpacing = Mathf.Round(spacingSlider.value * 2f) / 2f;
        spawner.MaxAttempts = Mathf.RoundToInt(attemptsSlider.value);

        for (int i = 0; i < weightSliders.Length; i++)
        {
            spawner.SetEnemyWeight(i, Mathf.RoundToInt(weightSliders[i].value));
        }

        regenerateTimer = RegenerateDelay;
    }

    private void Refresh()
    {
        if (panel == null)
        {
            return;
        }

        refreshing = true;

        enemySlider.SetValueWithoutNotify(gm.BaseEnemyCount);
        itemSlider.SetValueWithoutNotify(gm.BaseItemCount);
        safeSlider.SetValueWithoutNotify(spawner.MinDistanceFromPlayer);
        spacingSlider.SetValueWithoutNotify(spawner.MinimumSpacing);
        attemptsSlider.SetValueWithoutNotify(spawner.MaxAttempts);

        enemyValue.text = $"{gm.BaseEnemyCount}";
        itemValue.text = $"{gm.BaseItemCount}";
        safeValue.text = $"{spawner.MinDistanceFromPlayer:0.0}";
        spacingValue.text = $"{spawner.MinimumSpacing:0.0}";
        attemptsValue.text = $"{spawner.MaxAttempts}";

        for (int i = 0; i < weightSliders.Length; i++)
        {
            weightSliders[i].SetValueWithoutNotify(spawner.GetEnemyWeight(i));
            weightValues[i].text = $"{spawner.GetEnemyWeight(i)}";
        }

        refreshing = false;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Seed  <color=#4DF2FF><b>{spawner.CurrentSeed}</b></color>   <color=#8CA3BA>(base {gm.BaseSeed} · level {gm.Level})</color>");
        sb.AppendLine($"Enemy <b>{spawner.SuccessfulEnemySpawns}/{spawner.EnemyCount}</b>   Item <b>{spawner.SuccessfulItemSpawns}/{spawner.ItemCount}</b>   Rejected <color=#FFD14D><b>{spawner.RejectedPositions}</b></color>");

        int column = 0;

        foreach (var kv in spawner.SpawnStats)
        {
            sb.Append($"<color=#8CA3BA>{kv.Key}</color> <b>{kv.Value}</b>");
            sb.Append(++column % 2 == 0 ? "\n" : "    ");
        }

        summary.text = sb.ToString();

        RebuildHistory();
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
            Button button = UIFactory.MakeButton(historyRow, value.ToString(), 15f, () => gm.NewRun(value), new Color(0.15f, 0.22f, 0.32f));
            UIFactory.Size(button.gameObject, -1f, 34f, 1f);
        }
    }
}
