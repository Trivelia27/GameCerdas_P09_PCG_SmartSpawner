using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameState
{
    Title,
    Playing,
    Paused,
    LevelComplete,
    GameOver
}

/// <summary>
/// Aturan permainan: kumpulkan item, hindari/hancurkan enemy, lalu capai Exit.
/// Setiap level memakai seed turunan (baseSeed + level - 1) sehingga tetap reproducible.
/// </summary>
public class GameManager : MonoBehaviour
{
    private const string BestScoreKey = "P09_BestScore";

    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PCGSpawner spawner;
    [SerializeField] private Transform player;
    [SerializeField] private AutoPilot autoPilot;

    [Header("Rules")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private int baseRequiredItems = 6;
    [SerializeField] private int enemyGrowthPerLevel = 2;
    [SerializeField] private int maxEnemies = 40;
    [SerializeField] private float pickupRadius = 1.2f;
    [SerializeField] private float exitRadius = 1.5f;
    [SerializeField] private float invulnerableSeconds = 1.2f;
    [SerializeField] private float autoContinueDelay = 3f;

    public event Action<string, Color> Toast;
    public event Action<Vector3, string, Color> Popup;
    public event Action Damaged;

    private readonly List<CollectibleItem> items = new List<CollectibleItem>();
    private ExitPoint exitPoint;
    private SimplePlayerController playerController;
    private Transform playerVisual;
    private Vector3 playerStart;

    private int baseEnemyCount;
    private int baseItemCount;
    private int levelStartScore;
    private int levelStartHealth;
    private float invulnerableTimer;
    private float stateTimer;
    private bool unlockAnnounced;

    public GameState State { get; private set; } = GameState.Title;
    public int Level { get; private set; } = 1;
    public int BaseSeed { get; private set; }
    public int Score { get; private set; }
    public int Health { get; private set; }
    public int Collected { get; private set; }
    public int Required { get; private set; }
    public int TotalItems { get; private set; }
    public int Kills { get; private set; }
    public int LastBonus { get; private set; }
    public float LevelTime { get; private set; }
    public int BestScore { get; private set; }

    public int MaxHealth => maxHealth;
    public int LevelSeed => BaseSeed + Level - 1;
    public int NextLevelSeed => BaseSeed + Level;
    public int CurrentSeed => spawner.CurrentSeed;
    public bool ExitUnlocked => exitPoint != null && exitPoint.Unlocked;
    public bool IsPlaying => State == GameState.Playing;
    public Transform Player => player;
    public PCGSpawner Spawner => spawner;
    public AutoPilot AutoPilotComponent => autoPilot;
    public int BaseEnemyCount => baseEnemyCount;
    public int BaseItemCount => baseItemCount;

    private void Awake()
    {
        Instance = this;

        spawner.ExternalControl = true;

        playerStart = player.position;
        playerController = player.GetComponent<SimplePlayerController>();
        playerVisual = player.Find("Visual");

        BaseSeed = spawner.Seed;
        baseEnemyCount = spawner.EnemyCount;
        baseItemCount = spawner.ItemCount;

        Health = maxHealth;
        levelStartHealth = maxHealth;
        BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
    }

    private void OnEnable()
    {
        spawner.BeforeGenerate += OnBeforeGenerate;
        spawner.Generated += OnGenerated;
    }

    private void OnDisable()
    {
        spawner.BeforeGenerate -= OnBeforeGenerate;
        spawner.Generated -= OnGenerated;
    }

    public void SetBaseCounts(int enemies, int itemCount)
    {
        baseEnemyCount = Mathf.Max(0, enemies);
        baseItemCount = Mathf.Max(0, itemCount);
    }

    // ------------------------------------------------------------------ alur permainan

    public void StartGame()
    {
        if (State != GameState.Title)
        {
            return;
        }

        State = GameState.Playing;
        stateTimer = 0f;

        AudioManager.Play(Sfx.Click);
        Toast?.Invoke($"LEVEL {Level}", new Color(0.35f, 0.95f, 1f));
    }

    public void RestartLevel()
    {
        GenerateLevel(State != GameState.Title);
    }

    public void NewRun()
    {
        NewRun(UnityEngine.Random.Range(1, 1000000));
    }

    public void NewRun(int seed)
    {
        BaseSeed = seed;
        Level = 1;
        levelStartScore = 0;
        levelStartHealth = maxHealth;
        Score = 0;

        GenerateLevel(State != GameState.Title);
    }

    public void NextLevel()
    {
        Level++;
        levelStartScore = Score;
        levelStartHealth = Mathf.Min(maxHealth, Health + 1);

        GenerateLevel(true);
        Toast?.Invoke($"LEVEL {Level}", new Color(0.35f, 0.95f, 1f));
    }

    private void GenerateLevel(bool play)
    {
        spawner.EnemyCount = Mathf.Min(baseEnemyCount + (Level - 1) * enemyGrowthPerLevel, maxEnemies);
        spawner.ItemCount = baseItemCount;
        spawner.GenerateWithSeed(LevelSeed);

        State = play ? GameState.Playing : GameState.Title;
        stateTimer = 0f;
    }

    private void OnBeforeGenerate()
    {
        Time.timeScale = 1f;

        playerController.ResetState(playerStart);

        Score = levelStartScore;
        Health = levelStartHealth;
        Collected = 0;
        Kills = 0;
        LevelTime = 0f;
        invulnerableTimer = 0f;
        unlockAnnounced = false;

        if (playerVisual != null)
        {
            playerVisual.gameObject.SetActive(true);
        }
    }

    private void OnGenerated()
    {
        // Seed tidak cocok dengan level saat ini (mis. useRandomSeed): anggap sebagai run baru.
        if (spawner.CurrentSeed != LevelSeed)
        {
            BaseSeed = spawner.CurrentSeed;
            Level = 1;
        }

        items.Clear();
        items.AddRange(FindObjectsByType<CollectibleItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        exitPoint = FindFirstObjectByType<ExitPoint>(FindObjectsInactive.Exclude);

        TotalItems = items.Count;
        Required = Mathf.Min(baseRequiredItems + (Level - 1) / 2, TotalItems);

        if (exitPoint != null && Required == 0)
        {
            exitPoint.SetUnlocked(true);
        }
    }

    // ------------------------------------------------------------------ update

    private void Update()
    {
        HandleGlobalInput();

        stateTimer += Time.unscaledDeltaTime;

        switch (State)
        {
            case GameState.Title:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    (autoPilot != null && autoPilot.Active && stateTimer > 1f))
                {
                    StartGame();
                }
                break;

            case GameState.Playing:
                TickPlaying();
                break;

            case GameState.LevelComplete:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    (autoPilot != null && autoPilot.Active && stateTimer > autoContinueDelay))
                {
                    NextLevel();
                }
                break;

            case GameState.GameOver:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    RestartLevel();
                }
                break;
        }
    }

    private void HandleGlobalInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
        else if (Input.GetKeyDown(KeyCode.N))
        {
            NewRun();
        }

        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (State == GameState.Playing)
            {
                State = GameState.Paused;
                Time.timeScale = 0f;
            }
            else if (State == GameState.Paused)
            {
                State = GameState.Playing;
                Time.timeScale = 1f;
            }
        }
    }

    private void TickPlaying()
    {
        LevelTime += Time.deltaTime;

        if (invulnerableTimer > 0f)
        {
            invulnerableTimer -= Time.deltaTime;

            if (playerVisual != null)
            {
                playerVisual.gameObject.SetActive(invulnerableTimer <= 0f || Mathf.FloorToInt(invulnerableTimer * 14f) % 2 == 0);
            }
        }

        CollectItems();
        CheckExit();
    }

    private void CollectItems()
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            CollectibleItem item = items[i];

            if (item == null)
            {
                items.RemoveAt(i);
                continue;
            }

            if (FlatDistance(item.transform.position, player.position) > pickupRadius)
            {
                continue;
            }

            Collect(item);
            items.RemoveAt(i);
        }

        if (!unlockAnnounced && exitPoint != null && Collected >= Required)
        {
            unlockAnnounced = true;
            exitPoint.SetUnlocked(true);

            AudioManager.Play(Sfx.Unlock);
            Toast?.Invoke("EXIT UNLOCKED — GO!", new Color(0.3f, 1f, 0.5f));
        }
    }

    private void Collect(CollectibleItem item)
    {
        Collected++;
        Score += item.Points;
        Health = Mathf.Min(maxHealth, Health + item.Heal);

        Vector3 at = item.VisualPosition;
        int burst = 14 + (int)item.Rarity * 10;

        Fx.Burst(at, item.FxColor, burst, 5f + (int)item.Rarity, 0.2f, 0.6f);
        Fx.Ring(item.transform.position, item.FxColor, 1.8f + (int)item.Rarity * 0.5f, 0.45f);

        AudioManager.Play(item.Rarity >= ItemRarity.Rare ? Sfx.Rare : Sfx.Pickup, 1f, UnityEngine.Random.Range(0.95f, 1.1f));

        Popup?.Invoke(at, $"+{item.Points}", item.FxColor);

        if (item.Heal > 0)
        {
            Popup?.Invoke(at + Vector3.up * 0.8f, $"+{item.Heal} HP", new Color(1f, 0.4f, 0.5f));
        }

        if (item.Rarity >= ItemRarity.Rare)
        {
            Toast?.Invoke(item.DisplayName.ToUpperInvariant(), item.FxColor);
            CameraRig.Kick(2f);
        }

        item.gameObject.SetActive(false);
        Destroy(item.gameObject);
    }

    private void CheckExit()
    {
        if (exitPoint == null || FlatDistance(exitPoint.transform.position, player.position) > exitRadius)
        {
            return;
        }

        if (Collected >= Required)
        {
            CompleteLevel();
        }
        else if (stateTimer > 1f)
        {
            stateTimer = 0f;
            Toast?.Invoke($"Exit terkunci — kumpulkan {Required - Collected} item lagi", new Color(1f, 0.45f, 0.45f));
        }
    }

    private void CompleteLevel()
    {
        int timeBonus = Mathf.Max(0, 200 - Mathf.RoundToInt(LevelTime) * 3);
        LastBonus = 100 * Level + timeBonus;
        Score += LastBonus;

        State = GameState.LevelComplete;
        stateTimer = 0f;

        SaveBest();

        Vector3 at = exitPoint.transform.position + Vector3.up * 0.5f;
        Fx.Burst(at, new Color(0.3f, 1f, 0.5f), 60, 9f, 0.3f, 1.1f, 0.2f);
        Fx.Ring(exitPoint.transform.position, new Color(0.3f, 1f, 0.5f), 7f, 1f, true);

        AudioManager.Play(Sfx.Win);
        CameraRig.Kick(5f);
        PostFxController.Pulse(0.6f, 0.1f);
    }

    // ------------------------------------------------------------------ event dari entity

    public void DamagePlayer(int damage, Vector3 source)
    {
        if (State != GameState.Playing || invulnerableTimer > 0f || playerController.IsDashing)
        {
            return;
        }

        Health -= damage;
        invulnerableTimer = invulnerableSeconds;

        playerController.Knockback(player.position - source, 10f);

        Fx.Burst(player.position, new Color(1f, 0.3f, 0.35f), 22, 6f, 0.22f, 0.6f);

        AudioManager.Play(Sfx.Hit);
        CameraRig.Shake(0.6f);
        PostFxController.Pulse(1f, 0.25f);
        Popup?.Invoke(player.position + Vector3.up, $"-{damage}", new Color(1f, 0.35f, 0.4f));
        Damaged?.Invoke();

        if (Health <= 0)
        {
            Health = 0;
            GameOver();
        }
    }

    public void OnEnemyKilled(EnemyChaser enemy)
    {
        Kills++;
        Score += enemy.ScoreValue;

        AudioManager.Play(Sfx.Kill);
        CameraRig.Shake(0.25f);
        Popup?.Invoke(enemy.transform.position + Vector3.up, $"+{enemy.ScoreValue}", enemy.Tint);
    }

    private void GameOver()
    {
        State = GameState.GameOver;
        stateTimer = 0f;

        if (playerVisual != null)
        {
            playerVisual.gameObject.SetActive(false);
        }

        Fx.Burst(player.position, Color.white, 50, 9f, 0.28f, 1f, 0.3f);
        Fx.Ring(player.position - Vector3.up, new Color(1f, 0.3f, 0.35f), 6f, 0.9f);

        SaveBest();

        AudioManager.Play(Sfx.Lose);
        CameraRig.Shake(1f);
    }

    private void SaveBest()
    {
        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
        }
    }

    // ------------------------------------------------------------------ helper

    /// <summary>Target panduan: Exit jika sudah terbuka, jika belum item terdekat.</summary>
    public bool TryGetGuideTarget(out Vector3 position)
    {
        position = default;

        if (State != GameState.Playing)
        {
            return false;
        }

        if (ExitUnlocked)
        {
            position = exitPoint.transform.position;
            return true;
        }

        float best = float.MaxValue;
        bool found = false;

        foreach (CollectibleItem item in items)
        {
            if (item == null)
            {
                continue;
            }

            Vector3 delta = item.transform.position - player.position;
            delta.y = 0f;

            if (delta.sqrMagnitude < best)
            {
                best = delta.sqrMagnitude;
                position = item.transform.position;
                found = true;
            }
        }

        return found;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
