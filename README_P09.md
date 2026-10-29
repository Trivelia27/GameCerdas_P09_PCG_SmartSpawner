# Smart Spawner — Praktikum 9 PCG (Game Cerdas)

Unity 6000.0.83f1 · URP · scene `Assets/Scenes/PCG_SpawnArena`

## Cara main
| Tombol | Fungsi |
|---|---|
| WASD / Panah | Gerak |
| SPACE / Shift | Dash (kebal sesaat, **menghancurkan enemy** yang tertabrak) |
| R | Restart level (seed sama → layout sama) |
| N | Seed acak baru |
| TAB | Mode fokus: sembunyikan/tampilkan panel lab |
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
| R regenerate, debug seed/jumlah/rejected, Gizmos | Panel lab (kiri/kanan/bawah), overlay area + safe zone + X, `OnDrawGizmosSelected` |
| Tabel pengujian | `Hasil_Eksperimen_P09.md` (Tools > GameCerdas P09 > Run Experiments) |
| ≥3 fitur pengembangan | Random rotation, enemy ke-4, legendary item, statistik jenis, tombol UI, riwayat seed, jarak item–enemy, zona/level berbeda |
| Pengayaan gameplay | Enemy AI (4 perilaku), item, exit, level, skor |

## Layout lab (3 kolom + bar bawah)
- **01 Generation Controls (kiri)**: input seed, Generate [R] / Seed baru [N], layer PCG
  (Enemy + Item atau + Obstacle prosedural), slider Enemy/Item/Obstacle/Safe radius/Min spacing/Max attempts,
  preset bobot enemy, toggle validasi, toggle tanda X dan overlay area, **Uji seed sama 3x**,
  **Tes 10k bobot**, **Ekspor CSV** (ke folder `Exports/`). Parameter diterapkan saat Generate.
- **02 Arena (tengah)**: game berjalan di viewport. Garis cyan = area spawn, lingkaran hijau = safe zone,
  tanda X = kandidat ditolak (hijau safe zone, merah obstacle, kuning spacing; maks. 300).
- **03 Generation Report (kanan)**: seed, fingerprint layout, Enemy/Item/Obstacle, Attempts, Rejected | Gagal,
  rincian penolakan, dan komposisi hasil per jenis.
- **Bar bawah**: status generation (waktu, validasi, mode obstacle) dan riwayat seed yang bisa diklik.

## Perilaku enemy
- **Red (Grunt)**: kejar langsung.
- **Blue (Hunter)**: memprediksi posisi player (pursuit).
- **Yellow (Striker)**: mengejar, lalu windup dan menerjang (charge).
- **Green (Brute)**: lambat, 2 HP, damage 2.
Semua: wander saat idle, menghindari obstacle, dan saling menjauh (separation).

## Menu Editor
`Tools > GameCerdas P09`: Build Scene · Run Experiments · Open And Play · Open And Play (Autopilot).
