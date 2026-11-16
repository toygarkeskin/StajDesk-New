# Telefonda deneme

Portalı geliştirme kurulumunda (`docker compose up`) telefondan açmak için telefon ve bilgisayar
**aynı ağda** olmalı ve bilgisayar 3100 portuna gelen bağlantılara izin vermelidir.

## 1. Bilgisayarın ağ adresini bulun

```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object IPAddress -notlike '127.*' | Select-Object IPAddress, InterfaceAlias
```

Telefonun bağlı olduğu ağdaki adresi seçin (ör. `192.168.1.14`). Telefonda `http://192.168.1.14:3100`
açılır. **`localhost` yazmayın:** telefonda `localhost` telefonun kendisidir.

## 2. Ağ türünü kontrol edin

```powershell
Get-NetConnectionProfile | Select-Object InterfaceAlias, NetworkCategory, Name
```

- **Ev veya ofis ağı** (`Private`): devam edin.
- **Kafe, belediye, otel gibi herkese açık Wi-Fi** (`Public`): bu ağlarda cihazlar genellikle birbirini
  göremez (istemci yalıtımı) ve güvenlik duvarını bu ağa açmak portalı ağdaki herkese açar.
  **Bunun yerine telefonun kişisel erişim noktasına (hotspot) bilgisayarı bağlayın** ve o ağı
  `Private` yapın.

## 3. Güvenlik duvarında 3100 portuna izin verin

Yönetici olarak açılmış PowerShell'de, **yalnızca özel ağlar** için:

```powershell
New-NetFirewallRule -DisplayName "StajDesk portal (3100)" -Direction Inbound -Protocol TCP -LocalPort 3100 -Action Allow -Profile Private
```

Deneme bitince kaldırmak için:

```powershell
Remove-NetFirewallRule -DisplayName "StajDesk portal (3100)"
```

## 4. Giriş

Parola sonrası istenen 6 haneli kodu **bilgisayardaki** Mailpit'ten okuyun: http://localhost:8126.
Mailpit güvenlik nedeniyle yalnızca bilgisayardan açılır; giriş ekranındaki "test posta kutusu"
bağlantısı telefonda çalışmaz.

## Notlar

- Geliştirme kurulumu şifresiz HTTP kullanır; oturum çerezi bu yüzden bu kurulumda `Secure` değildir
  (`Auth__SecureCookie=false`). Aksi hâlde tarayıcı çerezi ağ adresinde saklamaz ve her sayfa
  yenilemesinde oturum düşer. Canlı katman HTTPS kullanır ve çerezi `Secure` yapar
  ([canlıya alma](deployment.md)).
- Ağdan yalnızca portal (3100) açılır. Veritabanı, Redis, RabbitMQ, Seq ve Mailpit yalnızca bilgisayardan erişilebilir.
