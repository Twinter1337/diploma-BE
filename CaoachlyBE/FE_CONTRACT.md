# Frontend API Contract

Base URL: `https://<host>/api`

All endpoints require a valid JWT access token in the `Authorization` header unless noted otherwise.

```
Authorization: Bearer <access_token>
```

---

## Table of Contents

1. [GET /users/{id}](#1-get-usersid)
2. [GET /users/{id}/bookings](#2-get-usersidbookings)
3. [GET /users/{id}/booking-history](#3-get-usersidbooking-history)
4. [GET /achievements](#4-get-achievements)
5. [GET /users/{id}/achievements](#5-get-usersidachievements)

---

## 1. GET /users/{id}

Returns the profile of a specific user.

**Authorization:** Authenticated user can only request their own profile. Admins can request any profile.

### Path Parameters

| Parameter | Type   | Description |
|-----------|--------|-------------|
| `id`      | `uuid` | User ID     |

### Response `200 OK`

```json
{
  "firstName": "string",
  "lastName": "string",
  "email": "string",
  "city": "string | null",
  "heightCm": "number (short) | null",
  "weightKg": "number (decimal) | null",
  "avatarUrl": "string | null",
  "disabilityTags": [
    {
      "id": "number",
      "name": "string",
      "isSelected": "boolean"
    }
  ]
}
```

**Notes:**
- `heightCm` and `weightKg` come from the client profile; both are `null` if the user has not filled them in.
- `disabilityTags` is always a full list of all disability tags in the system. Use `isSelected` to determine which ones apply to this user.

### Error Responses

| Status | When |
|--------|------|
| `401`  | Missing or invalid token |
| `403`  | Requesting another user's profile without admin role |
| `404`  | User not found |

---

## 2. GET /users/{id}/bookings

Returns the active and upcoming bookings for a user.

**Authorization:** Authenticated user can only request their own bookings. Admins can request any user's bookings.

### Path Parameters

| Parameter | Type   | Description |
|-----------|--------|-------------|
| `id`      | `uuid` | User ID     |

### Response `200 OK`

```json
[
  {
    "trainerFullName": "string",
    "trainerAvatarUrl": "string | null",
    "status": "number",
    "startTime": "string (ISO 8601 UTC)",
    "durationMinutes": "number",
    "format": "number"
  }
]
```

**`status` enum:**

| Value | Meaning     |
|-------|-------------|
| `0`   | Pending     |
| `1`   | Confirmed   |
| `2`   | Cancelled   |
| `3`   | Completed   |

**`format` enum:**

| Value | Meaning  |
|-------|----------|
| `0`   | Online   |
| `1`   | Offline  |

### Error Responses

| Status | When |
|--------|------|
| `401`  | Missing or invalid token |
| `403`  | Requesting another user's bookings without admin role |

---

## 3. GET /users/{id}/booking-history

Returns the completed booking history for a user, including any review left for each session.

> **Note:** the path is `booking-history`, not `bookings-history`.

**Authorization:** Authenticated user can only request their own history. Admins can request any user's history.

### Path Parameters

| Parameter | Type   | Description |
|-----------|--------|-------------|
| `id`      | `uuid` | User ID     |

### Response `200 OK`

```json
[
  {
    "startTime": "string (ISO 8601 UTC)",
    "trainerFullName": "string",
    "price": "number (decimal)",
    "review": {
      "rating": "number (1–5)",
      "comment": "string | null"
    } | null
  }
]
```

**Notes:**
- `review` is `null` when the user has not left a review for that session.

### Error Responses

| Status | When |
|--------|------|
| `401`  | Missing or invalid token |
| `403`  | Requesting another user's history without admin role |

---

## 4. GET /achievements

Returns all achievements that the authenticated user has **not yet earned**. Use this to show locked/available achievements.

**Authorization:** Required. The user ID is derived from the JWT — no path parameter needed.

### Response `200 OK`

```json
[
  {
    "id": "number",
    "type": "number",
    "title": "string",
    "description": "string",
    "iconUrl": "string"
  }
]
```

**`type` enum:**

| Value | Meaning        |
|-------|----------------|
| `1`   | FirstSession   |

### Error Responses

| Status | When |
|--------|------|
| `401`  | Missing or invalid token |

---

## 5. GET /users/{id}/achievements

Returns the full achievements list for a user, with each item indicating whether it has been earned.

**Authorization:** Authenticated user can only request their own achievements. Admins can request any user's achievements.

### Path Parameters

| Parameter | Type   | Description |
|-----------|--------|-------------|
| `id`      | `uuid` | User ID     |

### Response `200 OK`

```json
{
  "totalCount": "number",
  "earnedCount": "number",
  "achievements": [
    {
      "id": "number",
      "type": "number",
      "title": "string",
      "description": "string",
      "iconUrl": "string",
      "isEarned": "boolean",
      "earnedAt": "string (ISO 8601 UTC) | null"
    }
  ]
}
```

**Notes:**
- `earnedAt` is `null` when `isEarned` is `false`.

**`type` enum:** same as [GET /achievements](#4-get-achievements).

### Error Responses

| Status | When |
|--------|------|
| `401`  | Missing or invalid token |
| `403`  | Requesting another user's achievements without admin role |
