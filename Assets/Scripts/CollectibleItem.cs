using UnityEngine;

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Legendary
}

/// <summary>Item yang bisa diambil Player (deteksi jarak dilakukan GameManager).</summary>
public class CollectibleItem : MonoBehaviour
{
    [SerializeField] private string displayName = "Item";
    [SerializeField] private int points = 1;
    [SerializeField] private int heal = 0;
    [SerializeField] private ItemRarity rarity = ItemRarity.Common;
    [SerializeField] private Color fxColor = Color.yellow;

    [Header("Motion")]
    [SerializeField] private Transform visual;
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float magnetRadius = 3.2f;

    private SpawnPop pop;
    private float visualBaseY;
    private float bobOffset;

    public string DisplayName => displayName;
    public int Points => points;
    public int Heal => heal;
    public ItemRarity Rarity => rarity;
    public Color FxColor => fxColor;
    public Vector3 VisualPosition => visual != null ? visual.position : transform.position;

    public void Configure(string itemName, int itemPoints, int itemHeal, ItemRarity itemRarity, Color color)
    {
        displayName = itemName;
        points = itemPoints;
        heal = itemHeal;
        rarity = itemRarity;
        fxColor = color;
    }

    private void Awake()
    {
        pop = GetComponent<SpawnPop>();
        bobOffset = Random.value * 10f;

        if (visual != null)
        {
            visualBaseY = visual.localPosition.y;
        }
    }

    private void Start()
    {
        if (rarity >= ItemRarity.Rare)
        {
            Fx.AttachSparkle(transform, new Vector3(0f, 0.8f, 0f), fxColor, rarity == ItemRarity.Legendary ? 16f : 9f);
        }
    }

    private void Update()
    {
        if (visual != null)
        {
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            visual.localPosition = new Vector3(0f, visualBaseY + Mathf.Sin(Time.time * 2.4f + bobOffset) * bobHeight, 0f);
        }

        ApplyMagnet();
    }

    private void ApplyMagnet()
    {
        GameManager gm = GameManager.Instance;

        if (gm == null || !gm.IsPlaying || (pop != null && !pop.Done))
        {
            return;
        }

        Vector3 toPlayer = gm.Player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (distance > magnetRadius || distance < 0.05f)
        {
            return;
        }

        float pull = Mathf.Lerp(2f, 9f, 1f - distance / magnetRadius);
        transform.position += toPlayer / distance * (pull * Time.deltaTime);
    }
}
