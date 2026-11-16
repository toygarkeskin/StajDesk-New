# Canlıya alma (HTTPS)

Geliştirme kurulumu (`docker compose up`) portalı **şifrelenmemiş HTTP** ile `localhost:3100` üzerinden
sunar. Canlı ortamda trafik HTTPS ile şifrelenmeli; bunun için
[`docker-compose.prod.yml`](../../docker-compose.prod.yml) katmanı önüne **Caddy** koyar.

Caddy sertifikayı Let's Encrypt'ten kendisi alır ve süresi dolmadan yeniler. HTTP'den HTTPS'e
yönlendirme ve HSTS otomatiktir; elle sertifika kopyalamazsınız.

## Katmanın değiştirdikleri

| Konu | Geliştirme | Canlı katman |
|---|---|---|
| Portal | `http://localhost:3100` | `https://<DOMAIN>` (yalnızca 80/443 açık) |
| Aktarım şifrelemesi | Yok | TLS 1.2+/1.3, HSTS 1 yıl |
| PostgreSQL, Redis, RabbitMQ, Seq, API portları | Yalnızca `127.0.0.1` | Dışarıya hiç açılmaz |
| Mailpit (test posta kutusu) | Açık | Çalışmaz; gerçek SMTP zorunlu |
| Swagger | Açık | Kapalı (`Swagger__Enabled=false`) |
| İstemci IP'si | Docker ağ geçidi | Gerçek istemci IP'si (hız sınırı ve denetim kaydı için) |

## Adımlar

1. Alan adı alın, **A kaydını** sunucunuzun IP adresine yöneltin.
2. Sunucuda **80 ve 443** portlarını açın (Caddy sertifika doğrulamasını bu portlardan yapar).
3. Gizli değerleri üretin ve `.env` dosyasına canlı ayarlarını ekleyin:

   ```bash
   node scripts/setup-secrets.mjs
   ```

   ```ini
   DOMAIN=portal.sirket.com
   ACME_EMAIL=bt@sirket.com
   SMTP_HOST=smtp.sirket.com
   SMTP_PORT=587
   SMTP_USER=portal@sirket.com
   SMTP_PASSWORD=...
   DEV_MAILBOX_URL=
   ```

4. Başlatın:

   ```bash
   docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
   ```

Eksik bir ayar varsa compose başlamadan durur ve hangi değerin eksik olduğunu söyler.

## Yönetim arayüzlerine erişim

Seq, RabbitMQ ve veritabanı canlıda dışarı açılmaz. Gerektiğinde SSH tüneliyle bağlanın; örneğin
Seq için önce sunucuda yalnızca yerel porta açın, sonra tünel kurun:

```bash
ssh -L 5342:localhost:5342 kullanici@sunucu
```

## Yerelde denemek

Caddy, `DOMAIN=localhost` verildiğinde kendi yerel sertifika yetkilisini kullanır. Tarayıcı bu
sertifikaya güvenmediği için uyarı gösterir; yalnızca denemek içindir.

```bash
DOMAIN=localhost ACME_EMAIL=deneme@example.com SMTP_HOST=mailpit \
  docker compose -f docker-compose.yml -f docker-compose.prod.yml --profile mailpit up -d --build
```

## Canlıya almadan önce kontrol listesi

- [ ] `node scripts/setup-secrets.mjs` ile üretilmiş, kimseyle paylaşılmamış gizli değerler
- [ ] Gerçek SMTP sunucusu tanımlı, `DEV_MAILBOX_URL` boş
- [ ] Demo hesapları silindi ya da parolaları değiştirildi; gerçek kurum verisi `backend/seed-private/` ile yüklendi
- [ ] [Yedekleme](backup.md) zamanlandı ve bir kez geri yükleme denendi
- [ ] [SECURITY.md](../../SECURITY.md) okundu
