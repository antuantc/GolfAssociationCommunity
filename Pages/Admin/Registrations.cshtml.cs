using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class RegistrationsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IRegistrationService _registrationService;
        private readonly IAdminAuditService _adminAuditService;

        public RegistrationsModel(
            ApplicationDbContext context,
            IRegistrationService registrationService,
            IAdminAuditService adminAuditService)
        {
            _context = context;
            _registrationService = registrationService;
            _adminAuditService = adminAuditService;
        }

        public List<RegistrationRow> Registrations { get; private set; } = new();
        public int PageSize { get; } = 20;
        public int PageNumber { get; private set; } = 1;
        public int TotalPages { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public RegistrationStatus? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PaymentFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public class RegistrationRow
        {
            public int Id { get; set; }
            public string PlayerEmail { get; set; } = string.Empty;
            public string TournamentName { get; set; } = string.Empty;
            public string AssociationName { get; set; } = string.Empty;
            public DateTime RegistrationDate { get; set; }
            public RegistrationStatus Status { get; set; }
            public bool PaymentConfirmed { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostConfirmPaymentAsync(int id)
        {
            var confirmed = await _registrationService.ConfirmPaymentAsync(id, $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}");
            TempData["SuccessMessage"] = confirmed
                ? "Payment confirmed and registration marked as Registered."
                : "Registration not found.";

            await AuditAsync(confirmed ? "Confirmed payment" : "Failed to confirm payment", id, null);

            return RedirectToPage(GetStateRouteValues());
        }

        public async Task<IActionResult> OnPostWithdrawAsync(int id)
        {
            var withdrawn = await _registrationService.WithdrawRegistrationAsync(id, "Withdrawn by administrator");
            TempData["SuccessMessage"] = withdrawn
                ? "Registration withdrawn."
                : "Registration not found.";

            await AuditAsync(withdrawn ? "Withdrew registration" : "Failed to withdraw registration", id, null);

            return RedirectToPage(GetStateRouteValues());
        }

        public async Task<IActionResult> OnPostSetStatusAsync(int id, RegistrationStatus status)
        {
            var registration = await _context.Registrations.FindAsync(id);
            if (registration is null)
            {
                TempData["SuccessMessage"] = "Registration not found.";
                await AuditAsync("Failed to update registration status", id, status.ToString());
                return RedirectToPage(GetStateRouteValues());
            }

            registration.Status = status;
            registration.UpdatedAt = DateTime.UtcNow;

            if (status == RegistrationStatus.Registered)
            {
                registration.PaymentConfirmed = true;
                registration.PaymentDate ??= DateTime.UtcNow;
                registration.AuthorizeNetTransactionId ??= $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}";
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Registration status updated.";
            await AuditAsync("Updated registration status", id, status.ToString());
            return RedirectToPage(GetStateRouteValues());
        }

        private async Task LoadAsync()
        {
            var query = _context.Registrations
                .AsNoTracking()
                .Include(r => r.Player)
                .Include(r => r.Tournament)
                    .ThenInclude(t => t!.GolfAssociation)
                .OrderByDescending(r => r.RegistrationDate)
                .Select(r => new RegistrationRow
                {
                    Id = r.Id,
                    PlayerEmail = r.Player != null ? (r.Player.Email ?? r.Player.Id) : r.PlayerId,
                    TournamentName = r.Tournament != null ? r.Tournament.Name : "-",
                    AssociationName = r.Tournament != null && r.Tournament.GolfAssociation != null
                        ? r.Tournament.GolfAssociation.Name
                        : "-",
                    RegistrationDate = r.RegistrationDate,
                    Status = r.Status,
                    PaymentConfirmed = r.PaymentConfirmed
                })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                query = query.Where(r =>
                    r.PlayerEmail.Contains(term) ||
                    r.TournamentName.Contains(term) ||
                    r.AssociationName.Contains(term));
            }

            if (StatusFilter.HasValue)
            {
                query = query.Where(r => r.Status == StatusFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(PaymentFilter))
            {
                query = PaymentFilter switch
                {
                    "Confirmed" => query.Where(r => r.PaymentConfirmed),
                    "Pending" => query.Where(r => !r.PaymentConfirmed),
                    _ => query
                };
            }

            var totalCount = await query.CountAsync();
            PageNumber = CurrentPage < 1 ? 1 : CurrentPage;
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);
            if (PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
            }

            Registrations = await query
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        private object GetStateRouteValues()
        {
            var search = Search ?? Request.Query["Search"].ToString();
            var paymentFilter = PaymentFilter ?? Request.Query["PaymentFilter"].ToString();

            var pageValue = CurrentPage;
            if (pageValue < 1 && int.TryParse(Request.Query["CurrentPage"], out var parsedPage) && parsedPage > 0)
            {
                pageValue = parsedPage;
            }

            if (pageValue < 1)
            {
                pageValue = 1;
            }

            return new
            {
                Search = search,
                StatusFilter,
                PaymentFilter = paymentFilter,
                CurrentPage = pageValue
            };
        }

        private async Task AuditAsync(string action, int registrationId, string? status)
        {
            await _adminAuditService.WriteAsync(action, User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
            {
                ["RegistrationId"] = registrationId.ToString(),
                ["Status"] = status
            });
        }
    }
}
