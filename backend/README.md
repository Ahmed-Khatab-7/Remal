# Remal Backend

Production-ready .NET 9 Web API for the Remal perfume e-commerce platform.

## Stack

- **.NET 9** Web API + Controllers
- **EF Core 9** (Code First, SQL Server)
- **ASP.NET Identity** + JWT Bearer auth
- **Clean Architecture**: Domain → Application → Infrastructure → Api
- **FluentValidation** for DTO validation
- **Serilog** for structured logging
- **Built-in rate limiting** (.NET 9)
- **Swagger / OpenAPI**
- **Health checks** (SQL + UI)
- **Paymob** integration (Egyptian payments)
- **Docker** + docker-compose for production
- **Automatic audit log** via EF Core SaveChanges interceptor

## Project Structure

```
backend/
├── Remal.sln
├── Directory.Build.props          # shared MSBuild props (TargetFramework, Nullable, etc.)
├── Dockerfile                     # multi-stage: SDK build → ASP.NET runtime
├── docker-compose.yml             # SQL Server + API
├── .env.example                   # copy to .env and fill secrets
└── src/
    ├── Remal.Domain/              # Entities, Enums, ApplicationUser (no dependencies)
    ├── Remal.Application/         # Services, DTOs, Validators, Interfaces (depends on Domain)
    ├── Remal.Infrastructure/      # DbContext, Identity, Audit, Paymob (depends on Application)
    └── Remal.Api/                 # Controllers, Middleware, Program.cs (depends on Infra)
```

## Getting Started

### Option 1 — Local development (LocalDB or SQL Express)

```powershell
cd backend
dotnet restore
dotnet build

# update connection string in src/Remal.Api/appsettings.Development.json if needed
cd src/Remal.Api
dotnet run
```

The API starts on `https://localhost:7000` and `http://localhost:5000`. Swagger UI: `https://localhost:7000/swagger`.

On first run, it auto-runs migrations and seeds:
- 3 partner users (Admin + Partner roles)
- 6 sample products (each with 30/50/100 ML sizes)
- 2 bundles, 1 collection, 2 coupons
- Default app settings

**Default partner credentials:**

| Email           | Password      | Name            |
|-----------------|---------------|-----------------|
| aby@remal.eg    | `Remal@2026`  | عبدالرحمن ياسر |
| omr@remal.eg    | `Remal@2026`  | عمر ماهر        |
| akh@remal.eg    | `Remal@2026`  | أحمد خطاب       |

⚠️ **Change the default password immediately in production** via the `Seed:DefaultPartnerPassword` config or per-user via `/api/auth/change-password`.

### Option 2 — Docker (production-like)

```bash
cd backend
cp .env.example .env
# edit .env: set strong SA_PASSWORD and JWT_SECRET_KEY (64+ chars)
docker compose up -d --build
```

The API is now on `http://localhost:5000`. SQL Server on `localhost:1433`.

Tail logs:
```bash
docker compose logs -f api
```

## Authentication

All endpoints (except a few public ones) require a JWT in `Authorization: Bearer <token>` header.

```bash
# 1. Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"aby@remal.eg","password":"Remal@2026"}'

# 2. Use the token
curl http://localhost:5000/api/products \
  -H "Authorization: Bearer <accessToken>"

# 3. When the access token expires, refresh
curl -X POST http://localhost:5000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"accessToken":"<expired>","refreshToken":"<refresh>"}'
```

## API Reference (overview)

