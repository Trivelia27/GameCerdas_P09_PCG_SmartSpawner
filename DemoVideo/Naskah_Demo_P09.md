# Naskah Demo — Praktikum 9 PCG (Smart Spawner)

Durasi target **±9:45**. Kecepatan baca yang pas: 130–140 kata per menit. Setiap bagian punya **Layar** (apa yang diklik/ditunjukkan)
dan **Narasi** (kalimat yang dibaca). Tanda [jeda] = diam sebentar sambil menunggu hasil muncul.
Detail setting dan angka eksperimen ada di `Panduan_Eksperimen_P09.md`.

Isi `[...]` diganti sendiri (nama kelompok, nama, NRP).

---

## Persiapan sebelum rekam

1. Buka scene `PCG_SpawnArena`. Di tab Game atur resolusi **1920×1080** agar layout sesuai desain.
2. Pilih `PCGManager` → pastikan **Seed = 12345** di Inspector (kalau tidak, tekan **F9** setelah Play).
3. Tekan **Play**. Eksperimen dilakukan di **layar judul** (belum Start): tengah arena bebas, tanda X dan safe zone terlihat.
4. Kondisi awal panel (baseline): Enemy 15 · Item 10 · Safe radius 5.0 · Min spacing 1.5 · Max attempts 50 ·
   Validasi ON · Layer **ENEMY + ITEM** · Bobot **Default**. Kalau berantakan, **Stop lalu Play lagi**: parameter kembali ke bawaan scene.
5. Hotkey berguna: **F5–F9** = preset seed 100 / 200 / 300 / 500 / 12345 · **F4** = mode obstacle tetap/prosedural · **R** generate · **N** seed baru · **TAB** mode fokus · **F2** autopilot · **M** mute.
6. Suara: tekan **M** saat berbicara kalau musik mengganggu, lalu hidupkan lagi saat bagian gameplay.
7. Rekam dengan OBS atau Win+G, 1080p. Segmen Inspector/Gizmos perlu jendela Unity normal (bukan Play Maximized).

---

## 00:00 – 00:30 | Pembuka

**Layar:** layar judul, kamera mengorbit arena.

**Narasi:**
Selamat pagi, Bapak/Ibu. Kami dari kelompok **[nama kelompok]**: **[nama 1, NRP]** dan **[nama 2, NRP]**.
Ini demo Praktikum 9, **Procedural Content Generation**, dengan project **Smart Spawner**.
Enemy dan item di arena ini tidak kami letakkan manual. Semuanya dibuat oleh algoritma yang **acak tetapi terkendali**:
bisa diulang dengan **seed**, dipilih dengan **weighted random**, dan setiap posisinya **divalidasi**.

## 00:30 – 01:15 | Tur layout

**Layar:** arahkan kursor ke kiri, tengah, kanan, lalu bawah.

**Narasi:**
Layarnya dibagi tiga. Di kiri ada **Generation Controls**: seed, tombol Generate, parameter, bobot enemy, dan alat uji.
Di tengah adalah arena yang dibuat secara live. **Garis cyan** adalah area spawn, **lingkaran hijau** adalah safe zone di sekitar player,
dan **tanda X** adalah posisi acak yang ditolak validasi: hijau karena terlalu dekat player, merah karena mengenai obstacle, kuning karena terlalu dekat object lain.
Di kanan ada **Generation Report**: seed, fingerprint layout, jumlah enemy dan item, jumlah percobaan, alasan penolakan, dan komposisi hasil.
Di bawah ada status generation dan riwayat seed.

## 01:15 – 01:50 | Parameter lewat Inspector dan Gizmos

**Layar:** Shift+Space agar jendela normal. Pilih `PCGManager` di Hierarchy → tunjukkan Inspector (Seed, Spawn Area Size, Enemy/Item Count, Validation, Max Attempts, dua tabel weighted).
Pindah ke tab **Scene**: kotak cyan (area spawn) dan bola merah (safe zone). Kembali ke tab Game.

**Narasi:**
Semua parameter bisa diatur dari Inspector tanpa mengubah kode: ukuran area, jumlah enemy dan item, jarak aman player, check radius, minimum spacing, dan batas percobaan.
Di bawahnya ada dua tabel weighted. Enemy Red, Blue, Yellow, dan Green dengan bobot **60, 30, 10, 5**. Item Coin, Health, Rare, dan Legendary dengan bobot **70, 20, 8, 2**.
Di Scene view, Gizmos menampilkan area spawn dan radius safe zone.

## 01:50 – 02:40 | Seed dan reproducibility

