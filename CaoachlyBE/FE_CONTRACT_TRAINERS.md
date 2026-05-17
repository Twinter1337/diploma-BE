# Frontend API Contract — Trainer Endpoints

Base URL: `http://localhost:5064` (dev) / `https://api.coachly.app` (prod)

All timestamps are **UTC ISO 8601** strings (`"2025-06-01T10:00:00Z"`).

---

## 1. GET `/api/trainers/{id}`

Returns the public profile of a trainer.

### Auth
None required.

### Path parameter
| Name | Type | Description |
|---|---|---|
| `id` | `UUID` | Trainer's user ID |

### Success — `200 OK`

```json
{
  "fullName": "John Doe",
  "verificationStatus": 2,
  "isAccessible": true,
  "avatarUrl": "https://blob.example.com/avatars/abc.jpg",
  "experienceYears": 5,
  "bio": "Certified strength and conditioning coach.",
  "rating": 4.8,
  "minPrice": 500.00,
  "city": "Kyiv",
  "numOfReviews": 42,
  "numOfCompletedClasses": 128,
  "numOfActiveClients": 9,
  "specializationTags": [
    {
      "id": 1,
      "name": "Strength Training",
      "category": 0,
      "description": "Weightlifting, powerlifting, etc."
    }
  ],
  "methodologyTags": [
    {
      "id": 7,
      "name": "HIIT",
      "category": 2,
      "description": null
    }
  ]
}
```

### Field reference

| Field | Type | Notes |
|---|---|---|
| `fullName` | `string` | `firstName + " " + lastName` |
| `verificationStatus` | `number` | `0` = NotVerified, `1` = Pending, `2` = Verified, `3` = Rejected |
| `isAccessible` | `boolean` | Trainer works with clients with disabilities |
| `avatarUrl` | `string \| null` | Azure Blob public URL |
| `experienceYears` | `number` | Integer |
| `bio` | `string \| null` | — |
| `rating` | `number` | `0.0` – `5.0`; maintained by DB trigger |
| `minPrice` | `number \| null` | Lowest price across all available slots |
| `city` | `string \| null` | — |
| `numOfReviews` | `number` | — |
| `numOfCompletedClasses` | `number` | Completed bookings count |
| `numOfActiveClients` | `number` | Unique clients with confirmed/completed bookings |
| `specializationTags` | `Tag[]` | Category `0` |
| `methodologyTags` | `Tag[]` | Category `2` |

**Tag object:**

| Field | Type | Notes |
|---|---|---|
| `id` | `number` | — |
| `name` | `string` | — |
| `category` | `number` | `0` = Specialization, `1` = Disability, `2` = Methodology |
| `description` | `string \| null` | — |

### Errors

| Status | Body | When |
|---|---|---|
| `404` | `{ "message": "Trainer not found." }` | ID does not exist or user is not a trainer |

---

## 2. GET `/api/trainers/{id}/slots`

Returns all **available** (status = 0) upcoming slots for a trainer.

### Auth
None required.

### Path parameter
| Name | Type | Description |
|---|---|---|
| `id` | `UUID` | Trainer's user ID |

### Success — `200 OK`

Returns a flat array (no pagination).

```json
[
  {
    "startDateTime": "2025-06-01T10:00:00Z",
    "durationInMinutes": 60
  },
  {
    "startDateTime": "2025-06-03T14:00:00Z",
    "durationInMinutes": 90
  }
]
```

### Field reference

| Field | Type | Notes |
|---|---|---|
| `startDateTime` | `string` | UTC ISO 8601 |
| `durationInMinutes` | `number` | Derived from `endTime - startTime` |

> **Note:** The array can be empty (`[]`) when a trainer has no available slots. This is a normal `200` response, not an error.

### Errors

| Status | Body | When |
|---|---|---|
| `404` | `{ "message": "Trainer not found." }` | ID does not exist or user is not a trainer |

---

## 3. GET `/api/trainers/{id}/reviews`

Returns all reviews left for a trainer.

### Auth
None required.

### Path parameter
| Name | Type | Description |
|---|---|---|
| `id` | `UUID` | Trainer's user ID |

### Success — `200 OK`

Returns a flat array (no pagination).

```json
[
  {
    "avatarUrl": "https://blob.example.com/avatars/client1.jpg",
    "fullName": "Jane Smith",
    "rating": 5,
    "comment": "Amazing trainer, highly recommend!",
    "createdAt": "2025-05-12T08:30:00Z"
  },
  {
    "avatarUrl": null,
    "fullName": "Alex Brown",
    "rating": 4,
    "comment": null,
    "createdAt": "2025-04-20T17:00:00Z"
  }
]
```

### Field reference

| Field | Type | Notes |
|---|---|---|
| `avatarUrl` | `string \| null` | Reviewer's avatar |
| `fullName` | `string` | Reviewer's full name |
| `rating` | `number` | Integer `1` – `5` |
| `comment` | `string \| null` | Optional text left by the client |
| `createdAt` | `string` | UTC ISO 8601 |

> **Note:** The array can be empty (`[]`) when a trainer has no reviews yet. This is a normal `200` response, not an error.

### Errors

| Status | Body | When |
|---|---|---|
| `404` | `{ "message": "Trainer X not found." }` | ID does not exist |

---

## Enum quick reference

```
VerificationStatus: 0=NotVerified  1=Pending  2=Verified  3=Rejected
TagCategory:        0=Specialization  1=Disability  2=Methodology
SlotStatus:         0=Available  1=Booked  2=SoldOut  3=Cancelled
```
