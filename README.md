# Linden Practice Manager

A small web application for a private psychology practice: client records, appointment scheduling and encrypted session notes. The practice and all people in the demo data are made up.

I built it to practise handling sensitive data properly, so most of the work is in access control, encryption and audit logging rather than in the screens.

Back end is an ASP.NET Core Web API (.NET 8) with SQL Server. The front end is plain HTML, CSS and JavaScript (ES modules, no framework) served from `wwwroot`.

## What it does

- Client records: create, edit, search, archive, restore
- Appointments: weekly schedule, booking with overlap detection, mark as completed, no-show or cancelled
- Session notes: written by the psychologist, encrypted before they reach the database
- Audit log: who opened or changed which record, and when
- Two roles: **Psychologist** (Admin) sees everything, **Reception** (Assistant) manages clients and the calendar but cannot read or write notes and cannot see the audit log

## Security and privacy

| Topic | What the code does |
|---|---|
| Notes at rest | AES-256-GCM (`Security/AesGcmNoteCipher.cs`). A fresh random nonce for every save. The client id is bound to the ciphertext as authenticated data, so a note copied to another client's row fails to decrypt. |
| Keys | The key is never in the repository. User-secrets in development, an environment variable in production. The app refuses to start without a valid key. |
| Access to notes | Admin role only, enforced on the server. The notes are not loaded when a client is opened. The psychologist has to press "Show session notes". |
| Audit trail | Every login, client view, note view and change is stored with user, time and IP. The entry is saved in the same transaction as the change. Reading notes is logged **before** the data is returned, so if logging fails nothing is returned. The log holds record ids, never names or note text. There is no endpoint to edit or delete entries. |
| Right to erasure | An archived client can be erased together with all appointments and notes (Admin only, two steps on purpose). The audit entry keeps the id only. |
| Sessions | Cookie auth with Identity: HttpOnly, Secure, SameSite=Strict, 30 minute sliding expiry. Lockout after 5 wrong passwords, rate limit on the login endpoint. |
| CSRF | SameSite=Strict plus a check that every state-changing request sends JSON or an `X-Requested-With` header. |
| XSS | The front end builds the page with DOM calls and text nodes. Server data never goes through `innerHTML`. In production a strict Content-Security-Policy is sent. |
| Caching | `Cache-Control: no-store` on all API responses. |

This is a portfolio project, not a certified system. A real deployment would also need things like backups, key rotation, a data processing agreement and a proper security review.

## Running it

You need the .NET 8 SDK and SQL Server or LocalDB.

```
cd src/Linden.Api

# trust the development HTTPS certificate (cookies are Secure-only)
dotnet dev-certs https --trust

# create an encryption key and store it in user-secrets
```

Generate the key, then set it.

PowerShell:

```
$b = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
dotnet user-secrets set "Encryption:Key" ([Convert]::ToBase64String($b))
```

macOS / Linux:

```
dotnet user-secrets set "Encryption:Key" "$(openssl rand -base64 32)"
```

Then create the database and start:

```
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet run
```

The app applies the migration on start. In development it also creates two users and a few invented clients with encrypted notes:

| Role | Email | Password |
|---|---|---|
| Psychologist | psychologist@linden.test | Therapy#2024x |
| Reception | reception@linden.test | Reception#2024x |

Open https://localhost:7180. Swagger is at `/swagger` in development (sign in on the main page first, the same cookie is used).

**If you lose the key, the notes cannot be recovered.** That is the point of encrypting them, but keep a copy of the production key somewhere safe.

### Production settings

Nothing sensitive is read from files. Set these as environment variables:

```
ConnectionStrings__DefaultConnection=...
Encryption__Key=<base64 of 32 random bytes>
SeedUsers__AdminEmail=...        (optional, first start only)
SeedUsers__AdminPassword=...
```

## Tests

```
dotnet test
```

Covers the cipher (round trip, wrong key, wrong client, tampering, bad keys) and the booking rules (opening hours, weekends, past times).

## Structure

```
src/Linden.Api
  Controllers/   one controller per resource, thin
  Security/      AES-GCM cipher
  Services/      audit service, schedule rules
  Models/ Data/  entities, DbContext, seed data
  Dtos/          request and response shapes, validated with attributes
  wwwroot/       the front end (index.html, css, js/views)
tests/Linden.Tests
```

## Decisions I would explain in an interview

- **No double booking.** One person is treated at a time, so any two active appointments must not overlap. The service checks the overlap and a filtered unique index on the start time catches two requests arriving at once.
- **Appointment times are local.** The practice works in one time zone, so times are stored without an offset. Audit timestamps are UTC.
- **DTOs instead of entities** in the API, so the encrypted column and internal fields never leave the server.
- **Why cookies and not JWT.** The browser keeps an HttpOnly cookie that JavaScript can't read. A token in `localStorage` would be exposed to any XSS bug, which matters more with clinical data.
- **Listing clients is not audited, opening one is.** Logging every row of a search would bury the useful entries.

## Next steps

- Reschedule an appointment instead of cancelling and booking again
- Email reminders
- Key rotation (the stored format already has a version byte for it)
- Integration tests against a real SQL Server container
