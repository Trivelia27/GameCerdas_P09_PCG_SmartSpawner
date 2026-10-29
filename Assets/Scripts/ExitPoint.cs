using UnityEngine;

/// <summary>Titik exit: merah saat terkunci, hijau (dengan burst) saat terbuka.</summary>
public class ExitPoint : MonoBehaviour
{
    [SerializeField] private Color lockedColor = new Color(1f, 0.2f, 0.25f);
    [SerializeField] private Color unlockedColor = new Color(0.2f, 1f, 0.45f);
    [SerializeField] private Transform ring;
    [SerializeField] private Light pointLight;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    private readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();
    private bool unlocked;
    private float blend;
    private float time;

    public bool Unlocked => unlocked;

    private void Awake()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            materials.AddRange(r.materials);
        }

        Apply(lockedColor);
    }

    public void SetUnlocked(bool value)
    {
        if (value && !unlocked)
        {
            Fx.Burst(transform.position + Vector3.up * 0.3f, unlockedColor, 40, 7f, 0.3f, 0.9f, 0f);
            Fx.Ring(transform.position, unlockedColor, 4.5f, 0.8f, true);
        }

        unlocked = value;
    }

    private void Update()
    {
        time += Time.deltaTime;
        blend = Mathf.MoveTowards(blend, unlocked ? 1f : 0f, Time.deltaTime * 2.5f);

        Color color = Color.Lerp(lockedColor, unlockedColor, blend);
        float pulse = unlocked ? 1f + 0.25f * Mathf.Sin(time * 5f) : 0.7f + 0.15f * Mathf.Sin(time * 2f);

        Apply(color * pulse);

        if (ring != null)
        {
            ring.Rotate(0f, (unlocked ? 70f : 20f) * Time.deltaTime, 0f, Space.World);
        }

        if (pointLight != null)
        {
            pointLight.color = color;
            pointLight.intensity = (unlocked ? 5f : 2.2f) * pulse;
        }
    }

    private void Apply(Color color)
    {
        foreach (Material m in materials)
        {
            if (m.HasProperty(BaseColorId))
            {
                m.SetColor(BaseColorId, color * 0.4f);
            }

            if (m.HasProperty(EmissionColorId))
            {
                m.SetColor(EmissionColorId, color * 2.2f);
            }

            if (m.HasProperty(TintColorId))
            {
                m.SetColor(TintColorId, new Color(color.r, color.g, color.b, 0.5f));
            }
        }
    }
}
