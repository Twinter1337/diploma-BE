# Frontend — Sports Trainer Platform

## Project Overview

A web platform connecting sports clients with trainers. Clients find and book trainers; trainers manage their schedule and client base. Built as a **single-page application (SPA)** using React, communicating with an ASP.NET Core backend via RESTful API over HTTPS. Data is exchanged in JSON format.

---

## Tech Stack (Frontend)

- **Framework:** React (SPA)
- **API communication:** RESTful API (JSON over HTTPS)
- **Auth:** JWT access tokens + refresh tokens (stored securely, not in localStorage)
- **Payments:** Stripe (redirect-based checkout — frontend redirects user to Stripe-hosted page)
- **File uploads:** Sent to backend via multipart/form-data; backend handles Azure Blob Storage

---

## User Roles

| Role | Value | Description |
|---|---|---|
| Client | `0` | Searches trainers, books sessions, pays, leaves reviews |
| Trainer | `1` | Manages schedule slots, views clients, uploads documents |
| Admin | `2` | Verifies trainer documents, manages support tickets |

Role is returned in the JWT payload and in the user profile response.

---

## Authentication Flow

### Registration & Login
- `POST /api/auth/register` — register new user, returns access + refresh tokens
- `POST /api/auth/login` — login, returns access + refresh tokens
- `POST /api/auth/refresh` — get new access token using refresh token
- `POST /api/auth/logout` — invalidate refresh token

### Password Reset
1. `POST /api/auth/forgot-password` — send reset link to email
2. `POST /api/auth/reset-password` — submit new password with token from email

### Token handling
- Access token: short-lived JWT, include as `Authorization: Bearer <token>` header on all authenticated requests
- Refresh token: longer-lived, used to silently refresh access token when it expires
- On 401 response → attempt silent refresh → retry original request → if refresh fails → redirect to login

---

## API Base URL

All endpoints are prefixed with `/api`. Assume base URL is configured via environment variable (e.g. `VITE_API_URL`).

---

## Enumerations (SMALLINT values from backend)

### User Role
```
0 = client
1 = trainer
2 = admin
```

### Gender
```
0 = male
1 = female
2 = other
```

### Trainer Verification Status
```
0 = not_verified
1 = pending
2 = verified
3 = rejected
```

### Schedule Slot Format
```
0 = online
1 = offline
```

### Slot Status
```
0 = available
1 = booked (individual — max_clients = 1, already booked)
2 = sold_out (group — max_clients > 1, all spots taken)
3 = cancelled
```

### Booking Status
```
0 = pending (awaiting payment)
1 = confirmed (payment successful)
2 = cancelled
3 = completed
```

### Payment Method
```
0 = online (Stripe)
1 = cash
```

### Payment Status
```
0 = pending
1 = paid
2 = refunded
3 = failed
```

### Notification Type
```
0 = booking_confirmed
1 = booking_cancelled
2 = payment_success
3 = payment_refunded
4 = verification_approved
5 = verification_rejected
6 = session_reminder
```

### Document Type
```
0 = certificate
1 = diploma
2 = license
3 = other
```

### Document Status
```
0 = pending
1 = approved
2 = rejected
```

### Tag Category
```
0 = specialization
1 = disability
2 = methodology
```

### Support Ticket Status
```
0 = open
1 = in_progress
2 = resolved
3 = closed
```

### Cancelled By
```
0 = client
1 = trainer
2 = admin
```

---

## Key API Endpoints

### Auth
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | No | Register new user |
| POST | `/api/auth/login` | No | Login |
| POST | `/api/auth/refresh` | No | Refresh access token |
| POST | `/api/auth/logout` | Yes | Logout |
| POST | `/api/auth/forgot-password` | No | Request password reset email |
| POST | `/api/auth/reset-password` | No | Reset password with token |

### User Profile
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/users/me` | Yes | Get current user profile |
| PUT | `/api/users/me` | Yes | Update current user profile |
| POST | `/api/users/me/avatar` | Yes | Upload avatar (multipart/form-data) |

### Trainers (Client-facing search)
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/trainers` | No | Search & filter trainers (paginated) |
| GET | `/api/trainers/{id}` | No | Get trainer public profile |
| GET | `/api/trainers/{id}/slots` | No | Get trainer's available schedule slots |
| GET | `/api/trainers/{id}/reviews` | No | Get trainer's reviews (paginated) |

#### Trainer search query parameters:
```
specialization  — tag id (category=0)
methodology     — tag id (category=2)
disability      — tag id (category=1), or boolean flag
city            — string
minRating       — decimal (0.0–5.0)
minPrice        — decimal
maxPrice        — decimal
sortBy          — "rating" | "price"
page            — int (1-based)
pageSize        — int
```

#### Paginated response shape:
```json
{
  "items": [...],
  "totalCount": 42,
  "totalPages": 5,
  "page": 1,
  "pageSize": 10
}
```

