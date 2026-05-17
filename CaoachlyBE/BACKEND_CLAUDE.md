# Backend — Sports Trainer Platform

## Project Overview

A web platform connecting sports clients with trainers. Clients find and book trainers; trainers manage their schedule and client base. The backend is an ASP.NET Core RESTful API following Clean Architecture principles.

---

## Tech Stack

| Technology | Purpose |
|---|---|
| ASP.NET Core | Web framework, API controllers, DI, SMTP |
| Entity Framework Core | ORM, migrations, LINQ queries |
| PostgreSQL | Primary relational database |
| AutoMapper | DTO ↔ domain model mapping |
| Stripe SDK | Payment processing and refunds |
| Azure Blob Storage SDK | File storage (avatars, documents) |
| Redis (StackExchange.Redis) | Caching search results |
| JWT | Authentication (access + refresh tokens) |
| Background Service (IHostedService) | Notification scheduler |

---

## Architecture: Clean Architecture (4 layers)

```
Domain      ← Entities, interfaces (no dependencies)
Application ← Business logic, services (depends on Domain only)
Infrastructure ← EF Core, Stripe, Azure, Redis, SMTP (implements Domain interfaces)
API (Presentation) ← Controllers, DTOs, AutoMapper (depends on Application)
```

**Dependency rule:** outer layers depend on inner layers, never the reverse.

- Domain defines `IRepository<T>`, `IUnitOfWork`, `IPaymentService`, `IStorageService`, etc.
- Infrastructure implements those interfaces using EF Core, Stripe SDK, Azure SDK, etc.
- Application layer services call domain interfaces only — they never reference EF Core or Stripe directly.
- API controllers call Application services; they receive/return DTOs.

---

## Patterns Used

### Repository + Unit of Work
- Each aggregate root has a repository: `IUserRepository`, `IBookingRepository`, `ISlotRepository`, etc.
- `IUnitOfWork` wraps multiple repository operations in a single transaction via `SaveChangesAsync()`.
- Used especially in booking creation and cancellation to ensure atomicity.

### DTO Pattern (via AutoMapper)
- All API inputs and outputs use DTOs, never domain entities directly.
- AutoMapper profiles defined per feature (e.g. `UserProfile`, `BookingProfile`).
- DTOs are defined in the Application layer; controllers live in API layer.

### Dependency Injection
- All services, repositories, and infrastructure components registered in `Program.cs` via DI container.
- Lifetimes: Repositories and services → `Scoped`; Redis client → `Singleton`; Background service → `Singleton`.

### Background Service
- `NotificationBackgroundService : BackgroundService` runs every **10 minutes**.
- Queries `bookings` joined with `schedule_slots` where `status = 1` (confirmed) and `start_time - NOW() ≈ reminder_minutes` (within ±1 minute window).
- Sends email via SMTP for each match.
- Creates a record in `notifications` table.
- Prevents re-sending automatically because after 10 minutes the time difference is outside the window.

---

## Database: PostgreSQL

All primary keys are `UUID` generated via `gen_random_uuid()`. Soft delete: set `is_active = false` on users; no hard deletes on `bookings` or `schedule_slots`.

### Tables (13 main entities + 2 junction tables + 2 technical tables)

**Core entities:**
- `users` — all roles (client=0, trainer=1, admin=2)
- `trainer_info` — extended trainer profile (1:1 with users)
- `client_info` — extended client profile (1:1 with users)
- `trainer_documents` — qualification files for admin verification
- `tags` — catalog (specialization=0, disability=1, methodology=2)
- `schedule_slots` — time slots created by trainers
- `bookings` — client bookings for slots
- `payments` — one payment per booking
- `reviews` — one review per booking (client → trainer)
- `session_notes` — notes by trainer or client per booking
- `notifications` — log of sent emails
- `achievements` — catalog of badges
- `support_tickets` — user support requests

**Junction tables (M:N):**
- `user_tags` — users ↔ tags (composite PK: user_id + tag_id)
- `user_achievements` — users ↔ achievements (composite PK: user_id + achievement_id)

**Technical tables:**
- `password_reset_tokens` — temporary password reset tokens
- `refresh_tokens` — hashed refresh tokens for JWT rotation

