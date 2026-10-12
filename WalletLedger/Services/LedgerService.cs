using Microsoft.EntityFrameworkCore;
using WalletLedger.Data;
using WalletLedger.Domain;
using WalletLedger.Models;

namespace WalletLedger.Services;

public class DomainException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

public class LedgerService(LedgerDbContext db)
{
    public async Task<Account> CreateAccountAsync(string owner, string currency, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new DomainException("Owner name is required.");

        var account = new Account { OwnerName = owner.Trim(), Currency = currency.ToUpperInvariant() };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    public Task<LedgerTransaction> DepositAsync(Guid accountId, decimal amount, string key, CancellationToken ct) =>
        PostAsync(EntryType.Deposit, LedgerDbContext.SystemAccountId, accountId, amount, key, "Deposit", ct);

    public Task<LedgerTransaction> WithdrawAsync(Guid accountId, decimal amount, string key, CancellationToken ct) =>
        PostAsync(EntryType.Withdrawal, accountId, LedgerDbContext.SystemAccountId, amount, key, "Withdrawal", ct);

    public Task<LedgerTransaction> TransferAsync(Guid fromId, Guid toId, decimal amount, string key, CancellationToken ct) =>
        PostAsync(EntryType.Transfer, fromId, toId, amount, key, "Transfer", ct);

    // One balanced posting: debit 'from', credit 'to', both entries in one DB transaction.
    private async Task<LedgerTransaction> PostAsync(
        EntryType type, Guid fromId, Guid toId, decimal amount, string key, string description, CancellationToken ct)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new DomainException("Amount must be positive with at most 2 decimal places.");
        if (fromId == toId)
            throw new DomainException("Source and destination accounts must differ.");
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("An idempotency key is required.");

        // Replay of an already-processed request returns the original result.
        var existing = await db.Transactions.Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.IdempotencyKey == key, ct);
        if (existing is not null) return existing;

        var accounts = await db.Accounts.Where(a => a.Id == fromId || a.Id == toId).ToListAsync(ct);
        var from = accounts.SingleOrDefault(a => a.Id == fromId)
            ?? throw new DomainException("Source account not found.", 404);
        var to = accounts.SingleOrDefault(a => a.Id == toId)
            ?? throw new DomainException("Destination account not found.", 404);

        if (from.Currency != to.Currency)
            throw new DomainException("Currency mismatch between accounts.");
        if (!from.AllowNegative && from.Balance < amount)
            throw new DomainException("Insufficient funds.", 409);

        from.Balance -= amount;
        to.Balance += amount;

        var tx = new LedgerTransaction
        {
            Type = type,
            IdempotencyKey = key,
            Description = description,
            Entries =
            {
                new LedgerEntry { AccountId = from.Id, Amount = -amount, BalanceAfter = from.Balance },
                new LedgerEntry { AccountId = to.Id, Amount = amount, BalanceAfter = to.Balance }
            }
        };
        db.Transactions.Add(tx);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("An account was modified by another request. Please retry.", 409);
        }
        catch (DbUpdateException)
        {
            // e.g. unique index hit by a parallel request with the same idempotency key
            throw new DomainException("Conflicting or duplicate request. Please retry.", 409);
        }

        return tx;
    }

    public Task<List<AccountDto>> GetAccountsAsync(CancellationToken ct) =>
        db.Accounts.AsNoTracking()
            .Where(a => a.Id != LedgerDbContext.SystemAccountId)
            .OrderBy(a => a.OwnerName)
            .Select(a => new AccountDto(a.Id, a.OwnerName, a.Currency, a.Balance, a.CreatedUtc))
            .ToListAsync(ct);

    public Task<AccountDto?> GetAccountAsync(Guid id, CancellationToken ct) =>
        db.Accounts.AsNoTracking()
            .Where(a => a.Id == id && a.Id != LedgerDbContext.SystemAccountId)
            .Select(a => new AccountDto(a.Id, a.OwnerName, a.Currency, a.Balance, a.CreatedUtc))
            .FirstOrDefaultAsync(ct);

    public Task<List<StatementLine>> GetStatementAsync(Guid accountId, int take, CancellationToken ct) =>
        db.Entries.AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.Id)
            .Take(take)
            .Select(e => new StatementLine(e.CreatedUtc, e.Transaction.Type, e.Transaction.Description, e.Amount, e.BalanceAfter))
            .ToListAsync(ct);
}
