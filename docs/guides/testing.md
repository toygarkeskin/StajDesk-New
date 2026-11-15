# Testler

| Katman | Araç | Sayı | Nerede |
|---|---|---|---|
| Backend birim + iş akışı | xUnit, EF Core InMemory | 46 | `backend/tests/StajDesk.Tests` |
| Mimari kuralları | xUnit + yansıma | 5 | `backend/tests/StajDesk.Tests/ArchitectureTests.cs` |
| Frontend birim | Vitest + Testing Library | 22 | `frontend/src/**/*.test.ts(x)` |
| Uçtan uca | Playwright | 21 | `frontend/e2e/*.spec.ts` |

## Hızlı: altyapı gerektirmez

```bash
cd backend && dotnet test
cd frontend && npm test && npm run lint
```

## Uçtan uca: çalışan sisteme karşı

```bash
docker compose up -d --build
cd frontend && npm run e2e
```

Windows'ta kurulu Edge kullanılır, ek tarayıcı indirilmez. Başka tarayıcı için `E2E_BROWSER=chrome`
veya `chromium`.

| Dosya | Ne denetler |
|---|---|
| `auth.spec.ts` | Parola + e-posta kodu (Mailpit API'sinden okunur), yanlış kodda kalan hak, 5 hatalı parolada `429`, ilk girişte zorunlu parola, oturumsuz erişim engeli, çıkış, genel sitede çalışan bilgisi sızmaması |
| `crawl.spec.ts` | 7 rol × 40 sayfa: konsol hatası ve başarısız istek yok, liste → ayrıntı geçişleri, 375 px'te yatay taşma yok |
| `flows.spec.ts` | Talep yaşam döngüsü ve anlık bildirim, izin onay zinciri, ciro ve vardiya kuralları, form tasarımcısı, kaydırma davranışı |

Ortam değişkenleri: `E2E_BASE_URL` (varsayılan `http://localhost:3100`), `E2E_MAILPIT`
(varsayılan `http://localhost:8126`), `E2E_PASSWORD` (varsayılan `.env` içindeki demo parolası).

## Mimari testleri

Katman kuralları belgede yazmakla kalmaz, testte kırılır:

| Kural | Neden |
|---|---|
| `Domain` hiçbir `StajDesk.*` projesine ve EF Core / ASP.NET / Npgsql'e bağlı değildir | İş kuralları çerçeveden bağımsız kalsın |
| `Infrastructure` yalnızca `Domain`'e bağlıdır | Bağımlılık yönü içeri doğru |
| Controller'lar `StajDesk.Api.Features` ad alanında ve `…Controller` adıyla durur | Giriş noktaları tek yerde |
| Form türü işleyicileri `…Service` (standart: `…Handler`) adını taşır | [ADR 0003](../architecture/decisions/0003-form-turu-isleyicileri.md) |

## CI

Her push ve pull request'te [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml):

1. **Gizli bilgi taraması:** gitleaks, tüm geçmiş
2. **Backend:** `dotnet build -warnaserror` + `dotnet test`
3. **Frontend:** `oxlint --deny-warnings`, `npm test`, `npm run build` (tip kontrolü dahil)

Bir sürüm yayınlandığında [`release.yml`](../../.github/workflows/release.yml) API, Worker ve Web
imajlarını `ghcr.io`'ya gönderir.

## README ekran görüntülerini yenilemek

Görüntüler **yalnızca kurgusal demo verisiyle** alınmalıdır. Gerçek kurum dosyası yüklü bir sisteme
karşı çalıştırmayın.

```bash
docker compose up -d --build          # backend/seed-private/ boşken, temiz veritabanı
cd frontend && npm run screenshots    # → docs/images/*.png
```
