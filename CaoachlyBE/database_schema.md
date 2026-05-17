# Database Schema

## Table: `users`

Stores all platform users regardless of role (client, trainer, admin).

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| email | VARCHAR(255) | UNIQUE NOT NULL | User email address |
| password_hash | VARCHAR(255) | NOT NULL | Hashed password |
| role | SMALLINT | NOT NULL | User role: 0 = client, 1 = trainer, 2 = admin |
| first_name | VARCHAR(100) | NOT NULL | First name |
| last_name | VARCHAR(100) | NOT NULL | Last name |
| avatar_url | VARCHAR(500) | NULL | Link to avatar stored in Azure Blob Storage |
| city | VARCHAR(100) | NULL | User's city |
| phone | VARCHAR(20) | NULL | Phone number |
| birth_date | DATE | NULL | Date of birth |
| gender | SMALLINT | NULL | Gender: 0 = male, 1 = female, 2 = other |
| is_active | BOOLEAN | DEFAULT true | Whether the account is active (not banned) |
| created_at | TIMESTAMP | DEFAULT NOW() | Account creation timestamp |
| updated_at | TIMESTAMP | DEFAULT NOW() | Last update timestamp |

---

## Table: `password_reset_tokens`

Stores temporary tokens used for password recovery via email.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| user_id | UUID | FK → users.id NOT NULL | Reference to the user who requested the reset |
| token | VARCHAR(255) | UNIQUE NOT NULL | Randomly generated reset token |
| expires_at | TIMESTAMP | NOT NULL | Token expiration time |
| used_at | TIMESTAMP | NULL | Timestamp when the token was used (NULL if unused) |
| created_at | TIMESTAMP | DEFAULT NOW() | Token creation timestamp |

---

## Table: `trainer_info`

Extended profile for trainers. Created automatically when a user registers with the trainer role.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| user_id | UUID | FK → users.id UNIQUE NOT NULL | Reference to the base user |
| bio | TEXT | NULL | Trainer's description and activity overview |
| experience_years | SMALLINT | DEFAULT 0 | Years of experience |
| verification_status | SMALLINT | DEFAULT 0 | 0 = not_verified, 1 = pending, 2 = verified, 3 = rejected |
| rating | DECIMAL(3,2) | DEFAULT 0.00 | Cached average rating based on reviews |
| reviews_count | INT | DEFAULT 0 | Cached total number of reviews |

---

## Table: `client_info`

Extended profile for clients. Created automatically when a user registers with the client role.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| user_id | UUID | FK → users.id UNIQUE NOT NULL | Reference to the base user |
| height_cm | SMALLINT | NULL | Height in centimeters |
| weight_kg | DECIMAL(5,2) | NULL | Weight in kilograms |
| fitness_goals | TEXT | NULL | Client's fitness goals |

---

## Table: `trainer_documents`

Stores qualification documents uploaded by trainers for admin verification.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| trainer_id | UUID | FK → users.id NOT NULL | Reference to the trainer |
| file_url | VARCHAR(500) | NOT NULL | Link to file stored in Azure Blob Storage |
| file_name | VARCHAR(255) | NOT NULL | Original file name |
| file_size_bytes | INT | NOT NULL | File size in bytes |
| document_type | SMALLINT | NOT NULL | 0 = certificate, 1 = diploma, 2 = license, 3 = other |
| status | SMALLINT | DEFAULT 0 | 0 = pending, 1 = approved, 2 = rejected |
| rejection_reason | TEXT | NULL | Reason for rejection if status = 2 |
| reviewed_by | UUID | FK → users.id NULL | Admin who reviewed the document |
| reviewed_at | TIMESTAMP | NULL | Timestamp of review |
| uploaded_at | TIMESTAMP | DEFAULT NOW() | Upload timestamp |

---

## Table: `tags`

A catalog of tags used to describe trainer specializations, methodologies, and disability types.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | SERIAL | PK | Primary key |
| name | VARCHAR(100) | UNIQUE NOT NULL | Tag name (e.g. "Yoga", "Amputation", "Rehabilitation") |
| category | SMALLINT | NOT NULL | 0 = specialization, 1 = disability, 2 = methodology |
| description | TEXT | NULL | Short description of the tag |

---

## Table: `user_tags`

Links users (both trainers and clients) to tags.

