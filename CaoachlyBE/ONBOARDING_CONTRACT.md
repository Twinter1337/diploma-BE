# Client Onboarding — Frontend Contract

## Overview

After a client registers, they are redirected to an onboarding flow before accessing the main app.
The flow has **2 steps**, each making a `PATCH /api/clients/{id}` request.
There is also a tag-picker endpoint used in Step 2.

All `PATCH` calls are **partial updates** — only send the fields for the current step.
Fields that are `null` or omitted are ignored by the backend.

---

## Prerequisites

The client must be **registered and authenticated** before entering onboarding.
After `POST /api/auth/register` the response contains:

```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<token>",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "role": 0,
    "firstName": "Anna",
    "lastName": "Kovalenko",
    ...
  }
}
```

Store `user.id` — you will use it as `{id}` in every PATCH call.

All requests to `/api/clients/{id}` require:
```
Authorization: Bearer <accessToken>
```

The backend will return **403 Forbidden** if the token's user ID does not match `{id}`.

---

## Step 1 — "About Yourself"

### What the user fills in

| UI Field | JSON field | Type | Notes |
|---|---|---|---|
| Avatar photo | `avatarUrl` | `string` | Mock — plain URL string, no real upload yet |
| City | `city` | `string` | Max 100 chars |
| About / Fitness goals | `about` | `string` | Free text |

### Request

```
PATCH /api/clients/{id}
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "avatarUrl": "https://example.com/avatar.jpg",
  "city": "Kyiv",
  "about": "I want to lose weight and improve flexibility"
}
```

Fields not relevant to this step (`heightCm`, `weightKg`, `gender`, `birthDate`, `accessTagIds`) must be **omitted or set to null** — they will be ignored.

### Response `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "firstName": "Anna",
  "lastName": "Kovalenko",
  "avatarUrl": "https://example.com/avatar.jpg",
  "city": "Kyiv",
  "gender": null,
  "birthDate": null,
  "about": "I want to lose weight and improve flexibility",
  "heightCm": null,
  "weightKg": null,
  "accessTags": []
}
```

After a successful response → advance to Step 2.

---

## Step 2 — "Parameters"

### Tag picker — load disability tags first

Before rendering Step 2, fetch the disability tags catalog:

```
GET /api/tags?category=1
```

No auth required.

**Response `200 OK`:**
```json
[
  { "id": 1, "name": "Visual impairment" },
  { "id": 2, "name": "Mobility limitation" },
  { "id": 3, "name": "Hearing impairment" }
]
```

Render these as a multi-select chip/checkbox list under "Accessibility needs".

### What the user fills in

| UI Field | JSON field | Type | Notes |
|---|---|---|---|
| Height | `heightCm` | `integer` (1–32767) | In centimetres |
| Weight | `weightKg` | `decimal` (0.01–999.99) | In kilograms |
| Gender | `gender` | `integer` | 0 = Male, 1 = Female, 2 = Other |
| Date of birth | `birthDate` | `string` | ISO 8601: `"YYYY-MM-DD"` |
| Accessibility tags | `accessTagIds` | `integer[]` | IDs from `GET /api/tags?category=1` |

### Request

```
PATCH /api/clients/{id}
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "heightCm": 168,
  "weightKg": 62.5,
  "gender": 1,
  "birthDate": "1995-04-20",
  "accessTagIds": [1, 3]
}
```

Fields not relevant to this step (`avatarUrl`, `city`, `about`) must be **omitted or set to null**.

### Request — user selects no accessibility tags

Send an **empty array** to explicitly clear all disability tags:

```json
{
  "heightCm": 168,
  "weightKg": 62.5,
  "gender": 1,
  "birthDate": "1995-04-20",
  "accessTagIds": []
}
```

> `null` vs `[]` distinction:
> - `"accessTagIds": null` (or field omitted) → skip tag step entirely, leave existing tags unchanged
> - `"accessTagIds": []` → clear all disability tags for this user
> - `"accessTagIds": [1, 3]` → replace disability tags with IDs 1 and 3

### Response `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "firstName": "Anna",
  "lastName": "Kovalenko",
  "avatarUrl": "https://example.com/avatar.jpg",
  "city": "Kyiv",
  "gender": 1,
  "birthDate": "1995-04-20",
  "about": "I want to lose weight and improve flexibility",
  "heightCm": 168,
  "weightKg": 62.50,
  "accessTags": [
    { "id": 1, "name": "Visual impairment" },
    { "id": 3, "name": "Hearing impairment" }
  ]
}
```

After a successful response → onboarding complete, redirect to main app (e.g. trainer search).

---

## Tags Endpoint Reference

### `GET /api/tags?category={n}`

| `category` | Meaning | Used where |
|---|---|---|
| `0` | Specialization (e.g. Yoga, Boxing, Rehab) | Trainer profile setup, search filters |
| `1` | Disability / accessibility | **Client onboarding Step 2** |
| `2` | Methodology (e.g. Functional, CrossFit) | Trainer profile setup, search filters |

**Missing or invalid category → `400 Bad Request`:**
```json
{ "message": "category is required and must be 0 (Specialization), 1 (Disability), or 2 (Methodology)." }
```

---

## Full PATCH `/api/clients/{id}` Reference

### Request body (all fields optional)

```typescript
{
  avatarUrl?:    string | null,   // max 500 chars
  city?:         string | null,   // max 100 chars
  about?:        string | null,   // free text, fitness goals
  heightCm?:     number | null,   // integer, 1–32767
  weightKg?:     number | null,   // decimal, 0.01–999.99
  gender?:       0 | 1 | 2 | null, // 0=Male 1=Female 2=Other
  birthDate?:    string | null,   // "YYYY-MM-DD"
  accessTagIds?: number[] | null  // disability tag IDs; [] clears; null = no change
}
```

### Response body `200 OK`

```typescript
{
  id:          string,             // UUID
  email:       string,
  firstName:   string,
  lastName:    string,
  avatarUrl:   string | null,
  city:        string | null,
  gender:      0 | 1 | 2 | null,
  birthDate:   string | null,      // "YYYY-MM-DD"
  about:       string | null,      // fitnessGoals
  heightCm:    number | null,
  weightKg:    number | null,
  accessTags:  Array<{ id: number, name: string }>
}
```

### Error responses

| Status | When | Body |
|---|---|---|
| `400` | Validation failed (range, max length) | `{ "errors": { "field": ["message"] } }` |
| `401` | No or invalid JWT | — |
| `403` | JWT user ID ≠ route `{id}` | `{ "message": "Forbidden." }` |
| `404` | User not found or not a client role | `{ "message": "Client not found." }` |

---

## Complete Onboarding Flow Diagram

```
POST /api/auth/register
         │
         ▼
   Store user.id + tokens
         │
         ▼