| Resource                              | Methods                              | Auth        |
|---------------------------------------|--------------------------------------|-------------|
| `/api/auth/{login,refresh,logout,me}` | login/refresh/logout/profile         | mixed       |
| `/api/products`                       | GET (public), CRUD (Partner)         | mixed       |
| `/api/products/{id}/stock`            | adjust per-size stock                | Partner     |
| `/api/bundles`                        | GET (public), CRUD (Partner)         | mixed       |
| `/api/collections`                    | GET (public), CRUD (Partner)         | mixed       |
| `/api/customers`                      | list/get                             | Partner     |
| `/api/orders`                         | list/get + place (public)            | mixed       |
| `/api/orders/track/{code}`            | public tracking                      | public      |
| `/api/orders/{id}/status`             | update status                        | Partner     |
| `/api/coupons`                        | CRUD + validate (public)             | mixed       |
| `/api/reviews`                        | list/get/moderate + create (public)  | mixed       |
| `/api/accounting/summary`             | P&L + partner balances + suggestions | Partner     |
| `/api/accounting/expenses`            | CRUD                                 | Partner     |
| `/api/accounting/settlements`         | CRUD                                 | Partner     |
| `/api/audit`                          | activity log                         | Partner     |
| `/api/reports/overview`               | dashboard KPIs                       | Partner     |
| `/api/reports`                        | analytics by date range              | Partner     |
| `/api/team`                           | partners list                        | Partner     |
| `/api/payments/paymob/session/{id}`   | create payment session               | public      |
| `/api/payments/paymob/webhook`        | Paymob callback                      | public+HMAC |
| `/health`                             | health check                         | public      |

Full OpenAPI: `http://localhost:5000/swagger` (Development only).

## Migrations

Migrations live in `Remal.Infrastructure`. The DbContext is `ApplicationDbContext`.

```bash
# Add a new migration
cd backend
dotnet ef migrations add InitialCreate \
  --project src/Remal.Infrastructure \
  --startup-project src/Remal.Api

# Apply migrations
dotnet ef database update \
  --project src/Remal.Infrastructure \
  --startup-project src/Remal.Api

# Generate idempotent SQL script (for staged deployment)
dotnet ef migrations script \
  --project src/Remal.Infrastructure \
  --startup-project src/Remal.Api \
  -o migrate.sql --idempotent
```

## Audit Log

Two layers:
1. **Automatic** (via `AuditInterceptor` on `SaveChangesAsync`) — captures every CREATE/UPDATE/DELETE with before/after JSON snapshots.
2. **Explicit** (via `IAuditService.LogAsync`) — used inside services for high-level business events ("partner X created bundle Y").

Both write to the `AuditLogs` table. Query via `/api/audit?category=Expense&search=...`.

## Production Notes

1. **Change all secrets** in `.env`:
   - `SA_PASSWORD` — strong 16+ char password
   - `JWT_SECRET_KEY` — random 64+ char string (`openssl rand -base64 64`)
   - `SEED_PARTNER_PASSWORD` — only used on first boot, change after
2. **Run behind a reverse proxy** (nginx / Caddy) with TLS termination
3. **Set `ASPNETCORE_ENVIRONMENT=Production`** to disable Swagger and detailed errors
4. **Backup `RemalDb`** daily — use SQL Server's built-in or scheduled `sqlcmd`
5. **Monitor**: `docker logs remal-api`, `/health` endpoint, Serilog file logs in `logs/`
6. **Rotate JWT secret** every few months — invalidate all sessions
7. **HTTPS only** — enforced via `UseHsts()` + `UseHttpsRedirection()` in production

## Frontend Integration

The existing `remal.html` (storefront) and `remal-dashboard.html` (admin) connect via `fetch`:

```js
const API = 'http://localhost:5000/api';

async function api(path, opts = {}) {
  const token = localStorage.getItem('token');
  const res = await fetch(`${API}${path}`, {
    ...opts,
    headers: {
      'Content-Type': 'application/json',
      ...(token && { Authorization: `Bearer ${token}` }),
      ...opts.headers,
    },
  });
  const json = await res.json();
  if (!json.success) throw new Error(json.message || 'Request failed');
  return json.data;
}

// Usage
const products = await api('/products?page=1&pageSize=20');
const order = await api('/orders', { method: 'POST', body: JSON.stringify(orderDto) });
```

## License

Proprietary — © Remal Fragrances
