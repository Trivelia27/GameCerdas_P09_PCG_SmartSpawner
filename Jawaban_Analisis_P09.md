# Jawaban Analisis — Praktikum 9 PCG

Angka merujuk ke `Hasil_Eksperimen_P09.md`.

1. **Mengapa PCG tidak sama dengan random murni?**
   Random murni hanya menghasilkan nilai acak (misalnya `Random.Range`) tanpa peduli hasilnya layak. PCG menambahkan parameter, aturan, constraint, dan validasi, sehingga hasil acak tetap sesuai tujuan desain (tidak di dalam tembok, tidak menumpuk, tidak tepat di depan player).

2. **Fungsi seed?**
   Nilai awal generator pseudo-random. Seed menentukan seluruh urutan angka acak yang dihasilkan.

3. **Apa itu reproducibility?**
   Kemampuan menghasilkan hasil yang sama persis ketika seed, parameter, dan urutan pemanggilan Random sama. Pada percobaan, seed 500 dijalankan 3 kali dan layout hash-nya identik (1094546299).

4. **Mengapa seed penting untuk debugging?**
   Bug pada layout tertentu dapat diulang dengan memasukkan seed yang sama, dilaporkan ke anggota tim lain, dan diuji kembali setelah perbaikan. Panel debug dan riwayat seed memudahkan ini.

5. **Fungsi `Random.InitState()`?**
   Menginisialisasi state generator Unity dengan seed tertentu agar urutan `Random.Range` berikutnya deterministik.

6. **Fungsi `Random.Range()`?**
   Mengambil angka acak dalam rentang tertentu. Versi int: batas atas eksklusif (`Range(0,5)` → 0–4). Versi float: kedua batas inklusif. Dipakai untuk posisi X/Z, rotasi, dan pemilihan weighted random.

7. **Apa itu controlled randomness?**
   Keacakan yang dibatasi aturan: safe zone player, obstacle, spacing, `maxAttempts`, dan weight. Hasilnya bervariasi tetapi tetap valid.

8. **Mengapa spawn membutuhkan validation?**
   Posisi acak bisa berada di dalam obstacle, terlalu dekat player, atau bertumpuk. Valid secara algoritma belum tentu adil atau baik secara game design. Pada seed 100, 38 kandidat ditolak validasi sebelum 25 object berhasil ditempatkan.

9. **Fungsi LayerMask?**
   Memilih layer mana yang disertakan dalam physics query. `obstacleMask = Obstacle` membuat `Physics.CheckSphere` hanya memeriksa obstacle, bukan Player atau Ground.

10. **Fungsi `Physics.CheckSphere()`?**
    Mengecek apakah ada collider pada layer tertentu yang overlap dengan sphere virtual di suatu posisi. Dipakai untuk menolak kandidat yang menyentuh obstacle (dengan radius `checkRadius`).

11. **Mengapa obstacle harus punya Collider?**
    Physics query hanya mendeteksi collider. Tanpa Collider, obstacle tidak terdeteksi dan object bisa muncul menembusnya.

12. **Fungsi `minimumSpacing`?**
    Menjaga jarak minimum antar-object procedural agar tidak menumpuk. Spacing 0,5 → 1,5 → 3 menaikkan rata-rata rejected dari 17,1 → 24,6 → 59,3 dan jarak terkecil antar-object meningkat sesuai nilainya.

13. **Fungsi `minDistanceFromPlayer`?**
    Membuat safe zone di sekitar player agar enemy tidak muncul tepat di dekatnya. Safe 3/5/8 menghasilkan jarak terdekat 3,78 / 6,78 / 8,04, selalu di atas batas.

14. **Mengapa perlu `maxAttemptsPerObject`?**
    Mencegah pencarian posisi berjalan tanpa batas (infinite loop / freeze) ketika constraint terlalu ketat atau ruang tidak tersedia. Setelah batas tercapai, spawn dilewati dan warning dicatat.

15. **Apa yang terjadi jika constraint terlalu ketat?**
    Rejected melonjak dan jumlah spawn berhasil turun. Pada 50 enemy dengan spacing 5, hanya 18/50 berhasil (maxAttempts 50) dan 15/50 (maxAttempts 5). Ini bukan bug, tetapi parameter yang perlu dilonggarkan.

16. **Apa itu weighted random?**
    Pemilihan acak di mana tiap pilihan punya bobot relatif. Bobot dijumlahkan, angka acak diambil dari 0 sampai total, lalu dicocokkan ke rentang kumulatif. Pada 1000 sampel, bobot 60:30:10 menghasilkan 57,9% / 32,0% / 10,1%.

17. **Mengapa cocok untuk loot atau enemy selection?**
    Jenis umum muncul sering dan jenis langka jarang, sehingga ada rarity dan pacing. Desainer cukup mengubah bobot di Inspector tanpa mengubah kode, dan bobot relatif tidak harus berjumlah 100.

18. **Hubungan PCG dengan pathfinding?**
    Posisi spawn harus dapat dijangkau. Kandidat bisa divalidasi dengan `NavMesh.SamplePosition` agar enemy muncul di area walkable. Sebaliknya, hasil PCG (obstacle, layout) memengaruhi jalur yang dihitung pathfinding.

19. **Perbedaan constructive PCG dan generate-and-test?**
    - *Constructive*: content dibuat langsung sesuai aturan tanpa evaluasi ulang.
    - *Generate-and-test*: kandidat dibuat lalu diuji; yang gagal dibuang dan dibuat lagi.

20. **Di bagian mana keduanya muncul?**
    - *Constructive*: `SpawnGroup` membuat sejumlah enemy/item sesuai `enemyCount` dan `itemCount` dan langsung meng-instantiate yang valid.
    - *Generate-and-test*: loop `GenerateRandomPosition()` → `IsPositionValid()` → ulangi sampai valid atau mencapai `maxAttemptsPerObject`.
