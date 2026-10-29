using System.Collections.Generic;
using UnityEngine;

public class PCGSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        public string name;
        public GameObject prefab;

        [Min(0)]
        public int weight = 1;

        [Tooltip("Tinggi pivot object dari ground (Y dunia relatif ke PCGManager).")]
        public float yOffset = 0f;
    }

    /// <summary>Ringkasan satu kali generation (dipakai debug panel dan eksperimen).</summary>
    public class GenerationResult
    {
        public int seed;
        public int enemyRequested, enemySpawned;
        public int itemRequested, itemSpawned;
        public int rejected;
        public int layoutHash;
        public float minPlayerDistance = float.MaxValue;
        public float minPairDistance = float.MaxValue;
        public readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    }

    [Header("Seed")]
    [SerializeField] private bool useRandomSeed = false;
    [SerializeField] private int seed = 12345;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform generatedRoot;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(24f, 24f);
    [SerializeField] private float spawnY = 0.5f;

    [Header("Spawn Counts")]
    [SerializeField] private int enemyCount = 15;
    [SerializeField] private int itemCount = 10;

    [Header("Validation")]
    [SerializeField] private float minDistanceFromPlayer = 5f;
    [SerializeField] private float checkRadius = 0.75f;
    [SerializeField] private float minimumSpacing = 1.5f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private int maxAttemptsPerObject = 50;

    [Header("Extra Features")]
    [SerializeField] private bool randomRotation = true;
    [Tooltip("Jarak minimum item terhadap enemy (di atas minimumSpacing).")]
    [SerializeField] private float minItemDistanceFromEnemy = 2.5f;
    [SerializeField] private int seedHistorySize = 5;

    [Header("Exit (Gameplay)")]
    [SerializeField] private GameObject exitPrefab;
    [SerializeField] private float minExitDistanceFromPlayer = 12f;
    [SerializeField] private float exitYOffset = 0.02f;

    [Header("Presentation")]
    [Tooltip("Jeda antar-object saat animasi pop-in (detik).")]
    [SerializeField] private float popStagger = 0.035f;

    [Header("Enemy Weighted Table")]
    [SerializeField] private SpawnEntry[] enemyEntries;

    [Header("Item Weighted Table")]
    [SerializeField] private SpawnEntry[] itemEntries;

    [Header("Runtime Debug")]
    [SerializeField] private int currentSeed;
    [SerializeField] private int successfulEnemySpawns;
    [SerializeField] private int successfulItemSpawns;
    [SerializeField] private int rejectedPositions;

    private readonly List<Vector3> usedPositions = new List<Vector3>();
    private readonly List<Vector3> enemyPositions = new List<Vector3>();
    private readonly List<int> seedHistory = new List<int>();
    private readonly Dictionary<string, int> spawnStats = new Dictionary<string, int>();

    /// <summary>Dipanggil sebelum generation dimulai (mis. reset player).</summary>
    public event System.Action BeforeGenerate;

    /// <summary>Dipanggil setelah semua object selesai di-spawn.</summary>
    public event System.Action Generated;

    private GenerationResult lastResult;
    private bool instantiateObjects = true;
    private int spawnOrder;

    // ------------------------------------------------------------ properti publik (debug panel / game)

    /// <summary>Jika true, tombol R/N ditangani GameManager, bukan spawner.</summary>
    public bool ExternalControl { get; set; }

    public int Seed => seed;
    public int CurrentSeed => currentSeed;
    public int SuccessfulEnemySpawns => successfulEnemySpawns;
    public int SuccessfulItemSpawns => successfulItemSpawns;
    public int RejectedPositions => rejectedPositions;
    public IReadOnlyDictionary<string, int> SpawnStats => spawnStats;
    public IReadOnlyList<int> SeedHistory => seedHistory;
    public int EnemyEntryCount => enemyEntries != null ? enemyEntries.Length : 0;

    public int EnemyCount { get => enemyCount; set => enemyCount = Mathf.Max(0, value); }
    public int ItemCount { get => itemCount; set => itemCount = Mathf.Max(0, value); }
    public float MinDistanceFromPlayer { get => minDistanceFromPlayer; set => minDistanceFromPlayer = Mathf.Max(0f, value); }
    public float MinimumSpacing { get => minimumSpacing; set => minimumSpacing = Mathf.Max(0f, value); }
    public int MaxAttempts { get => maxAttemptsPerObject; set => maxAttemptsPerObject = Mathf.Max(1, value); }

    public string GetEnemyName(int index) => enemyEntries[index].name;
    public int GetEnemyWeight(int index) => enemyEntries[index].weight;
    public void SetEnemyWeight(int index, int weight) => enemyEntries[index].weight = Mathf.Max(0, weight);

    private void Start()
    {
        Generate();
    }

    private void Update()
    {
        if (ExternalControl)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Generate();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            GenerateWithNewSeed();
        }
    }

    /// <summary>Generate ulang memakai seed yang sedang aktif (atau seed acak jika useRandomSeed).</summary>
    public void Generate()
    {
        Generate(true);
    }

    /// <summary>
    /// instantiate = false menjalankan seluruh algoritma tanpa membuat object
    /// (dipakai untuk eksperimen batch di Editor).
    /// </summary>
    public GenerationResult Generate(bool instantiate)
    {
        instantiateObjects = instantiate;

        BeforeGenerate?.Invoke();

        ClearGeneratedObjects();

        usedPositions.Clear();
        enemyPositions.Clear();
        spawnStats.Clear();
        spawnOrder = 0;

        successfulEnemySpawns = 0;
        successfulItemSpawns = 0;
        rejectedPositions = 0;

        if (useRandomSeed)
        {
            seed = System.Environment.TickCount;
        }

        currentSeed = seed;
        Random.InitState(currentSeed);

        lastResult = new GenerationResult
        {
            seed = currentSeed,
            enemyRequested = enemyCount,
            itemRequested = itemCount
        };

        SpawnGroup(enemyEntries, enemyCount, true);
        SpawnGroup(itemEntries, itemCount, false);

        lastResult.enemySpawned = successfulEnemySpawns;
        lastResult.itemSpawned = successfulItemSpawns;
        lastResult.rejected = rejectedPositions;
        lastResult.layoutHash = ComputeLayoutHash();
        ComputeDistanceMetrics(lastResult);

        foreach (KeyValuePair<string, int> kv in spawnStats)
        {
            lastResult.counts[kv.Key] = kv.Value;
        }

        RecordSeed(currentSeed);

        // Exit dibuat terakhir dan tidak ikut hash/metrik/rejected, supaya hasil eksperimen PCG tidak berubah.
        SpawnExit();

        Generated?.Invoke();

        Debug.Log(
            $"PCG Generation Complete | Seed: {currentSeed} | " +
            $"Enemies: {successfulEnemySpawns}/{enemyCount} | " +
            $"Items: {successfulItemSpawns}/{itemCount} | " +
            $"Rejected Positions: {rejectedPositions}"
        );

        return lastResult;
    }

    /// <summary>Pakai seed baru (acak) lalu generate; seed lama tetap ada di riwayat.</summary>
    public void GenerateWithNewSeed()
    {
        bool previous = useRandomSeed;
        useRandomSeed = true;
        Generate();
        useRandomSeed = previous;
    }

    /// <summary>Pilih seed tertentu lalu generate (tanpa mengubah useRandomSeed permanen).</summary>
    public void GenerateWithSeed(int newSeed)
    {
        bool previous = useRandomSeed;
        useRandomSeed = false;
        seed = newSeed;
        Generate();
        useRandomSeed = previous;
    }

    private void SpawnGroup(SpawnEntry[] entries, int count, bool enemyGroup)
    {
        if (entries == null || entries.Length == 0)
        {
            Debug.LogWarning("Spawn table kosong.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            bool spawned = false;

            for (int attempt = 0; attempt < maxAttemptsPerObject; attempt++)
            {
                Vector3 candidate = GenerateRandomPosition();

                if (!IsPositionValid(candidate, enemyGroup))
                {
                    rejectedPositions++;
                    continue;
                }

                SpawnEntry selected = SelectWeightedEntry(entries);

                if (selected == null)
                {
                    Debug.LogWarning("Tidak ada prefab valid pada weighted table.");
                    return;
                }

                Vector3 spawnPosition = new Vector3(
                    candidate.x,
                    transform.position.y + selected.yOffset,
                    candidate.z
                );

                Quaternion rotation = randomRotation
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : Quaternion.identity;

                if (instantiateObjects)
                {
                    GameObject spawnedObject = Instantiate(
                        selected.prefab,
                        spawnPosition,
                        rotation,
                        generatedRoot
                    );

                    spawnedObject.name = $"{selected.prefab.name}_Generated_{i}";
                    PlayPop(spawnedObject);
                }

                usedPositions.Add(candidate);

                if (enemyGroup)
                {
                    enemyPositions.Add(candidate);
                    successfulEnemySpawns++;
                }
                else
                {
                    successfulItemSpawns++;
                }

                string key = string.IsNullOrEmpty(selected.name)
                    ? selected.prefab.name
                    : selected.name;

                spawnStats.TryGetValue(key, out int current);
                spawnStats[key] = current + 1;

                spawned = true;
                break;
            }

            if (!spawned)
            {
                Debug.LogWarning(
                    $"Gagal spawn object ke-{i}. " +
                    $"Tidak menemukan posisi valid setelah {maxAttemptsPerObject} percobaan."
                );
            }
        }
    }

    private void PlayPop(GameObject spawnedObject)
    {
        if (spawnedObject.TryGetComponent(out SpawnPop pop))
        {
            pop.Play(spawnOrder * popStagger);
        }

        spawnOrder++;
    }

    private void SpawnExit()
    {
        if (exitPrefab == null)
        {
            return;
        }

        for (int attempt = 0; attempt < maxAttemptsPerObject; attempt++)
        {
            Vector3 candidate = GenerateRandomPosition();

            if (player != null)
            {
                float dx = candidate.x - player.position.x;
                float dz = candidate.z - player.position.z;

                if (Mathf.Sqrt(dx * dx + dz * dz) < minExitDistanceFromPlayer)
                {
                    continue;
                }
            }

            if (!IsPositionValid(candidate, false))
            {
                continue;
            }

            if (instantiateObjects)
            {
                GameObject exit = Instantiate(
                    exitPrefab,
                    new Vector3(candidate.x, transform.position.y + exitYOffset, candidate.z),
                    Quaternion.identity,
                    generatedRoot
                );

                exit.name = "Exit";
                PlayPop(exit);
            }

            return;
        }

        Debug.LogWarning("Gagal menempatkan Exit. Longgarkan constraint.");
    }

    private Vector3 GenerateRandomPosition()
    {
        float halfWidth = spawnAreaSize.x * 0.5f;
        float halfDepth = spawnAreaSize.y * 0.5f;

        float randomX = Random.Range(-halfWidth, halfWidth);
        float randomZ = Random.Range(-halfDepth, halfDepth);

        return transform.position + new Vector3(randomX, spawnY, randomZ);
    }

    private bool IsPositionValid(Vector3 candidate, bool enemyGroup)
    {
        // Rule 1: jangan spawn terlalu dekat dengan player.
        if (player != null)
        {
            Vector3 playerFlat = new Vector3(
                player.position.x,
                candidate.y,
                player.position.z
            );

            if (Vector3.Distance(candidate, playerFlat) < minDistanceFromPlayer)
            {
                return false;
            }
        }

        // Rule 2: jangan spawn di dalam obstacle.
        bool blocked = Physics.CheckSphere(
            candidate,
            checkRadius,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        if (blocked)
        {
            return false;
        }

        // Rule 3: jangan terlalu dekat dengan procedural object sebelumnya.
        for (int i = 0; i < usedPositions.Count; i++)
        {
            if (Vector3.Distance(candidate, usedPositions[i]) < minimumSpacing)
            {
                return false;
            }
        }

        // Rule 4 (tambahan): item tidak boleh terlalu dekat dengan enemy.
        if (!enemyGroup)
        {
            for (int i = 0; i < enemyPositions.Count; i++)
            {
                if (Vector3.Distance(candidate, enemyPositions[i]) < minItemDistanceFromEnemy)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private SpawnEntry SelectWeightedEntry(SpawnEntry[] entries)
    {
        int totalWeight = 0;

        foreach (SpawnEntry entry in entries)
        {
            if (entry.prefab != null && entry.weight > 0)
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int randomValue = Random.Range(0, totalWeight);
        int cumulativeWeight = 0;

        foreach (SpawnEntry entry in entries)
        {
            if (entry.prefab == null || entry.weight <= 0)
            {
                continue;
            }

            cumulativeWeight += entry.weight;

            if (randomValue < cumulativeWeight)
            {
                return entry;
            }
        }

        return null;
    }

    private void ClearGeneratedObjects()
    {
        if (generatedRoot == null)
        {
            return;
        }

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = generatedRoot.GetChild(i).gameObject;

            if (Application.isPlaying)
            {
                child.SetActive(false);
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private int ComputeLayoutHash()
    {
        unchecked
        {
            int hash = 17;

            for (int i = 0; i < usedPositions.Count; i++)
            {
                hash = hash * 31 + Mathf.RoundToInt(usedPositions[i].x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(usedPositions[i].z * 100f);
            }

            return hash;
        }
    }

    private void ComputeDistanceMetrics(GenerationResult result)
    {
        if (player != null)
        {
            foreach (Vector3 p in usedPositions)
            {
                float dx = p.x - player.position.x;
                float dz = p.z - player.position.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);

                if (d < result.minPlayerDistance)
                {
                    result.minPlayerDistance = d;
                }
            }
        }

        for (int i = 0; i < usedPositions.Count; i++)
        {
            for (int j = i + 1; j < usedPositions.Count; j++)
            {
                float d = Vector3.Distance(usedPositions[i], usedPositions[j]);

                if (d < result.minPairDistance)
                {
                    result.minPairDistance = d;
                }
            }
        }
    }

    private void RecordSeed(int value)
    {
        if (seedHistory.Count > 0 && seedHistory[seedHistory.Count - 1] == value)
        {
            return;
        }

        seedHistory.Add(value);

        while (seedHistory.Count > seedHistorySize)
        {
            seedHistory.RemoveAt(0);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Vector3 center = transform.position + Vector3.up * spawnY;
        Vector3 size = new Vector3(spawnAreaSize.x, 0.1f, spawnAreaSize.y);

        Gizmos.DrawWireCube(center, size);

        if (player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);
        }
    }
}
