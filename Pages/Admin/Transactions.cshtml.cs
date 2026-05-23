using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class TransactionsModel : PageModel
    {
        private readonly IAuthorizeNetTransactionService _transactionService;

        public TransactionsModel(IAuthorizeNetTransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        public List<AuthorizeNetTransactionSummary> Transactions { get; private set; } = new();
        public decimal TotalAmount { get; private set; }
        public decimal RefundedAmount { get; private set; }
        public int RefundedCount { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var transactions = await _transactionService.GetTransactionsAsync();

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