**Layar:** klik preset **12345** (F9) → tunjuk fingerprint **2A090538**, Rejected **34**. Klik **GENERATE** dua kali: tetap sama.
Klik **SEED BARU**: berubah. Klik preset 12345 lagi: kembali **2A090538**. Lalu klik **UJI SEED SAMA 3x** → status "LOLOS".

**Narasi:**
Seed adalah nilai awal generator pseudo-random, kami set lewat `Random.InitState`. Dengan seed 12345, fingerprint layout selalu **2A090538** dan rejected selalu 34.
Fingerprint ini hash dari semua posisi object. [jeda] Seed baru menghasilkan layout lain, tetapi begitu seed 12345 dipakai lagi, layout yang sama kembali persis.
Tombol **Uji seed sama tiga kali** melakukan generation berulang lalu membandingkan fingerprint-nya. Syaratnya: seed, parameter, dan urutan pemanggilan random harus sama.
Inilah yang membuat bug layout bisa direproduksi: cukup catat seednya.

## 02:40 – 03:30 | Validasi dan maxAttempts

**Layar:** tekan preset **100** (F5). Tunjuk tanda X, lalu angka di report: Attempts 63, Rejected 38, rincian Safe zone 13 · Obstacle 9 · Spacing 16.
Matikan toggle **Validasi enemy / item aktif** → **GENERATE** → Rejected 0, Attempts 25; tunjukkan object yang menumpuk atau masuk obstacle dan lingkaran hijau.
Nyalakan lagi → **GENERATE**.

**Narasi:**
Algoritmanya **generate-and-test**. Untuk setiap object, sistem mengambil posisi acak, lalu mengujinya: apakah terlalu dekat player, apakah mengenai obstacle lewat **Physics.CheckSphere** dengan LayerMask, dan apakah terlalu dekat object sebelumnya.
Yang gagal ditolak dan dicoba lagi. Dengan seed 100, dari **63 percobaan**, **38 ditolak**: 13 karena safe zone, 9 karena obstacle, 16 karena spacing. [jeda]
Rejected bukan bug. Itu bukti validasi bekerja. Kalau validasinya kami matikan, rejected jadi nol, tetapi lihat hasilnya: object menumpuk dan ada yang di dalam obstacle.
Pencarian juga dibatasi oleh **maxAttempts**, supaya kalau ruangnya tidak cukup, algoritma menyerah dengan aman dan tidak berputar selamanya. Ini kami buktikan di eksperimen nanti.

## 03:30 – 04:10 | Weighted random

**Layar:** lihat bar **Komposisi hasil**. Klik **TES 10K BOBOT** → baca persentase di status. Klik tombol bobot sampai **Merata** → **TES 10K BOBOT** lagi.

**Narasi:**
Jenis enemy dipilih dengan **weighted random**: bobot dijumlahkan, lalu angka acak dicocokkan ke rentang kumulatif. Bobot bersifat relatif, jadi tidak harus berjumlah seratus.
Di sini ada 15 enemy, jadi komposisinya masih naik-turun. Dengan **sepuluh ribu sampel**, persentasenya hampir persis sama dengan bobot. Bobot **Merata** menghasilkan sekitar sepertiga untuk tiap jenis.
Semakin banyak sampel, hasil semakin mendekati bobot.

## 04:10 – 05:10 | Tiga fitur pengembangan

**Layar:**
1. Klik **+ OBSTACLE** (atau **F4**) → **GENERATE** → Obstacle **8 / 8**; tekan N beberapa kali: obstacle berubah tiap seed. Kembali ke **ENEMY + ITEM**.
2. Tunjuk Komposisi hasil: **Elite Green** (enemy ke-4) dan **Legendary Gem** (sinar ungu di arena). Item dan enemy berputar acak karena random rotation.
3. Klik **EKSPOR CSV** → status menampilkan path file. Klik salah satu seed di **RIWAYAT** untuk kembali ke layout lama.

**Narasi:**
Fitur pengembangan pertama, **obstacle prosedural**. Pipeline-nya menjadi Obstacle, lalu Enemy, lalu Item, jadi enemy dan item divalidasi terhadap obstacle yang baru dibuat. Obstacle ikut berubah setiap seed.
Kedua, **jenis baru dan statistik**: enemy keempat Elite Green, item Legendary Gem dengan peluang dua persen, dan laporan komposisi per jenis. Semua object juga diputar acak dengan random rotation.
Ketiga, **riwayat seed dan ekspor CSV**: setiap generation dicatat, bisa dipilih ulang dari riwayat, dan diekspor untuk tabel laporan.
Selain itu ada juga jarak minimum item dari enemy, dan level berikutnya memakai seed turunan.

