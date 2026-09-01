# Remal — Niche Perfume E‑Commerce Platform

A production e‑commerce platform built from scratch for an Egyptian niche perfume house:
storefront, checkout, and a full back‑office dashboard — running live and taking real orders.

**.NET 9 · Clean Architecture · EF Core 9 · SQL Server · SignalR · Docker**

| | |
|---|---|
| **Backend** | ~59,600 lines of C# across 4 layers |
| **API** | 100 REST endpoints |
| **Tests** | 98 passing (xUnit, in‑memory EF) |
| **Data** | 19 domain entities · 21 EF migrations |
| **Frontend** | ~8,500 lines of dependency‑free JS/CSS (bilingual AR/EN, RTL) |
| **Status** | Live in production |

---

## What it does

A single deployable that serves both halves of a real retail business.

**Storefront** — bilingual (Arabic RTL / English) product catalogue with per‑size pricing,
bundles, curated collections, wishlist, reviews, coupons, a loyalty‑points tier system,
and single‑page checkout with cash‑on‑delivery and Paymob card payments.

**Back office** — a dashboard for orders, inventory, customers, promotions, expenses and
accounting, with role‑based access, a full audit trail, live order notifications over
SignalR, Web Push, and Telegram alerts to the owner's phone.

---

## Engineering highlights

Things in here that were harder than they look, and how they were solved.

### Server-side rendering for social crawlers, without a framework

Facebook, Twitter and WhatsApp crawlers don't execute JavaScript, so a client‑rendered SPA
shows them nothing — and every page reported the same canonical URL, telling Google that
every product page was a duplicate of the home page.

`SocialMetaMiddleware` intercepts the request, injects per‑route Open Graph tags and the
correct canonical URL into the static HTML before it leaves the server, and serves it with a
content‑derived `ETag` so unchanged pages still return `304`.

<sub>[`Middleware/SocialMetaMiddleware.cs`](backend/src/Remal.Api/Middleware/SocialMetaMiddleware.cs)</sub>

### An image proxy that keeps mobile Safari alive

Source product photography is 6000×6000. Decoded, a single one costs roughly 137 MB of
RAM — enough for iOS Safari to kill the tab on a catalogue page.

`ImageController` resizes and re‑encodes on demand through ImageSharp, caches the result on
disk keyed by a cache version, and the frontend requests it through DPR‑aware `srcset` with
measured `sizes`, so a device asks for the pixels it will actually paint and nothing more.

<sub>[`Controllers/ImageController.cs`](backend/src/Remal.Api/Controllers/ImageController.cs)</sub>

### Rate limiting tuned for carrier-grade NAT

Egyptian mobile carriers put hundreds of subscribers behind one shared public IP. A
conventional per‑IP limit of 100 requests/minute locks out an entire network segment: one
browsing session costs about 14 limited requests, so roughly seven real users on the same
carrier IP were enough to trigger it.

The global limit is deliberately set to 1000/minute with the reasoning recorded in code,
while the auth endpoints keep a tight per‑IP limit where brute force is the actual threat.

<sub>[`Program.cs`](backend/src/Remal.Api/Program.cs)</sub>

### Deduplicated conversion tracking

Browser pixels are blocked often enough that client‑side analytics under‑reports. Purchase
events are sent twice — from the browser pixel and server‑to‑server through the Meta
Conversions API — sharing one `event_id` so Meta collapses them into a single conversion
instead of double counting.

<sub>[`Services/MetaConversionsApi.cs`](backend/src/Remal.Infrastructure/Services/MetaConversionsApi.cs)</sub>

### Audit trail as a persistence concern

Every insert, update and delete is captured by an EF Core `SaveChangesInterceptor` rather
than by remembering to log in each service — so the audit log cannot silently drift out of
sync with what actually happened to the data.

<sub>[`Persistence/Interceptors/AuditInterceptor.cs`](backend/src/Remal.Infrastructure/Persistence/Interceptors/AuditInterceptor.cs)</sub>

### Runtime configuration without redeploys

Shipping rates, tracking IDs, notification tokens and similar operational settings live in
an `AppSettings` table and are read per use, so the shop owner changes them from the
dashboard and they take effect immediately — no rebuild, no restart.

---

## Architecture

Clean Architecture with dependencies pointing inward. The domain knows nothing about EF,
HTTP or any external service.

