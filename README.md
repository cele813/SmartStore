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

Open **http://localhost:5080** and select **Create account**. Use a password with at least eight characters, including uppercase, lowercase, and a number. Registration signs you in automatically; there is no default account or seeded password.

Start by creating a category, add a product, receive stock through **Stock**, then use **Sales → New sale** to record an order. Add supplier details as needed. Values are displayed in VND.

- **Web app:** http://localhost:5080
- **OpenAPI JSON:** http://localhost:5080/openapi/v1.json (Development only)
- **Health checks:** `/health/live` and `/health/ready`

You can also set the connection string with `ConnectionStrings__Store`. Keep real credentials out of source control. After pulling updates, apply migrations before starting the app.

## Authentication & API

The application is a **single-store, shared staff workspace**: every registered account can access and manage the same store data. Authentication is required for store endpoints. Registration is open; role-based permissions, invitations, email verification, and password recovery can be added as extensions.

The browser uses an HttpOnly session cookie. For API clients, first GET `/api/auth/csrf`, preserve the returned cookies, and send its `token` in the `X-CSRF-TOKEN` header on POST/PUT/DELETE requests. Register at `/api/auth/register` or sign in at `/api/auth/login`; fetch a fresh CSRF token after sign-in/sign-out. `/api/auth/me` returns the current user and POST `/api/auth/logout` ends the session.

Local HTTP is supported by the Development launch profile. Other environments require HTTPS for authentication cookies. Sales do not yet use idempotency keys; check the order list before retrying a submission after a timeout.

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
