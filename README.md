# SmartStore

A full-stack store management application built with C# and ASP.NET Core. Register an account, sign in, and manage your catalog, inventory, and sales through a responsive web interface.

## Tech Stack

- **Backend:** .NET 10, ASP.NET Core Web API, ASP.NET Core Identity
- **Frontend:** HTML, CSS, JavaScript, served by the same ASP.NET Core application
- **Database:** PostgreSQL 16+, Entity Framework Core 10, Npgsql
- **Testing & CI:** xUnit, API/PostgreSQL integration tests, GitHub Actions

## Features

- Register, sign in, sign out, and restore your session on page reload.
- Cookie authentication, hashed passwords, CSRF protection, login rate limiting, and temporary account lockout after repeated failed attempts.
- Dashboard with catalog counts and recent sales.
- Create, view, edit, and delete categories, suppliers, and products. Records referenced by products or stock/sales history cannot be deleted.
- Paginated product catalog, stock adjustments, and inventory movement history.
- Create sales with multiple products, automatically deduct stock, and view order details and stored sale prices.
- Reject negative stock and detect concurrent inventory updates.
- Responsive layouts, form validation, empty states, and error messages.

## Project Structure

```text
src/
  SmartStore.Api/             Controllers, authentication, and wwwroot frontend
  SmartStore.Application/     Request DTOs and validation
  SmartStore.Domain/          Entities and inventory rules
  SmartStore.Infrastructure/  DbContext, Identity user, mappings, and migrations
tests/
  SmartStore.Tests/           Unit, API, authentication, and PostgreSQL tests
```

The frontend and API share one origin. No Node.js build step or separate frontend server is required.

## Getting Started

Install .NET SDK 10.0.401 (or a later compatible .NET 10 SDK) and PostgreSQL 16+. Start PostgreSQL, then create a `smartstore` user and a `smartstore` database owned by that user using pgAdmin or your preferred PostgreSQL administration tool.

From the repository root:

```powershell
dotnet tool restore
dotnet restore

# Replace YOUR_PASSWORD with your local PostgreSQL password.
dotnet user-secrets set "ConnectionStrings:Store" "Host=localhost;Port=5432;Database=smartstore;Username=smartstore;Password=YOUR_PASSWORD" --project src/SmartStore.Api

# Creates/updates both the store and Identity account tables.
dotnet ef database update --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api
dotnet run --project src/SmartStore.Api --launch-profile http
```

Open **http://localhost:5080** and select **Create account**. Use a password with at least eight characters, including uppercase, lowercase, and a number. Registration signs you in automatically; demo accounts are available through the optional command below.

After granting Admin access as described below, start by creating a category, add a product, receive stock through **Stock**, then use **Sales → New sale** to record an order. Add supplier details as needed. Values are displayed in VND.

- **Web app:** http://localhost:5080
- **OpenAPI JSON:** http://localhost:5080/openapi/v1.json (Development only)
- **Health checks:** `/health/live` and `/health/ready`

You can also set the connection string with `ConnectionStrings__Store`. Keep real credentials out of source control. After pulling updates, apply migrations before starting the app.

## Authentication & API

The application is a **single-store, shared staff workspace**: all accounts work with the same store data. Authentication is required for store endpoints. Registration is open and grants Staff access. Staff can view catalog, inventory history, and orders, and record sales. Admins can also create, edit, and delete catalog records and manually adjust stock.

### Seed demo data

After applying migrations, run this command against your local development database.

```powershell
dotnet run --project src/SmartStore.Api --launch-profile http -- seed-demo
```

The seed creates `admin@smartstore.example` (Admin), `staff@smartstore.example` (Staff), and three stocked products. Newly created demo accounts share the password `SmartStoreDemo123!`. Rerunning the command preserves existing accounts, products, and stock quantities. The command is available only in Development.

### Grant admin access

Register and verify that you can sign in to the account you intend to promote. From the repository root, run the following command with that account's email after applying the migrations.

```powershell
dotnet run --project src/SmartStore.Api --launch-profile http -- grant-admin admin@example.com
```

The command uses the same database configuration as the web app. Sign out and sign in again after promotion to refresh your session.

Admins can open **Accounts** to view users, create Staff accounts, edit display names, and delete Staff accounts. Email and role are read-only when editing. Admin accounts are protected from deletion.

The browser uses an HttpOnly session cookie. For API clients, first GET `/api/auth/csrf`, preserve the returned cookies, and send its `token` in the `X-CSRF-TOKEN` header on POST/PUT/DELETE requests. Register at `/api/auth/register` or sign in at `/api/auth/login`; fetch a fresh CSRF token after sign-in/sign-out. `/api/auth/me` returns the current user and POST `/api/auth/logout` ends the session.

Local HTTP is supported by the Development launch profile. Other environments require HTTPS for authentication cookies. Sales do not yet use idempotency keys; check the order list before retrying a submission after a timeout.

### JWT for API clients

The web UI uses its Identity cookie and CSRF token. API clients can sign in separately with a JWT. The Development launch profile enables JWT automatically with an in-memory signing key. Tokens issued with this temporary key stop working when SmartStore restarts. If you want local tokens to survive restarts, set a signing key in user secrets and restart SmartStore.

```powershell
$keyBytes = [byte[]]::new(32)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($keyBytes)
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($keyBytes)) --project src/SmartStore.Api
```

In Postman, send `POST /api/auth/token` with a JSON body containing `email` and `password`. Copy `accessToken` from the response and use **Authorization → Bearer Token** on requests such as `GET /api/auth/me` or `GET /api/accounts` (Admin only). Successfully authenticated Bearer requests to store APIs do not require a CSRF token. JWTs expire after 15 minutes. Request a new token with your credentials when one expires.

For deployed environments, provide a dedicated Base64 signing key through `Jwt__SigningKey` and use HTTPS. Outside Development, if no signing key is configured, the token endpoint returns `503` and cookie sign-in remains available. API tokens are rejected if the account was deleted, its permissions or security stamp changed, or it is currently locked. Web sign-out clears the browser cookie only.

## EF Core Migrations

Migrations are applied explicitly, rather than automatically on startup:

```powershell
dotnet ef migrations add MigrationName --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api
```

## Testing

Run tests that do not need a running database:

```powershell
dotnet test --filter "Category!=Integration"
```

For all tests, create a separate `smartstore_test` database owned by the `smartstore` user:

```powershell
$env:ConnectionStrings__Store = "Host=localhost;Port=5432;Database=smartstore_test;Username=smartstore;Password=YOUR_PASSWORD"
dotnet test
```

Integration tests apply migrations and write data, so use a dedicated test database. GitHub Actions builds the solution, runs the tests against PostgreSQL, and checks that the EF model matches the migrations.
