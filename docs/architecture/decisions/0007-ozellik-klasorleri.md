# 0007. Özellik klasörleri ve dosya başına tek tip

- **Durum:** Kabul
- **Tarih:** 2026-11-15

## Bağlam

API ilk sürümde alan başına tek dosyaydı: `Identity.cs` 900, `Collaboration.cs` 740 satırdı; DTO,
doğrulayıcı, servis ve controller aynı dosyadaydı. Bir değişikliğin nereye yazılacağı açık değildi,
birleştirme çakışmaları sıktı.

## Karar

- `StajDesk.Api/Features/<Alan>/` klasörü bir alanın tüm parçalarını taşır:
  `<Ad>Controller.cs`, `<Ad>Service.cs`, `Contracts.cs` (istek/yanıt kayıtları), `Validators.cs`.
- Ortak altyapı `Common/` altında konuya göre ayrılır: `Http/`, `Security/`, `Caching/`, `Storage/`,
  `Realtime/`, `Messaging/`.
- `StajDesk.Domain/<Alan>/` altında her varlık kendi dosyasındadır; alanın enum'ları `Enums.cs`'te.
- Ad alanları (namespace) değişmedi; yalnızca dosya yerleşimi değişti, davranış aynıdır.

## Sonuçlar

- Bir sınıfı bulmak için adını aramak yeterli; dosya adı tip adıdır.
- Küçük kayıt tipleri (`record`) `Contracts.cs`'te toplu durur: onlarca tek satırlık dosya oluşmaz.
- Katman bağımlılıkları [`ArchitectureTests`](../../../backend/tests/StajDesk.Tests/ArchitectureTests.cs)
  ile denetlenir; controller'ların `Features` altında ve `…Controller` adıyla durması da test edilir.
