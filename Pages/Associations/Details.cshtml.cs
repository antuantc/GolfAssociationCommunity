using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class DetailsModel : PageModel
    {
        private readonly IAssociationService _associationService;
        private readonly ILeaderboardService _leaderboardService;

        public DetailsModel(IAssociationService associationService, ILeaderboardService leaderboardService)
        {
            _associationService = associationService;
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
        public Dictionary<string, List<Leaderboard>> FlightLeaders { get; private set; } = new();
        public List<AssociationLeaderboardRow> SeasonStandings { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await LoadAssociationAsync(id))
                return NotFound();
            return Page();
        }

        private async Task<bool> LoadAssociationAsync(int id)
        {
            Association = await _associationService.GetAssociationByIdAsync(id);
            if (Association == null) return false;

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

            // Fetch enough entries per tournament to cover multiple flights (up to 5 each)
            RecentLeaderboards = (await _leaderboardService.GetRecentTournamentLeaderboardsAsync(id, 3, 20)).ToList();
            LatestResult = RecentLeaderboards.FirstOrDefault();

            if (LatestResult?.TopEntries.Count > 0)
            {
                FlightLeaders = LatestResult.TopEntries
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.Flight) ? "Overall" : e.Flight)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Position).Take(5).ToList());
            }

            ActiveMembersCount = Association.Players.Count(p => p.IsActive);
            CoursesPlayedCount = Association.Tournaments
                .Select(t => t.GolfCourse)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            SeasonStandings = (await _leaderboardService.GetAssociationLeaderboardAsync(id)).Take(5).ToList();

            return true;
        }
    }
}