```
┌─────────────────────────────────────────────────────────────┐
│  Remal.Api            Controllers · Middleware · SignalR     │
│                       JWT auth · rate limiting · Swagger     │
├─────────────────────────────────────────────────────────────┤
│  Remal.Infrastructure EF Core · Identity · SMTP · Paymob     │
│                       Web Push · Telegram · Meta CAPI        │
├─────────────────────────────────────────────────────────────┤
│  Remal.Application    Feature services · DTOs · validators   │
│                       MediatR pipeline · AutoMapper          │
├─────────────────────────────────────────────────────────────┤
│  Remal.Domain         Entities · value objects · enums       │
│                       (no external dependencies)             │
└─────────────────────────────────────────────────────────────┘
```

**Cross‑cutting** — FluentValidation and logging run as MediatR pipeline behaviours;
exceptions map to consistent API responses in one middleware; security headers and CSP are
applied centrally.

---

## Tech stack

**Backend** .NET 9 · ASP.NET Core Web API · EF Core 9 · SQL Server · ASP.NET Identity + JWT
· MediatR · AutoMapper · FluentValidation · Serilog · SignalR · built‑in rate limiting ·
health checks · Swagger/OpenAPI

**Integrations** Paymob (Egyptian card payments) · MailKit SMTP · Web Push (VAPID) ·
Telegram Bot API · Meta Conversions API · Google Analytics 4

**Frontend** Vanilla JavaScript and CSS, no framework and no build step — two single‑page
applications (storefront and dashboard), bilingual AR/EN with full RTL, PWA service worker,
DPR‑aware responsive images

**Infrastructure** Docker · docker‑compose · ImageSharp

---

## Running it

### Docker (everything, including SQL Server)

```bash
cd backend
cp .env.example .env      # set JWT_SECRET_KEY to a 64+ character random string
docker compose up --build
```

API on `http://localhost:5000`, storefront at `/remal.html`, dashboard at
`/remal-dashboard.html`, Swagger at `/swagger`.

### Local .NET

Requires the .NET 9 SDK and a reachable SQL Server.

```bash
cd backend
dotnet restore
dotnet ef database update -p src/Remal.Infrastructure -s src/Remal.Api
dotnet run --project src/Remal.Api
```

### Tests

```bash
cd backend
dotnet test
```

98 tests, no database required — they run against EF Core's in‑memory provider.

---

## Project structure

```
backend/
├── src/
│   ├── Remal.Domain/            19 entities, value objects, enums
│   │   ├── Common/              BaseEntity, Address, Money
│   │   ├── Entities/
│   │   └── Identity/
│   ├── Remal.Application/       business logic, no infrastructure
│   │   ├── Common/              interfaces, MediatR behaviors, mapping
│   │   ├── Features/            16 feature folders (Orders, Cart, Loyalty, …)
│   │   └── Validators/
│   ├── Remal.Infrastructure/    EF Core, Identity, external services
│   │   ├── Persistence/         DbContext, configurations, interceptors
│   │   ├── Identity/            JWT issuing, auth service
│   │   ├── Migrations/          21 migrations
│   │   └── Services/            email, push, Telegram, Paymob, Meta CAPI
│   └── Remal.Api/
│       ├── Controllers/         12 controllers, 100 endpoints
│       ├── Middleware/          exceptions, security headers, social meta
│       ├── Hubs/                SignalR dashboard notifications
│       └── wwwroot/             storefront + dashboard SPAs, assets
├── tests/Remal.Tests/           98 tests
├── Dockerfile
└── docker-compose.yml
```

---

## Configuration & security

No secrets are committed. `appsettings.json` ships with placeholders only; real values come
from `appsettings.Production.json` (git‑ignored) or environment variables.

| Setting | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server |
| `Jwt:SecretKey` | HS256 signing key, 64+ characters |
| `Email:Smtp*` | transactional email |
| `Vapid:PublicKey` / `PrivateKey` | Web Push |
| `Paymob:*` | card payments |
| `Meta:CapiAccessToken` | stored in the database, set from the dashboard |

Applied throughout: JWT with refresh‑token rotation and reuse detection (replaying a
revoked token revokes that user’s whole active token family), role‑based authorisation, ASP.NET
Identity password hashing, FluentValidation on every input, parameterised queries via EF
Core, a locked‑down CORS origin list, security headers with CSP, and rate limiting on auth
endpoints.

The credentials in `.env.example` and the `docker-compose.yml` defaults are throwaway values
for an ephemeral local container, overridable by environment variables. Production never
uses them.

---

## Notes

Built and maintained solo — architecture, backend, frontend, deployment and production
operations. It runs a real shop: the constraints that shaped it (mobile Safari memory
limits, carrier‑grade NAT, crawlers that don't run JavaScript, an owner who needs to change
shipping rates without calling a developer) came from production, not from a specification.

The catalogue data, brand assets and marketing content are proprietary and not included.

## License

No license granted. The source is published for portfolio and code‑review purposes; it is
not free for reuse or redistribution.