## 05:10 – 08:40 | Eksperimen

Setiap eksperimen: kembali ke baseline, ubah **satu** parameter, tekan **GENERATE**, baca report. Detail klik dan angka lengkap ada di `Panduan_Eksperimen_P09.md`.
Angka berikut sudah diverifikasi di game dan sama dengan tabel `Hasil_Eksperimen_P09.md`.

### 05:10 – 05:45 | Eksperimen 1: seed 100, 200, 300
**Layar:** klik preset 100, 200, 300 bergantian (F5, F6, F7); tunjuk fingerprint, Rejected, dan komposisi.
**Narasi:** Seed berbeda menghasilkan layout, fingerprint, dan komposisi berbeda. Rejected-nya **38, 18, dan 25**, tetapi semuanya tetap berhasil menempatkan 15 enemy dan 10 item. Jadi seed memberi variasi tanpa melanggar aturan.

### 05:45 – 06:15 | Eksperimen 2: seed 500 tiga kali
**Layar:** preset 500 → **UJI SEED SAMA 3x**; tunjuk status LOLOS dan fingerprint **413D737B**.
**Narasi:** Seed 500 dijalankan tiga kali. Fingerprint tetap **413D737B** dan rejected tetap 13. Layout konsisten, jadi generation ini reproducible.

### 06:15 – 06:55 | Eksperimen 3: jarak aman player 3, 5, 8
**Layar:** preset 100. Geser **Safe radius** ke 3.0 → GENERATE; 5.0; 8.0. Perhatikan lingkaran hijau melebar.
**Narasi:** Radius tiga menghasilkan **12 penolakan**, lima menghasilkan **38**, delapan menghasilkan **49**. Semakin besar safe zone, semakin banyak kandidat yang ditolak karena ruang valid menyempit. Gameplay lebih adil, tetapi pencarian posisi lebih berat.

### 06:55 – 07:30 | Eksperimen 4: minimum spacing 0,5; 1,5; 3
**Layar:** kembali baseline. Geser **Min spacing** ke 0.5 → GENERATE; 1.5; 3.0.
**Narasi:** Spacing 0,5 hanya **27 penolakan** dan object boleh berdekatan. Spacing 1,5 menghasilkan **38**. Spacing tiga melonjak menjadi **68** karena object harus tersebar. Spacing kecil membuat arena padat, spacing besar membuatnya merata tetapi lebih mahal dicari.

### 07:30 – 08:05 | Eksperimen 5: bobot 60:30:10 dan 33:33:34
**Layar:** klik tombol bobot sampai **Modul 60:30:10** → **TES 10K BOBOT**; lalu **Merata** → **TES 10K BOBOT**. Naikkan Enemy ke 40 → GENERATE beberapa kali untuk melihat bar komposisi berubah.
**Narasi:** Untuk bobot 60:30:10, hasil sepuluh ribu sampel mendekati 60, 30, dan 10 persen. Untuk bobot merata mendekati sepertiga. Pada sampel kecil hasilnya menyimpang, pada sampel besar mendekati bobot. Itu sifat probabilitas, bukan kesalahan algoritma.

### 08:05 – 08:40 | Eksperimen 6: constraint terlalu ketat (maxAttempts)
**Layar:** set Enemy **50**, Item **0**, Min spacing **5.0**, Max attempts **50** → GENERATE (Enemy 18/50). Ubah Max attempts ke **5** → GENERATE (Enemy 15/50). Tunjuk status "Ruang terbatas" dan kolom **Gagal**.
**Narasi:** Lima puluh enemy dengan spacing lima tidak muat di area ini. Dengan 50 percobaan hanya **18 berhasil**, dengan 5 percobaan hanya **15**. Sisanya gagal, dan itu ditampilkan sebagai peringatan, bukan error atau hang. Ini fungsi **maxAttempts**: mengubah constraint yang terlalu ketat menjadi kegagalan yang aman.

## 08:40 – 09:20 | Gameplay singkat (pengayaan)

**Layar:** kembali baseline (Stop lalu Play), tekan **F2** (autopilot) lalu **Enter**. Biarkan bot mengumpulkan item, enemy mengejar, dash menghancurkan enemy, Exit terbuka, lalu Level 2 (seed 12346).

