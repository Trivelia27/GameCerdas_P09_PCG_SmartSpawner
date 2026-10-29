# Panduan Eksperimen — Praktikum 9 PCG

Dokumen ini menjelaskan **cara mengatur** tiap eksperimen di panel, **angka yang akan muncul**, dan **cara menjelaskannya**.
Semua angka sudah dicocokkan dengan game (seed 100/200/300/500/12345 di layar sama dengan tabel otomatis
`Hasil_Eksperimen_P09.md`, yang dibuat lewat *Tools > GameCerdas P09 > Run Experiments*).

---

## 1. Cara membaca panel

| Tampilan | Artinya |
|---|---|
| **Current seed** | Seed yang dipakai generation terakhir. |
| **Layout fingerprint** | Hash (8 digit hex) dari semua posisi object. Layout sama persis ⇔ fingerprint sama. |
| **Enemy / Item / Obstacle a / b** | Berhasil ditempatkan / jumlah yang diminta. |
| **Attempts** | Total posisi kandidat yang diuji. Bila semua object berhasil: `Attempts = Rejected + 25` (15 enemy + 10 item). |
| **Rejected \| Gagal** | Rejected = kandidat yang ditolak validasi. Gagal = object yang tidak dapat ditempatkan setelah `maxAttempts`. |
| **Safe zone / Obstacle / Spacing** | Rincian penyebab penolakan. Jumlahnya sama dengan Rejected. |
| **Komposisi hasil** | Jumlah tiap jenis enemy dan item yang tercipta. |
| **Tanda X di arena** | Posisi kandidat yang ditolak: hijau = safe zone, merah = obstacle, kuning = spacing (maks. 300). |

**Rejected bukan error.** Itu posisi acak yang dibuang oleh validasi. Rejected naik berarti constraint makin ketat.

## 2. Aturan pakai panel (penting)

- Slider **tidak** me-regenerate sendiri. Perubahan berlaku saat menekan **GENERATE [R]** (atau tombol preset seed).
- **GENERATE** memakai seed di kolom seed. Kalau seed sama dengan yang aktif, layout di-restart dengan seed itu.
- Safe radius dan Min spacing dibulatkan ke kelipatan **0,5** saat diterapkan. Setelah GENERATE, label slider menampilkan nilai yang dipakai.
- Eksperimen dilakukan di **layar judul** (belum Start) supaya tidak ada enemy yang mengejar dan tengah arena bebas.
- Preset seed: **F5=100 · F6=200 · F7=300 · F8=500 · F9=12345**. **F4** = ganti mode obstacle (tetap ⇄ prosedural) lalu generate.
- Selesai satu eksperimen? Kembalikan ke baseline, atau **Stop lalu Play** (parameter kembali ke bawaan scene).

### Baseline (kondisi awal setiap eksperimen)
| Parameter | Nilai |
|---|---|
| Enemy / Item | 15 / 10 |
| Safe radius | 5.0 |
| Min spacing | 1.5 |
| Max attempts | 50 |
| Validasi | ON |
| Layer PCG | ENEMY + ITEM |
| Bobot enemy | Default (60 : 30 : 10 : 5) |

---

## Eksperimen 1: Seed berbeda (100, 200, 300)

**Tujuan:** menunjukkan seed berbeda menghasilkan variasi layout.
**Setting:** baseline. Klik preset **100**, lalu **200**, lalu **300**. Catat angka di report setiap kali.

| Seed | Enemy | Item | Rejected | Attempts | Fingerprint | Enemy R/B/Y/G |
|---|---|---|---|---|---|---|
| 100 | 15/15 | 10/10 | 38 | 63 | BC4DC059 | 9 / 4 / 1 / 1 |
| 200 | 15/15 | 10/10 | 18 | 43 | F98E8323 | 8 / 5 / 2 / 0 |
| 300 | 15/15 | 10/10 | 25 | 50 | B73EDADA | 11 / 2 / 2 / 0 |