| Column | Type | Constraints | Description |
|---|---|---|---|
| user_id | UUID | FK → users.id NOT NULL | Reference to the user |
| tag_id | INT | FK → tags.id NOT NULL | Reference to the tag |
| PK | (user_id, tag_id) | | Composite primary key |

---

## Table: `schedule_slots`

Stores available time slots created by trainers.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| trainer_id | UUID | FK → users.id NOT NULL | Reference to the trainer |
| start_time | TIMESTAMP | NOT NULL | Session start time |
| end_time | TIMESTAMP | NOT NULL | Session end time |
| format | SMALLINT | NOT NULL | 0 = online, 1 = offline |
| price | DECIMAL(10,2) | NOT NULL | Price for this specific session |
| max_clients | SMALLINT | DEFAULT 1 | Max clients (1 = individual, >1 = group) |
| description | TEXT | NULL | Short session description e.g. "Beginner friendly" |
| gym_name | VARCHAR(200) | NULL | Gym name, filled if format = 1 (offline) |
| gym_address | VARCHAR(300) | NULL | Gym address, filled if format = 1 (offline) |
| status | SMALLINT | DEFAULT 0 | 0 = available, 1 = booked (individual only), 2 = sold_out (group only, max_clients reached), 3 = cancelled |
| created_at | TIMESTAMP | DEFAULT NOW() | Creation timestamp |

---

## Table: `bookings`

Stores client bookings for trainer sessions.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| slot_id | UUID | FK → schedule_slots.id NOT NULL | Reference to the session slot |
| client_id | UUID | FK → users.id NOT NULL | Reference to the client |
| status | SMALLINT | DEFAULT 0 | 0 = pending, 1 = confirmed, 2 = cancelled, 3 = completed |
| cancellation_reason | TEXT | NULL | Reason for cancellation if status = 2 |
| cancelled_by | SMALLINT | NULL | Who cancelled: 0 = client, 1 = trainer, 2 = admin |
| created_at | TIMESTAMP | DEFAULT NOW() | Creation timestamp |
| updated_at | TIMESTAMP | DEFAULT NOW() | Last update timestamp |

---

## Table: `session_notes`

Stores notes written by either the trainer or client after a session.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| booking_id | UUID | FK → bookings.id NOT NULL | Reference to the booking |
| author_id | UUID | FK → users.id NOT NULL | User who wrote the note |
| content | TEXT | NOT NULL | Note content |
| is_private | BOOLEAN | DEFAULT false | If true, note is visible only to the author |
| created_at | TIMESTAMP | DEFAULT NOW() | Creation timestamp |
| updated_at | TIMESTAMP | DEFAULT NOW() | Last update timestamp |

---

## Table: `payments`

Stores payment information for bookings. Business rule: if session format = 0 (online) then payment_method must be 0 (online). If session format = 1 (offline), any payment method is allowed.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| booking_id | UUID | FK → bookings.id UNIQUE NOT NULL | Reference to the booking |
| amount | DECIMAL(10,2) | NOT NULL | Payment amount |
| currency | VARCHAR(3) | DEFAULT 'UAH' | Currency code |
| payment_method | SMALLINT | NOT NULL | 0 = online, 1 = cash |
| status | SMALLINT | DEFAULT 0 | 0 = pending, 1 = paid, 2 = refunded, 3 = failed |
| transaction_id | VARCHAR(255) | NULL | Stripe transaction ID, NULL if cash payment |
| paid_at | TIMESTAMP | NULL | Timestamp when payment was completed |
| refunded_at | TIMESTAMP | NULL | Timestamp when payment was refunded |
| created_at | TIMESTAMP | DEFAULT NOW() | Creation timestamp |

---

## Table: `reviews`

Stores client reviews for trainers. Each booking can have only one review. Reviews are displayed on the trainer's profile.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| booking_id | UUID | FK → bookings.id UNIQUE NOT NULL | Ensures one review per booking |
| client_id | UUID | FK → users.id NOT NULL | Reference to the client |
| trainer_id | UUID | FK → users.id NOT NULL | Reference to the trainer (for efficient querying) |
| rating | SMALLINT | NOT NULL, CHECK(rating BETWEEN 1 AND 5) | Rating from 1 to 5 |
| comment | TEXT | NULL | Optional review text |
| created_at | TIMESTAMP | DEFAULT NOW() | Creation timestamp |

---

## Table: `notifications`

