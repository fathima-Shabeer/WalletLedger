using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletLedger.Data;
using WalletLedger.Models;
using WalletLedger.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LedgerDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<LedgerService>();
builder.Services.AddRazorPages();

var app = builder.Build();

// Create the database and the hidden system account on first start.
// Replace EnsureCreated with EF Core migrations before a real deployment.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
    db.Database.EnsureCreated();
    if (!await db.Accounts.AnyAsync(a => a.Id == LedgerDbContext.SystemAccountId))
    {
        db.Accounts.Add(LedgerDbContext.NewSystemAccount());
        await db.SaveChangesAsync();
    }
}

// Turn business-rule errors into clean JSON responses for the API.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (DomainException ex) when (ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.StatusCode = ex.Status;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.UseStaticFiles();
app.UseRouting();

static string RequireKey(string? key) =>
    string.IsNullOrWhiteSpace(key) ? throw new DomainException("The Idempotency-Key header is required.") : key;

var api = app.MapGroup("/api");

api.MapPost("/accounts", async (CreateAccountRequest r, LedgerService s, CancellationToken ct) =>
{
    var a = await s.CreateAccountAsync(r.OwnerName, r.Currency ?? "AED", ct);
    return Results.Created($"/api/accounts/{a.Id}", AccountDto.From(a));
});

api.MapGet("/accounts", async (LedgerService s, CancellationToken ct) =>
    Results.Ok(await s.GetAccountsAsync(ct)));

api.MapGet("/accounts/{id:guid}", async (Guid id, LedgerService s, CancellationToken ct) =>
    await s.GetAccountAsync(id, ct) is { } a ? Results.Ok(a) : Results.NotFound());

api.MapGet("/accounts/{id:guid}/statement", async (Guid id, LedgerService s, CancellationToken ct, int take = 50) =>
    Results.Ok(await s.GetStatementAsync(id, Math.Clamp(take, 1, 200), ct)));

api.MapPost("/accounts/{id:guid}/deposit", async (Guid id, MoneyRequest r,
    [FromHeader(Name = "Idempotency-Key")] string? key, LedgerService s, CancellationToken ct) =>
    Results.Ok(TransactionDto.From(await s.DepositAsync(id, r.Amount, RequireKey(key), ct))));

api.MapPost("/accounts/{id:guid}/withdraw", async (Guid id, MoneyRequest r,
    [FromHeader(Name = "Idempotency-Key")] string? key, LedgerService s, CancellationToken ct) =>
    Results.Ok(TransactionDto.From(await s.WithdrawAsync(id, r.Amount, RequireKey(key), ct))));

api.MapPost("/transfers", async (TransferRequest r,
    [FromHeader(Name = "Idempotency-Key")] string? key, LedgerService s, CancellationToken ct) =>
    Results.Ok(TransactionDto.From(await s.TransferAsync(r.FromAccountId, r.ToAccountId, r.Amount, RequireKey(key), ct))));

app.MapRazorPages();

app.Run();