Untuk seed 100, rincian penolakan: Safe zone 13 · Obstacle 9 · Spacing 16.

**Cara menjelaskan:** Seed yang berbeda memberi urutan angka acak yang berbeda, jadi posisi, komposisi, fingerprint, dan jumlah rejected ikut berbeda.
Meski begitu semuanya tetap menempatkan 15 enemy dan 10 item dengan valid. Seed memberi **variasi** tanpa melanggar aturan.
Seed dengan rejected paling rendah (200) berarti kandidat acaknya kebetulan lebih banyak jatuh di area valid, bukan berarti lebih baik.

## Eksperimen 2: Reproducibility (seed 500 tiga kali)

**Tujuan:** membuktikan seed + parameter yang sama menghasilkan layout yang sama.
**Setting:** baseline. Klik preset **500**, lalu klik **UJI SEED SAMA 3x**.

| Run | Rejected | Fingerprint |
|---|---|---|
| 1 | 13 | 413D737B |
| 2 | 13 | 413D737B |
| 3 | 13 | 413D737B |

Status bawah akan menulis *"Uji seed sama 3x: LOLOS — fingerprint 413D737B identik pada 3 generation."*
Cara manual: klik **GENERATE** tiga kali dan tunjuk fingerprint yang tidak berubah.

**Cara menjelaskan:** Reproducibility butuh tiga hal sama: seed, parameter, dan urutan pemanggilan `Random`. Selama itu terpenuhi hasilnya identik, sehingga bug layout bisa direproduksi hanya dengan mencatat seed.
Kalau salah satunya berubah (misalnya bobot atau jumlah enemy), layout berikutnya ikut berubah.

## Eksperimen 3: Jarak aman player (3, 5, 8)

**Tujuan:** melihat pengaruh `minDistanceFromPlayer`.
**Setting:** preset **100**. Geser **Safe radius** ke **3.0** → GENERATE. Ulangi untuk **5.0** (baseline) dan **8.0**. Lingkaran hijau ikut membesar.

| Safe radius | Enemy | Item | Rejected | Attempts | Objek terdekat ke player* |
|---|---|---|---|---|---|
| 3.0 | 15/15 | 10/10 | 12 | 37 | 3,78 |
| 5.0 | 15/15 | 10/10 | 38 | 63 | 6,78 |
| 8.0 | 15/15 | 10/10 | 49 | 74 | 8,04 |

\*Rata-rata rejected dari seed 1–20: **17,2 → 24,6 → 40,3**. Kolom jarak terdekat ada di `Hasil_Eksperimen_P09.md`.

**Cara menjelaskan:** Makin besar safe zone, makin banyak kandidat yang jatuh di dalamnya dan ditolak (tanda X hijau bertambah), karena area valid mengecil. Jarak terdekat object ke player selalu di atas radius yang diatur, jadi aturannya benar-benar bekerja.
Trade-off: **keadilan** (enemy tidak muncul di depan player) lawan **efisiensi** pencarian posisi.
Ingat: safe zone hanya aturan spawn, bukan zona perlindungan saat game berjalan.

## Eksperimen 4: Minimum spacing (0,5; 1,5; 3,0)

**Tujuan:** melihat pengaruh jarak minimum antar-object.
**Setting:** preset **100**, safe radius kembali 5.0. Geser **Min spacing** ke **0.5** → GENERATE. Ulangi untuk **1.5** dan **3.0**.

| Min spacing | Enemy | Item | Rejected | Attempts | Jarak antar-object terkecil* |
|---|---|---|---|---|---|
| 0.5 | 15/15 | 10/10 | 27 | 52 | 0,87 |
| 1.5 | 15/15 | 10/10 | 38 | 63 | 1,91 |
| 3.0 | 15/15 | 10/10 | 68 | 93 | 3,16 |

\*Rata-rata rejected dari seed 1–20: **17,1 → 24,6 → 59,3**.

