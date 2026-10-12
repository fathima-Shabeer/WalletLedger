namespace WalletLedger.Domain;

public enum EntryType { Deposit, Withdrawal, Transfer }

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerName { get; set; } = default!;
    public string Currency { get; set; } = "AED";
    public decimal Balance { get; set; }
    public bool AllowNegative { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public byte[]? RowVersion { get; set; }   // optimistic concurrency
}

public class LedgerTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public EntryType Type { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<LedgerEntry> Entries { get; set; } = [];
}

public class LedgerEntry
{
    public long Id { get; set; }
    public Guid TransactionId { get; set; }
    public LedgerTransaction Transaction { get; set; } = default!;
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }        // positive = credit, negative = debit
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
