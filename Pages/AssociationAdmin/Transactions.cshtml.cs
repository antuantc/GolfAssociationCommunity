using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class TransactionsModel : AssociationAdminPageModel
    {
        private readonly IAuthorizeNetTransactionService _transactionService;

        public TransactionsModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IAuthorizeNetTransactionService transactionService)
            : base(userManager, context)
        {
            _transactionService = transactionService;
        }

        public List<AuthorizeNetTransactionSummary> Transactions { get; private set; } = new();
        public decimal TotalAmount { get; private set; }
        public decimal RefundedAmount { get; private set; }
        public int RefundedCount { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            await LoadAsync();
            return Page();
        }

        private async Task LoadAsync()
        {
            var transactions = await _transactionService.GetTransactionsAsync(CurrentAssociation.Id);

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                transactions = transactions.Where(transaction =>
                    transaction.TransactionId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    transaction.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    transaction.CustomerEmail.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    transaction.AssociationName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    transaction.SourceLabel.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    transaction.StatusLabel.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            Transactions = transactions;
            TotalAmount = transactions.Sum(transaction => transaction.Amount);
            RefundedAmount = transactions.Where(transaction => transaction.RefundAmount.HasValue).Sum(transaction => transaction.RefundAmount ?? 0m);
            RefundedCount = transactions.Count(transaction => transaction.IsRefunded);
        }
    }
}
