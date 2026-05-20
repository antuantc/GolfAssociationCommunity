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
    public class TournamentsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ITournamentService _tournamentService;
        private readonly IAdminAuditService _adminAuditService;

        public TournamentsModel(
            ApplicationDbContext context,
            ITournamentService tournamentService,
            IAdminAuditService adminAuditService)
        {
            _context = context;
            _tournamentService = tournamentService;
            _adminAuditService = adminAuditService;
        }

        public List<TournamentRow> Tournaments { get; private set; } = new();
        public int PageSize { get; } = 20;
        public int PageNumber { get; private set; } = 1;
        public int TotalPages { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public TournamentStatus? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public class TournamentRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string AssociationName { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public TournamentStatus Status { get; set; }
            public int RegisteredCount { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostSetStatusAsync(int id, TournamentStatus status)
        {
            var changed = await _tournamentService.UpdateTournamentStatusAsync(id, status);
            TempData["SuccessMessage"] = changed
                ? "Tournament status updated."
                : "Tournament not found.";

            await AuditAsync(changed ? "Updated tournament status" : "Failed to update tournament status", id, status.ToString());

            return RedirectToPage(GetStateRouteValues());
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var deleted = await _tournamentService.DeleteTournamentAsync(id);
            TempData["SuccessMessage"] = deleted
                ? "Tournament deleted."
                : "Tournament not found.";

            await AuditAsync(deleted ? "Deleted tournament" : "Failed to delete tournament", id, null);

            return RedirectToPage(GetStateRouteValues());
        }

        private async Task LoadAsync()
        {
            var query = _context.Tournaments
                .AsNoTracking()
                .Include(t => t.GolfAssociation)
                .Select(t => new TournamentRow
                {
                    Id = t.Id,
                    Name = t.Name,
                    AssociationName = t.GolfAssociation != null ? t.GolfAssociation.Name : "-",
                    StartDate = t.StartDate,
                    Status = t.Status,
                    RegisteredCount = t.Registrations.Count(r => r.Status == RegistrationStatus.Registered)
                })
                .OrderByDescending(t => t.StartDate)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                query = query.Where(t =>
                    t.Name.Contains(term) ||
                    t.AssociationName.Contains(term));
            }

            if (StatusFilter.HasValue)
            {
                query = query.Where(t => t.Status == StatusFilter.Value);
            }

            var totalCount = await query.CountAsync();
            PageNumber = CurrentPage < 1 ? 1 : CurrentPage;
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);
            if (PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
            }

            Tournaments = await query
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        private object GetStateRouteValues()
        {
            var search = Search ?? Request.Query["Search"].ToString();
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
                CurrentPage = pageValue
            };
        }

        private async Task AuditAsync(string action, int tournamentId, string? status)
        {
            await _adminAuditService.WriteAsync(action, User?.Identity?.Name ?? "anonymous", new Dictionary<string, string?>
            {
                ["TournamentId"] = tournamentId.ToString(),
                ["Status"] = status
            });
        }
    }
}
