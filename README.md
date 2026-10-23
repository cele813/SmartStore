# SmartStore

A store management backend built with C# and ASP.NET Core Web API. SmartStore provides a foundation for managing a product catalog, tracking inventory, and recording sales using PostgreSQL and Entity Framework Core.

## Tech Stack

- **Backend:** .NET 10, ASP.NET Core Web API
- **Database:** PostgreSQL 16+, Entity Framework Core 10, Npgsql
- **Testing:** xUnit, ASP.NET Core integration testing
- **Version Control & CI:** Git, GitHub Actions

## Features

- Create and view categories, suppliers, and products.
- Browse products with pagination.
- Adjust stock levels and track inventory movements.
- Create sales orders with automatic stock deduction and stored sale prices.
- Prevent negative stock and detect concurrent inventory updates.
- Validate requests and return structured error responses.

The project is under development. Authentication, authorization, and a frontend are not implemented yet.

## Project Structure

```text
src/
  SmartStore.Api/             Controllers, HTTP endpoints, and configuration
  SmartStore.Application/     Request DTOs and validation
  SmartStore.Domain/          Entities and inventory rules
  SmartStore.Infrastructure/  EF Core DbContext, mappings, and migrations
tests/
  SmartStore.Tests/           Unit, API, and PostgreSQL integration tests
```

The project uses a layered structure. Domain rules are independent of HTTP and EF Core, while Infrastructure manages persistence. The current API controllers coordinate operations through an injected DbContext.

## Getting Started

Install the .NET 10 SDK (10.0.401 or a later compatible .NET 10 SDK) and PostgreSQL 16 or later. Start PostgreSQL, then create a `smartstore` user and a `smartstore` database owned by that user.

Run the following commands from the repository root:

```powershell
dotnet tool restore
dotnet restore

# Replace YOUR_PASSWORD with your local PostgreSQL password.
dotnet user-secrets set "ConnectionStrings:Store" "Host=localhost;Port=5432;Database=smartstore;Username=smartstore;Password=YOUR_PASSWORD" --project src/SmartStore.Api

# Apply EF Core migrations and start the API.
dotnet ef database update --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api
dotnet run --project src/SmartStore.Api --launch-profile http
```

- **API:** http://localhost:5080
- **OpenAPI JSON:** http://localhost:5080/openapi/v1.json (Development only)
- **Health checks:** `/health/live` and `/health/ready`

You can also provide the connection string through the `ConnectionStrings__Store` environment variable. Keep real credentials out of source control.

## Database Migrations

The database schema is managed through EF Core migrations. After changing the model or its mappings, create and apply a new migration:

```powershell
dotnet ef migrations add MigrationName --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/SmartStore.Infrastructure --startup-project src/SmartStore.Api
```

Migrations are applied explicitly; the API does not apply them automatically on startup.

## Testing

Run unit and API tests without a running database:

```powershell
dotnet test --filter "Category!=Integration"
```

To include PostgreSQL integration tests, create a separate `smartstore_test` database owned by the `smartstore` user:

```powershell
$env:ConnectionStrings__Store = "Host=localhost;Port=5432;Database=smartstore_test;Username=smartstore;Password=YOUR_PASSWORD"
dotnet test
```

Integration tests apply migrations and write test data. Always use a dedicated test database. GitHub Actions builds the solution, runs all tests against PostgreSQL, and checks for pending model changes.