### Enumerations (stored as SMALLINT)

```csharp
// User
Role:   0=Client, 1=Trainer, 2=Admin
Gender: 0=Male,   1=Female,  2=Other

// Trainer
VerificationStatus: 0=NotVerified, 1=Pending, 2=Verified, 3=Rejected

// Schedule Slot
SlotFormat: 0=Online, 1=Offline
SlotStatus: 0=Available, 1=Booked, 2=SoldOut, 3=Cancelled

// Booking
BookingStatus:  0=Pending, 1=Confirmed, 2=Cancelled, 3=Completed
CancelledBy:    0=Client,  1=Trainer,   2=Admin

// Payment
PaymentMethod:  0=Online,  1=Cash
PaymentStatus:  0=Pending, 1=Paid, 2=Refunded, 3=Failed

// Documents
DocumentType:   0=Certificate, 1=Diploma, 2=License, 3=Other
DocumentStatus: 0=Pending, 1=Approved, 2=Rejected

// Tags
TagCategory: 0=Specialization, 1=Disability, 2=Methodology

// Notifications
NotificationType:
  0=BookingConfirmed, 1=BookingCancelled, 2=PaymentSuccess,
  3=PaymentRefunded,  4=VerificationApproved, 5=VerificationRejected,
  6=SessionReminder

// Support
TicketStatus: 0=Open, 1=InProgress, 2=Resolved, 3=Closed
```

### Triggers (PostgreSQL-level)

**Trigger 1: Recalculate trainer rating**
- Fires on: INSERT, UPDATE, DELETE in `reviews`
- Calls: `recalculate_trainer_rating(trainer_id)`
- Logic: `AVG(rating)` and `COUNT(*)` from `reviews` → update `trainer_info.rating` and `trainer_info.reviews_count`
- This means rating is always accurate regardless of how reviews are added/removed

**Trigger 2: Update slot status after booking change**
- Fires on: INSERT, UPDATE in `bookings`
- Calls: `update_slot_status(slot_id)`
- Logic:
  - Count active bookings for slot (`status != 2`)
  - If `max_clients = 1` and count >= 1 → `status = 1` (booked)
  - If `max_clients > 1` and count >= `max_clients` → `status = 2` (sold_out)
  - If count < `max_clients` → `status = 0` (available)

### Cascades
- Users: no hard delete, set `is_active = false`
- When a slot is deleted (soft): all related bookings → `status = 2`, `cancelled_by = 1`, `cancellation_reason = 'Session cancelled by trainer'`
- Bookings: no hard delete, only `status = 2`

---

## Authentication & Authorization

### JWT Access Token
- Short-lived (e.g. 15 minutes)
- Payload must include: `sub` (user id), `role`, `email`
- Validated on every request via `[Authorize]` + JWT middleware

### Refresh Token
- Stored hashed (SHA-256) in `refresh_tokens` table
- Longer-lived (e.g. 7 days)
- On refresh: validate token, issue new access + refresh tokens, invalidate old refresh token (rotation)
- On logout: delete refresh token from table

### Password Reset
- Generate random token → store in `password_reset_tokens` with expiry
- Send email with reset link (token in URL)
- On reset: validate token not expired and not used → update password → mark token as used (`used_at = NOW()`)

### Role-based Authorization
- `[Authorize(Roles = "Trainer")]` — trainer-only endpoints
- `[Authorize(Roles = "Admin")]` — admin-only endpoints
- `[Authorize]` — any authenticated user

---

## API Endpoints

### Auth — `/api/auth`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/register` | No | Register; body: email, password, firstName, lastName, role (0 or 1 only) |
| POST | `/login` | No | Login; returns access + refresh tokens |
| POST | `/refresh` | No | Refresh; body: refreshToken |
| POST | `/logout` | Yes | Delete refresh token from DB |
| POST | `/forgot-password` | No | Send reset email |
| POST | `/reset-password` | No | body: token, newPassword |

