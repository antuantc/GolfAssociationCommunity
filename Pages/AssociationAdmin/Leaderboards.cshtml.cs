using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class LeaderboardsModel : AssociationAdminPageModel
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardsModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILeaderboardService leaderboardService)
            : base(userManager, context)
        {
            _leaderboardService = leaderboardService;
        }

        [BindProperty(SupportsGet = true)]
        public int? TournamentId { get; set; }

        public List<Tournament> Tournaments { get; private set; } = new();
        public Tournament? SelectedTournament { get; private set; }
        public List<Leaderboard> TournamentLeaderboard { get; private set; } = new();
        public List<AssociationLeaderboardRow> AssociationLeaderboard { get; private set; } = new();
        public bool HasTiebreakerData { get; private set; }
        public Dictionary<int, List<int>> TiebreakersByPlayer { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            await LoadPageDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostRecalculateTournamentAsync(int tournamentId)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var tournament = await Context.Tournaments
                .FirstOrDefaultAsync(item => item.Id == tournamentId && item.GolfAssociationId == CurrentAssociation.Id);

            if (tournament == null)
            {
                return NotFound();
            }

            await _leaderboardService.RecalculateLeaderboardAsync(tournamentId);
            TempData["SuccessMessage"] = $"Leaderboard recalculated for {tournament.Name}.";
            return RedirectToPage(new { tournamentId });
        }

        public async Task<IActionResult> OnPostRecalculateAssociationAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var tournamentIds = await Context.Tournaments
                .Where(item => item.GolfAssociationId == CurrentAssociation.Id)
                .Select(item => item.Id)
                .ToListAsync();

            foreach (var tournamentId in tournamentIds)
            {
                await _leaderboardService.RecalculateLeaderboardAsync(tournamentId);
            }

            TempData["SuccessMessage"] = "Association leaderboards recalculated.";
            return RedirectToPage(new { tournamentId = TournamentId });
        }

        private async Task LoadPageDataAsync()
        {
            Tournaments = await Context.Tournaments
                .Where(item => item.GolfAssociationId == CurrentAssociation.Id)
                .OrderByDescending(item => item.StartDate)
                .ToListAsync();

            AssociationLeaderboard = (await _leaderboardService.GetAssociationLeaderboardAsync(CurrentAssociation.Id)).ToList();

            if (!TournamentId.HasValue)
            {
                return;
            }

            SelectedTournament = Tournaments.FirstOrDefault(item => item.Id == TournamentId.Value);
            if (SelectedTournament == null)
            {
                TournamentId = null;
                return;
            }

            TournamentLeaderboard = (await _leaderboardService.GetTournamentLeaderboardAsync(TournamentId.Value)).ToList();
            TiebreakersByPlayer = await _leaderboardService.GetTournamentTiebreakersAsync(TournamentId.Value);
            HasTiebreakerData = TiebreakersByPlayer.Count > 0;
        }
    }
}
