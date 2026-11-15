# Demo senaryosu (10 dakika)

Sistemi birine göstermek için önerilen akış. Her adım bir öncekinin sonucunu kullanır; iki tarayıcı
penceresi (biri gizli pencere) yeterlidir.

Hazırlık: `docker compose up -d --build`, ardından http://localhost:3100. Giriş kodları için
http://localhost:8126 (Mailpit) bir sekmede açık dursun. Parola `.env` içindeki `SEED_DEFAULT_PASSWORD`.

## 1. Genel site ve giriş (1 dk)

- http://localhost:3100 → banner'lar, **gerçek** TCMB kurları ve gram altın, hava durumu.
- **Portala Git** → `calisan@devoplus.local` ile giriş. Parolanın ardından kod ekranı açılır; kodu
  Mailpit'ten alın. Kodun kendisinin hiçbir API yanıtında dönmediğini söyleyin.
- Kayıt bağlantısı yoktur: hesabı yalnızca İK talep eder, IT onaylar.

## 2. Destek talebi ve anlık bildirim (3 dk)

1. **Çalışan:** *Talep aç* → Yazıcı kategorisi, başlık ve açıklama → gönder. Talep numarası ve SLA
   bitiş zamanı görünür.
2. **Gizli pencere, IT:** `it.destek@devoplus.local` → *Talepler*. Talep, kategorisine ve yönlendirme
   kurallarına göre bir destek grubuna atanmıştır. *Üzerime al* → *İşleme al* → yanıt yazın.
3. **Çalışan penceresine dönün:** sayfayı yenilemeden yanıt ve bildirim gelir (SignalR).
4. IT *Çözüldü* der, çalışan *Onayla ve kapat* ile kapatır.

## 3. Çok adımlı onay: izin (2 dk)

1. **Çalışan:** *Uygulama Merkezi* → *İzin*. Bakiye ve hesaplanan iş günü görünür; hafta sonu ve
   resmî tatil düşülür. Başvurun.
2. **Yönetici** (`mudur@devoplus.local`) → *Onaylarım* → onaylayın. Başvuru İK adımına geçer.
3. **İK** (`ik.uzman@devoplus.local`) onaylar; izin bakiyeden düşer. Çalışan ekranında zincirin her
   adımı tarih ve kişiyle görünür.

## 4. Kural sunucudadır: ciro (1 dk)

*İzin* yerine *Ciro*: nakit 100, kart 200, toplam 500 girin. Arayüz uyarır; uyarıyı yok sayıp
gönderseniz bile API reddeder. Aynı mağaza ve gün için ikinci bildirim de reddedilir.

## 5. Yönetici gözü (2 dk)

`superadmin@devoplus.local`:

- **Form tasarımcısı:** yeni form → alan ekle → onay adımlarında *Formda seçilen kişi*. Kodsuz yeni
  departman uygulaması.
- **Raporlar:** talep, iş akışı, envanter, İK; CSV dışa aktarım.
- **Sistem Durumu:** PostgreSQL, Redis, RabbitMQ, Worker ve Seq canlı sağlık kontrolü, outbox durumu.

## 6. Dayanıklılık (1 dk, isteğe bağlı)

```bash
docker compose stop worker
```

Bir talep açın: kullanıcı işlemi başarılıdır, olay kalıcı kuyrukta bekler (RabbitMQ arayüzünde
`stajdesk.notifications` kuyruğunda görünür). Worker'ı başlatın:

```bash
docker compose start worker
```

Bekleyen bildirimler işlenir. Hiçbiri kaybolmaz, hiçbiri iki kez gelmez
([ADR 0002](../architecture/decisions/0002-transactional-outbox.md)).
