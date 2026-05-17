# Stripe Checkout — Frontend Integration Guide

This document describes the complete payment flow from the frontend perspective: what to call, when, what to expect back, and what the user sees at each step.

---

## How the Flow Works (Big Picture)

```
1. FE calls POST /api/bookings  →  BE creates booking + calls Stripe API
2. BE returns a Stripe Checkout URL
3. FE redirects the user to that URL (Stripe-hosted payment page)
4. User completes payment on Stripe's page
5. Stripe redirects the user back to your SuccessUrl or CancelUrl
6. Stripe fires a webhook to the backend (handled server-side automatically)
7. Backend confirms the booking and emails the receipt to the user
```

The frontend only participates in steps 1–5. Steps 6–7 are fully server-side.

---

## Step 1 — Authenticate the User

All booking endpoints require a valid JWT access token. The user must be logged in with role `Client` (role = 0).

**Login request:**
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "client@example.com",
  "password": "password123"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "abc123...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "client@example.com",
    "firstName": "Ivan",
    "lastName": "Koval",
    "role": 0
  }
}
```

Store `accessToken` and send it as `Authorization: Bearer <token>` on all subsequent requests.

---

## Step 2 — Create a Booking

When the user selects a slot and clicks "Book", call this endpoint.

**Request:**
```
POST /api/bookings
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "slotId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Field:**

| Field | Type | Required | Description |
|---|---|---|---|
| `slotId` | UUID string | Yes | The ID of the schedule slot to book |

**Success response — `201 Created`:**
```json
{
  "bookingId": "b1a2c3d4-0000-0000-0000-000000000001",
  "checkoutUrl": "https://checkout.stripe.com/c/pay/cs_test_...",
  "status": 0
}
```

| Field | Type | Description |
|---|---|---|
| `bookingId` | UUID | Your internal booking ID — save this |
| `checkoutUrl` | string | Full Stripe Checkout URL — redirect the user here |
| `status` | number | Always `0` (Pending) at this point |

**Error responses:**

| HTTP | Condition | Body |
|---|---|---|
| `400` | Slot is no longer available | `{ "message": "This slot is no longer available." }` |
| `401` | Missing or invalid JWT | — |
| `403` | User is not a Client (role ≠ 0) | — |
| `404` | Slot ID does not exist | `{ "message": "Slot ... not found." }` |
| `409` | Client already has an active booking that overlaps in time | `{ "message": "You already have an active booking that overlaps with this time slot." }` |

---

## Step 3 — Redirect to Stripe Checkout

After receiving the `201` response, immediately redirect the browser to `checkoutUrl`:

```js
window.location.href = response.checkoutUrl;
```

The user will see a Stripe-hosted payment page with:
- The session title: **"Training session with [Trainer Name]"**
- The price and currency (UAH)
- Email pre-filled from their account
- Card input fields

---

## Step 4 — Handle the Return from Stripe

Stripe redirects the user back to one of two URLs configured on the backend:

| Outcome | Redirect URL |
|---|---|
| Payment succeeded | `http://localhost:5173/booking/success?session={CHECKOUT_SESSION_ID}` |
| User cancelled | `http://localhost:5173/booking/cancel` |

### Success page — `/booking/success`

The URL contains a `session` query parameter with the Stripe session ID. You can use it to display a confirmation message, but **you do not need to call the backend** — the webhook has already confirmed the booking and sent the receipt email automatically.

```
/booking/success?session=cs_test_abc123...
```

What to show:
- "Payment successful! Your session is confirmed."
- "A receipt has been sent to your email."
- Link to view bookings (once that endpoint is implemented)

### Cancel page — `/booking/cancel`

The user abandoned the checkout. The booking record in the database remains in `Pending` status. You can:
- Show "Payment was not completed."
- Offer to retry (send the user back to the slot page — they can call `POST /api/bookings` again with the same slot ID if it is still available)

---

## Step 5 — Receipt Email (Automatic)

After a successful payment, the user automatically receives an HTML email at their registered email address. You do **not** need to trigger this — it happens server-side via the Stripe webhook.

The email contains:
- Coachly branding header
- "Payment Successful" confirmation
- The amount paid (e.g., ₴500.00 UAH)
- Trainer name
- Session date and time
- Format (Online / Offline)
- Stripe Payment ID
- Date and time of payment

---

## Booking Status Reference

| Value | Name | Meaning |
|---|---|---|
| `0` | Pending | Booking created, payment not yet completed |
| `1` | Confirmed | Payment successful, session booked |
| `2` | Cancelled | Booking was cancelled |
| `3` | Completed | Session has taken place |

---

## Full Request/Response Contracts

### `POST /api/auth/login`

**Request body:**
```ts
{
  email: string;
  password: string;
}
```

**Response `200`:**
```ts
{
  accessToken: string;
  refreshToken: string;
  user: {
    id: string;        // UUID
    email: string;
    firstName: string;
    lastName: string;
    role: 0 | 1 | 2;  // 0=Client, 1=Trainer, 2=Admin
  };
}
```

---

### `POST /api/bookings`

**Headers:**
```
Authorization: Bearer <accessToken>
Content-Type: application/json
```

**Request body:**
```ts
{
  slotId: string;   // UUID of the schedule slot
}
```

**Response `201`:**
```ts
{
  bookingId: string;    // UUID — your internal booking ID
  checkoutUrl: string;  // Stripe Checkout redirect URL
  status: 0;            // always Pending at creation
}
```

**Error responses:**
```ts
// 400 / 404 / 409
{
  message: string;
}
```

---

## TypeScript Types (copy-paste ready)

```ts
// POST /api/auth/login
interface LoginRequest {
  email: string;
  password: string;
}

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    role: 0 | 1 | 2;
  };
}

// POST /api/bookings
interface CreateBookingRequest {
  slotId: string;
}

interface CreateBookingResponse {
  bookingId: string;
  checkoutUrl: string;
  status: 0;
}

interface ApiError {
  message: string;
}
```

---

## Example Implementation (fetch)

```ts
async function bookSlot(slotId: string, accessToken: string): Promise<void> {
  const res = await fetch('/api/bookings', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ slotId }),
  });

  if (res.status === 201) {
    const data: CreateBookingResponse = await res.json();
    // Save bookingId if needed, then redirect to Stripe
    window.location.href = data.checkoutUrl;
    return;
  }

  const error: ApiError = await res.json();

  if (res.status === 400) throw new Error(`Slot unavailable: ${error.message}`);
  if (res.status === 409) throw new Error(`Time conflict: ${error.message}`);
  if (res.status === 404) throw new Error(`Slot not found: ${error.message}`);
  if (res.status === 401) throw new Error('Please log in again.');
  if (res.status === 403) throw new Error('Only clients can book sessions.');

  throw new Error('Unexpected error. Please try again.');
}
```

---

## Stripe Test Cards

Use these during development (no real money charged):

| Card number | Scenario |
|---|---|
| `4242 4242 4242 4242` | Payment succeeds |
| `4000 0000 0000 9995` | Card declined |
| `4000 0025 0000 3155` | Requires 3D Secure authentication |

Use any future expiry date (e.g. `12/28`) and any 3-digit CVC.

---

## Routes to Implement on the Frontend

| Route | Purpose |
|---|---|
| `/booking/success?session=<id>` | Shown after successful Stripe payment |
| `/booking/cancel` | Shown when user cancels the Stripe checkout |

No backend calls are required on these pages — they are purely informational.
