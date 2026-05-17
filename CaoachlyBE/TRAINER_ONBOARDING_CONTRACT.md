# Trainer Onboarding — Frontend Contract

## Overview

After a trainer registers, they are redirected to a 4-step onboarding flow before accessing the dashboard.

| Step | Name | Endpoint |
|---|---|---|
| 1 | About yourself | `PATCH /api/trainers/{id}` |
| 2 | Professional info | `PATCH /api/trainers/{id}` |
| 3 | Qualification documents *(optional)* | `POST /api/trainers/{id}/documents` |
| 4 | Schedule slots | `POST /api/trainers/{id}/slots` (one call per slot) |

Steps 1 and 2 hit the same PATCH endpoint as **partial updates** — send only the fields for the current step. Fields that are `null` or omitted are ignored by the backend.

Steps 3 and 4 are dedicated endpoints. Step 3 is **optional** — the user can skip it and go straight to Step 4.

---

## Prerequisites

The trainer must be **registered and authenticated** before entering onboarding.
After `POST /api/auth/register` (with `role: 1`) the response contains:

```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<token>",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "trainer@example.com",
    "role": 1,
    "firstName": "Ivan",
    "lastName": "Petrenko"
  }
}
```

Store `user.id` — you will use it as `{id}` in every call.

All requests require:
```
Authorization: Bearer <accessToken>
```

The backend returns **403 Forbidden** if the token's user ID does not match `{id}`.

---

## Step 1 — "About Yourself"

### What the user fills in

| UI Field | JSON field | Type | Notes |
|---|---|---|---|
| Avatar photo | `avatarUrl` | `string` | Mock — plain URL string, no real upload yet |
| City | `city` | `string` | Max 100 chars |
| Gender | `gender` | `integer` | 0 = Male, 1 = Female, 2 = Other |
| Date of birth | `birthDate` | `string` | ISO 8601: `"YYYY-MM-DD"` |
| Bio / About | `bio` | `string` | Free text, trainer's self-description |

### Request

```
PATCH /api/trainers/{id}
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "avatarUrl": "https://example.com/avatar.jpg",
  "city": "Kyiv",
  "gender": 0,
  "birthDate": "1990-06-15",
  "bio": "Certified strength coach with 8 years of experience"
}
```

Fields not relevant to this step must be **omitted or set to null**.

