# StajDesk — Devoplus Kurumsal Çalışan Portalı ve Talep Yönetim Sistemi

StajDesk; destek talepleri (GLPI yaklaşımı), envanter, form + çok adımlı onay iş akışları, 40+ departman uygulaması, kurum içi iletişim ve raporlamayı tek portalda toplayan, rol/yetki tabanlı bir kurumsal web uygulamasıdır.

| Katman | Teknoloji |
|---|---|
| Arayüz | React 19, TypeScript, Vite, Tailwind CSS 4, React Router, TanStack Query, Zustand, React Hook Form + Zod, Recharts, Lucide |
| API | ASP.NET Core 10 (LTS), EF Core 10 (Npgsql), FluentValidation, JWT + Refresh Token, OpenAPI/Swagger |
| Veri | PostgreSQL 17 |
| Önbellek / hız sınırı | Redis 7 |
| Mesajlaşma | RabbitMQ 4 (topic exchange, kalıcı kuyruk, dead-letter, transactional outbox) |
| Arka plan | .NET Worker Service (bildirim servisi: uygulama içi, SignalR ile anlık, SMTP e-posta) |
| Gerçek zamanlı | ASP.NET Core SignalR (Redis pub/sub köprüsü) |
| E-posta | MailKit (SMTP); geliştirmede Mailpit |
| Loglama | Serilog → Seq |
| Altyapı | Docker, Docker Compose, sağlık kontrolleri |

## Hızlı başlangıç (tek komut)

```bash
node scripts/setup-secrets.mjs   # .env'i rastgele, güçlü parolalar ve JWT anahtarıyla oluşturur (demo parolasını bir kez yazdırır)
docker compose up -d --build
```

Depoda hiçbir gerçek parola, anahtar veya kişisel veri yoktur: `.env.example` yalnızca `CHANGE_ME` yer tutucuları içerir ve API/Worker bu değerlerle **açılmayı reddeder**.

| Adres | Açıklama |
|---|---|
| http://localhost:3100 | Genel tanıtım sitesi → **Portala Git** ile giriş |
| http://localhost:3100/swagger | Swagger UI (OpenAPI: `/openapi/v1.json`) |
| http://localhost:15673 | RabbitMQ yönetim arayüzü (`.env` içindeki kullanıcı) |
| http://localhost:5342 | Seq log arayüzü |
| http://localhost:8126 | Mailpit — Worker'ın gönderdiği e-postalar (dışarıya e-posta gitmez) |
| http://localhost:3100/health/ready | Sağlık kontrolü (PostgreSQL + Redis) |

API ilk açılışta migration'ları uygular ve boş veritabanını demo verisiyle doldurur.

### Demo hesapları

Tüm demo hesaplarının parolası `.env` dosyasındaki `SEED_DEFAULT_PASSWORD` değeridir.

**Giriş iki adımlıdır:** parola doğrulandıktan sonra hesabın e-postasına 6 haneli bir doğrulama kodu gider (5 dk geçerli, en fazla 5 deneme, 30 sn yeniden gönderme beklemesi). Geliştirmede e-postalar dışarı çıkmaz; kod **Mailpit**'te görünür: http://localhost:8126 (giriş ekranında bağlantısı vardır).

| E-posta | Rol | Ne görmeli? |
|---|---|---|
| `calisan@devoplus.local` | Çalışan | Kendi talepleri, izin/masraf başvuruları, zimmetli cihazlar |
| `mudur@devoplus.local` | Departman Yöneticisi | Ekip onayları, departman talepleri ve raporları |
| `it.destek@devoplus.local` | IT | Destek kuyruğu, envanter, hesap onayları |
| `ik.uzman@devoplus.local` | İK | Hesap talebi açma, izin bakiyeleri, içerik ve anketler |
| `superadmin@devoplus.local` | Super Admin + IT | Tüm yönetim ekranları, rol/izin matrisi, sistem durumu |
| `ceo@devoplus.local` | CEO | Şirket geneli raporlar |
| `magaza.yonetici@devoplus.local` | Müdür (mağaza) | Mağaza personeli onayları |
| `yeni.personel@devoplus.local` | Çalışan | **İlk girişte parola belirleme** akışı |

