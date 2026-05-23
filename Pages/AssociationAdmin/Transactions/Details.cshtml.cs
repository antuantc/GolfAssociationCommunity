using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.AssociationAdmin.Transactions
{
    public class DetailsModel : AssociationAdminPageModel
    {
        private readonly IAuthorizeNetTransactionService _transactionService;
        private readonly IAdminAuditService _adminAuditService;

        public DetailsModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IAuthorizeNetTransactionService transactionService,
            IAdminAuditService adminAuditService)
            : base(userManager, context)
        {
            _transactionService = transactionService;
            _adminAuditService = adminAuditService;
        }

        public AuthorizeNetTransactionDetail? Transaction { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string SourceType { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty]
        public RefundInput Input { get; set; } = new();

        public class RefundInput
        {
            [Range(typeof(decimal), "0.01", "100000000")]
            public decimal? Amount { get; set; }

            [StringLength(250)]
            public string? Reason { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string sourceType, int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            SourceType = sourceType;
            Id = id;
            await LoadAsync();
            if (Transaction is null)
            {
                return NotFound();
            }

            Input.Amount = Transaction.Amount;
            return Page();
        }

        public async Task<IActionResult> OnPostRefundAsync(string sourceType, int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            SourceType = sourceType;
            Id = id;
            await LoadAsync();
            if (Transaction is null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var refundAmount = Input.Amount ?? Transaction.Amount;
            if (refundAmount > Transaction.Amount)
            {
                ModelState.AddModelError(nameof(Input.Amount), "Refund amount cannot exceed the original transaction amount.");
                return Page();
            }

            var refundResult = await _transactionService.RefundTransactionAsync(SourceType, Id, refundAmount, CurrentAssociation.Id);
            if (!refundResult.Succeeded)
            {
                TempData["ErrorMessage"] = refundResult.ErrorMessage ?? "Refund failed.";
                await AuditAsync("Failed refund transaction", refundResult.TransactionId, refundAmount);
                return RedirectToPage(new { sourceType = SourceType, id = Id });
            }

            TempData["SuccessMessage"] = $"Refund processed successfully. Refund reference: {BuildReference(refundResult.TransactionId)}.";
            await AuditAsync("Refunded transaction", refundResult.TransactionId, refundAmount);
            return RedirectToPage(new { sourceType = SourceType, id = Id });
        }

        private async Task LoadAsync()
        {
            Transaction = await _transactionService.GetTransactionAsync(SourceType, Id, CurrentAssociation.Id);
        }

        private async Task AuditAsync(string action, string? refundTransactionId, decimal refundAmount)
        {
            await _adminAuditService.WriteAsync(action, User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
            {
                ["SourceType"] = SourceType,
                ["SourceId"] = Id.ToString(),
                ["RefundTransactionId"] = refundTransactionId,
                ["RefundAmount"] = refundAmount.ToString("0.00")
            });
        }

        private static string BuildReference(string? transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return "unavailable";
            }

            var trimmed = transactionId.Trim();
            return trimmed.Length <= 4 ? trimmed : $"****{trimmed[^4..]}";
        }
    }
}