### Response `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "trainer@example.com",
  "firstName": "Ivan",
  "lastName": "Petrenko",
  "avatarUrl": "https://example.com/avatar.jpg",
  "city": "Kyiv",
  "gender": 0,
  "birthDate": "1990-06-15",
  "bio": "Certified strength coach with 8 years of experience",
  "experienceYears": 0,
  "verificationStatus": 0,
  "rating": 0.0,
  "reviewsCount": 0,
  "specializationTags": [],
  "methodologyTags": [],
  "accessTags": []
}
```

After a successful response → advance to Step 2.

---

## Step 2 — "Professional Info"

### Tag pickers — load catalogs first

Before rendering Step 2, fetch all three tag catalogs in parallel:

```
GET /api/tags?category=0   → specialization tags
GET /api/tags?category=2   → methodology tags
GET /api/tags?category=1   → disability / accessibility tags
```

No auth required.

**Response `200 OK` (same shape for all three):**
```json
[
  { "id": 1, "name": "Yoga" },
  { "id": 2, "name": "Boxing" },
  { "id": 3, "name": "Rehabilitation" }
]
```

### What the user fills in

| UI Field | JSON field | Type | Notes |
|---|---|---|---|
| Years of experience | `experienceYears` | `integer` (0–100) | |
| Specializations | `specializationTagIds` | `integer[]` | IDs from `GET /api/tags?category=0` |
| Methodologies | `methodologyTagIds` | `integer[]` | IDs from `GET /api/tags?category=2` |
| Works with people with disabilities | `hasAccess` | `boolean` | Toggle/checkbox |
| Disability tags *(shown if hasAccess = true)* | `accessTagIds` | `integer[]` | IDs from `GET /api/tags?category=1` |

### Request

```
PATCH /api/trainers/{id}
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "experienceYears": 8,
  "specializationTagIds": [1, 2],
  "methodologyTagIds": [5, 6],
  "hasAccess": true,
  "accessTagIds": [1, 3]
}
```

### `hasAccess` + `accessTagIds` logic

| Sent values | Backend behaviour |
|---|---|
| `"hasAccess": true, "accessTagIds": [1, 3]` | Replace disability tags with IDs 1 and 3 |
| `"hasAccess": false` | Clear all disability tags (regardless of `accessTagIds`) |
| `"hasAccess": null` (or omitted) | Leave existing disability tags unchanged |
| `"hasAccess": true, "accessTagIds": []` | No change (empty + true is treated as no-op) |

### Request — trainer does not work with disabilities

```json
{
  "experienceYears": 8,
  "specializationTagIds": [1, 2],
  "methodologyTagIds": [5, 6],
  "hasAccess": false
}
```

### Response `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "trainer@example.com",
  "firstName": "Ivan",
  "lastName": "Petrenko",
  "avatarUrl": "https://example.com/avatar.jpg",
  "city": "Kyiv",
  "gender": 0,
  "birthDate": "1990-06-15",
  "bio": "Certified strength coach with 8 years of experience",
  "experienceYears": 8,
  "verificationStatus": 0,
  "rating": 0.0,
  "reviewsCount": 0,
  "specializationTags": [
    { "id": 1, "name": "Yoga" },
    { "id": 2, "name": "Boxing" }
  ],
  "methodologyTags": [
    { "id": 5, "name": "Functional training" },
    { "id": 6, "name": "CrossFit" }
  ],
  "accessTags": [
    { "id": 1, "name": "Visual impairment" },
    { "id": 3, "name": "Hearing impairment" }
  ]
}
```

After a successful response → advance to Step 3 (or skip to Step 4).

---

## Step 3 — "Qualification Documents" *(optional)*

This step is **optional**. If the trainer skips it, navigate directly to Step 4 without making any request.

The trainer can upload one document per call. To upload multiple documents, call the endpoint once per file.

### Request

```
POST /api/trainers/{id}/documents
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "fileName": "sports_diploma.pdf",
  "fileSizeBytes": 1200000,
  "documentType": 0
}
```

**`documentType` values:**

| Value | Meaning |
|---|---|
| `0` | Certificate |
| `1` | Diploma |
| `2` | License |
| `3` | Other |

> **Note:** This endpoint is currently a **mock** — no file is stored. `fileName` is echoed back and `fileUrl` is a placeholder. Real file upload will be wired in a later release.

### Response `201 Created`

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "fileName": "sports_diploma.pdf",
  "fileSizeBytes": 1200000,
  "documentType": 0,
  "fileUrl": "mock://placeholder"
}
```

After uploading documents (or skipping) → advance to Step 4.

---

## Step 4 — "Schedule Slots"

The trainer creates their initial availability. **Each slot is saved individually** — the user fills in the slot form, clicks Save, and the frontend immediately calls `POST /api/trainers/{id}/slots`. The same endpoint is reused on the trainer's schedule management page after onboarding.

### Flow

```
User clicks "Add slot"
      │
      ▼
Slot creation form opens
      │
      ▼
User fills in fields and clicks "Save"
      │
      │  POST /api/trainers/{id}/slots
      │
      ▼
201 Created → slot appears in the list
      │
      ▼
User adds more slots or clicks "Finish"
      │
      ▼
Redirect to trainer dashboard
```

### What the user fills in (per slot)

| UI Field | JSON field | Type | Notes |
|---|---|---|---|
| Start time | `startTime` | `string` | ISO 8601 datetime: `"2026-05-10T10:00:00"` |
| End time | `endTime` | `string` | ISO 8601 datetime: `"2026-05-10T11:00:00"` |
| Format | `format` | `integer` | 0 = Online, 1 = Offline |
| Price per session | `pricePerSession` | `decimal` | |
| Max clients | `maxClients` | `integer` (1–100) | Defaults to 1 |

### Request

```
POST /api/trainers/{id}/slots
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "startTime": "2026-05-10T10:00:00",
  "endTime": "2026-05-10T11:00:00",
  "format": 1,
  "pricePerSession": 600.00,
  "maxClients": 1
}
```

### Response `201 Created`

```json
{
  "id": "a1b2c3d4-0000-0000-0000-000000000001",
  "trainerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "startTime": "2026-05-10T10:00:00",
  "endTime": "2026-05-10T11:00:00",
  "format": 1,
  "price": 600.00,
  "maxClients": 1,
  "description": null,
  "gymName": null,
  "gymAddress": null,
  "status": 0,
  "createdAt": "2026-05-05T12:34:56"
}
```