**Cara menjelaskan:** Spacing kecil membuat object boleh berdekatan, sehingga rejected rendah dan arena **padat**. Spacing besar memaksa object **tersebar merata**, tetapi tiap posisi baru lebih sulit ditemukan, jadi rejected melonjak (tanda X kuning bertambah).
Trade-off: **kepadatan** lawan **biaya pencarian**. Spacing yang terlalu besar bisa membuat object gagal ditempatkan.

## Eksperimen 5: Weighted random (60:30:10 vs 33:33:34)

**Tujuan:** menunjukkan bobot bekerja secara statistik.
**Setting:** baseline. Klik tombol **BOBOT ENEMY** sampai tertulis **Modul 60:30:10** → klik **TES 10K BOBOT**. Klik tombol bobot sampai **Merata** → **TES 10K BOBOT** lagi.
Untuk melihat variasi pada sampel kecil: set Enemy 40, lalu tekan GENERATE beberapa kali dan lihat bar komposisi berubah-ubah.

Hasil yang diharapkan (target teoretis; hasil tes mendekati nilai ini, selisih sekitar ±1%):

| Bobot (R:B:Y) | Target Red | Target Blue | Target Yellow |
|---|---|---|---|
| 60 : 30 : 10 | 60,0% | 30,0% | 10,0% |
| 33 : 33 : 34 | 33,0% | 33,0% | 34,0% |
| Default 60:30:10:5 (ada Green) | 57,1% | 28,6% | 9,5% (Green 4,8%) |

Bukti dari tabel otomatis (100 enemy × 10 seed = 1000 enemy, Green = 0):

| Bobot | Red | Blue | Yellow |
|---|---|---|---|
| 60 : 30 : 10 | 57,9% | 32,0% | 10,1% |
| 33 : 33 : 34 | 32,6% | 31,2% | 36,2% |
| 80 : 15 : 5 | 78,0% | 17,4% | 4,6% |
| 10 : 20 : 70 | 9,9% | 20,5% | 69,6% |

**Cara menjelaskan:** Bobot adalah probabilitas relatif, bukan jumlah pasti. Dengan sampel kecil (15 enemy) komposisi bisa menyimpang jauh, tetapi dengan sampel besar rasionya mendekati bobot. Itu sifat statistik dan bukan bug.
Bobot juga bisa diganti tanpa mengubah kode, jadi desainer dapat mengatur kelangkaan enemy dan item (misalnya Legendary Gem hanya 2%).

## Eksperimen 6: Constraint terlalu ketat (maxAttempts)

**Tujuan:** membuktikan `maxAttempts` mencegah algoritma terjebak.
**Setting:** preset **100**. Set **Enemy = 50**, **Item = 0**, **Min spacing = 5.0**. Pertama **Max attempts = 50** → GENERATE. Lalu **Max attempts = 5** → GENERATE.

| Max attempts | Enemy berhasil | Rejected | Gagal | Attempts |
|---|---|---|---|---|
| 50 | 18 / 50 | 1695 | 32 | 1713 |
| 5 | 15 / 50 | 193 | 35 | 208 |

Status bawah menampilkan *"Ruang terbatas: … object gagal ditempatkan setelah batas percobaan"*, dan Console mencatat *"Gagal spawn object…"*.

**Cara menjelaskan:** Lima puluh enemy dengan jarak lima unit tidak muat di area 24×24 (belum lagi obstacle dan safe zone). Tanpa batas percobaan, algoritma akan mencari tanpa henti. Dengan `maxAttempts`, tiap object menyerah setelah N percobaan, jadi game tetap berjalan dan sisanya dilaporkan sebagai gagal.
Percobaan lebih sedikit (5) menghasilkan lebih sedikit object berhasil daripada 50. Ini bukan error script, melainkan **constraint yang terlalu ketat**.

---

## Eksperimen tambahan (opsional)