Stores a log of email notifications sent to users.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| user_id | UUID | FK → users.id NOT NULL | Reference to the user |
| type | SMALLINT | NOT NULL | 0 = booking_confirmed, 1 = booking_cancelled, 2 = payment_success, 3 = payment_refunded, 4 = verification_approved, 5 = verification_rejected, 6 = session_reminder |
| title | VARCHAR(255) | NOT NULL | Email subject |
| body | TEXT | NOT NULL | Email body |
| sent_at | TIMESTAMP | DEFAULT NOW() | Timestamp when the email was sent |

---

## Table: `achievements`

A catalog of badges that users can earn for activity on the platform.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | SERIAL | PK | Primary key |
| type | SMALLINT | UNIQUE NOT NULL | Badge type, enum handled on the backend |
| title | VARCHAR(255) | NOT NULL | Badge title |
| description | TEXT | NOT NULL | Condition for earning the badge |
| icon_url | VARCHAR(500) | NOT NULL | Badge icon |

---

## Table: `user_achievements`

Stores badges earned by users.

| Column | Type | Constraints | Description |
|---|---|---|---|
| user_id | UUID | FK → users.id NOT NULL | Reference to the user |
| achievement_id | INT | FK → achievements.id NOT NULL | Reference to the achievement |
| earned_at | TIMESTAMP | DEFAULT NOW() | Timestamp when the badge was earned |
| PK | (user_id, achievement_id) | | Composite primary key, prevents duplicate badges |

---

## Table: `support_tickets`

Stores user support requests, optionally linked to a specific booking.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| created_by | UUID | FK → users.id NOT NULL | User who created the ticket |
| subject | VARCHAR(255) | NOT NULL | Ticket subject |
| description | TEXT | NOT NULL | Problem description |
| status | SMALLINT | DEFAULT 0 | 0 = open, 1 = in_progress, 2 = resolved, 3 = closed |
| related_booking_id | UUID | FK → bookings.id NULL | Reference to a booking if ticket is about a session |
| assigned_to | UUID | FK → users.id NULL | Admin assigned to the ticket |
| created_at | TIMESTAMP | DEFAULT NOW() | Creation timestamp |
| updated_at | TIMESTAMP | DEFAULT NOW() | Last update timestamp |

---

## Table: `refresh_tokens`

Stores refresh tokens for JWT authentication. Used to issue new access tokens and to invalidate sessions on logout.

| Column | Type | Constraints | Description |
|---|---|---|---|
| id | UUID | PK, DEFAULT gen_random_uuid() | Primary key |
| user_id | UUID | FK → users.id NOT NULL | Reference to the user |
| token | VARCHAR(500) | UNIQUE NOT NULL | Hashed refresh token (SHA256) |
| expires_at | TIMESTAMP | NOT NULL | Token expiration time |
| created_at | TIMESTAMP | DEFAULT NOW() | Creation timestamp |

---

# Cascades & Triggers

## Cascades

### `users`
No hard delete. When a user is removed, only `is_active` is set to `false`. All related data remains intact.

### `schedule_slots`
No hard delete. When a trainer deletes a slot, all related `bookings` are automatically updated:
- `status` = 2 (cancelled)
- `cancelled_by` = 1 (trainer)
- `cancellation_reason` = 'Session cancelled by trainer'

### `bookings`
No hard delete. Bookings are only cancelled by setting `status` = 2 (cancelled).

---

## Triggers & Stored Procedures

### Trigger 1 — Recalculate trainer rating

**Fires on:** INSERT, UPDATE, DELETE in `reviews`

**Calls procedure:** `recalculate_trainer_rating(trainer_id)`

**Procedure logic:**
1. Calculate `AVG(rating)` across all reviews for the trainer
2. Calculate `COUNT(*)` of all reviews for the trainer
3. Update `rating` and `reviews_count` in `trainer_info`

---

### Trigger 2 — Update slot status after booking

**Fires on:** INSERT, UPDATE in `bookings`

**Calls procedure:** `update_slot_status(slot_id)`

**Procedure logic:**
1. Count active bookings for the slot (`status` != 2)
2. Get `max_clients` for the slot
3. If `max_clients` = 1 and count >= 1 → set slot `status` = 1 (booked)
4. If `max_clients` > 1 and count >= `max_clients` → set slot `status` = 2 (sold_out)
5. If count < `max_clients` → set slot `status` = 0 (available)
