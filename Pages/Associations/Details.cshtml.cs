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

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await LoadAssociationAsync(id))
            {
                return NotFound();
            }
            return Page();
        }

        private async Task<bool> LoadAssociationAsync(int id)
        {
            Association = await _associationService.GetAssociationByIdAsync(id);
            if (Association == null)
            {
                return false;
            }

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Association.ThemeKey);
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
            RecentLeaderboards = (await _leaderboardService.GetRecentTournamentLeaderboardsAsync(id, 3, 5)).ToList();
            return true;
        }
    }
}