`status: 0` means `Available`. It is set automatically by the backend.

After the trainer has added all desired slots and clicks "Finish" → redirect to trainer dashboard.

---

## Tags Endpoint Reference

### `GET /api/tags?category={n}`

| `category` | Meaning | Used in trainer onboarding |
|---|---|---|
| `0` | Specialization (e.g. Yoga, Boxing, Rehab) | Step 2 |
| `1` | Disability / accessibility | Step 2 (`hasAccess` flow) |
| `2` | Methodology (e.g. Functional, CrossFit) | Step 2 |

No auth required.

**Missing or invalid category → `400 Bad Request`:**
```json
{ "message": "category is required and must be 0 (Specialization), 1 (Disability), or 2 (Methodology)." }
```

---

## Full `PATCH /api/trainers/{id}` Reference

### Request body (all fields optional)

```typescript
{
  // Step 1
  avatarUrl?:            string | null,          // max 500 chars
  city?:                 string | null,           // max 100 chars
  gender?:               0 | 1 | 2 | null,       // 0=Male 1=Female 2=Other
  birthDate?:            string | null,           // "YYYY-MM-DD"
  bio?:                  string | null,           // free text

  // Step 2
  experienceYears?:      number | null,           // integer, 0–100
  specializationTagIds?: number[] | null,         // category=0 tag IDs; null = no change
  methodologyTagIds?:    number[] | null,         // category=2 tag IDs; null = no change
  hasAccess?:            boolean | null,          // controls disability tag behaviour
  accessTagIds?:         number[] | null,         // category=1 tag IDs; used when hasAccess=true
}
```

### Response body `200 OK`

```typescript
{
  id:                 string,              // UUID
  email:              string,
  firstName:          string,
  lastName:           string,
  avatarUrl:          string | null,
  city:               string | null,
  gender:             0 | 1 | 2 | null,
  birthDate:          string | null,       // "YYYY-MM-DD"
  bio:                string | null,
  experienceYears:    number,              // integer
  verificationStatus: 0 | 1 | 2 | 3,     // 0=NotVerified 1=Pending 2=Verified 3=Rejected
  rating:             number,             // decimal
  reviewsCount:       number,             // integer
  specializationTags: Array<{ id: number, name: string }>,
  methodologyTags:    Array<{ id: number, name: string }>,
  accessTags:         Array<{ id: number, name: string }>
}
```

### Error responses

| Status | When | Body |
|---|---|---|
| `400` | Validation failed (range, max length) | `{ "errors": { "field": ["message"] } }` |
| `401` | No or invalid JWT | — |
| `403` | JWT user ID ≠ route `{id}` | `{ "message": "Forbidden." }` |
| `404` | User not found or not a trainer role | `{ "message": "Trainer not found." }` |

---

## Full `POST /api/trainers/{id}/slots` Reference

### Request body

```typescript
{
  startTime:       string,       // ISO 8601 datetime, e.g. "2026-05-10T10:00:00"
  endTime:         string,       // ISO 8601 datetime
  format:          0 | 1,        // 0=Online, 1=Offline
  pricePerSession: number,       // decimal
  maxClients:      number,       // integer, 1–100, default 1
}
```

### Response body `201 Created`

```typescript
{
  id:          string,       // UUID of the created slot
  trainerId:   string,       // UUID
  startTime:   string,       // ISO 8601 datetime
  endTime:     string,       // ISO 8601 datetime
  format:      0 | 1,        // 0=Online, 1=Offline
  price:       number,       // decimal (same value as pricePerSession)
  maxClients:  number,
  description: string | null,
  gymName:     string | null,
  gymAddress:  string | null,
  status:      0,            // always 0 (Available) on creation
  createdAt:   string        // ISO 8601 datetime
}
```

### Error responses

| Status | When | Body |
|---|---|---|
| `400` | Validation failed | `{ "errors": { "field": ["message"] } }` |
| `401` | No or invalid JWT | — |
| `403` | JWT user ID ≠ route `{id}` | `{ "message": "Forbidden." }` |
| `404` | User not found or not a trainer role | `{ "message": "Trainer not found." }` |

---

## Full `POST /api/trainers/{id}/documents` Reference

### Request body

```typescript
{
  fileName:      string,   // max 255 chars, required
  fileSizeBytes: number,   // integer, 1–10485760 (10 MB)
  documentType:  0 | 1 | 2 | 3  // 0=Certificate 1=Diploma 2=License 3=Other
}
```

