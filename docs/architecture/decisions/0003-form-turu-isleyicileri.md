# 0003. Form türleri strateji deseniyle (`IFormKindHandler`)

- **Durum:** Kabul
- **Tarih:** 2026-11-14

## Bağlam

43 departman formunun çoğu "alanları doldur, onay zincirinden geçsin" yeterlidir. Beşi ise kendi
kuralını ister: izin (iş günü, tatil, bakiye), avans ve masraf (limit), ciro (nakit + kart = toplam,
mükerrer engeli), vardiya (izin çakışması, takas onayı). Bu kurallar ilk sürümde `WorkflowService`
içinde `switch` bloklarıyla duruyordu; motor 900 satıra çıkmış ve her yeni tür motoru değiştirmeyi
gerektiriyordu.

## Karar

Türe özgü davranış `IFormKindHandler` arayüzüne taşındı:

| Üye | Ne zaman çağrılır |
|---|---|
| `ValidateAsync` | Başvuru gönderilirken |
| `OnApprovedAsync` | Son onaydan sonra (ör. izin bakiyesinden düşme) |
| `UserContextAsync` | Form açılırken (bakiye, limit bilgisi) |
| `PrepareFieldsAsync` | Form açılırken (ör. açık mağaza listesi) |
| `ExtraFields` | Ayrıntı ekranında hesaplanan alanlar |

`FormKindRegistry` türü işleyiciye eşler; tanınmayan tür `StandardFormHandler`'a düşer. İşleyiciler:
`LeaveService`, `SpendingLimitService`, `RevenueService`, `ShiftService`.

## Sonuçlar

- `WorkflowService` türden bağımsız hâle geldi (900 → 615 satır).
- Yeni form türü = yeni sınıf + DI kaydı. Motor değişmez (açık/kapalı ilkesi).
- Her işleyici tek başına birim testiyle sınanır (`DepartmentAppTests`).
- [`ArchitectureTests`](../../../backend/tests/StajDesk.Tests/ArchitectureTests.cs) işleyicilerin
  adlandırma kuralını denetler.
