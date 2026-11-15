# 0005. Gizli değerler ve gerçek kurum verisi depoya girmez

- **Durum:** Kabul
- **Tarih:** 2026-11-14

## Bağlam

Depo herkese açıktır. Geliştirme sırasında bağlantı dizeleri, JWT anahtarı ve demo parolası
`appsettings.Development.json` içinde duruyordu; organizasyon dosyası gerçek mağaza ve bölge adlarını
içeriyordu.

## Karar

- **Gizli değerler** yalnızca git dışı dosyalarda durur: Docker için `.env`, yerel çalıştırma için
  `appsettings.Local.json`. İkisini de [`scripts/setup-secrets.mjs`](../../../scripts/setup-secrets.mjs)
  kriptografik rastgele değerlerle üretir.
- `.env.example` yalnızca `CHANGE_ME` içerir. `SecretGuard` boş veya `CHANGE_ME` değerle API ve
  Worker'ın **açılmasını reddeder**; compose dosyası zorunlu değişkenler eksikse durur.
- **Kurum verisi:** depodaki `organization.json` anonimdir ("Ankara Mağaza 01" gibi). Gerçek dosya
  `backend/seed-private/organization.json` yoluna konur (git dışı), compose bu klasörü salt okunur bağlar.
- CI her push'ta gitleaks ile gizli bilgi taraması yapar.

## Sonuçlar

- Depoyu klonlayan biri, gerçek bir değeri bilmeden sistemi kendi rastgele değerleriyle kurar.
- Yanlış yapılandırılmış sistem sessizce çalışmaz; nedenini söyleyerek durur.
- İlk kurulum bir komut uzar (`node scripts/setup-secrets.mjs`).
- Git geçmişinde de gizli değer yoktur: depo bu karar uygulandıktan sonra temiz bir geçmişle açıldı.
