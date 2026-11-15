# 0002. Olaylar transactional outbox ile RabbitMQ'ya

- **Durum:** Kabul
- **Tarih:** 2026-11-15

## Bağlam

Bir talep oluşturulduğunda ya da onay verildiğinde ilgili kişilere bildirim gitmeli. Olay doğrudan
RabbitMQ'ya gönderilirse iki hata türü oluşur: veritabanı işlemi geri alınır ama olay gitmiştir
(var olmayan kayıt için bildirim) ya da işlem kaydedilir ama RabbitMQ o an kapalıdır (bildirim kaybolur).

## Karar

Servisler olayı `IEventBus.Enqueue` ile **aynı veritabanı işleminde** `outbox_messages` tablosuna yazar.
`OutboxPublisherService` bekleyen olayları kalıcı mesaj olarak `stajdesk.events` exchange'ine yayınlar
(publisher confirms açık) ve gönderileni işaretler. Worker mesajları elle onaylar (`ack`); hatalı mesaj
bir kez yeniden denenir, sonra dead-letter kuyruğuna düşer. Bildirim kaydı `(SourceEventId, UserId)`
benzersiz indeksiyle idempotenttir.

## Sonuçlar

- Olay ile veri değişikliği ya birlikte kaydedilir ya hiç kaydedilmez.
- RabbitMQ veya Worker kapalıyken üretilen olaylar sonradan işlenir.
- Aynı mesaj iki kez gelirse kullanıcı iki bildirim görmez.
- Bildirim birkaç saniye gecikebilir (yayıncı aralığı 2 sn). Kabul edilebilir.
- Sistem durumu ekranı bekleyen ve başarısız outbox olaylarını gösterir.

## Değerlendirilen seçenekler

| Seçenek | Neden seçilmedi |
|---|---|
| Doğrudan RabbitMQ'ya yayın | Yukarıdaki iki tutarsızlık türü |
| Bildirimi API içinde senkron göndermek | SMTP yavaşlığı istek süresine yansır; e-posta hatası kullanıcı işlemini bozar |
| MassTransit vb. kütüphane | Tek exchange ve tek tüketici için gereğinden ağır |
