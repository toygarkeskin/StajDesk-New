# Değişiklik günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/), sürüm numaraları
[Semantic Versioning](https://semver.org/lang/tr/) kurallarına uyar.

## [Yayınlanmadı]

### Güvenlik

- Redis artık parolalı; `setup-secrets` eski `.env` dosyalarına eksik parolayı kendisi ekler.
- PostgreSQL, Redis, RabbitMQ, Seq, Mailpit ve API portları yalnızca `127.0.0.1`'e açılır (önceden tüm ağa açıktı).
- Arayüze içerik güvenlik politikası (CSP), `Permissions-Policy` ve `Cross-Origin-Opener-Policy` eklendi.
- JS/CSS dosyalarında güvenlik başlıkları eksikti (nginx başlık mirası); tüm yanıtlara uygulanıyor.
- Yazı tipleri uygulamayla sunuluyor; Google Fonts'a istek gitmiyor.
- Canlı ortam katmanı: Caddy ile otomatik HTTPS, HSTS, kapalı yönetim portları, Swagger kapalı.

### Eklendi

- Mimari testleri: katman bağımlılıkları ve adlandırma kuralları derlemede denetlenir.
- README için gerçek arayüzden, kurgusal veriyle ekran görüntüsü alan betik (`npm run screenshots`).
- Belgeler: karar kayıtları (ADR), demo, ayarlar, test ve yedekleme rehberleri.
- GitHub şablonları: hata bildirimi, özellik önerisi, pull request, Dependabot.
- Data Protection anahtarları kalıcı birimde (`dpkeys`) saklanır.

### Değişti

- Tanıtım sayfası canlı: haber, banner, ilan ve etkinlikler dakikada bir, hava durumu 10 dakikada bir kendiliğinden yenilenir.
- Bağımlılıklar güncellendi: 10 NuGet paketi, 8 GitHub Actions eylemi (küçük sürümler).

- API, Domain ve Worker kodu özellik klasörlerine ayrıldı; her dosyada tek tip.

### Düzeltildi

- Edge'de parola kutusunda tarayıcının kendi göz düğmesi uygulamanınkiyle birlikte görünüyordu.
- Demo verisindeki izin başvurusu özeti, hesaplanan iş günüyle uyuşmuyordu.
- Yoğun saatlerde TCMB 6 saniyeden geç yanıt verince döviz kurları gösterilemiyordu; kur ve altın kaynakları artık aynı anda istenir.
- Boş veritabanı doldurulurken EF Core sırasız `Take` uyarısı veriyordu.
- Postman koleksiyonundaki örnek istekte gerçek bir mağaza adı geçiyordu.
- Docker yeniden açıldığında PostgreSQL, Redis, RabbitMQ, Seq ve Mailpit kendiliğinden başlamıyordu; portal açılıyor ama veri gelmiyordu. Tüm servisler `restart: unless-stopped`.
- "Bugün" bazı yerlerde UTC'ye göre hesaplanıyordu: gece 00:00-03:00 arası günün cirosu "ileri tarihli" sayılıyor, raporlar ve panodaki günlük grafikler günü 3 saat kaydırıyor, doğum günleri bir gün geç görünüyordu. Takvim günü artık her yerde Türkiye saatine göre (`BusinessTime`).
- Adında "İ" ya da "I" geçen mağazalar (İstanbul, İzmir, Isparta, Iğdır…) ciro bildiremiyordu: veritabanı ve C# Türkçe harfleri farklı küçültüyordu. Eşleşme artık Türkçe kurallarla yapılıyor; uçtan uca test bu mağazaları öncelikli seçiyor.
- Raporlar sayfasının varsayılan tarih aralığı gece 00:00-03:00 arası bir gün geriden başlıyordu.
- Telefondan ağ adresiyle (HTTP) girişte oturum çerezi `Secure` olduğu için saklanmıyor, her yenilemede oturum düşüyordu. Geliştirme kurulumunda kapalı, HTTPS canlı katmanda zorunlu.

## [1.0.0] - 2026-11-15

İlk sürüm.

### Eklendi

- Destek talepleri: SLA, otomatik yönlendirme, grup/kişi ataması, iç not, dosya eki, kanban, CSV.
- Envanter: donanım, yazılım lisansı, zimmet geçmişi.
- Form tasarımcısı ve çok adımlı onay motoru; formda seçilen kişiye onay.
- Departman uygulamaları: izin, avans, masraf, ciro, vardiya ve 38 standart form.
- E-posta doğrulama kodlu iki adımlı giriş.
- SignalR ile anlık bildirim, SMTP e-posta (geliştirmede Mailpit).
- İletişim modülleri, raporlar, genel tanıtım sitesi.
- CI (derleme, test, lint, gizli bilgi taraması) ve sürümde `ghcr.io` imajları.

[Yayınlanmadı]: https://github.com/toygarkeskin/StajDesk-New/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/toygarkeskin/StajDesk-New/releases/tag/v1.0.0
