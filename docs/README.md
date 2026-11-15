# StajDesk belgeleri

Ana sayfa için [README](../README.md). Bu klasör sistemin nasıl kurulduğunu, neden böyle kurulduğunu ve
nasıl işletileceğini anlatır.

## Mimari

| Belge | İçerik |
|---|---|
| [architecture/overview.md](architecture/overview.md) | Bileşenler, katmanlar, istek akışı, olay akışı, Redis kullanımı, iş akışı motoru |
| [architecture/data-model.md](architecture/data-model.md) | ER diyagramları: organizasyon, talepler, iş akışı, iletişim |
| [architecture/decisions/](architecture/decisions/README.md) | Mimari karar kayıtları (ADR): neyi neden seçtik, neyi göze aldık |

## API

| Belge | İçerik |
|---|---|
| [api/README.md](api/README.md) | Yanıt zarfı, iki adımlı giriş, tüm uç noktalar ve gereken yetkiler |
| [api/StajDesk.postman_collection.json](api/StajDesk.postman_collection.json) | Postman koleksiyonu; giriş isteği jetonu kendisi kaydeder |

Canlı şema: çalışan sistemde `/swagger`.

## Rehberler

| Belge | Kimin için |
|---|---|
| [guides/demo.md](guides/demo.md) | Sistemi birine 10 dakikada göstermek isteyenler |
| [guides/configuration.md](guides/configuration.md) | Ayarlar, gizli değerler, SMTP, gerçek kurum verisini yükleme |
| [guides/testing.md](guides/testing.md) | Birim, mimari ve uçtan uca testler; README görüntülerini yenileme |
| [guides/backup.md](guides/backup.md) | Veritabanı ve dosya yedeği, geri yükleme |

## Güvenlik

[SECURITY.md](../SECURITY.md): açık bildirme yolu ve uygulanan kontrollerin tam listesi.
