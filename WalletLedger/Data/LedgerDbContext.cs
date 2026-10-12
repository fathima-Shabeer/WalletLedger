using Microsoft.EntityFrameworkCore;
using WalletLedger.Domain;

namespace WalletLedger.Data;

public class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public static readonly Guid SystemAccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static Account NewSystemAccount() => new()
    {
        Id = SystemAccountId,
        OwnerName = "SYSTEM-CASH",
        AllowNegative = true
    };

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerTransaction> Transactions => Set<LedgerTransaction>();
    public DbSet<LedgerEntry> Entries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.Property(x => x.OwnerName).HasMaxLength(100);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Balance).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<LedgerTransaction>(e =>
        {
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(200);
            e.HasIndex(x => x.IdempotencyKey).IsUnique();   // the idempotency guarantee
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            e.HasIndex(x => new { x.AccountId, x.Id });
            e.HasOne(x => x.Transaction).WithMany(t => t.Entries).HasForeignKey(x => x.TransactionId);
        });
    }
}
