using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WalletLedger.Models;
using WalletLedger.Services;

namespace WalletLedger.Pages;

public class IndexModel(LedgerService ledger) : PageModel
{
    public List<AccountDto> Accounts { get; private set; } = [];

    [BindProperty] public string OwnerName { get; set; } = "";
    [BindProperty] public Guid FromId { get; set; }
    [BindProperty] public Guid ToId { get; set; }
    [BindProperty] public decimal Amount { get; set; }
    // A fresh key per page load: refreshing and re-submitting the same form cannot post twice.
    [BindProperty] public string Key { get; set; } = Guid.NewGuid().ToString();

    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync(CancellationToken ct) =>
        Accounts = await ledger.GetAccountsAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        try
        {
            var a = await ledger.CreateAccountAsync(OwnerName, "AED", ct);
            Message = $"Account created for {a.OwnerName}.";
        }
        catch (DomainException ex) { Error = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTransferAsync(CancellationToken ct)
    {
        try
        {
            await ledger.TransferAsync(FromId, ToId, Amount, Key, ct);
            Message = "Transfer completed.";
        }
        catch (DomainException ex) { Error = ex.Message; }
        return RedirectToPage();
    }
}