### Trainer Profile (Trainer-facing management)
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| PUT | `/api/trainers/me` | Trainer | Update trainer-specific profile (bio, experience, etc.) |
| GET | `/api/trainers/me/clients` | Trainer | Get list of clients with confirmed/completed bookings |

### Tags
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/tags` | No | Get all tags (optionally filter by `?category=0/1/2`) |
| POST | `/api/users/me/tags` | Yes | Set tags for current user (replaces all existing) |
| GET | `/api/users/me/tags` | Yes | Get tags of current user |

### Schedule Slots (Trainer manages their slots)
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/slots` | Trainer | Create a new schedule slot |
| PUT | `/api/slots/{id}` | Trainer | Edit a slot |
| DELETE | `/api/slots/{id}` | Trainer | Cancel a slot (triggers booking cancellations) |
| GET | `/api/slots/me` | Trainer | Get own slots |

### Bookings
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/bookings` | Client | Create booking (returns Stripe checkout URL) |
| GET | `/api/bookings/me` | Yes | Get current user's bookings (paginated, filter by status) |
| GET | `/api/bookings/{id}` | Yes | Get booking details |
| POST | `/api/bookings/{id}/cancel` | Yes | Cancel a booking |

#### Booking creation request:
```json
{
  "slotId": "uuid",
  "reminderMinutes": 60
}
```

#### Booking creation response (pending payment):
```json
{
  "bookingId": "uuid",
  "stripeCheckoutUrl": "https://checkout.stripe.com/...",
  "status": 0
}
```

→ Frontend must redirect user to `stripeCheckoutUrl`. After payment, Stripe redirects back to a configured success/cancel URL.

### Payments
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/payments/{bookingId}` | Yes | Get payment details for a booking |

### Reviews
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/reviews` | Client | Submit a review (only for completed bookings) |
| GET | `/api/reviews/trainer/{trainerId}` | No | Get trainer reviews (paginated) |

#### Review creation request:
```json
{
  "bookingId": "uuid",
  "rating": 5,
  "comment": "Great trainer!"
}
```

### Session Notes
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/session-notes` | Yes | Add note to a booking |
| GET | `/api/session-notes/booking/{bookingId}` | Yes | Get notes for a booking |

### Trainer Documents (for trainers to upload for verification)
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/trainer-documents` | Trainer | Upload document (multipart/form-data) |
| GET | `/api/trainer-documents/me` | Trainer | Get own uploaded documents |
| DELETE | `/api/trainer-documents/{id}` | Trainer | Delete a document |

### Notifications
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/notifications/me` | Yes | Get notification history for current user |

### Achievements
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/achievements` | No | Get full achievements catalog |
| GET | `/api/achievements/me` | Yes (Client) | Get earned achievements of current user |

### Support Tickets
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/support-tickets` | Yes | Create support ticket |
| GET | `/api/support-tickets/me` | Yes | Get own tickets |

#### Ticket creation request:
```json
{
  "subject": "Payment issue",
  "description": "I was charged but booking is still pending",
  "relatedBookingId": "uuid (optional)"
}
```