### Users — `/api/users`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/me` | Yes | Current user + role-specific profile |
| PUT | `/me` | Yes | Update base user fields |
| POST | `/me/avatar` | Yes | Upload avatar (multipart); store in Azure Blob; save URL |
| GET | `/me/tags` | Yes | Get user's current tags |
| POST | `/me/tags` | Yes | Replace all user tags; body: `{ tagIds: [1, 2, 3] }` |

### Trainers — `/api/trainers`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/` | No | Search + filter trainers (paginated) |
| GET | `/{id}` | No | Trainer public profile |
| GET | `/{id}/slots` | No | Trainer's available slots |
| GET | `/{id}/reviews` | No | Trainer's reviews (paginated) |
| PUT | `/me` | Trainer | Update trainer-specific fields (bio, experience) |
| GET | `/me/clients` | Trainer | Clients with confirmed/completed bookings with this trainer |

#### Search query parameters:
```
specialization (tag id, category=0)
methodology    (tag id, category=2)
disability     (tag id, category=1)
city           (string)
minRating      (decimal)
minPrice       (decimal)
maxPrice       (decimal)
sortBy         ("rating" | "price")
page           (int, 1-based)
pageSize       (int)
```

**Search algorithm:**
1. Base query: `users` WHERE `role = 1` AND `is_active = true`
2. JOIN `trainer_info` → filter by rating, verificationStatus, experienceYears
3. JOIN `schedule_slots` → filter trainers who have at least one `status = 0` slot
4. JOIN `user_tags` → filter by tag ids (specialization, methodology, disability)
5. Sort by selected criterion
6. Pass to pagination (LIMIT + OFFSET)
7. Cache result in Redis with key = hash of query params

### Tags — `/api/tags`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/` | No | All tags; optional `?category=0/1/2` |

### Schedule Slots — `/api/slots`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/` | Trainer | Create slot |
| PUT | `/{id}` | Trainer (owner) | Edit slot (only if no confirmed bookings) |
| DELETE | `/{id}` | Trainer (owner) | Cancel slot (triggers cascade booking cancellations) |
| GET | `/me` | Trainer | Own slots |

#### Slot creation body:
```json
{
  "startTime": "2025-06-01T10:00:00",
  "endTime": "2025-06-01T11:00:00",
  "format": 0,
  "price": 500.00,
  "maxClients": 1,
  "description": "...",
  "gymName": null,
  "gymAddress": null
}
```

### Bookings — `/api/bookings`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/` | Client | Create booking; returns Stripe checkout URL |
| GET | `/me` | Yes | Own bookings (paginated, filter by status) |
| GET | `/{id}` | Yes | Booking details (must be participant) |
| POST | `/{id}/cancel` | Yes | Cancel booking |

#### Booking creation flow:
1. Validate slot is `status = 0` (available)
2. Check no active booking conflict for this client at same time
3. Create booking with `status = 0` (pending)
4. Create Stripe Checkout Session via Stripe SDK
5. Store `stripe_session_id` reference if needed
6. Return `{ bookingId, stripeCheckoutUrl, status: 0 }`

#### Stripe Webhook — `/api/webhooks/stripe`
- Event `checkout.session.completed` → find booking by Stripe session id → set `status = 1` (confirmed), update payment `status = 1` (paid), set `paid_at`
- Trigger notification: `booking_confirmed` + `payment_success`
- Uses Unit of Work for atomic update

#### Cancellation algorithm:
```
1. Check booking status = 1 (confirmed)
2. Determine initiator (from JWT role vs booking data)
3. If canceller is trainer → refund = 100%
4. If canceller is client:
   - hours_until = slot.start_time - NOW()
   - If hours_until > 24h → refund = 100%
   - If hours_until <= 24h → refund = 50%
5. Call Stripe Refund API with calculated amount
6. Update: booking.status = 2, payment.status = 2, payment.refunded_at = NOW()
7. Record refund amount in payments table
8. Trigger notification: booking_cancelled + payment_refunded
```

### Payments — `/api/payments`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/{bookingId}` | Yes | Get payment for booking (must be participant) |

### Reviews — `/api/reviews`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/` | Client | Submit review; booking must have status=3 and no existing review |
| GET | `/trainer/{trainerId}` | No | Trainer reviews (paginated) |