**A. Validasi dimatikan.** Preset 100, matikan **Validasi enemy / item aktif** → GENERATE.
Hasil: Rejected **0**, Attempts **25**. Tetapi object bisa menumpuk, masuk obstacle, atau berada di dalam lingkaran hijau. Menunjukkan *kenapa* validasi perlu.

**B. Check radius** (hanya lewat Inspector `PCGManager > Check Radius`, bukan di panel). Seed 100: radius 0,25 → rejected 38; 0,75 → 38; 2,0 → 45. Radius besar membuat jarak ke obstacle lebih longgar sehingga lebih banyak kandidat ditolak.

**C. Obstacle prosedural.** Klik **+ OBSTACLE** (atau F4) → GENERATE: Obstacle 8/8, posisi dan ukuran obstacle ikut ditentukan seed. Enemy dan item divalidasi terhadap obstacle baru.

---

## Tabel catatan untuk laporan

| Percobaan | Parameter | Enemy | Item | Rejected | Attempts | Catatan |
|---|---|---|---|---|---|---|
| Seed 100 | baseline | 15/15 | 10/10 | 38 | 63 | layout A |
| Seed 200 | baseline | 15/15 | 10/10 | 18 | 43 | layout B |
| Seed 300 | baseline | 15/15 | 10/10 | 25 | 50 | layout C |
| Seed 500 ×3 | baseline | 15/15 | 10/10 | 13 | 38 | fingerprint sama 3 kali |
| Safe radius 3 | seed 100 | 15/15 | 10/10 | 12 | 37 | rejected rendah |
| Safe radius 5 | seed 100 | 15/15 | 10/10 | 38 | 63 | baseline |
| Safe radius 8 | seed 100 | 15/15 | 10/10 | 49 | 74 | ruang valid menyempit |
| Spacing 0,5 | seed 100 | 15/15 | 10/10 | 27 | 52 | padat |
| Spacing 1,5 | seed 100 | 15/15 | 10/10 | 38 | 63 | baseline |
| Spacing 3 | seed 100 | 15/15 | 10/10 | 68 | 93 | tersebar |
| 50 enemy, spacing 5, attempts 50 | seed 100 | 18/50 | 0/0 | 1695 | 1713 | constraint terlalu ketat |
| 50 enemy, spacing 5, attempts 5 | seed 100 | 15/50 | 0/0 | 193 | 208 | gagal lebih banyak |

Tabel lengkap (termasuk rata-rata seed 1–20 dan bobot 1000 sampel) ada di `Hasil_Eksperimen_P09.md`. Tombol **EKSPOR CSV** menulis seluruh generation yang kamu jalankan ke folder `Exports/`.

## Kesimpulan eksperimen (siap dibaca)

1. **Seed** menentukan variasi sekaligus reproducibility: seed berbeda → layout berbeda, seed sama → fingerprint sama.
2. **Safe radius dan spacing** adalah constraint: makin besar, makin banyak penolakan dan makin sulit mencari posisi valid.
3. **Weighted random** bekerja secara statistik: sampel kecil menyimpang, sampel besar mendekati bobot.
4. **maxAttempts** menjaga algoritma tetap aman ketika constraint terlalu ketat; kegagalan dilaporkan, bukan membuat game hang.
5. Perancang PCG harus menyeimbangkan **variasi, kepadatan, keadilan, dan performa**.

## Jika angka di layar berbeda

- Pastikan baseline benar dan **Stop lalu Play** untuk mereset parameter.
- Pastikan seed aktif di report sama dengan yang dimaksud (lihat **Current seed**).
- Pastikan toggle **Validasi** ON dan layer **ENEMY + ITEM**.
- Pastikan Seed di Inspector `PCGManager` bukan nilai lain yang tidak sengaja tersimpan di scene (preset F5–F9 mengabaikan nilai itu).
- Parameter yang diubah di **Inspector** (mis. Check Radius, Spawn Area Size, Player digeser) ikut memengaruhi hasil.
