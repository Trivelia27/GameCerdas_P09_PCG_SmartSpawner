# Hasil Eksperimen Praktikum 9 — PCG Smart Spawner

Semua angka dihasilkan otomatis oleh `PCGExperimentRunner` (Tools > GameCerdas P09 > Run Experiments).
Konfigurasi dasar: area 24x24, Enemy 15, Item 10, jarak player 5, checkRadius 0.75, spacing 1.5, maxAttempts 50, 4 obstacle.

## 1. Seed 100 vs 200 vs 300

| Seed | Enemy | Item | Rejected | Layout hash | Komposisi enemy (R/B/Y/G) |
|---|---|---|---|---|---|
| 100 | 15/15 | 10/10 | 38 | -1135755175 | 9/4/1/1 |
| 200 | 15/15 | 10/10 | 18 | -108100829 | 8/5/2/0 |
| 300 | 15/15 | 10/10 | 25 | -1220617510 | 11/2/2/0 |

Seed berbeda menghasilkan layout hash, posisi, dan komposisi enemy yang berbeda.

## 2. Reproducibility — seed 500 tiga kali

| Run | Enemy | Item | Rejected | Layout hash |
|---|---|---|---|---|
| 1 | 15/15 | 10/10 | 13 | 1094546299 |
| 2 | 15/15 | 10/10 | 13 | 1094546299 |
| 3 | 15/15 | 10/10 | 13 | 1094546299 |

Ketiga run identik (hash sama) → generation **reproducible**.

## 3. Jarak aman player (minDistanceFromPlayer)

Seed 100 untuk satu run; kolom rata-rata dihitung dari seed 1–20.

| Safe distance | Enemy | Item | Rejected (seed 100) | Jarak terdekat ke player | Rata-rata rejected |
|---|---|---|---|---|---|
| 3 | 15/15 | 10/10 | 12 | 3.78 | 17.2 |
| 5 | 15/15 | 10/10 | 38 | 6.78 | 24.6 |
| 8 | 15/15 | 10/10 | 49 | 8.04 | 40.3 |

## 4. Minimum spacing

| Spacing | Enemy | Item | Rejected (seed 100) | Jarak antar-object terkecil | Rata-rata rejected |
|---|---|---|---|---|---|
| 0.5 | 15/15 | 10/10 | 27 | 0.87 | 17.1 |
| 1.5 | 15/15 | 10/10 | 38 | 1.91 | 24.6 |
| 3 | 15/15 | 10/10 | 68 | 3.16 | 59.3 |

## 5. Weighted random enemy

Enemy Count = 100, spacing 1.0, dijumlahkan dari seed 1–10 (Green weight = 0 agar rasio 3-warna murni).

| Bobot R:B:Y | Total enemy | Red | Blue | Yellow | Persentase R/B/Y |
|---|---|---|---|---|---|
| 60:30:10 | 1000 | 579 | 320 | 101 | 57.9% / 32.0% / 10.1% |
| 33:33:34 | 1000 | 326 | 312 | 362 | 32.6% / 31.2% / 36.2% |
| 80:15:5 | 1000 | 780 | 174 | 46 | 78.0% / 17.4% / 4.6% |
| 10:20:70 | 1000 | 99 | 205 | 696 | 9.9% / 20.5% / 69.6% |

## 6. Constraint terlalu ketat (maxAttempts)

| Enemy Count | Spacing | maxAttempts | Enemy berhasil | Rejected | Catatan |
|---|---|---|---|---|---|
| 50 | 5 | 50 | 18/50 | 1695 | sebagian gagal → muncul warning "Gagal spawn object" |
| 50 | 5 | 5 | 15/50 | 193 | sebagian gagal → muncul warning "Gagal spawn object" |

## 7. checkRadius (obstacle validation)

| checkRadius | Rejected (seed 100) | Rata-rata rejected |
|---|---|---|
| 0.25 | 38 | 20.8 |
| 0.75 | 38 | 24.6 |
| 2 | 45 | 40.7 |

