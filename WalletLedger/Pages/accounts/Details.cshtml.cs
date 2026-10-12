using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WalletLedger.Domain;
using WalletLedger.Models;
using WalletLedger.Services;

namespace WalletLedger.Pages.Accounts;

public class DetailsModel(LedgerService ledger) : PageModel
{
    public AccountDto Account { get; private set; } = default!;
    public List<StatementLine> Statement { get; private set; } = [];

    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public string Key { get; set; } = Guid.NewGuid().ToString();

    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var account = await ledger.GetAccountAsync(id, ct);
        if (account is null) return NotFound();

        Account = account;
        Statement = await ledger.GetStatementAsync(id, 50, ct);
        return Page();
    }

    public Task<IActionResult> OnPostDepositAsync(Guid id, CancellationToken ct) =>
        RunAsync(id, () => ledger.DepositAsync(id, Amount, Key, ct), "Deposit posted.");

    public Task<IActionResult> OnPostWithdrawAsync(Guid id, CancellationToken ct) =>
        RunAsync(id, () => ledger.WithdrawAsync(id, Amount, Key, ct), "Withdrawal posted.");

    private async Task<IActionResult> RunAsync(Guid id, Func<Task<LedgerTransaction>> action, string success)
    {
        try
        {
            await action();
            Message = success;
        }
        catch (DomainException ex) { Error = ex.Message; }
        return RedirectToPage(new { id });
    }
}
