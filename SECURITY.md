# Güvenlik politikası

## Güvenlik açığı bildirme

Bir güvenlik açığı bulduysanız lütfen **herkese açık issue açmayın**. Bunun yerine GitHub'ın
[özel güvenlik bildirimi](../../security/advisories/new) özelliğini kullanın (depo → *Security* → *Report a vulnerability*).

Bildirime şunları ekleyin:

- Etkilenen bileşen (API, Worker, arayüz, docker yapılandırması) ve sürüm / commit
- Sorunu yeniden üretme adımları
- Olası etki (veri sızıntısı, yetki yükseltme, hizmet kesintisi…)

Bildirim 7 gün içinde yanıtlanır; düzeltme yayınlanana kadar ayrıntılar gizli tutulur.

## Desteklenen sürümler

| Sürüm | Güvenlik düzeltmesi |
|---|---|
| 1.x (`main`) | ✅ |

> Bu proje bir staj projesidir ve demo verisiyle çalışır. Canlı ortama almadan önce aşağıdaki kontrolleri
> kendi altyapınıza göre gözden geçirin; özellikle SMTP, HTTPS sonlandırma ve yedekleme.

## Uygulanan kontroller

| Kontrol | Uygulama |
|---|---|
| Depoda gizli bilgi yok | Bağlantı dizeleri, JWT anahtarı, SMTP ve seed parolası `.env` (docker) veya `appsettings.Local.json` (yerel) dosyalarından gelir; ikisi de `.gitignore`'dadır ve `scripts/setup-secrets.mjs` tarafından kriptografik rastgele üretilir. `.env.example` yalnızca `CHANGE_ME` içerir; `SecretGuard` boş veya `CHANGE_ME` değerle açılışı reddeder. CI'da gitleaks ile gizli bilgi taraması yapılır. |
| Kişisel / kurumsal veri | Depodaki organizasyon dosyası anonimdir (mağaza/bölge adları kurgusal). Gerçek dosya yalnızca yerelde `backend/seed-private/` altında durur (git dışı). |
| Parolalar | ASP.NET Core Identity `PasswordHasher` (PBKDF2, tuzlu). Politika: ≥10 karakter, büyük/küçük harf, rakam. |
| Hesap yaşam döngüsü | Dışarıdan kayıt yok. İK talep → IT/Admin onay → kriptografik rastgele geçici parola (bir kez gösterilir) → ilk girişte zorunlu parola değişikliği (sunucu tarafında `PasswordChangeGateMiddleware`). Devre dışı bırakılan hesabın tüm refresh token'ları iptal edilir. |
| Oturum | 15 dk JWT + 7 gün refresh token (httpOnly, SameSite=Strict, `/api/auth` yolu). Refresh token veritabanında SHA-256 özetiyle saklanır, her kullanımda döndürülür; **eski token tekrar kullanılırsa tüm oturumlar kapatılır**. Access token yalnızca bellekte tutulur. |
| İki adımlı giriş | Parola sonrası e-postaya 6 haneli kod (`RandomNumberGenerator`). Kod saklanmaz, yalnızca meydan okuma kimliğiyle tuzlanmış SHA-256 özeti tutulur; sabit zamanlı karşılaştırma, 5 dk geçerlilik, 5 hatalı denemede kilit, tek kullanımlık, 30 sn yeniden gönderme beklemesi ve en fazla 4 gönderim. Yeni giriş eski bekleyen kodları geçersiz kılar. Kod hiçbir API yanıtında ve logda yer almaz; e-posta adresi ekranda maskelenir. |
| Kaba kuvvet | Redis `INCR + EXPIRE`: aynı hesap için 5 dk içinde 5 hatalı giriş → `429` + `Retry-After`. Genel site uçları IP başına dakikada 120 istek. |
| Yetkilendirme | Her uçta `[Authorize]` / `[HasPermission]`; veri seviyesinde görünürlük (talep, başvuru, dosya, doküman, zimmet) sorgu filtresiyle uygulanır. Admin/Super Admin rollerini yalnızca Super Admin atayabilir. Ön yüz yalnızca arayüzü gizler; asıl kontrol sunucudadır. |
| Girdi doğrulama | FluentValidation (tüm request DTO'ları) + dinamik form alanları için sunucu tarafı tip/zorunluluk/aralık/seçenek kontrolü. Standart hata zarfı. |
| Dosya yükleme | Uzantı beyaz listesi, MIME, **dosya imzası (magic number)** kontrolü, 10 MB sınırı, rastgele dosya adı, yol geçişine karşı kök dizin kontrolü; indirmede bağlı kayda erişim kontrolü. |
| Denetim | `AuditInterceptor` tüm IAuditable varlıkların oluşturma/güncelleme/silme değişikliklerini kullanıcı ve IP ile kaydeder; giriş, parola değişimi ve sıfırlama ayrıca kaydedilir. |
| Loglama | Serilog → Seq. Parola, token, e-posta adresi loglanmaz; kullanıcı kimliği (GUID) loglanır (e-posta gönderim logları dahil). |
| Gerçek zamanlı kanal | SignalR hub `[Authorize]`; kullanıcı yalnızca kendi `sub` kimliğine gönderilen olayları alır. Sorgu dizesindeki token yalnızca `/hubs` yolunda kabul edilir; ilk parola belirlenmeden hub'a bağlanılamaz (`PasswordChangeGateMiddleware`). |
| E-posta | SMTP kimlik bilgileri `.env` / ortam değişkeninden gelir. Geliştirmede Mailpit kullanıldığı için dışarıya e-posta gitmez. Şablondaki tüm değerler HTML-encode edilir. |
| Kişisel veri | Çalışan rehberi yalnızca iş iletişim bilgilerini döner; doğum günleri yalnızca gün/ay. Genel site yalnızca `IsPublic` içerik ve toplu sayıları gösterir. Anonim anketlerde kullanıcı kimliği saklanmaz (tekrar oyu engellemek için özet tutulur). |
| HTTP | nginx: `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`. API ve SPA aynı kökenden sunulur (CORS minimum). |
| Dış veri | Piyasa ve hava verisi sunucu tarafında çekilir, önbelleğe alınır; kaynak erişilemezse değer uydurulmaz, hata gösterilir. |
