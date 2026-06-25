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
    public class PlayersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IAdminAuditService _adminAuditService;

        public PlayersModel(ApplicationDbContext context, IAdminAuditService adminAuditService)
        {
            _context = context;
            _adminAuditService = adminAuditService;
        }

        public List<PlayerGroupRow> Players { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public class PlayerAssociationRow
        {
            public int Id { get; set; }
            public int AssociationId { get; set; }
            public string AssociationName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }

        public class PlayerGroupRow
        {
            public string EmailKey { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public int AssociationCount { get; set; }
            public int ActiveCount { get; set; }
            public int ArchivedCount { get; set; }
            public int RegistrationCount { get; set; }
            public int ScoreCount { get; set; }
            public int LeaderboardCount { get; set; }
            public DateTime? LastUpdatedAt { get; set; }
            public List<PlayerAssociationRow> Associations { get; set; } = new();
        }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnGetDownloadCsvAsync()
        {
            await LoadAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Name,Email,Associations,Active,Archived,Registrations,Scores,Last Updated");

            foreach (var p in Players)
            {
                var assocNames = string.Join("; ", p.Associations.Select(a => a.AssociationName));
                sb.AppendLine(
                    $"{CsvEscape(p.DisplayName)}," +
                    $"{CsvEscape(p.Email)}," +
                    $"{CsvEscape(assocNames)}," +
                    $"{p.ActiveCount}," +
                    $"{p.ArchivedCount}," +
                    $"{p.RegistrationCount}," +
                    $"{p.ScoreCount}," +
                    $"{p.LastUpdatedAt?.ToString("yyyy-MM-dd") ?? string.Empty}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var filename = $"players-{DateTime.UtcNow:yyyy-MM-dd}.csv";
            return File(bytes, "text/csv", filename);
        }

        private static string CsvEscape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        public async Task<IActionResult> OnPostDeleteAsync(string emailKey)
        {
            if (string.IsNullOrWhiteSpace(emailKey))
            {
                TempData["SuccessMessage"] = "Player not found.";
                return RedirectToPage(GetStateRouteValues());
            }

            var normalizedEmail = emailKey.Trim().ToUpperInvariant();
            var players = await _context.AssociationPlayers
                .Where(player => player.Email.ToUpper() == normalizedEmail)
                .ToListAsync();

            if (players.Count == 0)
            {
                TempData["SuccessMessage"] = "Player not found.";
                return RedirectToPage(GetStateRouteValues());
            }

            var associationNames = await _context.GolfAssociations
                .Where(association => players.Select(player => player.GolfAssociationId).Contains(association.Id))
                .Select(association => association.Name)
                .ToListAsync();

            _context.AssociationPlayers.RemoveRange(players);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Deleted player from {players.Count} association(s).";
            await _adminAuditService.WriteAsync(
                "Deleted global player",
                User?.Identity?.Name ?? "anonymous",
                new Dictionary<string, string?>
                {
                    ["Email"] = emailKey,
                    ["AssociationCount"] = players.Count.ToString(),
                    ["Associations"] = string.Join(", ", associationNames)
                });

            return RedirectToPage(GetStateRouteValues());
        }

        public async Task<IActionResult> OnPostDeleteSelectedAsync(List<string> emailKeys)
        {
            if (emailKeys == null || emailKeys.Count == 0)
            {
                TempData["SuccessMessage"] = "No players selected.";
                return RedirectToPage(GetStateRouteValues());
            }

            var normalizedKeys = emailKeys.Select(k => k.Trim().ToUpperInvariant()).ToList();
            var players = await _context.AssociationPlayers
                .Where(p => normalizedKeys.Contains(p.Email.ToUpper()))
                .ToListAsync();

            if (players.Count == 0)
            {
                TempData["SuccessMessage"] = "No matching players found.";
                return RedirectToPage(GetStateRouteValues());
            }

            _context.AssociationPlayers.RemoveRange(players);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Deleted {players.Count} player record(s) across {normalizedKeys.Count} selected player(s).";
            await _adminAuditService.WriteAsync(
                "Bulk deleted players",
                User?.Identity?.Name ?? "anonymous",
                new Dictionary<string, string?>
                {
                    ["Count"] = players.Count.ToString(),
                    ["EmailKeys"] = string.Join(", ", normalizedKeys)
                });

            return RedirectToPage(GetStateRouteValues());
        }

        private async Task LoadAsync()
        {
            var query = _context.AssociationPlayers
                .AsNoTracking()
                .Include(player => player.GolfAssociation)
                .AsQueryable();

            var rows = await query
                .Select(player => new
                {
                    player.Id,
                    player.GolfAssociationId,
                    AssociationName = player.GolfAssociation != null ? player.GolfAssociation.Name : "-",
                    player.DisplayName,
                    player.Email,
                    player.IsActive,
                    player.UpdatedAt
                })
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                rows = rows.Where(row =>
                    row.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    row.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    row.AssociationName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var grouped = rows
                .GroupBy(row => row.Email.Trim().ToUpperInvariant())
                .Select(group => new PlayerGroupRow
                {
                    EmailKey = group.Key,
                    DisplayName = group.OrderByDescending(row => row.IsActive).ThenByDescending(row => row.UpdatedAt).Select(row => row.DisplayName).FirstOrDefault() ?? "Unknown Player",
                    Email = group.OrderByDescending(row => row.IsActive).ThenByDescending(row => row.UpdatedAt).Select(row => row.Email).FirstOrDefault() ?? string.Empty,
                    AssociationCount = group.Count(),
                    ActiveCount = group.Count(row => row.IsActive),
                    ArchivedCount = group.Count(row => !row.IsActive),
                    LastUpdatedAt = group.Max(row => row.UpdatedAt),
                    Associations = group
                        .OrderBy(row => row.AssociationName)
                        .Select(row => new PlayerAssociationRow
                        {
                            Id = row.Id,
                            AssociationId = row.GolfAssociationId,
                            AssociationName = row.AssociationName,
                            IsActive = row.IsActive
                        })
                        .ToList()
                })
                .OrderBy(row => row.DisplayName)
                .ThenBy(row => row.Email)
                .ToList();

            var playerIds = grouped.SelectMany(group => group.Associations.Select(association => association.Id)).ToList();
            if (playerIds.Count == 0)
            {
                Players = grouped;
                return;
            }

            var registrationCounts = await _context.Registrations
                .Where(registration => registration.AssociationPlayerId != null && playerIds.Contains(registration.AssociationPlayerId.Value))
                .GroupBy(registration => registration.AssociationPlayerId!.Value)
                .Select(group => new { PlayerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.PlayerId, item => item.Count);

            var scoreCounts = await _context.PlayerScores
                .Where(score => playerIds.Contains(score.AssociationPlayerId))
                .GroupBy(score => score.AssociationPlayerId)
                .Select(group => new { PlayerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.PlayerId, item => item.Count);

            var leaderboardCounts = await _context.Leaderboards
                .Where(leaderboard => playerIds.Contains(leaderboard.AssociationPlayerId))
                .GroupBy(leaderboard => leaderboard.AssociationPlayerId)
                .Select(group => new { PlayerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.PlayerId, item => item.Count);

            foreach (var group in grouped)
            {
                var ids = group.Associations.Select(association => association.Id).ToList();
                group.RegistrationCount = ids.Sum(id => registrationCounts.GetValueOrDefault(id));
                group.ScoreCount = ids.Sum(id => scoreCounts.GetValueOrDefault(id));
                group.LeaderboardCount = ids.Sum(id => leaderboardCounts.GetValueOrDefault(id));
            }

            Players = grouped;
        }

        private object GetStateRouteValues()
        {
            return new { Search };
        }
    }
}
