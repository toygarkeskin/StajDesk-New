# API Referansı

Canlı ve eksiksiz şema: **`/swagger`** (OpenAPI 3: `/openapi/v1.json`). Tüm yanıtlar standart zarfla döner:

```json
{ "success": true, "data": { }, "message": "İşlem mesajı", "errors": { "alan": ["hata"] }, "code": "VALIDATION" }
```

Kimlik doğrulama iki adımlıdır:
1. `POST /api/auth/login` `{ email, password }` → `{ requiresCode: true, challenge: { challengeId, maskedEmail, expiresAt, resendAfterSeconds, codeLength } }`; kod e-postayla gider.
2. `POST /api/auth/login/verify` `{ challengeId, code }` → `accessToken` (15 dk, `Authorization: Bearer …`) + httpOnly `sd_rt` refresh çerezi (7 gün, rotasyonlu).

`POST /api/auth/login/resend` `{ challengeId }` yeni kod gönderir. SMTP tanımlı değilse (veya `Auth:LoginCode:Enabled=false`) 1. adım doğrudan `auth` döner.
Durum kodları: `200/201/204` başarı · `400` doğrulama · `401` oturum yok/süresi doldu · `403` yetki yok (`PASSWORD_CHANGE_REQUIRED` dahil) · `404` · `409` çakışma · `429` hız sınırı · `500`.

| Grup | Uç noktalar | Yetki |
|---|---|---|
| Kimlik | `POST /api/auth/login` · `POST /api/auth/login/verify` · `POST /api/auth/login/resend` · `POST /api/auth/refresh` · `POST /api/auth/logout` · `GET /api/auth/me` · `POST /api/auth/change-password` · `PUT /api/auth/profile` | login/refresh/logout anonim |
| Kullanıcılar | `GET/POST /api/users` · `GET/PUT /api/users/{id}` · `PUT /api/users/{id}/roles` · `POST /api/users/{id}/deactivate` · `/activate` · `/reset-password` | `users.manage` (roller: `roles.manage` gerekebilir) |
| Hesap talepleri | `GET/POST /api/account-requests` · `POST /api/account-requests/{id}/approve` · `/reject` | `accounts.request` / `accounts.approve` |
| Organizasyon | `GET/POST/PUT /api/org/departments` · `GET/POST/DELETE /api/org/titles` · `GET/POST/PUT/DELETE /api/org/groups` · `GET/POST/PUT/DELETE /api/org/roles` · `GET /api/org/permissions` | okuma: oturum · yazma: `org.manage`, `roles.manage` |
| Rehber | `GET /api/directory` · `GET /api/directory/{id}` · `GET /api/directory/birthdays` | oturum (yalnızca iş iletişim bilgileri) |
| Talepler | `GET/POST /api/tickets` · `GET/PUT /api/tickets/{id}` · `PATCH /api/tickets/{id}/status` · `PATCH /api/tickets/{id}/assign` · `POST /api/tickets/{id}/comments` · `POST /api/tickets/{id}/attachments` · `DELETE /api/tickets/{id}` | görünürlük kuralı; atama `tickets.manage`; silme `tickets.delete` |
| Talep ayarları | `GET/POST/PUT/DELETE /api/ticket-categories` · `GET/POST/PUT/DELETE /api/routing-rules` · `POST /api/routing-rules/test` | `tickets.configure` |
| Bilgi bankası | `GET/POST /api/kb` · `GET/PUT/DELETE /api/kb/{id}` · `POST /api/kb/{id}/helpful` | yazma `kb.manage` |
| Dosyalar | `POST /api/files` (multipart, ≤10 MB, tür + imza kontrolü) · `GET /api/files/{id}` (bağlı kayda erişim kontrolü) | oturum |
| Envanter | `GET/POST /api/assets` · `GET/PUT/DELETE /api/assets/{id}` · `POST /api/assets/{id}/checkout` · `/checkin` · `GET/POST/PUT /api/licenses` · `POST /api/licenses/{id}/install` | okuma: kendi zimmeti / `inventory.view` · yazma `inventory.manage` |
| Formlar | `GET /api/forms` · `GET /api/forms/{key}` · `POST/PUT /api/forms` · `POST /api/forms/{key}/submissions` | tasarım `forms.manage` (kendi birimi) |
| Başvurular | `GET /api/submissions?scope=mine|approvals|assigned|history|all` · `GET /api/submissions/{id}` · `POST …/approve` · `/reject` · `/complete` · `/cancel` · `/comments` | onaylayıcı / başvuran / sorumlu |
| Ciro / Vardiya | `GET /api/revenue/summary?days=30` · `GET /api/shifts?from=&to=` | ciro: rapor yetkisi / sorumlu birim → şirket geneli, diğerleri yalnızca kendi bildirimleri · vardiya: kendi departmanı, `leave.manage` tümü |
| İzin ve limit | `GET /api/leave/balance` · `GET /api/leave/calendar` · `GET/PUT /api/leave/balances` · `GET/PUT /api/limits` | `leave.manage`, `limits.manage` |
| İletişim | `/api/announcements` · `/api/banners` (+`PUT /order`) · `/api/events` (+`/attend`) · `/api/jobs` · `/api/achievements` (+`/like`) · `/api/surveys` (+`/responses`, `/results`) · `/api/documents` · `/api/messages` | yazma `content.manage`, `surveys.manage`, `documents.manage` |
| Operasyon | `/api/projects` · `/api/tasks` (+`PATCH /status`) · `/api/stores` · `/api/on-call` | `projects.manage`, `stores.manage`, `oncall.manage` |
| Gerçek zamanlı | SignalR hub `/hubs/notifications` (sunucu → istemci olayı: `notification`). WebSocket başlık taşıyamadığı için token yalnızca bu yolda `access_token` sorgu parametresiyle kabul edilir. | oturum (yalnızca kendi) |
| Bildirimler | `GET /api/notifications` · `GET /api/notifications/unread-count` · `POST /api/notifications/{id}/read` · `POST /api/notifications/read-all` | oturum (yalnızca kendi) |
| Panel / rapor | `GET /api/dashboard` · `GET /api/reports/tickets` (+`/export` CSV) · `/workflows` · `/inventory` · `/people` · `GET /api/apps` | rapor kapsamı yetkiye göre |
| Genel site | `GET /api/public/home` · `GET /api/public/market` · `GET /api/public/weather?city=` | anonim, IP başına 120 istek/dk |
| Sistem | `GET /api/system/health` · `GET/DELETE /api/system/cache` · `GET /api/audit-logs` · `GET /health/live` · `GET /health/ready` · `GET /api/health` | `system.manage`, `audit.view` |

Postman koleksiyonu: [`docs/postman/StajDesk.postman_collection.json`](postman/StajDesk.postman_collection.json) — `Login` isteği token'ı koleksiyon değişkenine otomatik yazar.
