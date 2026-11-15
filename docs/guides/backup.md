# Yedekleme ve geri yükleme

Kalıcı veri iki yerdedir:

| Birim | İçerik |
|---|---|
| `pgdata` | PostgreSQL: kullanıcılar, talepler, başvurular, bildirimler, denetim kayıtları |
| `uploads` | Talep ve başvurulara eklenen dosyalar |

Redis yalnızca önbellek ve hız sınırı tutar; RabbitMQ'daki olaylar zaten veritabanındaki outbox'tan
üretilir. Bu ikisinin yedeği gerekmez.

## Yedek almak

Komutlar depo kökünde çalıştırılır; `.env` içindeki kullanıcı ve veritabanı adlarını kullanır.

```bash
# Veritabanı: sıkıştırılmış, tek dosya
docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > stajdesk-$(date +%F).dump

# Yüklenen dosyalar
docker compose exec -T api tar -C /app/data -czf - uploads > stajdesk-uploads-$(date +%F).tar.gz
```

Yedek dosyaları kişisel veri içerir. Depoya koymayın, şifreli bir yerde saklayın.

## Geri yüklemek

```bash
# Sistemi durdurun, yalnızca veritabanını açık bırakın
docker compose stop api worker web

# Veritabanını geri yükleyin (mevcut nesneler önce silinir)
docker compose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists' < stajdesk-2026-11-15.dump

# Dosyaları geri yükleyin
docker compose start api
docker compose exec -T api tar -C /app/data -xzf - < stajdesk-uploads-2026-11-15.tar.gz

docker compose up -d
```

Geri yüklemeden sonra **Sistem Durumu** ekranında (`/system`) tüm bileşenlerin sağlıklı olduğunu ve
outbox'ta başarısız olay kalmadığını kontrol edin.

## Ne sıklıkla?

Canlı kullanımda veritabanı için günlük yedek ve en az 7 günlük saklama önerilir. Yedeğin işe
yaradığından emin olmanın tek yolu düzenli aralıklarla ayrı bir ortama geri yüklemeyi denemektir.
