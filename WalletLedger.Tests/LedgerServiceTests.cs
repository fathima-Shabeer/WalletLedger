using Microsoft.EntityFrameworkCore;
using WalletLedger.Data;
using WalletLedger.Services;
using Xunit;

namespace WalletLedger.Tests;

public class LedgerServiceTests
{
    private static (LedgerService Svc, LedgerDbContext Db) Create()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new LedgerDbContext(options);
        db.Accounts.Add(LedgerDbContext.NewSystemAccount());
        db.SaveChanges();
        return (new LedgerService(db), db);
    }

    [Fact]
    public async Task Deposit_then_transfer_updates_balances_and_entries_sum_to_zero()
    {
        var (svc, db) = Create();
        var alice = await svc.CreateAccountAsync("Alice", "AED", default);
        var bob = await svc.CreateAccountAsync("Bob", "AED", default);

        await svc.DepositAsync(alice.Id, 100m, "k1", default);
        await svc.TransferAsync(alice.Id, bob.Id, 40m, "k2", default);

        Assert.Equal(60m, (await svc.GetAccountAsync(alice.Id, default))!.Balance);
        Assert.Equal(40m, (await svc.GetAccountAsync(bob.Id, default))!.Balance);
        Assert.Equal(0m, await db.Entries.SumAsync(e => e.Amount));
    }

    [Fact]
    public async Task Transfer_with_insufficient_funds_is_rejected()
    {
        var (svc, _) = Create();
        var alice = await svc.CreateAccountAsync("Alice", "AED", default);
        var bob = await svc.CreateAccountAsync("Bob", "AED", default);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.TransferAsync(alice.Id, bob.Id, 10m, "k1", default));

        Assert.Equal(409, ex.Status);
    }

    [Fact]
    public async Task Same_idempotency_key_posts_only_once()
    {
        var (svc, _) = Create();
        var alice = await svc.CreateAccountAsync("Alice", "AED", default);

        var first = await svc.DepositAsync(alice.Id, 50m, "same-key", default);
        var second = await svc.DepositAsync(alice.Id, 50m, "same-key", default);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(50m, (await svc.GetAccountAsync(alice.Id, default))!.Balance);
    }
}
