# Mimari karar kayıtları (ADR)

Her kayıt tek bir kararı anlatır: bağlam, karar, sonuçları ve göze alınan bedel. Kayıtlar silinmez;
bir karar değişirse yeni kayıt açılır ve eskisi *Yerini aldı* olarak işaretlenir.

Yeni kayıt için [0000-template.md](0000-template.md) dosyasını kopyalayın, sıradaki numarayı verin.

| No | Karar | Durum |
|---|---|---|
| [0001](0001-teknoloji-yigini.md) | .NET 10 + React 19 + PostgreSQL, tek komutla Docker | Kabul |
| [0002](0002-transactional-outbox.md) | Olaylar transactional outbox ile RabbitMQ'ya | Kabul |
| [0003](0003-form-turu-isleyicileri.md) | Form türleri strateji deseniyle (`IFormKindHandler`) | Kabul |
| [0004](0004-eposta-dogrulama-kodu.md) | E-posta doğrulama kodlu iki adımlı giriş | Kabul |
| [0005](0005-depoda-sir-yok.md) | Gizli değerler ve gerçek kurum verisi depoya girmez | Kabul |
| [0006](0006-anlik-bildirim-koprusu.md) | Anlık bildirim: Worker → Redis pub/sub → API → SignalR | Kabul |
| [0007](0007-ozellik-klasorleri.md) | Özellik klasörleri ve dosya başına tek tip | Kabul |
