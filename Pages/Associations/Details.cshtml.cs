using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaderboardService _leaderboardService;

        public DetailsModel(ApplicationDbContext context, ILeaderboardService leaderboardService)
        {
            _context = context;
            _leaderboardService = leaderboardService;
        }

        public GolfAssociation? Association { get; set; }
        public List<SponsorshipPackage> ActiveSponsorshipPackages { get; private set; } = new();
        public List<RecentTournamentLeaderboard> RecentLeaderboards { get; private set; } = new();
        public int UpcomingTournamentCount { get; private set; }
        public List<Tournament> UpcomingTournaments { get; private set; } = new();
        public Tournament? NextTournament { get; private set; }
        public int ActiveMembersCount { get; private set; }
        public int CoursesPlayedCount { get; private set; }
        public RecentTournamentLeaderboard? LatestResult { get; private set; }
        public List<KeyValuePair<string, List<Leaderboard>>> FlightLeaders { get; private set; } = new();
        public List<AssociationLeaderboardRow> SeasonStandings { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await LoadAssociationAsync(id))
                return NotFound();
            return Page();
        }

        private async Task<bool> LoadAssociationAsync(int id)
        {
            // Lean query — excludes Members (identity users), SponsorshipPayments, and Players.
            // Players are not enumerated on this page; we count them with a separate scalar query.
            Association = await _context.GolfAssociations
                .Include(ga => ga.Tournaments)
                .Include(ga => ga.SponsorshipPackages)
                .Include(ga => ga.OfficersAndMembers)
                .Include(ga => ga.MediaItems)
                .Include(ga => ga.Sponsors)
                .Include(ga => ga.Charities)
                .FirstOrDefaultAsync(ga => ga.Id == id && ga.IsActive);

            if (Association == null) return false;

            // Scalar count — avoids loading every AssociationPlayer row
            ActiveMembersCount = await _context.AssociationPlayers
                .CountAsync(p => p.GolfAssociationId == id && p.IsActive);

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Association.ThemeKey);
            ViewData["PublicAssociationLogoUrl"] = Association.LogoUrl;

            ActiveSponsorshipPackages = Association.SponsorshipPackages
                .Where(sp => sp.IsActive)
                .OrderBy(sp => sp.DisplayOrder)
                .ThenByDescending(sp => sp.Amount)
                .ToList();

            var upcoming = Association.Tournaments
                .Where(t => t.StartDate >= DateTime.UtcNow && t.Status != TournamentStatus.Cancelled)
                .OrderBy(t => t.StartDate)
                .ToList();
            UpcomingTournamentCount = upcoming.Count;
            UpcomingTournaments = upcoming.Take(3).ToList();
            NextTournament = upcoming.FirstOrDefault();

            if (NextTournament != null)
            {
                ViewData["NextTournamentName"] = NextTournament.Name;
                ViewData["NextTournamentDate"] = NextTournament.StartDate.ToString("MMMM d, yyyy");
                ViewData["NextTournamentCourse"] = NextTournament.GolfCourse;
                ViewData["NextTournamentLocation"] = NextTournament.Location;
                ViewData["NextTournamentId"] = NextTournament.Id;
            }

            // Run leaderboard queries sequentially — DbContext is not thread-safe;
            // concurrent async operations on the same instance can cause errors.
            RecentLeaderboards = (await _leaderboardService.GetRecentTournamentLeaderboardsAsync(id, 3, 20)).ToList();
            SeasonStandings = (await _leaderboardService.GetAssociationLeaderboardAsync(id)).Take(5).ToList();

            LatestResult = RecentLeaderboards.FirstOrDefault();

            if (LatestResult?.TopEntries.Count > 0)
            {
                // Use the Flights already fetched by GetRecentTournamentLeaderboardsAsync (no extra query).
                var flightOrder = LatestResult.Flights.Select(f => f.Name.Trim()).ToList();

                FlightLeaders = LatestResult.TopEntries
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.Flight) ? "Overall" : e.Flight.Trim())
                    .OrderBy(g =>
                    {
                        // Champ always sorts first regardless of DisplayOrder
                        if (string.Equals(g.Key, "Champ", StringComparison.OrdinalIgnoreCase)) return -1;
                        if (flightOrder.Count > 0)
                        {
                            var i = flightOrder.FindIndex(f => string.Equals(f, g.Key, StringComparison.OrdinalIgnoreCase));
                            if (i >= 0) return i;
                        }
                        return string.Compare(g.Key, "A", StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(g => new KeyValuePair<string, List<Leaderboard>>(g.Key, g.OrderBy(e => e.Position).Take(5).ToList()))
                    .ToList();
            }

            CoursesPlayedCount = Association.Tournaments
                .Select(t => t.GolfCourse)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return true;
        }
    }
}