### Admin Endpoints
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/admin/trainer-documents` | Admin | Get all pending documents |
| POST | `/api/admin/trainer-documents/{id}/approve` | Admin | Approve document |
| POST | `/api/admin/trainer-documents/{id}/reject` | Admin | Reject document with reason |
| GET | `/api/admin/support-tickets` | Admin | Get all support tickets |
| PUT | `/api/admin/support-tickets/{id}` | Admin | Update ticket status / assign |

---

## Pages & Screen Structure

### Public / Unauthenticated
- **Home / Trainer Search** — main landing page with search filters and trainer card list
- **Trainer Profile** — full trainer profile, available slots, reviews
- **Login / Register** — auth forms
- **Password Reset** — forgot password, reset password

### Client Dashboard
- **Upcoming Sessions** — confirmed bookings, cancel button, time until session
- **Session History** — completed/cancelled bookings, leave review button
- **Achievements** — earned badges
- **Profile Settings** — edit personal info, avatar, fitness goals, tags (disability/preference tags)

### Trainer Dashboard
- **Schedule** — calendar with own slots; create/edit/delete slots
- **Active Bookings** — upcoming confirmed bookings
- **Clients** — list of clients who have/had sessions with this trainer; click to see their physical info and disability tags
- **Statistics** — total sessions, average rating, review dynamics
- **Documents** — upload qualification documents; see verification status
- **Profile Settings** — edit bio, experience, city, tags (specialization/methodology/disability)

### Admin Panel
- **Document Review** — list of pending trainer documents; approve/reject
- **Support Tickets** — view and manage tickets

---

## Trainer Card (Search Result) — Expected Fields

```json
{
  "userId": "uuid",
  "firstName": "Ivan",
  "lastName": "Petrenko",
  "avatarUrl": "https://...",
  "city": "Kyiv",
  "bio": "...",
  "experienceYears": 5,
  "rating": 4.75,
  "reviewsCount": 23,
  "verificationStatus": 2,
  "tags": [
    { "id": 1, "name": "Yoga", "category": 0 },
    { "id": 5, "name": "Rehabilitation", "category": 1 }
  ],
  "minPrice": 400.00
}
```

---

## Stripe Payment Flow (Frontend Perspective)

1. Client selects a slot → opens booking confirmation page
2. Client submits booking → `POST /api/bookings`
3. Backend responds with `stripeCheckoutUrl`
4. **Frontend redirects the user** to `stripeCheckoutUrl` (Stripe-hosted page)
5. After payment Stripe redirects back to:
   - `?success=true` → show success page, fetch updated booking
   - `?canceled=true` → show canceled state
6. Booking status is updated on backend via Stripe Webhook (backend handles it, frontend just polls or reacts to URL)

> Note: Frontend does NOT handle card numbers or payment data directly. Everything goes through Stripe's hosted checkout.

---

## File Upload Notes

- Avatar: `POST /api/users/me/avatar` — multipart/form-data, field name: `file`
- Trainer documents: `POST /api/trainer-documents` — multipart/form-data, fields: `file` + `documentType` (SMALLINT)
- Backend validates file type and size; frontend should show validation errors from API response
- Returned URLs (avatarUrl, fileUrl) are direct links to Azure Blob Storage and can be used in `<img src>` directly

---

## Pagination

All list endpoints return:
```json
{
  "items": [...],
  "totalCount": 100,
  "totalPages": 10,
  "page": 2,
  "pageSize": 10
}
```

Query params: `?page=1&pageSize=10` (1-based page numbering)

---

## Error Handling

Backend returns standard HTTP status codes:
- `400` — validation error, body contains error details
- `401` — not authenticated → refresh token or redirect to login
- `403` — not authorized (wrong role)
- `404` — resource not found
- `409` — conflict (e.g. slot already booked, booking overlap)
- `500` — server error

Error body shape:
```json
{
  "message": "Human-readable error message",
  "errors": { "fieldName": ["Validation error"] }
}
```

---

## Business Rules Frontend Must Enforce / Display

1. **Booking cancellation refund policy** (display to user before cancelling):
   - Cancelled by client > 24h before session → 100% refund
   - Cancelled by client < 24h before session → 50% refund
   - Cancelled by trainer → 100% refund always

2. **Review can only be submitted** for bookings with status `3` (completed)

3. **Slot availability:**
   - `status = 0` → available (show "Book" button)
   - `status = 1` → booked (individual slot taken)
   - `status = 2` → sold out (group slot full)
   - `status = 3` → cancelled (don't show)

4. **Trainer verification badge:** show verification checkmark only when `verificationStatus = 2`

5. **Disability filter:** when client has disability tags set in their profile, they can filter trainers who have matching disability tags → those trainers have experience working with that disability type

6. **Online sessions** always require online payment (Stripe); offline sessions allow cash or online

7. **Session reminder:** client sets `reminderMinutes` (e.g. 30, 60, 120) at booking time; backend sends email at that time before session

---

## Notification Types (for notification list UI)

| Type | Value | Label |
|---|---|---|
| booking_confirmed | 0 | Booking confirmed |
| booking_cancelled | 1 | Booking cancelled |
| payment_success | 2 | Payment successful |
| payment_refunded | 3 | Payment refunded |
| verification_approved | 4 | Documents approved |
| verification_rejected | 5 | Documents rejected |
| session_reminder | 6 | Session reminder |

---

## Achievement System

Achievements are earned by clients based on activity:
- Completing N sessions
- Training with N different trainers
- Trying N different sport directions

The catalog is fetched from `GET /api/achievements`. Earned achievements are fetched from `GET /api/achievements/me` (includes `earned_at` timestamp).

---

## Data Model Summary (What Frontend Receives)

### User / Profile
```
id, email, role, firstName, lastName, avatarUrl, city, phone, birthDate, gender, isActive, createdAt
```

### Client extended info
```
heightCm, weightKg, fitnessGoals
```

### Trainer extended info
```
bio, experienceYears, verificationStatus, rating, reviewsCount
```

### Slot
```
id, trainerId, startTime, endTime, format, price, maxClients, description, gymName, gymAddress, status
```

### Booking
```
id, slotId, clientId, status, cancellationReason, cancelledBy, createdAt, updatedAt
+ nested: slot details, trainer details, payment details
```

### Review
```
id, bookingId, clientId, trainerId, rating, comment, createdAt
+ nested: client name + avatar
```

### Tag
```
id, name, category, description
```
