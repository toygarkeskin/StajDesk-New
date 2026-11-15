# Bu depoda çalışma rehberi

Bu dosya depoda değişiklik yapacak geliştiriciler içindir. Mimariyi anlatmaz (onun için
[docs/architecture/overview.md](docs/architecture/overview.md) var); **nereye ne yazılacağını ve nelerin
kırılacağını** söyler.

## Önce bunu çalıştırın

```bash
node scripts/setup-secrets.mjs
cd backend && dotnet test                       # altyapı gerektirmez
cd frontend && npm install && npm test && npm run lint
```

Değişiklik bittiğinde uçtan uca testleri de çalıştırın:

```bash
docker compose up -d --build
cd frontend && npm run e2e
```

## Katman kuralları (testler zorlar)

[`ArchitectureTests`](backend/tests/StajDesk.Tests/ArchitectureTests.cs) şunları kırılma noktası yapar:

| Kural | Neden |
|---|---|
| `Domain` hiçbir projeye ve EF Core / ASP.NET / Npgsql'e bağlı değildir | İş kuralları çerçeveden bağımsız kalsın |
| `Infrastructure` yalnızca `Domain`'e bağlıdır | Bağımlılık yönü içeri doğru |
| Controller'lar `Features` altında, `…Controller` adıyla | Giriş noktaları tek yerde |
| Form türü işleyicileri `…Service` adıyla | Bkz. [ADR 0003](docs/architecture/decisions/0003-form-turu-isleyicileri.md) |

Bir kuralı çiğnemek gerekiyorsa testi değiştirmeden önce nedenini yeni bir
[karar kaydına](docs/architecture/decisions/README.md) yazın.

## Dosya yerleşimi

- Bir C# dosyasında **tek tip**; dosya adı tip adıdır.
- API'de bir alanın her şeyi kendi klasöründe: `StajDesk.Api/Features/<Alan>/`
  - `<Ad>Controller.cs`: yalnızca HTTP: yetki özniteliği, servisi çağır, sonucu döndür. İş mantığı yok.
  - `<Ad>Service.cs`: iş mantığı. `UserContext` alır, `HttpContext` bilmez; birim testinde bellek içi veritabanıyla çalışır.
  - `Contracts.cs`: istek/yanıt `record`'ları.
  - `Validators.cs`: FluentValidation doğrulayıcıları. `ValidationFilter` bunları kendiliğinden çalıştırır.
- Saf iş kuralları (`TicketRules`, `LeaveRules`) `StajDesk.Domain/<Alan>/` altına.
- Arayüzde sayfalar `frontend/src/pages/<alan>/`, ortak parçalar `components/`, API çağrıları `lib/queries.ts`.

## Yeni uç nokta eklemek

1. İsteği `Features/<Alan>/Contracts.cs` içine, doğrulayıcısını `Validators.cs` içine yazın.
2. İş mantığını servise ekleyin; görünürlük kuralını sorguya ekleyin (yalnızca arayüzde gizlemek yetmez).
3. Controller'da yetkiyi `[HasPermission(Permissions.…)]` ile belirtin.
4. Olay üretiyorsa `IEventBus.Enqueue` kullanın; **aynı `SaveChanges` içinde** kaydedilir (outbox).
5. Test yazın ve [docs/api/README.md](docs/api/README.md) tablosunu güncelleyin.

## Yeni form türü eklemek

1. `Features/FormKinds/` altında `FormKindHandler`'dan türeyen bir sınıf yazın; `Kinds` ile hangi form türlerini işlediğini belirtin, yalnızca gereken üyeleri ezin.
2. `Program.cs` içinde diğer işleyiciler gibi kaydedin:
   `AddScoped<YeniService>()` ve `AddScoped<IFormKindHandler>(sp => sp.GetRequiredService<YeniService>())`.
3. `FormKinds` sabitlerine ve form tasarımcısındaki *Tür* listesine ekleyin.

`WorkflowService` değişmez. Değişmesi gerekiyorsa bir şey yanlış gidiyordur.

## Veritabanı değişikliği

```bash
cd backend
dotnet ef migrations add <AnlamliAd> --project src/StajDesk.Infrastructure --startup-project src/StajDesk.Api
```

Migration API açılışında uygulanır. Var olan veriyi bozan değişiklikte (sütun silme, tip değiştirme)
veriyi taşıyan adımı migration'a yazın.

## Sık düşülen tuzaklar

- **Gizli değer yazmayın.** `appsettings*.json` içine parola, anahtar, bağlantı dizesi girmez; CI'daki
  gitleaks yakalar. Yerel değerler `appsettings.Local.json` içindedir (git dışı).
- **Gerçek kurum verisi koymayın.** Mağaza, bölge, kişi adı depoya girmez; örnek veri kurgusaldır.
- **Günlüğe kişisel veri yazmayın.** Parola, jeton, e-posta adresi, doğrulama kodu loglanmaz; kullanıcı
  kimliği (GUID) yeterlidir.
- **Uyarı bırakmayın.** CI `-warnaserror` ve `oxlint --deny-warnings` ile çalışır.
- **Tarihleri UTC saklayın**, arayüzde yerel saate çevirin. İzin ve vardiya gibi gün bazlı alanlar `DateOnly`.

## Commit ve pull request

- Commit mesajı Türkçe, emir kipinde ve ne yapıldığını söyler: `İzin bakiyesinde resmî tatili düş`.
- Pull request şablonundaki kontrol listesini doldurun; CI yeşil olmadan birleştirilmez.
- Davranış değişikliği [CHANGELOG.md](CHANGELOG.md) dosyasına *Yayınlanmadı* başlığı altına eklenir.