### Session Notes — `/api/session-notes`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/` | Yes | Add note to booking (must be participant) |
| GET | `/booking/{bookingId}` | Yes | Get notes for booking |

Note: `is_private = true` notes are only returned to the author.

### Trainer Documents — `/api/trainer-documents`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/` | Trainer | Upload document; body: multipart (file + documentType) |
| GET | `/me` | Trainer | Own documents |
| DELETE | `/{id}` | Trainer | Delete own document (only if status=0 pending) |

Upload flow:
1. Validate file type (PDF, JPG, PNG) and max size
2. Upload to Azure Blob Storage
3. Save `file_url`, `file_name`, `file_size_bytes`, `document_type` in `trainer_documents`
4. Default `status = 0` (pending)

### Notifications — `/api/notifications`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/me` | Yes | Notification log for current user (paginated) |

### Achievements — `/api/achievements`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/` | No | Full achievements catalog |
| GET | `/me` | Client | User's earned achievements |

### Support Tickets — `/api/support-tickets`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/` | Yes | Create ticket |
| GET | `/me` | Yes | Own tickets |

### Admin — `/api/admin`
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/trainer-documents` | Admin | All pending documents |
| POST | `/trainer-documents/{id}/approve` | Admin | Approve → update status=1, set `reviewed_by`, `reviewed_at` |
| POST | `/trainer-documents/{id}/reject` | Admin | Reject → status=2, set `rejection_reason` |
| GET | `/support-tickets` | Admin | All tickets |
| PUT | `/support-tickets/{id}` | Admin | Update status, assign to admin |

On document approval → update `trainer_info.verification_status = 2` (if all required docs approved)
On document rejection → send notification type `5` (verification_rejected) with reason

---

## Algorithms

### 1. Search & Filter Trainers
- Input: filter params (optional)
- Build EF Core query dynamically (chain `.Where()` conditionally)
- Apply pagination (Skip + Take)
- Cache result in Redis: key = `trainers:search:{hash_of_params}`, TTL = configurable
- Invalidate cache when any trainer's profile, tags, or slots change

### 2. Booking
- Single UoW transaction covers: create booking + create Stripe session
- Stripe webhook is separate — updates status on async payment confirm
- Conflict check: no active booking (`status != 2`) for same client overlapping same time window

### 3. Refund Calculation
```csharp
decimal CalculateRefund(Booking booking, int cancelledBy)
{
    if (cancelledBy == 1) return booking.Payment.Amount; // trainer always full refund
    var hoursUntil = (booking.Slot.StartTime - DateTime.UtcNow).TotalHours;
    return hoursUntil > 24 ? booking.Payment.Amount : booking.Payment.Amount * 0.5m;
}
```

### 4. Rating Calculation
- Implemented as PostgreSQL trigger on `reviews` table
- Backend does NOT manually recalculate rating — it reads from `trainer_info.rating`
- `reviews_count` is also kept current by the same trigger

### 5. Notification Background Service
```
Every 10 minutes:
  SELECT b.*, s.start_time, s.trainer_id, b.client_id, b.reminder_minutes
  FROM bookings b JOIN schedule_slots s ON b.slot_id = s.id
  WHERE b.status = 1 -- confirmed
    AND ABS(EXTRACT(EPOCH FROM (s.start_time - NOW())) / 60 - b.reminder_minutes) < 1
  
  For each result:
    → send email to client (SMTP)
    → send email to trainer (SMTP)
    → INSERT into notifications
