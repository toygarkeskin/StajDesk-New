# 0001. .NET 10 + React 19 + PostgreSQL, tek komutla Docker

- **Durum:** Kabul
- **Tarih:** 2026-11-15

## Bağlam

Sistem merkez ofis, bölge ve mağaza çalışanlarının tamamına hizmet edecek; talep, onay, envanter ve
iletişim modülleri birbirine bağlı. Bir staj ekibi tarafından geliştirilecek ve devralınacak; kurulumu
uzun bir doküman gerektirmemeli.

## Karar

- **API ve Worker:** ASP.NET Core 10 (LTS) ve .NET Worker Service. EF Core 10 + Npgsql.
- **Veri:** PostgreSQL 17. Form alanları ve onay adımları `jsonb` sütunlarında.
- **Arayüz:** React 19 + TypeScript + Vite, sunucu durumu için TanStack Query, istemci durumu için Zustand.
- **Ara katman:** Redis (önbellek, hız sınırı, pub/sub), RabbitMQ (olaylar), Seq (merkezi günlük).
- **Çalıştırma:** Tüm sistem `docker compose up -d --build` ile ayağa kalkar; ek kurulum gerekmez.

## Sonuçlar

- LTS sürümler: .NET 10 ve PostgreSQL 17 uzun süre güvenlik desteği alır.
- Tip güvenliği uçtan uca: C# sözleşmeleri ve TypeScript tipleri aynı alan adlarını kullanır.
- `jsonb`, form tasarımcısının şema değişikliği gerektirmeden yeni alan eklemesine izin verir; bedeli,
  bu alanların veritabanı düzeyinde doğrulanamamasıdır. Doğrulama sunucuda kodla yapılır.
- Yedi konteyner çalışır; geliştirici makinesinde ~2 GB bellek ister.

## Değerlendirilen seçenekler

| Seçenek | Neden seçilmedi |
|---|---|
| GLPI'yi doğrudan kurmak | PHP eklentileriyle özel iş kuralları (ciro, vardiya, izin bakiyesi) yazmak zor; arayüz kurum kimliğine uymuyor |
| Node.js API | Ekibin .NET deneyimi daha fazla; EF Core migration'ları ve FluentValidation hazır |
| Mikroservisler | Ekip ve yük bu karmaşıklığı gerektirmiyor; modüler monolit + ayrı Worker yeterli |
