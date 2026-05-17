# Frontend Validation Notes

This file tracks validation gaps in the frontend that correspond to backend DataAnnotations added in the validation-fix pass. Use it as a checklist when building or tightening frontend forms.

---

## Forms already partially validated — need tightening

| Form | File | Gap |
|---|---|---|
| Login | `src/components/auth/LoginForm.tsx` | No client-side validation at all. Add: email required + regex format check; password required — before submit. |
| SignupForm | `src/components/auth/SignupForm.tsx` | Relies on HTML5 `type="email"` only. Add explicit JS email format check for consistent error messaging. |
| Client onboarding step 2 | `src/hooks/useOnboarding.ts` | `heightCm` only checks `> 0` → should check `50–300`. `weightKg` only checks `> 0` → should check `1–500`. |
| Trainer onboarding step 1 | `src/hooks/useTrainerOnboarding.ts` | No `bio` length check. Backend now enforces 2000 chars max. |
| Trainer onboarding step 4 (slot) | `src/hooks/useTrainerOnboarding.ts` | Missing: `endTime > startTime` check; `maxClients` range `1–100`; `description` max 1000 chars; `gymName` max 200 chars; `gymAddress` max 500 chars. |

---

## Forms not yet implemented on FE — validation to add when building them

| Form | Required frontend validations |
|---|---|
| Create Review | `rating` required, integer, range 1–5; `comment` max 2000 chars |
| Create Support Ticket | `subject` required, max 200 chars; `description` required, max 2000 chars |
| Create Session Note | `content` required, max 5000 chars |
| Cancel Booking | `cancellationReason` max 500 chars |
| Update User profile | `firstName`/`lastName` max 100 chars; `phone` max 20 chars; `city` max 100 chars |
| Update Trainer info | `bio` max 2000 chars; `experienceYears` range 0–100 |
| Update Client info | `heightCm` range 50–300; `weightKg` range 1–500; `fitnessGoals` max 2000 chars |

---

## Trainer search filters (OK as-is)

- `minRating` is already capped at 5 via the slider component — matches `[Range(0, 5)]` on backend.
- Price range max is 5000 on the slider — backend allows up to 99999.99, so no conflict.
- `city` and `name` fields are free-text inputs with no current length limit; backend now enforces max 100. Consider adding a `maxLength={100}` attribute to the inputs.
