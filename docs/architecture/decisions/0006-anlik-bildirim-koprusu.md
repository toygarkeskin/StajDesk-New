# 0006. Anlık bildirim: Worker → Redis pub/sub → API → SignalR

- **Durum:** Kabul
- **Tarih:** 2026-11-15

## Bağlam

Bildirimler Worker'da üretilir, ama tarayıcı bağlantıları API'dedir. Kullanıcı bir talebe yanıt
geldiğinde sayfayı yenilemeden görmeli. API ileride birden fazla örnekle çalışabilir.

## Karar

1. Worker bildirimi veritabanına yazdıktan sonra (`InAppChannel`), `RealtimeChannel` olayı Redis
   `stajdesk:realtime` kanalına yayınlar.
2. Her API örneğindeki `RealtimeRelayService` bu kanala abonedir ve olayı `NotificationHub` üzerinden
   yalnızca ilgili kullanıcının (`sub`) bağlantılarına iletir.
3. Arayüz olay gelince ilgili sorguları (sayaç, liste, açık talep/başvuru ayrıntısı) yeniler ve bildirim gösterir.
4. Bağlantı koparsa arayüz periyodik sorguya döner; bağlantı gelince sorgu aralığı uzar.

## Sonuçlar

- API yatayda çoğaltılabilir: her örnek kendi bağlantılarına iletir, ayrı bir SignalR backplane gerekmez.
- Redis kapalıysa anlık bildirim durur ama kayıt kaybolmaz; periyodik sorgu devreye girer.
- WebSocket başlık taşıyamadığı için JWT, **yalnızca** `/hubs` yolunda `access_token` sorgu
  parametresinden kabul edilir.
