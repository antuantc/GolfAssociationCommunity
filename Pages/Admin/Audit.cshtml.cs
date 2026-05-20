using System.Globalization;
using System.Text;
using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AuditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AuditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<AdminAuditEvent> Entries { get; private set; } = new();
        public List<string> AvailableActions { get; private set; } = new();
        public int PageSize { get; } = 50;
        public int PageNumber { get; private set; } = 1;
        public int TotalPages { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ActorFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ActionFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DateTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            var query = BuildFilteredQuery();
            var rows = await query
                .OrderByDescending(e => e.AtUtc)
                .Take(10000)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("AtUtc,Action,Actor,Details");

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",",
                    Csv(row.AtUtc.ToString("o", CultureInfo.InvariantCulture)),
                    Csv(row.Action),
                    Csv(row.Actor),
                    Csv(row.Details)));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"admin-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        private async Task LoadAsync()
        {
            AvailableActions = await _context.AdminAuditEvents
                .AsNoTracking()
                .Select(e => e.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            var query = BuildFilteredQuery();
            var totalCount = await query.CountAsync();

            PageNumber = CurrentPage < 1 ? 1 : CurrentPage;
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);
            if (PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
            }

            Entries = await query
                .OrderByDescending(e => e.AtUtc)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        private IQueryable<AdminAuditEvent> BuildFilteredQuery()
        {
            var query = _context.AdminAuditEvents.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                query = query.Where(e =>
                    EF.Functions.Like(e.Action, $"%{term}%") ||
                    EF.Functions.Like(e.Actor, $"%{term}%") ||
                    EF.Functions.Like(e.Details, $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(ActorFilter))
            {
                var actor = ActorFilter.Trim();
                query = query.Where(e => EF.Functions.Like(e.Actor, $"%{actor}%"));
            }

            if (!string.IsNullOrWhiteSpace(ActionFilter))
            {
                query = query.Where(e => e.Action == ActionFilter.Trim());
            }

            if (DateTime.TryParse(DateFrom, out var fromDate))
            {
                query = query.Where(e => e.AtUtc >= fromDate.Date.ToUniversalTime());
            }

            if (DateTime.TryParse(DateTo, out var toDate))
            {
                var endDate = toDate.Date.AddDays(1).ToUniversalTime();
                query = query.Where(e => e.AtUtc < endDate);
            }

            return query;
        }

        private static string Csv(string value)
        {
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}
