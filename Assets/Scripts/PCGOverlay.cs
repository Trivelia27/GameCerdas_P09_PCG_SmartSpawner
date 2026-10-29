using UnityEngine;

/// <summary>
/// Overlay visual proses validasi PCG di arena: garis area spawn (cyan), lingkaran safe zone (hijau),
/// dan tanda X untuk kandidat yang ditolak (hijau = safe zone, merah = obstacle, kuning = spacing).
/// </summary>
public class PCGOverlay : MonoBehaviour
{
    private static readonly Color AreaColor = new Color(0.3f, 0.9f, 1f, 0.9f);
    private static readonly Color SafeColor = new Color(0.3f, 1f, 0.5f, 0.95f);
    private static readonly Color ObstacleColor = new Color(1f, 0.3f, 0.35f, 1f);
    private static readonly Color SpacingColor = new Color(1f, 0.85f, 0.2f, 1f);

    [SerializeField] private PCGSpawner spawner;
    [SerializeField] private Transform player;

    private LineRenderer areaLine;
    private LineRenderer safeLine;
    private ParticleSystem markers;
    private ParticleSystemRenderer markerRenderer;
    private bool showRejected = true;
    private bool showArea = true;

    public bool ShowRejected
    {
        get => showRejected;
        set
        {
            showRejected = value;
            ApplyVisibility();
        }
    }

    public bool ShowArea
    {
        get => showArea;
        set
        {
            showArea = value;
            ApplyVisibility();
        }
    }

    private void Start()
    {
        areaLine = CreateLine("AreaLine", 4, AreaColor, 0.1f);
        safeLine = CreateLine("SafeZoneLine", 72, SafeColor, 0.1f);
        CreateMarkers();

        spawner.Generated += Rebuild;
        Rebuild();
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.Generated -= Rebuild;
        }
    }

    private LineRenderer CreateLine(string lineName, int points, Color color, float width)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = points;
        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = color;
        line.sharedMaterial = Resources.Load<Material>("Fx/FxLine");
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void CreateMarkers()
    {
        GameObject go = new GameObject("RejectedMarkers");
        go.SetActive(false);
        go.transform.SetParent(transform, false);

        markers = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = markers.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;
        main.startLifetime = 100000f;
        main.startSpeed = 0f;

        ParticleSystem.EmissionModule emission = markers.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = markers.shape;
        shape.enabled = false;

        markerRenderer = go.GetComponent<ParticleSystemRenderer>();
        markerRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        markerRenderer.sharedMaterial = Resources.Load<Material>("Fx/FxX");

        go.SetActive(true);
    }

    private void Rebuild()
    {
        Vector3 center = spawner.AreaCenter;
        Vector2 size = spawner.SpawnAreaSize;
        float y = 0.07f;

        float hx = size.x * 0.5f;
        float hz = size.y * 0.5f;

        areaLine.SetPosition(0, center + new Vector3(-hx, y, -hz));
        areaLine.SetPosition(1, center + new Vector3(-hx, y, hz));
        areaLine.SetPosition(2, center + new Vector3(hx, y, hz));
        areaLine.SetPosition(3, center + new Vector3(hx, y, -hz));

        Vector3 origin = player != null ? player.position : center;
        float radius = Mathf.Max(0.05f, spawner.MinDistanceFromPlayer);

        for (int i = 0; i < safeLine.positionCount; i++)
        {
            float angle = i / (float)safeLine.positionCount * Mathf.PI * 2f;
            safeLine.SetPosition(i, new Vector3(origin.x + Mathf.Cos(angle) * radius, y, origin.z + Mathf.Sin(angle) * radius));
        }

        markers.Clear();

        foreach (RejectedCandidate candidate in spawner.RejectedCandidates)
        {
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(candidate.position.x, y + 0.01f, candidate.position.z),
                startColor = ColorFor(candidate.reason),
                startSize = 0.62f,
                startLifetime = 100000f,
                velocity = Vector3.zero
            };

            markers.Emit(emit, 1);
        }

        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (areaLine != null)
        {
            areaLine.enabled = showArea;
        }

        if (safeLine != null)
        {
            safeLine.enabled = showArea;
        }

        if (markerRenderer != null)
        {
            markerRenderer.enabled = showRejected;
        }
    }

    private static Color ColorFor(RejectReason reason)
    {
        switch (reason)
        {
            case RejectReason.SafeZone: return SafeColor;
            case RejectReason.Obstacle: return ObstacleColor;
            default: return SpacingColor;
        }
    }
}