## Yerel geliştirme

```bash
node scripts/setup-secrets.mjs                        # .env + backend/src/*/appsettings.Local.json (gizli değerler, git dışı)
docker compose up -d postgres redis rabbitmq seq mailpit   # yalnızca altyapı
cd backend && dotnet run --project src/StajDesk.Api --launch-profile http   # http://localhost:5100
cd backend && dotnet run --project src/StajDesk.Worker
cd frontend && npm install && npm run dev            # http://localhost:5173 (/api → 5100 proxy)
```

Testler ve production build:

```bash
cd backend && dotnet test        # 46 xUnit testi (talep, iş akışı, Ciro/Vardiya, doğrulama kodu, domain kuralları)
cd frontend && npm test          # 22 Vitest testi (form şeması, biçimlendirme, yetki, DynamicForm)
cd frontend && npm run lint && npm run build
```

Uçtan uca testler (Playwright, çalışan sisteme karşı; Windows'ta kurulu Edge'i kullanır, tarayıcı indirmez):

```bash
docker compose up -d --build
cd frontend && npm run e2e       # giriş + doğrulama kodu (Mailpit), 7 rol × 40 sayfa taraması, mobil taşma, talep/izin/ciro/vardiya akışları, anlık bildirim
```

## Proje yapısı

```
StajDesk-New/
├─ backend/
│  ├─ src/StajDesk.Domain          Varlıklar, enum'lar, iş kuralları (talep yaşam döngüsü, SLA, izin hesabı)
│  ├─ src/StajDesk.Infrastructure  EF Core DbContext, migration'lar, denetim interceptor'ı, outbox, RabbitMQ, seed
│  ├─ src/StajDesk.Api             Controller'lar + servisler (feature klasörleri), JWT, Redis, doğrulama
│  ├─ src/StajDesk.Worker          RabbitMQ tüketicisi → bildirim kanalları (uygulama içi, e-posta)
│  └─ tests/StajDesk.Tests         xUnit (talep servisi, iş akışı motoru, Ciro/Vardiya, doğrulama kodu, domain kuralları)
├─ frontend/                       React + Vite SPA, nginx imajı (reverse proxy); e2e/ Playwright testleri
├─ scripts/setup-secrets.mjs       Yerel gizli değerleri üretir (.env, appsettings.Local.json)
├─ .github/workflows/ci.yml        CI: derleme, testler, lint, gizli bilgi taraması
├─ docs/                           Mimari, ER diyagramı, API referansı, güvenlik, Postman koleksiyonu
├─ docker-compose.yml
└─ .env.example
```

## Modüller

- **Destek talepleri:** Açık → İşlemde → (Beklemede) → Çözüldü → Kapandı; öncelik bazlı SLA, grup/kişi ataması, iç notlar, dosya ekleri, geçmiş, kanban görünümü, CSV dışa aktarım.
- **Otomatik yönlendirme kuralları:** kategori / departman / anahtar kelime / öncelik koşullarıyla gruba, kişiye, önceliğe yönlendirme; test aracı.
- **Envanter:** donanım + yazılım lisansları, zimmet ve iade geçmişi, garanti takibi.
- **Form ve iş akışı motoru:** form tasarımcısı (12 alan tipi, kişi seçimi dahil), çok adımlı onay (yönetici, departman yöneticisi, rol, aranarak seçilen kişi, sorumlu birim, **formda seçilen kişi**), otomatik adım atlama, sunucu tarafı doğrulama, işlem geçmişi.
- **Departman uygulamaları:** IT, Genel Süreçler, İK ve İdari İşler, Kalite, Lojistik, Mağaza Muhasebesi, Muhasebe, Pazarlama, Satın Alma, Ürün, Yönetim, Strateji, Yurtdışı/Yurtiçi Satış ve diğer modüller (43 form + 18 modül).
- **Özel iş mantığı:**
  - İzin: kıdeme göre hak ediş, iş günü/resmî tatil hesabı, bakiye ve çakışma kontrolü.
  - Avans/Masraf: limit kontrolü.
  - Ciro: açık mağaza listesi, nakit + kart = toplam kontrolü, aynı mağaza/gün için mükerrer bildirim engeli, ortalama sepet, 30 günlük ciro paneli ve mağaza sıralaması.
  - Vardiya: ileri tarih (≤ 60 gün), izinli güne ve aynı güne ikinci talep engeli; takas yapılan kişi aynı departmandan olmalı ve akışta **önce onun onayı** alınır; ekip vardiya değişiklikleri listesi.