**Narasi:**
Sebagai pengayaan, hasil PCG bisa dimainkan. Player mengumpulkan item, enemy mengejar dengan empat perilaku berbeda, dan dash bisa menghancurkan enemy. Setelah item cukup, **Exit** terbuka.
Setiap level memakai seed turunan, yaitu seed dasar ditambah nomor level, jadi progresinya tetap bisa direproduksi. Di level kedua enemy bertambah.

## 09:20 – 09:50 | Kesimpulan

**Layar:** kembali ke panel dengan seed 12345.

**Narasi:**
Kesimpulannya, PCG yang baik bukan random murni. Ia adalah **randomness yang dikendalikan** oleh seed, aturan validasi, weighted selection, dan batas percobaan.
Dari eksperimen: seed menentukan variasi dan reproducibility; safe radius dan spacing menaikkan penolakan; bobot bekerja secara statistik; dan maxAttempts mencegah algoritma terjebak.
Parameter harus diseimbangkan antara **variasi, kepadatan, keadilan, dan performa**. Terima kasih.

---

## Cue sheet ringkas

| Waktu | Aksi |
|---|---|
| 00:00 | Layar judul, identitas kelompok |
| 00:30 | Tur layout (kiri, tengah, kanan, bawah) |
| 01:15 | Inspector `PCGManager` + Gizmos di Scene view |
| 01:50 | Preset 12345 (F9), Generate ×2, Seed baru, **Uji seed sama 3x** |
| 02:40 | Preset 100 (F5): tanda X, rincian penolakan; toggle validasi OFF lalu ON |
| 03:30 | **Tes 10k bobot** (Default lalu Merata) |
| 04:10 | **+ Obstacle**; Elite Green, Legendary Gem; Ekspor CSV, riwayat seed |
| 05:10 | Eksperimen 1: seed 100/200/300 |
| 05:45 | Eksperimen 2: seed 500 ×3 |
| 06:15 | Eksperimen 3: safe radius 3/5/8 |
| 06:55 | Eksperimen 4: spacing 0,5/1,5/3 |
| 07:30 | Eksperimen 5: bobot 60:30:10 vs 33:33:34 |
| 08:05 | Eksperimen 6: maxAttempts (Enemy 50, spacing 5) |
| 08:40 | Gameplay singkat (F2 + Enter) |
| 09:20 | Kesimpulan |

## Kalau durasi harus dipotong
- Buang segmen Inspector (01:15) dan jelaskan Inspector sambil lalu: hemat ±35 detik.
- Eksperimen 6 dan gameplay boleh diringkas menjadi satu kalimat: hemat ±1 menit.
- Jangan buang eksperimen 1–5; itu yang diminta soal.

## Kemungkinan pertanyaan dosen
| Pertanyaan | Jawaban singkat |
|---|---|
| Apa bedanya random biasa dan PCG? | Random biasa hanya `Random.Range`. PCG menambah seed, validasi, constraint, dan weighted selection sehingga hasilnya valid dan bisa diulang. |
| Kenapa Rejected tinggi tidak masalah? | Itu kandidat acak yang gagal validasi dan dibuang. Yang penting object yang ditempatkan valid. Rejected naik saat constraint diperketat. |
| Kenapa Player di safe zone tetap bisa dipukul enemy? | Safe zone adalah **aturan spawn**: enemy tidak boleh *muncul* terlalu dekat player. Setelah muncul, enemy bebas mengejar. Ini sesuai definisi praktikum. |
| Apa fungsi seed? | Menentukan seluruh urutan angka acak. Seed, parameter, dan urutan call yang sama menghasilkan layout yang sama. |
| Bagaimana weighted random bekerja? | Jumlahkan bobot, ambil angka acak 0 sampai total, cocokkan ke rentang kumulatif. |
| Constructive atau generate-and-test? | Keduanya: jumlah object dibuat sesuai permintaan (constructive), tiap posisinya diuji dan diulang jika gagal (generate-and-test). |
| Apa yang terjadi jika constraint terlalu ketat? | Banyak penolakan lalu sebagian object gagal ditempatkan. maxAttempts membuat algoritma berhenti dengan aman. |
| Kenapa tabel hasil sama dengan yang di layar? | Eksperimen otomatis memakai algoritma yang sama dan seed yang sama (menu *Tools > GameCerdas P09 > Run Experiments*). |

## Checklist pengumpulan di Classroom
- [ ] Identitas kelompok serta nama dan NRP seluruh anggota
- [ ] Link video demo (pastikan **dapat diakses** tanpa login)
- [ ] Video memuat: fitur utama, **tiga fitur pengembangan**, hasil eksperimen, dan penjelasan singkat hasilnya
