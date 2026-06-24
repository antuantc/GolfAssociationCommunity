using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class LeaderboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ITournamentService _tournamentService;
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardModel(
            ApplicationDbContext context,
            ITournamentService tournamentService,
            ILeaderboardService leaderboardService)
        {
            _context = context;
            _tournamentService = tournamentService;
            _leaderboardService = leaderboardService;
        }

        public GolfAssociation? Association { get; private set; }
        public List<Tournament> Tournaments { get; private set; } = new();
        public Tournament? SelectedTournament { get; private set; }
        public List<AssociationLeaderboardRow> AssociationLeaderboard { get; private set; } = new();
        public List<Leaderboard> TournamentLeaderboard { get; private set; } = new();
        public bool HasTiebreakerData { get; private set; }
        public Dictionary<int, List<int>> TiebreakersByPlayer { get; private set; } = new();
        public List<string> TournamentFlights { get; private set; } = new();
        public bool HasMultipleFlights { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int? TournamentId { get; set; }

        public async Task<IActionResult> OnGetAsync(int associationId)
        {
            // Lean query — no navigation properties; next tournament derived from Tournaments list below
            Association = await _context.GolfAssociations
                .FirstOrDefaultAsync(ga => ga.Id == associationId && ga.IsActive);
            if (Association == null)
                return NotFound();

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Association.ThemeKey);
            ViewData["PublicAssociationLogoUrl"] = Association.LogoUrl;

            Tournaments = (await _tournamentService.GetAssociationTournamentsAsync(associationId))
                .OrderByDescending(t => t.StartDate)
                .ToList();

            // Derive next tournament from already-loaded list (no extra query)
            var nextTmmt = Tournaments
                .Where(t => t.StartDate >= DateTime.UtcNow && t.Status != TournamentStatus.Cancelled)
                .OrderBy(t => t.StartDate)
                .FirstOrDefault();
            if (nextTmmt != null)
            {
                ViewData["NextTournamentName"] = nextTmmt.Name;
                ViewData["NextTournamentDate"] = nextTmmt.StartDate.ToString("MMMM d, yyyy");
                ViewData["NextTournamentCourse"] = nextTmmt.GolfCourse;
                ViewData["NextTournamentLocation"] = nextTmmt.Location;
                ViewData["NextTournamentId"] = nextTmmt.Id;
            }

            AssociationLeaderboard = (await _leaderboardService.GetAssociationLeaderboardAsync(associationId)).ToList();

            if (TournamentId.HasValue)
            {
                // Lean query — only Flights needed for ordering; skip GolfAssociation and Registrations includes
                SelectedTournament = await _context.Tournaments
                    .Include(t => t.Flights.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name))
                    .FirstOrDefaultAsync(t => t.Id == TournamentId.Value && t.GolfAssociationId == associationId);

                if (SelectedTournament != null)
                {
                    TournamentLeaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(TournamentId.Value)).ToList();
                    TiebreakersByPlayer = await _leaderboardService.GetTournamentTiebreakersAsync(TournamentId.Value);
                    HasTiebreakerData = TiebreakersByPlayer.Count > 0;
                    var orderedFlightNames = SelectedTournament.Flights
                        .Select(f => f.Name).ToList();
                    TournamentFlights = TournamentLeaderboard
                        .Select(r => r.Flight ?? string.Empty)
                        .Distinct()
                        .OrderBy(f => { var i = orderedFlightNames.FindIndex(n => string.Equals(n, f, StringComparison.OrdinalIgnoreCase)); return i >= 0 ? i : int.MaxValue; })
                        .ThenBy(f => f)
                        .ToList();
                    HasMultipleFlights = TournamentFlights.Count > 1 || (TournamentFlights.Count == 1 && TournamentFlights[0] != string.Empty);
                }
            }

            return Page();
        }
    }
}
