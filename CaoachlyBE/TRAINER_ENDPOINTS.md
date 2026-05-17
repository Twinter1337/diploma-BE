# Trainer Endpoints — API Contract

---

## Enums

### SlotFormat
| Value | Name |
|---|---|
| `0` | Online |
| `1` | Offline |

### SlotStatus
| Value | Name |
|---|---|
| `0` | Available |
| `1` | Booked |
| `2` | SoldOut |
| `3` | Cancelled |
| `4` | Completed |

### BookingStatus
| Value | Name |
|---|---|
| `0` | Pending |
| `1` | Confirmed |
| `2` | Cancelled |
| `3` | Completed |

---

## GET /api/trainers/{id}/slots

Returns a list of upcoming available slots for a trainer.

**Auth:** public (no token required)

**Path params**
| Param | Type | Description |
|---|---|---|
| `id` | `uuid` | Trainer ID |

**Response `200`**
```json
[
  {
    "id": "uuid",
    "startDateTime": "2026-05-10T10:00:00Z",
    "durationInMinutes": 60,
    "format": 0,
    "price": 50.00,
    "maxClients": 5,
    "currentNumOfClients": 2
  }
]
```

| Field | Type | Description |
|---|---|---|
| `id` | `uuid` | Slot ID |
| `startDateTime` | `datetime` | Slot start (UTC) |
| `durationInMinutes` | `int` | Duration in minutes |
| `format` | `SlotFormat` | 0 = Online, 1 = Offline |
| `price` | `decimal` | Price per session |
| `maxClients` | `int` | Max number of clients |
| `currentNumOfClients` | `int` | Clients with Pending or Confirmed bookings |

**Response `404`** — trainer not found

---

## GET /api/trainers/{id}/slot-count

Returns slot counts for a trainer.

**Auth:** public (no token required)

**Path params**
| Param | Type | Description |
|---|---|---|
| `id` | `uuid` | Trainer ID |

**Response `200`**
```json
{
  "numOfAllSlots": 20,
  "numOfBookedSlots": 5
}
```

| Field | Type | Description |
|---|---|---|
| `numOfAllSlots` | `int` | Total number of slots (all statuses) |
| `numOfBookedSlots` | `int` | Slots with status Booked or SoldOut |

**Response `404`** — trainer not found

---

## GET /api/trainers/{id}/bookings

Returns a list of future bookings (Pending or Confirmed, slot start > now) for a trainer.

**Auth:** required

**Path params**
| Param | Type | Description |
|---|---|---|
| `id` | `uuid` | Trainer ID |

**Response `200`**
```json
[
  {
    "id": "uuid",
    "clientFullName": "Jane Smith",
    "clientAvatarUrl": "https://...",
    "startDateTime": "2026-05-10T10:00:00Z",
    "durationInMinutes": 60,
    "format": 1,
    "status": 1
  }
]
```

| Field | Type | Description |
|---|---|---|
| `id` | `uuid` | Booking ID |
| `clientFullName` | `string` | Client's first + last name |
| `clientAvatarUrl` | `string \| null` | Client avatar URL |
| `startDateTime` | `datetime` | Slot start (UTC) |
| `durationInMinutes` | `int` | Duration in minutes |
| `format` | `SlotFormat` | 0 = Online, 1 = Offline |
| `status` | `BookingStatus` | 0 = Pending, 1 = Confirmed |

**Response `404`** — trainer not found

---

## GET /api/trainers/{id}/clients

Returns all-time clients of a trainer (only from Completed bookings), ordered by most recent session.

**Auth:** required

**Path params**
| Param | Type | Description |
|---|---|---|
| `id` | `uuid` | Trainer ID |

**Response `200`**
```json
[
  {
    "clientId": "uuid",
    "clientFullName": "Jane Smith",
    "clientAvatarUrl": "https://...",
    "numOfClasses": 8,
    "lastSlotDate": "2026-04-30T09:00:00Z"
  }
]
```

| Field | Type | Description |
|---|---|---|
| `clientId` | `uuid` | Client user ID |
| `clientFullName` | `string` | Client's first + last name |
| `clientAvatarUrl` | `string \| null` | Client avatar URL |
| `numOfClasses` | `int` | Total completed sessions with this trainer |
| `lastSlotDate` | `datetime` | Start time of the most recent completed slot (UTC) |

**Response `404`** — trainer not found

---

## GET /api/trainers/{id}/stats

Returns aggregated stats for a trainer.

**Auth:** required

**Path params**
| Param | Type | Description |
|---|---|---|
| `id` | `uuid` | Trainer ID |

**Response `200`**
```json
{
  "numOfCompletedSlots": 42,
  "avgRating": 4.8,
  "activeClientsThisMonth": 7,
  "completedSlotsPerMonth": [
    { "month": 1, "numOfCompletedSlots": 5 },
    { "month": 2, "numOfCompletedSlots": 8 },
    { "month": 3, "numOfCompletedSlots": 6 },
    { "month": 4, "numOfCompletedSlots": 10 },
    { "month": 5, "numOfCompletedSlots": 3 }
  ]
}
```

| Field | Type | Description |
|---|---|---|
| `numOfCompletedSlots` | `int` | Total completed slots of all time |
| `avgRating` | `decimal` | Average rating (0 if no reviews yet) |
| `activeClientsThisMonth` | `int` | Distinct clients with Pending/Confirmed bookings in [now − 1 month, now + 1 month] |
| `completedSlotsPerMonth` | `array` | Completed slots per month from Jan 1 of the current year up to the current month |
| `completedSlotsPerMonth[].month` | `int` | Month number (1–12) |
| `completedSlotsPerMonth[].numOfCompletedSlots` | `int` | Number of completed slots in that month (0 if none) |

**Response `404`** — trainer not found

---

## PATCH /api/slots/{id}

Partially updates a schedule slot. Only the fields present in the request body are updated.

**Auth:** required — must be the trainer who owns the slot

**Path params**
| Param | Type | Description |
|---|---|---|
| `id` | `uuid` | Slot ID |

**Request body** — all fields optional
```json
{
  "startTime": "2026-05-15T09:00:00Z",
  "endTime": "2026-05-15T10:00:00Z",
  "format": 0,
  "price": 60.00,
  "maxClients": 3,
  "description": "Bring a mat",
  "gymName": "FitZone",
  "gymAddress": "123 Main St"
}
```

| Field | Type | Constraints | Description |
|---|---|---|---|
| `startTime` | `datetime \| null` | — | New slot start (UTC) |
| `endTime` | `datetime \| null` | — | New slot end (UTC) |
| `format` | `SlotFormat \| null` | 0 or 1 | Online / Offline |
| `price` | `decimal \| null` | 0.01–99999.99 | Price per session |
| `maxClients` | `int \| null` | 1–100 | Max number of clients |
| `description` | `string \| null` | max 1000 chars | Optional description |
| `gymName` | `string \| null` | max 200 chars | Gym name (Offline only) |
| `gymAddress` | `string \| null` | max 500 chars | Gym address (Offline only) |

**Response `200`** — returns the full updated slot
```json
{
  "id": "uuid",
  "trainerId": "uuid",
  "startTime": "2026-05-15T09:00:00Z",
  "endTime": "2026-05-15T10:00:00Z",
  "format": 0,
  "price": 60.00,
  "maxClients": 3,
  "description": "Bring a mat",
  "gymName": null,
  "gymAddress": null,
  "status": 0,
  "createdAt": "2026-05-01T12:00:00Z"
}
```

**Response `403`** — requesting user is not the slot's trainer  
**Response `404`** — slot not found
