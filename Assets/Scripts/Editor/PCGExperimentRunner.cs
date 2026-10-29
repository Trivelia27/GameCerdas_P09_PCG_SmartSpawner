using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menjalankan eksperimen praktikum secara otomatis (dry-run tanpa Instantiate)
/// dan menulis tabel hasil ke Hasil_Eksperimen_P09.md di root project.
/// </summary>
public static class PCGExperimentRunner
{
    private const string ScenePath = "Assets/Scenes/PCG_SpawnArena.unity";
    private const int AverageSeeds = 20;

    private static PCGSpawner spawner;
    private static SerializedObject so;

    [MenuItem("Tools/GameCerdas P09/Run Experiments")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        spawner = Object.FindFirstObjectByType<PCGSpawner>();
        so = new SerializedObject(spawner);

        StringBuilder md = new StringBuilder();
        md.AppendLine("# Hasil Eksperimen Praktikum 9 — PCG Smart Spawner");
        md.AppendLine();
        md.AppendLine("Semua angka dihasilkan otomatis oleh `PCGExperimentRunner` (Tools > GameCerdas P09 > Run Experiments).");
        md.AppendLine("Konfigurasi dasar: area 24x24, Enemy 15, Item 10, jarak player 5, checkRadius 0.75, spacing 1.5, maxAttempts 50, 4 obstacle.");
        md.AppendLine();

        // ------------------------------------------------ 1. Seed berbeda
        Reset();
        md.AppendLine("## 1. Seed 100 vs 200 vs 300");
        md.AppendLine();
        md.AppendLine("| Seed | Enemy | Item | Rejected | Layout hash | Komposisi enemy (R/B/Y/G) |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (int s in new[] { 100, 200, 300 })
        {
            var r = Gen(s);
            md.AppendLine($"| {s} | {r.enemySpawned}/{r.enemyRequested} | {r.itemSpawned}/{r.itemRequested} | {r.rejected} | {r.layoutHash} | {EnemyMix(r)} |");
        }
        md.AppendLine();
        md.AppendLine("Seed berbeda menghasilkan layout hash, posisi, dan komposisi enemy yang berbeda.");
        md.AppendLine();

        // ------------------------------------------------ 2. Reproducibility
        md.AppendLine("## 2. Reproducibility — seed 500 tiga kali");
        md.AppendLine();
        md.AppendLine("| Run | Enemy | Item | Rejected | Layout hash |");
        md.AppendLine("|---|---|---|---|---|");
        int firstHash = 0;
        bool allSame = true;
        for (int i = 1; i <= 3; i++)
        {
            var r = Gen(500);
            if (i == 1) firstHash = r.layoutHash;
            else if (r.layoutHash != firstHash) allSame = false;
            md.AppendLine($"| {i} | {r.enemySpawned}/{r.enemyRequested} | {r.itemSpawned}/{r.itemRequested} | {r.rejected} | {r.layoutHash} |");
        }
        md.AppendLine();
        md.AppendLine(allSame
            ? "Ketiga run identik (hash sama) → generation **reproducible**."
            : "Hash berbeda → TIDAK reproducible (periksa urutan Random call).");
        md.AppendLine();

        // ------------------------------------------------ 3. Safe radius
        md.AppendLine("## 3. Jarak aman player (minDistanceFromPlayer)");
        md.AppendLine();
        md.AppendLine($"Seed 100 untuk satu run; kolom rata-rata dihitung dari seed 1–{AverageSeeds}.");
        md.AppendLine();
        md.AppendLine("| Safe distance | Enemy | Item | Rejected (seed 100) | Jarak terdekat ke player | Rata-rata rejected |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (float d in new[] { 3f, 5f, 8f })
        {
            Reset();
            SetFloat("minDistanceFromPlayer", d);
            var r = Gen(100);
            float avg = AverageRejected();
            md.AppendLine($"| {d} | {r.enemySpawned}/{r.enemyRequested} | {r.itemSpawned}/{r.itemRequested} | {r.rejected} | {r.minPlayerDistance:F2} | {avg:F1} |");
        }
        md.AppendLine();

        // ------------------------------------------------ 4. Minimum spacing
        md.AppendLine("## 4. Minimum spacing");
        md.AppendLine();
        md.AppendLine("| Spacing | Enemy | Item | Rejected (seed 100) | Jarak antar-object terkecil | Rata-rata rejected |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (float sp in new[] { 0.5f, 1.5f, 3f })
        {
            Reset();
            SetFloat("minimumSpacing", sp);
            var r = Gen(100);
            float avg = AverageRejected();
            md.AppendLine($"| {sp} | {r.enemySpawned}/{r.enemyRequested} | {r.itemSpawned}/{r.itemRequested} | {r.rejected} | {r.minPairDistance:F2} | {avg:F1} |");
        }
        md.AppendLine();

        // ------------------------------------------------ 5. Weighted random
        md.AppendLine("## 5. Weighted random enemy");
        md.AppendLine();
        md.AppendLine("Enemy Count = 100, spacing 1.0, dijumlahkan dari seed 1–10 (Green weight = 0 agar rasio 3-warna murni).");
        md.AppendLine();
        md.AppendLine("| Bobot R:B:Y | Total enemy | Red | Blue | Yellow | Persentase R/B/Y |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (int[] w in new[] { new[] { 60, 30, 10 }, new[] { 33, 33, 34 }, new[] { 80, 15, 5 }, new[] { 10, 20, 70 } })
        {
            Reset();
            SetInt("enemyCount", 100);
            SetInt("itemCount", 0);
            SetFloat("minimumSpacing", 1.0f);
            SetEnemyWeights(w[0], w[1], w[2], 0);

            int red = 0, blue = 0, yellow = 0, total = 0;
            for (int s = 1; s <= 10; s++)
            {
                var r = Gen(s);
                red += Count(r, "Common Red");
                blue += Count(r, "Uncommon Blue");
                yellow += Count(r, "Rare Yellow");
                total += r.enemySpawned;
            }

            md.AppendLine($"| {w[0]}:{w[1]}:{w[2]} | {total} | {red} | {blue} | {yellow} | {Pct(red, total)} / {Pct(blue, total)} / {Pct(yellow, total)} |");
        }
        md.AppendLine();

        // ------------------------------------------------ 6. maxAttempts / constraint ketat
        md.AppendLine("## 6. Constraint terlalu ketat (maxAttempts)");
        md.AppendLine();
        md.AppendLine("| Enemy Count | Spacing | maxAttempts | Enemy berhasil | Rejected | Catatan |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (int attempts in new[] { 50, 5 })
        {
            Reset();
            SetInt("enemyCount", 50);
            SetInt("itemCount", 0);
            SetFloat("minimumSpacing", 5f);
            SetInt("maxAttemptsPerObject", attempts);
            var r = Gen(100);
            string note = r.enemySpawned < r.enemyRequested ? "sebagian gagal → muncul warning \"Gagal spawn object\"" : "semua berhasil";
            md.AppendLine($"| 50 | 5 | {attempts} | {r.enemySpawned}/{r.enemyRequested} | {r.rejected} | {note} |");
        }
        md.AppendLine();

        // ------------------------------------------------ 7. checkRadius
        md.AppendLine("## 7. checkRadius (obstacle validation)");
        md.AppendLine();
        md.AppendLine("| checkRadius | Rejected (seed 100) | Rata-rata rejected |");
        md.AppendLine("|---|---|---|");
        foreach (float cr in new[] { 0.25f, 0.75f, 2f })
        {
            Reset();
            SetFloat("checkRadius", cr);
            var r = Gen(100);
            md.AppendLine($"| {cr} | {r.rejected} | {AverageRejected():F1} |");
        }
        md.AppendLine();

        Reset();
        spawner.Generate(false);

        string outPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Hasil_Eksperimen_P09.md");
        File.WriteAllText(outPath, md.ToString(), new UTF8Encoding(false));
        Debug.Log("[Experiment] Selesai → " + outPath);
    }

    // ------------------------------------------------------------------ helpers

    // Nilai default sama dengan hasil PCGSceneBuilder.
    private static void Reset()
    {
        SetBool("useRandomSeed", false);
        SetInt("enemyCount", 15);
        SetInt("itemCount", 10);
        SetFloat("minDistanceFromPlayer", 5f);
        SetFloat("checkRadius", 0.75f);
        SetFloat("minimumSpacing", 1.5f);
        SetInt("maxAttemptsPerObject", 50);
        SetEnemyWeights(60, 30, 10, 5);
    }

    private static PCGSpawner.GenerationResult Gen(int seed)
    {
        SetInt("seed", seed);
        so.ApplyModifiedPropertiesWithoutUndo();
        Physics.SyncTransforms();
        return spawner.Generate(false);
    }

    private static float AverageRejected()
    {
        float sum = 0f;
        for (int s = 1; s <= AverageSeeds; s++)
        {
            sum += Gen(s).rejected;
        }
        return sum / AverageSeeds;
    }

    private static void SetFloat(string prop, float v) { so.FindProperty(prop).floatValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetInt(string prop, int v) { so.FindProperty(prop).intValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetBool(string prop, bool v) { so.FindProperty(prop).boolValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static void SetEnemyWeights(int r, int b, int y, int g)
    {
        SerializedProperty arr = so.FindProperty("enemyEntries");
        int[] w = { r, b, y, g };
        for (int i = 0; i < w.Length && i < arr.arraySize; i++)
        {
            arr.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue = w[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int Count(PCGSpawner.GenerationResult r, string key)
    {
        r.counts.TryGetValue(key, out int c);
        return c;
    }

    private static string EnemyMix(PCGSpawner.GenerationResult r)
    {
        return $"{Count(r, "Common Red")}/{Count(r, "Uncommon Blue")}/{Count(r, "Rare Yellow")}/{Count(r, "Elite Green")}";
    }

    private static string Pct(int part, int total)
    {
        return total == 0 ? "0%" : $"{100f * part / total:F1}%";
    }
}
