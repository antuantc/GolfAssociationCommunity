using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class LeaderboardModel : PageModel
    {
        private readonly IAssociationService _associationService;
        private readonly ITournamentService _tournamentService;
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardModel(
            IAssociationService associationService,
            ITournamentService tournamentService,
            ILeaderboardService leaderboardService)
        {
            _associationService = associationService;
            _tournamentService = tournamentService;
            _leaderboardService = leaderboardService;
        }

        public GolfAssociation? Association { get; private set; }
        public List<Tournament> Tournaments { get; private set; } = new();
        public Tournament? SelectedTournament { get; private set; }
        public List<AssociationLeaderboardRow> AssociationLeaderboard { get; private set; } = new();
        public List<Leaderboard> TournamentLeaderboard { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? TournamentId { get; set; }

        public async Task<IActionResult> OnGetAsync(int associationId)
        {
            Association = await _associationService.GetAssociationByIdAsync(associationId);
            if (Association == null)
            {
                return NotFound();
            }

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Association.ThemeKey);

            Tournaments = (await _tournamentService.GetAssociationTournamentsAsync(associationId))
                .OrderByDescending(t => t.StartDate)
                .ToList();

            AssociationLeaderboard = (await _leaderboardService.GetAssociationLeaderboardAsync(associationId)).ToList();

            if (TournamentId.HasValue)
            {
                SelectedTournament = Tournaments.FirstOrDefault(t => t.Id == TournamentId.Value);
                if (SelectedTournament != null)
                {
                    TournamentLeaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(TournamentId.Value)).ToList();
                }
            }

            return Page();
        }
    }
}
