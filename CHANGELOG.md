# Değişiklik günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/), sürüm numaraları
[Semantic Versioning](https://semver.org/lang/tr/) kurallarına uyar.

## [Yayınlanmadı]

### Eklendi

- Mimari testleri: katman bağımlılıkları ve adlandırma kuralları derlemede denetlenir.
- README için gerçek arayüzden, kurgusal veriyle ekran görüntüsü alan betik (`npm run screenshots`).
- Belgeler: karar kayıtları (ADR), demo, ayarlar, test ve yedekleme rehberleri.
- GitHub şablonları: hata bildirimi, özellik önerisi, pull request, Dependabot.
- Data Protection anahtarları kalıcı birimde (`dpkeys`) saklanır.

### Değişti

- API, Domain ve Worker kodu özellik klasörlerine ayrıldı; her dosyada tek tip.

### Düzeltildi

- Edge'de parola kutusunda tarayıcının kendi göz düğmesi uygulamanınkiyle birlikte görünüyordu.
- Demo verisindeki izin başvurusu özeti, hesaplanan iş günüyle uyuşmuyordu.
- Yoğun saatlerde TCMB 6 saniyeden geç yanıt verince döviz kurları gösterilemiyordu.
- Boş veritabanı doldurulurken EF Core sırasız `Take` uyarısı veriyordu.

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