### Response body `201 Created`

```typescript
{
  id:            string,  // UUID (currently always "00000000-0000-0000-0000-000000000000")
  fileName:      string,
  fileSizeBytes: number,
  documentType:  0 | 1 | 2 | 3,
  fileUrl:       string   // currently always "mock://placeholder"
}
```

### Error responses

| Status | When | Body |
|---|---|---|
| `401` | No or invalid JWT | — |
| `403` | JWT user ID ≠ route `{id}` | `{ "message": "Forbidden." }` |
| `404` | User not found or not a trainer role | `{ "message": "Trainer not found." }` |

---

## Complete Onboarding Flow Diagram

```
POST /api/auth/register  (role: 1)
              │
              ▼
    Store user.id + tokens
              │
              ▼
┌─────────────────────────────────────────────────┐
│  STEP 1: About Yourself                          │
│  avatarUrl, city, gender, birthDate, bio         │
└─────────────────────────────────────────────────┘
              │
              │  PATCH /api/trainers/{id}
              │
              ▼
          200 OK → advance
              │
              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Pre-load (parallel):                                                │
│  GET /api/tags?category=0  (specialization)                         │
│  GET /api/tags?category=2  (methodology)                            │
│  GET /api/tags?category=1  (disability)                             │
└─────────────────────────────────────────────────────────────────────┘
              │
              ▼
┌──────────────────────────────────────────────────────────────────────┐
│  STEP 2: Professional Info                                            │
│  experienceYears, specializationTagIds,                               │
│  methodologyTagIds, hasAccess, accessTagIds                           │
└──────────────────────────────────────────────────────────────────────┘
              │
              │  PATCH /api/trainers/{id}
              │
              ▼
          200 OK → advance
              │
              ▼
┌────────────────────────────────────────────────────────┐
│  STEP 3: Qualification Documents  ── optional ──        │
│  One POST per file; "Skip" skips entirely               │
└────────────────────────────────────────────────────────┘
              │
              │  POST /api/trainers/{id}/documents  (0 or more times)
              │
              ▼
        201 Created (or skipped) → advance
              │
              ▼
┌────────────────────────────────────────────────────────────────────┐
│  STEP 4: Schedule Slots                                              │
│  User clicks "Add slot" → fills form → clicks "Save"                │
│  Repeat for each slot; click "Finish" when done                     │
└────────────────────────────────────────────────────────────────────┘
              │
              │  POST /api/trainers/{id}/slots  (once per slot)
              │
              ▼
          201 Created per slot → "Finish" → redirect to dashboard
```

---

## Notes for Frontend

1. **Steps 1 and 2 hit the same PATCH endpoint.** The split into "steps" is purely a UI concept — the backend treats each call as a partial update to the same trainer record.

2. **Steps are independent and resumable.** If the trainer closes the browser mid-onboarding, their progress persists on the server. Store the current step index in session/local state and resume from where they left off.

3. **Step 3 is optional.** Render a "Skip" button. No request is needed when skipping — just navigate to Step 4.

4. **Step 4 sends one request per slot.** Each "Save" in the slot form triggers a single `POST /api/trainers/{id}/slots`. Display the created slot in the list immediately using the `201` response body. "Finish" requires no additional request — just redirect.

5. **The slots endpoint is reusable.** `POST /api/trainers/{id}/slots` is the same endpoint used on the trainer's schedule management page. No need to build a separate API call for post-onboarding slot creation.

6. **`gender` is a number, not a string.** Send `0`, `1`, or `2`. Do not send `"male"`.

7. **`birthDate` is a date string.** Send `"1990-06-15"` (ISO 8601 date, no time component).

8. **Slot `format` values: `0 = Online`, `1 = Offline`.**

9. **Tag ID arrays use replace semantics.** Sending `specializationTagIds: [1, 2]` replaces all existing specialization tags. Sending `null` or omitting the field leaves existing tags untouched. Sending `[]` clears all tags of that category.

10. **`hasAccess: false` clears disability tags** regardless of `accessTagIds`. Only send `accessTagIds` when `hasAccess` is `true`.

11. **`verificationStatus` in the response will be `0` (NotVerified)** throughout onboarding. It changes to `1` (Pending) when an admin starts reviewing uploaded documents.

12. **`avatarUrl` is currently a plain string.** Pass any URL string. Real file upload is not wired yet.