┌─────────────────────────┐
│  STEP 1: About Yourself │
│  avatarUrl, city, about │
└─────────────────────────┘
         │
         │  PATCH /api/clients/{id}
         │  { avatarUrl, city, about }
         │
         ▼
      200 OK → advance
         │
         ▼
┌───────────────────────────────────────────────────────────────┐
│  Pre-load: GET /api/tags?category=1  (disability tags list)   │
└───────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────┐
│  STEP 2: Parameters                                       │
│  heightCm, weightKg, gender, birthDate, accessTagIds     │
└──────────────────────────────────────────────────────────┘
         │
         │  PATCH /api/clients/{id}
         │  { heightCm, weightKg, gender, birthDate, accessTagIds }
         │
         ▼
      200 OK → redirect to main app
```

---

## Notes for Frontend

1. **Both steps hit the same endpoint.** The split into "steps" is purely a UI concept — the backend treats both as partial updates to the same client record.

2. **Steps are independent.** The user can skip Step 1 or Step 2 and come back later. Each call updates only the sent fields. Always returns the full current profile in the response.

3. **No re-registration.** If the user closes the browser mid-onboarding, store progress in session/local state and resume from where they left off. The profile persists on the server between steps.

4. **`gender` is a number, not a string.** Send `0`, `1`, or `2`. Do not send `"male"`.

5. **`birthDate` is a date string.** Send `"1995-04-20"` (ISO 8601 date, no time component).

6. **`accessTagIds` must be disability tag IDs only.** Fetch them exclusively from `GET /api/tags?category=1`. The backend filters by category=1 when inserting, so sending IDs from other categories has no effect.

7. **`avatarUrl` is currently a plain string.** Pass any URL string. Real file upload is not wired yet.
