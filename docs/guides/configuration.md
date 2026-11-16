# Ayarlar ve gizli değerler

Tüm ayarlar ortam değişkenlerinden okunur. Docker ile çalışırken kaynak `.env`, yerelde `dotnet run`
ile çalışırken `appsettings.Local.json` dosyasıdır. İkisi de git dışıdır.

## Gizli değerleri üretmek

```bash
node scripts/setup-secrets.mjs            # yoksa oluşturur, varsa dokunmaz
node scripts/setup-secrets.mjs --force    # yeniden üretir
```

Üretilenler:

| Dosya | Kullanan |
|---|---|
| `.env` | `docker compose` |
| `backend/src/StajDesk.Api/appsettings.Local.json` | API'yi yerelde çalıştırırken |
| `backend/src/StajDesk.Worker/appsettings.Local.json` | Worker'ı yerelde çalıştırırken |

Demo hesaplarının parolası yalnızca bir kez ekrana yazdırılır; sonra `.env` içindeki
`SEED_DEFAULT_PASSWORD` değerinden okunabilir.

> `--force` veritabanı parolasını da değiştirir. Mevcut PostgreSQL birimi eski parolayla kurulduğu için
> `docker compose down -v` ile birimleri silmeniz gerekir (veriler silinir; önce [yedek alın](backup.md)).

**Sunucu eksik ya da örnek değerle açılmayı reddeder.** `SecretGuard`, boş veya `CHANGE_ME` olan
zorunlu değerlerde API ve Worker'ı durdurur ve hangi değerin eksik olduğunu yazar.

## `.env` değişkenleri

| Değişken | Açıklama |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Veritabanı |
| `RABBITMQ_USER`, `RABBITMQ_PASSWORD` | Kuyruk |
| `REDIS_PASSWORD` | Önbellek; eski bir `.env`'de yoksa `setup-secrets` ekler |
| `JWT_KEY` | Erişim jetonu imza anahtarı, en az 32 karakter |
| `SEED_DEFAULT_PASSWORD` | Demo hesaplarının başlangıç parolası (yalnızca boş veritabanı doldurulurken) |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD` | E-posta sunucusu; varsayılan compose içindeki Mailpit |
| `DEV_MAILBOX_URL` | Giriş ekranındaki "test posta kutusunu aç" bağlantısı; **üretimde boş bırakın** |
| `DOMAIN`, `ACME_EMAIL` | Yalnızca [canlı katman](deployment.md): alan adı ve sertifika bildirimleri |

## Gerçek SMTP sunucusu

`.env` içinde `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD` değerlerini kurumsal sunucuya
göre doldurun, `DEV_MAILBOX_URL` değerini boşaltın ve yeniden başlatın:

```bash
docker compose up -d api worker
```

SMTP tanımlı değilse e-posta kanalı ve [giriş doğrulama kodu](../architecture/decisions/0004-eposta-dogrulama-kodu.md)
devre dışı kalır; uygulama içi ve anlık bildirimler çalışmaya devam eder.

## Giriş doğrulama kodu

`Auth:LoginCode` bölümü (ortam değişkeninde `Auth__LoginCode__…`):

| Ayar | Varsayılan |
|---|---|
| `Enabled` | `true` |
| `CodeLength` | `6` |
| `ValidMinutes` | `5` |
| `MaxAttempts` | `5` |
| `MaxSends` | `4` |
| `ResendCooldownSeconds` | `30` |

## Gerçek kurum verisini yüklemek

Depodaki `backend/src/StajDesk.Infrastructure/Seed/organization.json` anonim örnektir. Kendi
departman ve unvanlarınızla başlamak için:

1. Dosyayı aynı biçimde `backend/seed-private/organization.json` yoluna koyun (klasör git dışıdır).
2. Veritabanı boşken sistemi başlatın. Compose bu klasörü salt okunur bağlar; API günlüğünde
   "Organizasyon verisi dış dosyadan yükleniyor" satırını görürsünüz.

Dosya yalnızca **boş veritabanında** okunur; çalışan bir sistemin verisini değiştirmez. Çalışan
kayıtları içe aktarılmaz; departman ve unvanlar yalnızca CSV'nin bu iki sütunundan üretilir.

## Diğer ayarlar

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| `Jwt:AccessTokenMinutes` | `15` | Erişim jetonu ömrü |
| `Jwt:RefreshTokenDays` | `7` | Yenileme jetonu ömrü (httpOnly çerez) |
| `Storage:Root` | `data/uploads` | Yüklenen dosyalar; Docker'da `uploads` birimi |
| `Storage:MaxBytes` | `10485760` | Dosya başına üst sınır (10 MB) |
| `DataProtection:KeysPath` | — | ASP.NET Data Protection anahtarları; Docker'da `dpkeys` birimi |
| `Swagger:Enabled` | `true` | API belgesi (`/swagger`); canlı katmanda `false` |
| `Cors:Origins` | `localhost:5173`, `localhost:3000` | Yerel geliştirmede izin verilen kökenler |
