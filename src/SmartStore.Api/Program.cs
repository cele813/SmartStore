using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartStore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<StoreDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Store") ?? throw new InvalidOperationException("Configure ConnectionStrings:Store using user-secrets or environment variables.")));
var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = exception switch
    {
        ArgumentException => (400, exception.Message),
        OverflowException => (400, "Quantity is out of range."),
        DbUpdateConcurrencyException => (409, "Stock changed. Reload and retry."),
        DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => (409, "SKU or category name already exists."),
        DbUpdateException { InnerException: PostgresException { SqlState: "23503" } } => (409, "Referenced record is missing or still in use."),
        _ => (500, "An unexpected error occurred.")
    };
    await Results.Problem(statusCode: status, title: title).ExecuteAsync(context);
}));
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (StoreDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.Run();
public partial class Program;