- **İletişim:** duyuru/haber, etkinlik (katılım), doğum günleri, çalışan başarıları/takdir, anketler (anonim), mesajlaşma, dokümanlar, iş ilanları, nöbetçi kişi, mağazalar, projeler/görevler.
- **Bildirimler:** RabbitMQ → Worker → uygulama içi kayıt + SignalR ile anlık toast/sayaç güncellemesi + SMTP e-posta (onay, talep, hesap olayları). Anlık kanal koparsa arayüz periyodik sorguya geri döner.
- **Kullanıcı ve yetki:** dışarıdan kayıt yok; İK hesap talebi → IT onayı → geçici parola → ilk girişte parola belirleme; 10 sistem rolü, izin matrisi, devre dışı bırakma, denetim kayıtları.
- **Genel site:** yönetilebilir banner'lar (sıra, yayın aralığı), herkese açık haberler, ilanlar, istatistikler, **gerçek** döviz (TCMB), gram altın (Truncgil Finans piyasa verisi; erişilemezse gold-api.com ons × TCMB USD ile hesaplanır) ve hava durumu (Open-Meteo).
- **Raporlar:** talep, iş akışı, envanter ve İK raporları; tarih/departman/kategori/durum filtreleri.

Ayrıntılar: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/ER_DIAGRAM.md](docs/ER_DIAGRAM.md) · [docs/API.md](docs/API.md) · [docs/SECURITY.md](docs/SECURITY.md)

## Organizasyon verisi

Depodaki `backend/src/StajDesk.Infrastructure/Seed/organization.json` **anonimleştirilmiş örnektir**: merkez birim ve unvan adları geneldir; mağaza ("Ankara Mağaza 01" gibi) ve bölge adları kurgusaldır. Departman ve unvanlar çalışan CSV şablonunun **yalnızca Departman ve Unvan sütunlarından** üretilmiştir; çalışan kayıtları içe aktarılmamıştır ve demo kullanıcıları kurgusaldır.

Gerçek organizasyon dosyası kullanılacaksa `backend/seed-private/organization.json` yoluna konur (git dışıdır); docker compose bu klasörü bağlar ve seed yalnızca boş veritabanında bu dosyayı kullanır.

## Mimari notlar (SOLID)

- **Tek sorumluluk:** iş akışı motoru (`WorkflowService`) türden bağımsızdır; izin, limit, ciro ve vardiya kuralları kendi servislerindedir (`LeaveService`, `SpendingLimitService`, `RevenueService`, `ShiftService`).
- **Açık/kapalı:** yeni form türü = yeni `IFormKindHandler` + DI kaydı; motor değişmez. Yeni bildirim kanalı = yeni `INotificationChannel`.
- **Bağımlılıkların tersine çevrilmesi:** e-posta (`IEmailSender`), önbellek (`ICacheService`), olay yolu (`IEventBus`), talep servisi (`ITicketService`) arayüz üzerinden kullanılır; testlerde sahteleri verilir.
- Kalan iyileştirme alanı: bazı servisler henüz arayüzsüz enjekte edilir ve birkaç dosyada controller ile servis aynı dosyadadır.

## Bilinen sınırlamalar

- Ciro, Vardiya, İzin, Avans ve Masraf dışındaki departman uygulamaları ortak form + onay motoruyla çalışır; kendilerine özel hesaplama içermez.
- Mailpit yalnızca geliştirme içindir; gerçek gönderim için `.env` içindeki `SMTP_*` değerlerini kurumsal SMTP sunucusuna göre doldurunuz.