```

### 6. Achievement Awarding
Called after session completion (`booking.status` set to `3`):
```
Input: clientId
1. Count completed sessions → check "N sessions" achievements
2. Count distinct trainer_ids → check "trained with N trainers" achievements
3. Count distinct specialization tag_ids → check "tried N directions" achievements
4. For each achievement: if condition met AND not in user_achievements → INSERT + trigger notification
```

### 7. Pagination
```csharp
// Used on all list endpoints
var totalCount = await query.CountAsync();
var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
return new PagedResult<T>(items, totalCount, page, pageSize);
```

---

## External Services Integration

### Stripe
- SDK: official Stripe .NET SDK
- Create checkout session: `SessionService.CreateAsync(options)` with success/cancel URLs
- Refund: `RefundService.CreateAsync(options)` with amount and charge id
- Webhook: verify signature with `StripeClient.ConstructEvent()`; endpoint: `/api/webhooks/stripe`
- Store Stripe transaction id in `payments.transaction_id`

### Azure Blob Storage
- SDK: `Azure.Storage.Blobs`
- Two containers: `avatars`, `documents`
- Upload: `BlobContainerClient.UploadBlobAsync()`
- Return public URL saved to DB
- Validate file type (whitelist: jpg, jpeg, png, pdf) and size (e.g. max 10MB) before upload

### SMTP (Email)
- Built-in `System.Net.Mail.SmtpClient` or `MailKit`
- Used by: Background Service (reminders), booking/payment notification services
- All sent emails are logged to `notifications` table
- Configure host, port, credentials via `appsettings.json` / environment variables

### Redis
- Library: `StackExchange.Redis`
- Used for: caching trainer search results
- Key pattern: `trainers:search:{md5_of_query_params}`
- TTL: configurable (e.g. 5 minutes)
- Invalidate on: trainer profile update, slot create/delete, tag update

---

## Notification Events Reference

| Trigger | Notification Types Created |
|---|---|
| Booking confirmed (Stripe webhook) | `booking_confirmed` (client), `payment_success` (client) |
| Booking cancelled | `booking_cancelled` (client + trainer), `payment_refunded` (client, if applicable) |
| Slot deleted by trainer | `booking_cancelled` to all affected clients |
| Reminder time reached | `session_reminder` (client + trainer) |
| Document approved | `verification_approved` (trainer) |
| Document rejected | `verification_rejected` (trainer) |
| Achievement unlocked | (custom notification to client) |

---

## Security Considerations

- Never return `password_hash`, `token` fields in any response DTO
- Refresh tokens stored as SHA-256 hash in DB; never stored in plain text
- File uploads: validate MIME type server-side (don't trust Content-Type header alone)
- Stripe webhook: always verify signature before processing
- Admin endpoints: strict role check, not just `[Authorize]`
- Booking access check: user must be `client_id` or trainer of the slot — enforce in service layer, not just controller

---

## Configuration (appsettings.json structure)

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=...;Database=...;Username=...;Password=...",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "SecretKey": "...",
    "Issuer": "...",
    "Audience": "...",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "Stripe": {
    "SecretKey": "sk_...",
    "WebhookSecret": "whsec_...",
    "SuccessUrl": "https://frontend.com/booking/success",
    "CancelUrl": "https://frontend.com/booking/cancel"
  },
  "AzureBlob": {
    "ConnectionString": "DefaultEndpointsProtocol=https;...",
    "AvatarsContainer": "avatars",
    "DocumentsContainer": "documents"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "...",
    "Password": "...",
    "FromEmail": "noreply@platform.com"
  },
  "Notification": {
    "BackgroundServiceIntervalMinutes": 10
  },
  "Cache": {
    "TrainerSearchTtlSeconds": 300
  }
}
```

---

## Project Structure (suggested)

```
src/
├── Domain/
│   ├── Entities/          # User, Booking, Slot, etc.
│   ├── Enums/             # Role, BookingStatus, etc.
│   └── Interfaces/        # IRepository<T>, IUnitOfWork, IPaymentService, IStorageService
│
├── Application/
│   ├── DTOs/              # Request/Response DTOs per feature
│   ├── Mappings/          # AutoMapper profiles
│   └── Services/          # AuthService, BookingService, TrainerService, etc.
│
├── Infrastructure/
│   ├── Persistence/       # AppDbContext, EF Core configs, Repositories, UnitOfWork
│   ├── External/          # StripeService, AzureBlobService, SmtpEmailService
│   ├── BackgroundServices/# NotificationBackgroundService
│   └── Caching/           # RedisCacheService
│
└── API/
    ├── Controllers/       # AuthController, TrainersController, BookingsController, etc.
    ├── Webhooks/          # StripeWebhookController
    └── Program.cs         # DI registration, middleware pipeline
```
