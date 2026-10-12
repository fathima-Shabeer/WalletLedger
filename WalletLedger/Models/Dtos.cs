using WalletLedger.Domain;

namespace WalletLedger.Models;

public record AccountDto(Guid Id, string OwnerName, string Currency, decimal Balance, DateTime CreatedUtc)
{
    public static AccountDto From(Account a) => new(a.Id, a.OwnerName, a.Currency, a.Balance, a.CreatedUtc);
}

public record TransactionDto(Guid Id, string Type, decimal Amount, DateTime CreatedUtc)
{
    public static TransactionDto From(LedgerTransaction t) =>
        new(t.Id, t.Type.ToString(), t.Entries.Max(e => e.Amount), t.CreatedUtc);
}

public record StatementLine(DateTime CreatedUtc, EntryType Type, string? Description, decimal Amount, decimal BalanceAfter);

public record CreateAccountRequest(string OwnerName, string? Currency);
public record MoneyRequest(decimal Amount);
public record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount);

