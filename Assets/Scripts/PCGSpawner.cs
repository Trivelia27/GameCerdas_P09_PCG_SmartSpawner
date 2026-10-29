using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public enum RejectReason
{
    None,
    SafeZone,
    Obstacle,
    Spacing
}

public struct RejectedCandidate
{
    public Vector3 position;
    public RejectReason reason;
}

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

        [Tooltip("Warna untuk legenda laporan komposisi.")]
        public Color color = Color.white;
    }

    /// <summary>Ringkasan satu kali generation (dipakai debug panel, eksperimen, dan ekspor CSV).</summary>
    public class GenerationResult
    {
        public int index;
        public int seed;
        public int enemyRequested, enemySpawned;
        public int itemRequested, itemSpawned;
        public int obstacleRequested, obstacleSpawned;
        public int attempts;
        public int rejected;
        public int rejectedSafeZone, rejectedObstacle, rejectedSpacing;
        public int failed;
        public int layoutHash;
        public float milliseconds;
        public float minPlayerDistance = float.MaxValue;
        public float minPairDistance = float.MaxValue;
        public float safeRadius, spacing;
        public int maxAttempts;
        public bool validation, proceduralObstacles;
        public string weights;
        public readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public string Fingerprint => layoutHash.ToString("X8");
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
    [SerializeField] private bool validationEnabled = true;
    [SerializeField] private float minDistanceFromPlayer = 5f;
    [SerializeField] private float checkRadius = 0.75f;
    [SerializeField] private float minimumSpacing = 1.5f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private int maxAttemptsPerObject = 50;

    [Header("Obstacles")]
    [Tooltip("Aktif: obstacle ikut digenerate (Obstacle -> Enemy -> Item). Nonaktif: memakai 4 obstacle tetap.")]
    [SerializeField] private bool proceduralObstacles = false;
    [SerializeField] private int obstacleCount = 8;
    [SerializeField] private float obstacleGap = 1.5f;
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private GameObject[] fixedObstacles;

    [Header("Extra Features")]
    [SerializeField] private bool randomRotation = true;
    [Tooltip("Jarak minimum item terhadap enemy (di atas minimumSpacing).")]
    [SerializeField] private float minItemDistanceFromEnemy = 2.5f;
    [SerializeField] private int seedHistorySize = 5;
    [SerializeField] private int maxRejectedMarkers = 300;

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
    private readonly List<Vector3> obstaclePositions = new List<Vector3>();
    private readonly List<RejectedCandidate> rejectedMarkers = new List<RejectedCandidate>();
    private readonly List<int> seedHistory = new List<int>();
    private readonly List<GenerationResult> history = new List<GenerationResult>();
    private readonly Dictionary<string, int> spawnStats = new Dictionary<string, int>();

    /// <summary>Dipanggil sebelum generation dimulai (mis. reset player).</summary>
    public event System.Action BeforeGenerate;

    /// <summary>Dipanggil setelah semua object selesai di-spawn.</summary>
    public event System.Action Generated;

    private GenerationResult lastResult;
    private bool instantiateObjects = true;
    private int spawnOrder;
    private int attemptsTotal, rejectSafe, rejectObstacle, rejectSpacing, failedSpawns;
    private int obstacleSpawned, obstacleRequested;

    // ------------------------------------------------------------ properti publik (UI / game)

    /// <summary>Jika true, tombol R/N ditangani GameManager, bukan spawner.</summary>
    public bool ExternalControl { get; set; }

    public int Seed => seed;
    public int CurrentSeed => currentSeed;
    public int SuccessfulEnemySpawns => successfulEnemySpawns;
    public int SuccessfulItemSpawns => successfulItemSpawns;
    public int RejectedPositions => rejectedPositions;
    public int AttemptsTotal => attemptsTotal;
    public int RejectedSafeZone => rejectSafe;
    public int RejectedObstacle => rejectObstacle;
    public int RejectedSpacing => rejectSpacing;
    public int FailedSpawns => failedSpawns;
    public int ObstacleSpawned => obstacleSpawned;
    public int ObstacleRequested => obstacleRequested;
    public int LayoutHash => lastResult != null ? lastResult.layoutHash : 0;
    public string Fingerprint => lastResult != null ? lastResult.Fingerprint : "--------";
    public float GenerationMs => lastResult != null ? lastResult.milliseconds : 0f;
    public Vector2 SpawnAreaSize => spawnAreaSize;
    public Vector3 AreaCenter => transform.position;
    public IReadOnlyDictionary<string, int> SpawnStats => spawnStats;
    public IReadOnlyList<int> SeedHistory => seedHistory;
    public IReadOnlyList<GenerationResult> History => history;
    public IReadOnlyList<RejectedCandidate> RejectedCandidates => rejectedMarkers;
    public int EnemyEntryCount => enemyEntries != null ? enemyEntries.Length : 0;
    public int ItemEntryCount => itemEntries != null ? itemEntries.Length : 0;

    public int EnemyCount { get => enemyCount; set => enemyCount = Mathf.Max(0, value); }
    public int ItemCount { get => itemCount; set => itemCount = Mathf.Max(0, value); }
    public int ObstacleCount { get => obstacleCount; set => obstacleCount = Mathf.Max(0, value); }
    public float MinDistanceFromPlayer { get => minDistanceFromPlayer; set => minDistanceFromPlayer = Mathf.Max(0f, value); }
    public float MinimumSpacing { get => minimumSpacing; set => minimumSpacing = Mathf.Max(0f, value); }
    public int MaxAttempts { get => maxAttemptsPerObject; set => maxAttemptsPerObject = Mathf.Max(1, value); }
    public bool ValidationEnabled { get => validationEnabled; set => validationEnabled = value; }
    public bool ProceduralObstacles { get => proceduralObstacles; set => proceduralObstacles = value; }

    public SpawnEntry GetEnemyEntry(int index) => enemyEntries[index];
    public SpawnEntry GetItemEntry(int index) => itemEntries[index];
    public int GetEnemyWeight(int index) => enemyEntries[index].weight;
    public void SetEnemyWeight(int index, int weight) => enemyEntries[index].weight = Mathf.Max(0, weight);

    public int GetStat(string entryName)
    {
        spawnStats.TryGetValue(entryName, out int value);
        return value;
    }

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

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ClearGeneratedObjects();

        usedPositions.Clear();
        enemyPositions.Clear();
        obstaclePositions.Clear();
        rejectedMarkers.Clear();
        spawnStats.Clear();
        spawnOrder = 0;

        successfulEnemySpawns = 0;
        successfulItemSpawns = 0;
        rejectedPositions = 0;
        attemptsTotal = rejectSafe = rejectObstacle = rejectSpacing = failedSpawns = 0;

        if (useRandomSeed)
        {
            seed = System.Environment.TickCount;
        }

        currentSeed = seed;
        Random.InitState(currentSeed);

        lastResult = new GenerationResult
        {
            index = history.Count + 1,
            seed = currentSeed,
            enemyRequested = enemyCount,
            itemRequested = itemCount
        };

        // Urutan pipeline: Obstacle -> Enemy -> Item -> Exit.
        SpawnObstacles();
        SpawnGroup(enemyEntries, enemyCount, true);
        SpawnGroup(itemEntries, itemCount, false);

        lastResult.enemySpawned = successfulEnemySpawns;
        lastResult.itemSpawned = successfulItemSpawns;
        lastResult.obstacleRequested = obstacleRequested;
        lastResult.obstacleSpawned = obstacleSpawned;
        lastResult.attempts = attemptsTotal;
        lastResult.rejected = rejectedPositions;
        lastResult.rejectedSafeZone = rejectSafe;
        lastResult.rejectedObstacle = rejectObstacle;
        lastResult.rejectedSpacing = rejectSpacing;
        lastResult.failed = failedSpawns;
        lastResult.layoutHash = ComputeLayoutHash();
        lastResult.safeRadius = minDistanceFromPlayer;
        lastResult.spacing = minimumSpacing;
        lastResult.maxAttempts = maxAttemptsPerObject;
        lastResult.validation = validationEnabled;
        lastResult.proceduralObstacles = proceduralObstacles;
        lastResult.weights = DescribeWeights();
        ComputeDistanceMetrics(lastResult);

        foreach (KeyValuePair<string, int> kv in spawnStats)
        {
            lastResult.counts[kv.Key] = kv.Value;
        }

        RecordSeed(currentSeed);

        // Exit dibuat terakhir dan tidak ikut hash/metrik/rejected, supaya hasil eksperimen PCG tidak berubah.
        SpawnExit();

        stopwatch.Stop();
        lastResult.milliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;

        history.Add(lastResult);

        if (history.Count > 500)
        {
            history.RemoveAt(0);
        }

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

    // ------------------------------------------------------------------ obstacle

    private void SpawnObstacles()
    {
        bool procedural = proceduralObstacles && obstaclePrefab != null && instantiateObjects;
        int fixedCount = fixedObstacles != null ? fixedObstacles.Length : 0;

        if (fixedObstacles != null)
        {
            foreach (GameObject fixedObstacle in fixedObstacles)
            {
                if (fixedObstacle != null)
                {
                    fixedObstacle.SetActive(!procedural);
                }
            }
        }

        if (!procedural)
        {
            obstacleRequested = fixedCount;
            obstacleSpawned = fixedCount;
            Physics.SyncTransforms();
            return;
        }

        obstacleRequested = obstacleCount;
        obstacleSpawned = 0;

        List<Vector3> placed = new List<Vector3>();
        float halfWidth = spawnAreaSize.x * 0.5f - 1.5f;
        float halfDepth = spawnAreaSize.y * 0.5f - 1.5f;

        for (int i = 0; i < obstacleCount; i++)
        {
            for (int attempt = 0; attempt < maxAttemptsPerObject; attempt++)
            {
                float sizeX = Random.Range(1.6f, 3.8f);
                float sizeZ = Random.Range(1.6f, 3.8f);
                float yaw = Random.Range(0f, 90f);
                float px = Random.Range(-halfWidth, halfWidth);
                float pz = Random.Range(-halfDepth, halfDepth);

                Vector3 position = transform.position + new Vector3(px, 0f, pz);
                float radius = Mathf.Sqrt(sizeX * sizeX + sizeZ * sizeZ) * 0.5f;

                if (player != null && FlatDistance(position, player.position) < minDistanceFromPlayer + radius)
                {
                    continue;
                }

                bool overlap = false;

                foreach (Vector3 other in placed)
                {
                    // other.y menyimpan radius obstacle yang sudah ditempatkan.
                    if (FlatDistance(position, other) < other.y + radius + obstacleGap)
                    {
                        overlap = true;
                        break;
                    }
                }

                if (overlap)
                {
                    continue;
                }

                GameObject obstacle = Instantiate(
                    obstaclePrefab,
                    new Vector3(position.x, transform.position.y + 1f, position.z),
                    Quaternion.Euler(0f, yaw, 0f),
                    generatedRoot
                );

                obstacle.transform.localScale = new Vector3(sizeX, 2f, sizeZ);
                obstacle.name = $"Obstacle_Generated_{i}";

                placed.Add(new Vector3(position.x, radius, position.z));
                obstaclePositions.Add(position);
                obstacleSpawned++;
                break;
            }
        }

        // Collider obstacle baru harus terdaftar sebelum validasi enemy/item (Physics.CheckSphere).
        Physics.SyncTransforms();
    }

    // ------------------------------------------------------------------ enemy / item

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
                attemptsTotal++;

                if (!IsPositionValid(candidate, enemyGroup, out RejectReason reason))
                {
                    rejectedPositions++;
                    CountRejection(candidate, reason);
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
                    // Awake/Start komponen prefab (partikel, audio, dll) tidak boleh menggeser urutan Random PCG,
                    // supaya hasil di game sama dengan simulasi eksperimen untuk seed yang sama.
                    Random.State state = Random.state;

                    GameObject spawnedObject = Instantiate(
                        selected.prefab,
                        spawnPosition,
                        rotation,
                        generatedRoot
                    );

                    spawnedObject.name = $"{selected.prefab.name}_Generated_{i}";
                    PlayPop(spawnedObject);

                    Random.state = state;
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
                failedSpawns++;

                Debug.LogWarning(
                    $"Gagal spawn object ke-{i}. " +
                    $"Tidak menemukan posisi valid setelah {maxAttemptsPerObject} percobaan."
                );
            }
        }
    }

    private void CountRejection(Vector3 candidate, RejectReason reason)
    {
        switch (reason)
        {
            case RejectReason.SafeZone: rejectSafe++; break;
            case RejectReason.Obstacle: rejectObstacle++; break;
            case RejectReason.Spacing: rejectSpacing++; break;
        }

        if (rejectedMarkers.Count < maxRejectedMarkers)
        {
            rejectedMarkers.Add(new RejectedCandidate { position = candidate, reason = reason });
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

            if (player != null && FlatDistance(candidate, player.position) < minExitDistanceFromPlayer)
            {
                continue;
            }

            if (!IsPositionValid(candidate, false, out _, true))
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

    private bool IsPositionValid(Vector3 candidate, bool enemyGroup, out RejectReason reason, bool alwaysValidate = false)
    {
        reason = RejectReason.None;

        if (!validationEnabled && !alwaysValidate)
        {
            return true;
        }

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
                reason = RejectReason.SafeZone;
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
            reason = RejectReason.Obstacle;
            return false;
        }

        // Rule 3: jangan terlalu dekat dengan procedural object sebelumnya.
        for (int i = 0; i < usedPositions.Count; i++)
        {
            if (Vector3.Distance(candidate, usedPositions[i]) < minimumSpacing)
            {
                reason = RejectReason.Spacing;
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
                    reason = RejectReason.Spacing;
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

    // ------------------------------------------------------------------ uji & ekspor

    /// <summary>Mengambil sampel tabel bobot enemy (tanpa mengubah state random Unity) untuk uji distribusi.</summary>
    public Dictionary<string, int> SampleEnemyWeights(int samples)
    {
        Dictionary<string, int> result = new Dictionary<string, int>();
        System.Random rng = new System.Random(currentSeed);

        int total = 0;

        foreach (SpawnEntry entry in enemyEntries)
        {
            if (entry.prefab != null && entry.weight > 0)
            {
                total += entry.weight;
            }
        }

        if (total <= 0)
        {
            return result;
        }

        for (int i = 0; i < samples; i++)
        {
            int value = rng.Next(0, total);
            int cumulative = 0;

            foreach (SpawnEntry entry in enemyEntries)
            {
                if (entry.prefab == null || entry.weight <= 0)
                {
                    continue;
                }

                cumulative += entry.weight;

                if (value < cumulative)
                {
                    result.TryGetValue(entry.name, out int current);
                    result[entry.name] = current + 1;
                    break;
                }
            }
        }

        return result;
    }

    public string DescribeWeights()
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; enemyEntries != null && i < enemyEntries.Length; i++)
        {
            sb.Append(i > 0 ? ":" : "");
            sb.Append(enemyEntries[i].weight);
        }

        return sb.ToString();
    }

    /// <summary>Menulis seluruh riwayat generation ke CSV. Mengembalikan path file.</summary>
    public string ExportCsv()
    {
        string directory = Application.isEditor
            ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Exports")
            : Application.persistentDataPath;

        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, $"pcg_report_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        CultureInfo inv = CultureInfo.InvariantCulture;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("No,Seed,Fingerprint,Enemy,EnemyTarget,Item,ItemTarget,Obstacle,ObstacleTarget,Attempts,Rejected,SafeZone,ObstacleHit,Spacing,Failed,Ms,SafeRadius,MinSpacing,MaxAttempts,Validation,ProceduralObstacles,Weights,Composition");

        foreach (GenerationResult r in history)
        {
            StringBuilder composition = new StringBuilder();

            foreach (KeyValuePair<string, int> kv in r.counts)
            {
                composition.Append($"{kv.Key}={kv.Value};");
            }

            sb.AppendLine(string.Join(",",
                r.index, r.seed, r.Fingerprint,
                r.enemySpawned, r.enemyRequested, r.itemSpawned, r.itemRequested,
                r.obstacleSpawned, r.obstacleRequested,
                r.attempts, r.rejected, r.rejectedSafeZone, r.rejectedObstacle, r.rejectedSpacing, r.failed,
                r.milliseconds.ToString("0.00", inv), r.safeRadius.ToString("0.0", inv), r.spacing.ToString("0.0", inv),
                r.maxAttempts, r.validation, r.proceduralObstacles, r.weights, composition));
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }

    // ------------------------------------------------------------------ internal

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

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
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

            // Obstacle prosedural ikut sidik jari layout (kosong pada mode obstacle tetap).
            for (int i = 0; i < obstaclePositions.Count; i++)
            {
                hash = hash * 31 + Mathf.RoundToInt(obstaclePositions[i].x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(obstaclePositions[i].z * 100f);
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
