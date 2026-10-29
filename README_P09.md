# Smart Spawner — Praktikum 9 PCG (Game Cerdas)

Unity 6000.0.83f1 · URP · scene `Assets/Scenes/PCG_SpawnArena`

## Cara main
| Tombol | Fungsi |
|---|---|
| WASD / Panah | Gerak |
| SPACE / Shift | Dash (kebal sesaat, **menghancurkan enemy** yang tertabrak) |
| R | Restart level (seed sama → layout sama) |
| N | Seed acak baru |
| TAB | Tampil/sembunyikan panel debug PCG |
| F2 | Autopilot (bot AI untuk demo) |
| P / Esc | Pause · M mute |

Tujuan: kumpulkan item yang disyaratkan, lalu capai **EXIT**. Setiap level memakai seed turunan
(`baseSeed + level - 1`) dan jumlah enemy bertambah, jadi progresi tetap reproducible.

## Pemenuhan soal praktikum
| Soal | Implementasi |
|---|---|
| Procedural spawning, jumlah & area via Inspector | `PCGSpawner` (Enemy/Item Count, Spawn Area Size) |
| Seed & reproducibility | `Random.InitState(seed)`; seed 12345 selalu rejected 34 |
| Weighted random | Tabel enemy 60:30:10:5 dan item 70:20:8:2 |
| Validasi: jarak player, obstacle, spacing, `maxAttempts` | `IsPositionValid()` + `Physics.CheckSphere` + LayerMask |
| R regenerate, debug seed/jumlah/rejected, Gizmos | Panel PCG (TAB), `OnDrawGizmosSelected` (pilih `PCGManager`) |
| Tabel pengujian | `Hasil_Eksperimen_P09.md` (Tools > GameCerdas P09 > Run Experiments) |
| ≥3 fitur pengembangan | Random rotation, enemy ke-4, legendary item, statistik jenis, tombol UI, riwayat seed, jarak item–enemy, zona/level berbeda |
| Pengayaan gameplay | Enemy AI (4 perilaku), item, exit, level, skor |

## Panel PCG (kiri layar)
Preset seed (100/200/300/500/12345), slider Enemy/Item Count, Safe Distance, Min Spacing,
Max Attempts, dan bobot tiap enemy. Mengubah slider langsung me-regenerate arena — cocok
untuk eksperimen dan video demo.

## Perilaku enemy
- **Red (Grunt)**: kejar langsung.
- **Blue (Hunter)**: memprediksi posisi player (pursuit).
- **Yellow (Striker)**: mengejar, lalu windup dan menerjang (charge).
- **Green (Brute)**: lambat, 2 HP, damage 2.
Semua: wander saat idle, menghindari obstacle, dan saling menjauh (separation).

## Menu Editor
`Tools > GameCerdas P09`: Build Scene · Run Experiments · Open And Play · Open And Play (Autopilot).
