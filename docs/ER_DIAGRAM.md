# ER Diyagramı

Migration: `backend/src/StajDesk.Infrastructure/Data/Migrations`. Diyagram okunabilirlik için üç bölüme ayrılmıştır.

## 1. Organizasyon, kimlik, denetim

```mermaid
erDiagram
    DEPARTMENTS ||--o{ USERS : "çalışır"
    DEPARTMENTS |o--o| USERS : "yöneticisi"
    DEPARTMENTS |o--o{ DEPARTMENTS : "üst birim"
    JOB_TITLES ||--o{ USERS : "unvan"
    USERS |o--o{ USERS : "yönetici"
    USERS ||--o{ USER_ROLES : ""
    ROLES ||--o{ USER_ROLES : ""
    GROUPS ||--o{ GROUP_MEMBERS : ""
    USERS ||--o{ GROUP_MEMBERS : ""
    USERS ||--o{ REFRESH_TOKENS : ""
    USERS ||--o{ ACCOUNT_REQUESTS : "talep eden (İK)"
    DEPARTMENTS ||--o{ ACCOUNT_REQUESTS : ""
    USERS ||--o{ NOTIFICATIONS : "alır"

    USERS { uuid id PK; string email UK; string employee_no UK; string password_hash; bool must_change_password; bool is_active; int department_id FK; int job_title_id FK; uuid manager_id FK }
    ROLES { int id PK; string code UK; text[] permissions; bool is_system }
    DEPARTMENTS { int id PK; string name UK; string type; uuid manager_id FK }
    REFRESH_TOKENS { bigint id PK; string token_hash UK; timestamp expires_at; timestamp revoked_at }
    AUDIT_LOGS { bigint id PK; uuid user_id; string action; string entity_type; string entity_id; jsonb changes; timestamp created_at }
    OUTBOX_MESSAGES { bigint id PK; uuid event_id; string routing_key; jsonb payload; timestamp published_at; int attempts }
    NOTIFICATIONS { bigint id PK; uuid user_id FK; string title; string kind; string link; bool is_read; uuid source_event_id }
```

## 2. Destek ve envanter (GLPI çekirdeği)

```mermaid
erDiagram
    TICKET_CATEGORIES ||--o{ TICKETS : ""
    GROUPS |o--o{ TICKET_CATEGORIES : "varsayılan grup"
    USERS ||--o{ TICKETS : "talep sahibi"
    USERS |o--o{ TICKETS : "atanan"
    GROUPS |o--o{ TICKETS : "atanan grup"
    DEPARTMENTS |o--o{ TICKETS : "talep anındaki departman"
    ASSETS |o--o{ TICKETS : "ilgili cihaz"
    TICKETS ||--o{ TICKET_COMMENTS : ""
    TICKETS ||--o{ TICKET_HISTORY : ""
    TICKETS ||--o{ TICKET_ATTACHMENTS : ""
    FILES ||--o{ TICKET_ATTACHMENTS : ""
    ROUTING_RULES }o--o| TICKET_CATEGORIES : "koşul"
    ROUTING_RULES }o--o| GROUPS : "eylem"
    TICKET_CATEGORIES |o--o{ KNOWLEDGE_ARTICLES : ""
    ASSETS ||--o{ ASSET_ASSIGNMENTS : "zimmet/iade"
    USERS ||--o{ ASSET_ASSIGNMENTS : ""
    USERS |o--o{ ASSETS : "zimmetli"
    SOFTWARE_LICENSES ||--o{ SOFTWARE_INSTALLATIONS : ""
    ASSETS ||--o{ SOFTWARE_INSTALLATIONS : ""

    TICKETS { int id PK; string title; string status; string priority; string type; timestamp due_at; timestamp resolved_at; int applied_rule_id }
    ASSETS { int id PK; string asset_tag UK; string type; string status; date warranty_ends_at; numeric purchase_cost }
    ASSET_ASSIGNMENTS { bigint id PK; timestamp assigned_at; timestamp returned_at; string return_condition }
```

## 3. Form, iş akışı ve iletişim

```mermaid
erDiagram
    FORM_DEFINITIONS ||--o{ FORM_SUBMISSIONS : ""
    DEPARTMENTS |o--o{ FORM_DEFINITIONS : "sorumlu birim"
    USERS ||--o{ FORM_SUBMISSIONS : "başvuran"
    FORM_SUBMISSIONS ||--o{ SUBMISSION_ACTIONS : "işlem geçmişi"
    FORM_SUBMISSIONS ||--o{ SUBMISSION_FILES : ""
    USERS ||--o{ LEAVE_BALANCES : ""
    USERS ||--o{ ANNOUNCEMENTS : "yazar"
    EVENTS ||--o{ EVENT_ATTENDEES : ""
    SURVEYS ||--o{ SURVEY_RESPONSES : ""
    USERS ||--o{ MESSAGES : "gönderen/alıcı"
    FILES ||--o{ DOCUMENTS : ""
    PROJECTS ||--o{ TASKS : ""
    DEPARTMENTS |o--o| STORES : ""
    USERS ||--o{ ON_CALL_SHIFTS : ""
    USERS ||--o{ ACHIEVEMENTS : ""

    FORM_DEFINITIONS { int id PK; string key UK; string module; string kind; jsonb fields; jsonb steps; text[] allowed_roles }
    FORM_SUBMISSIONS { int id PK; string number UK; jsonb data; string status; int current_step; uuid[] pending_approver_ids; numeric amount }
    SUBMISSION_ACTIONS { bigint id PK; int step_index; string action; string comment }
    LEAVE_BALANCES { int id PK; int year; numeric entitled; numeric carried_over; numeric used }
    SURVEY_RESPONSES { bigint id PK; string respondent_hash; jsonb answers }
```

## İndeks gerekçeleri

| İndeks | Neden |
|---|---|
| `tickets(status)`, `(requester_id)`, `(assignee_id)`, `(department_id)`, `(created_at)` | Liste filtreleri, görünürlük kuralları, tarih sıralaması ve raporlar |
| `users(email)` UK, `(employee_no)` UK | Giriş ve tekillik |
| `notifications(user_id, is_read, created_at)` | Bildirim zili / okunmamış sayısı (sık sorgu) |
| `notifications(source_event_id, user_id)` UK | Worker'ın idempotent çalışması |
| `form_submissions(submitter_id)`, `(status)` | "Başvurularım", onay kuyrukları |
| `refresh_tokens(token_hash)` UK | Token yenileme araması |
| `audit_logs(created_at)`, `(entity_type, entity_id)` | Denetim filtreleri |
| `outbox_messages(published_at)` | Yayınlanmamış olayları bulma |

Her sütuna indeks konmaz: indeksler yazma maliyetini ve disk kullanımını artırır; yalnızca sık filtrelenen/sıralanan sütunlar indekslenmiştir.
