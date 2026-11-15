# 0004. E-posta doğrulama kodlu iki adımlı giriş

- **Durum:** Kabul
- **Tarih:** 2026-11-14

## Bağlam

Portal kişisel bilgilere, onaylara ve mağaza cirolarına erişim verir. Tek başına parola; tekrar
kullanılan veya paylaşılan parolalara karşı zayıftır. Kurumun her çalışanının bir kurumsal e-postası
vardır; ayrı bir doğrulayıcı uygulaması dağıtmak ise mağaza personeli için pratik değildir.

## Karar

Parola doğrulandıktan sonra hesabın e-postasına 6 haneli bir kod gönderilir:

- Kod `RandomNumberGenerator` ile üretilir ve **saklanmaz**; yalnızca meydan okuma kimliğiyle tuzlanmış
  SHA-256 özeti tutulur. Karşılaştırma sabit zamanlıdır.
- 5 dk geçerli, tek kullanımlık; 5 hatalı denemede iptal; en fazla 4 gönderim, 30 sn yeniden gönderme beklemesi.
- Yeni giriş, aynı kullanıcının bekleyen eski kodlarını geçersiz kılar.
- Kod hiçbir API yanıtında ve günlükte yer almaz; e-posta adresi ekranda maskelenir.
- SMTP tanımlı değilse özellik kapanır ve giriş tek adımlı çalışır.

## Sonuçlar

- Parola sızsa bile e-posta kutusuna erişim olmadan giriş yapılamaz.
- Giriş bir e-posta gecikmesi kadar uzar. Geliştirmede Mailpit kullanıldığı için anlıktır.
- Uçtan uca testler kodu Mailpit API'sinden okuyarak gerçek akışı sınar.

## Değerlendirilen seçenekler

| Seçenek | Neden seçilmedi |
|---|---|
| TOTP (doğrulayıcı uygulaması) | Mağaza personelinin kişisel telefonuna uygulama kurdurmak gerekir |
| SMS | Ücretli sağlayıcı ve telefon numarası doğrulaması gerektirir |
| Kurumsal SSO (Entra ID) | Hedef ortamda henüz yok; ileride eklenebilir |
