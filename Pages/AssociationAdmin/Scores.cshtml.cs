using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class ScoresModel : AssociationAdminPageModel
    {
        private readonly IScoreService _scoreService;
        private readonly ILeaderboardService _leaderboardService;

        public ScoresModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IScoreService scoreService,
            ILeaderboardService leaderboardService)
            : base(userManager, context)
        {
            _scoreService = scoreService;
            _leaderboardService = leaderboardService;
        }

        [BindProperty(SupportsGet = true)]
        public int? TournamentId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? AssociationPlayerId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Round { get; set; } = 1;

        [BindProperty]
        public RoundScoreInput Input { get; set; } = new();

        public List<Tournament> Tournaments { get; private set; } = new();
        public List<PlayerOption> Players { get; private set; } = new();
        public Tournament? SelectedTournament { get; private set; }
        public string? SelectedPlayerName { get; private set; }
        public int CurrentTotalScore { get; private set; }
        public int CurrentStablefordPoints { get; private set; }

        public class PlayerOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class RoundScoreInput
        {
            public int? TotalScore { get; set; }
            public int? StablefordPoints { get; set; }
            public int? TiebreakerHoleHandicap { get; set; }
        }

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

        public async Task<IActionResult> OnPostSaveAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            if (!TournamentId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Select a tournament before saving scores.");
                await LoadPageDataAsync();
                return Page();
            }

            if (!AssociationPlayerId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Select a player before saving scores.");
                await LoadPageDataAsync();
                return Page();
            }

            if (Round < 1)
            {
                ModelState.AddModelError(string.Empty, "Round must be 1 or greater.");
                await LoadPageDataAsync();
                return Page();
            }

            var tournament = await Context.Tournaments
                .FirstOrDefaultAsync(item => item.Id == TournamentId.Value && item.GolfAssociationId == CurrentAssociation.Id);

            if (tournament == null)
            {
                return NotFound();
            }

            if (tournament.Format == TournamentFormat.Stableford && !Input.StablefordPoints.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Enter the round Stableford points for Stableford tournaments.");
                await LoadPageDataAsync();
                return Page();
            }

            var registeredPlayer = await Context.Registrations
                .Include(registration => registration.AssociationPlayer)
                .FirstOrDefaultAsync(registration => registration.TournamentId == TournamentId.Value
                    && registration.AssociationPlayerId == AssociationPlayerId.Value
                    && registration.Status == RegistrationStatus.Registered);

            if (registeredPlayer?.AssociationPlayer == null)
            {
                ModelState.AddModelError(string.Empty, "Selected player is not a registered member for this tournament.");
                await LoadPageDataAsync();
                return Page();
            }

            var existingScores = await Context.PlayerScores
                .Where(score => score.TournamentId == TournamentId.Value
                    && score.AssociationPlayerId == AssociationPlayerId.Value
                    && score.Round == Round)
                .ToListAsync();

            if (!Input.TotalScore.HasValue)
            {
                if (existingScores.Count > 0)
                {
                    Context.PlayerScores.RemoveRange(existingScores);
                }
            }
            else
            {
                var roundScore = existingScores.OrderBy(score => score.HoleNumber).FirstOrDefault();
                if (roundScore == null)
                {
                    roundScore = new PlayerScore
                    {
                        TournamentId = TournamentId.Value,
                        AssociationPlayerId = AssociationPlayerId.Value,
                        Round = Round,
                        CreatedAt = DateTime.UtcNow
                    };

                    Context.PlayerScores.Add(roundScore);
                }

                roundScore.HoleNumber = PlayerScore.RoundTotalEntryHoleNumber;
                roundScore.Score = Input.TotalScore.Value;
                roundScore.HolePar = 0;
                roundScore.HandicapStrokes = 0;
                roundScore.TiebreakerHoleHandicap = Input.TiebreakerHoleHandicap;
                roundScore.StablefordPoints = Input.StablefordPoints ?? 0;
                roundScore.UpdatedAt = DateTime.UtcNow;

                if (existingScores.Count > 1)
                {
                    Context.PlayerScores.RemoveRange(existingScores.Where(score => score != roundScore));
                }
            }

            await Context.SaveChangesAsync();
            await _leaderboardService.RecalculateLeaderboardAsync(TournamentId.Value);

            TempData["SuccessMessage"] = $"Scores saved for {registeredPlayer.AssociationPlayer.DisplayName} in {tournament.Name}, round {Round}.";
            return RedirectToPage(new { tournamentId = TournamentId.Value, associationPlayerId = AssociationPlayerId.Value, round = Round });
        }

        private async Task LoadPageDataAsync()
        {
            Tournaments = await Context.Tournaments
                .Where(item => item.GolfAssociationId == CurrentAssociation.Id)
                .OrderByDescending(item => item.StartDate)
                .ToListAsync();

            Input = new RoundScoreInput();

            if (!TournamentId.HasValue && AssociationPlayerId.HasValue)
            {
                TournamentId = await Context.Registrations
                    .Where(registration => registration.AssociationPlayerId == AssociationPlayerId.Value
                        && registration.Status == RegistrationStatus.Registered
                        && registration.Tournament != null
                        && registration.Tournament.GolfAssociationId == CurrentAssociation.Id)
                    .OrderByDescending(registration => registration.Tournament!.StartDate)
                    .Select(registration => (int?)registration.TournamentId)
                    .FirstOrDefaultAsync();
            }

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

            Players = await Context.Registrations
                .Where(registration => registration.TournamentId == TournamentId.Value
                    && registration.AssociationPlayerId != null
                    && registration.AssociationPlayer != null
                    && registration.Status == RegistrationStatus.Registered)
                .Select(registration => registration.AssociationPlayer!)
                .Distinct()
                .OrderBy(player => player.DisplayName)
                .ThenBy(player => player.Email)
                .Select(player => new PlayerOption
                {
                    Id = player.Id,
                    Name = player.DisplayName
                })
                .ToListAsync();

            if (!AssociationPlayerId.HasValue)
            {
                return;
            }

            if (!Players.Any(player => player.Id == AssociationPlayerId.Value))
            {
                AssociationPlayerId = null;
                return;
            }

            var player = await Context.AssociationPlayers.FirstOrDefaultAsync(item => item.Id == AssociationPlayerId.Value && item.GolfAssociationId == CurrentAssociation.Id);
            if (player == null)
            {
                AssociationPlayerId = null;
                return;
            }

            SelectedPlayerName = player.DisplayName;

            var existingScores = await Context.PlayerScores
                .Where(score => score.TournamentId == TournamentId.Value
                    && score.AssociationPlayerId == AssociationPlayerId.Value
                    && score.Round == Round)
                .ToListAsync();

            if (existingScores.Count > 0)
            {
                var roundScore = existingScores.OrderBy(score => score.HoleNumber).First();
                Input = new RoundScoreInput
                {
                    TotalScore = existingScores.Sum(score => score.Score),
                    StablefordPoints = existingScores.Sum(score => score.StablefordPoints),
                    TiebreakerHoleHandicap = roundScore.TiebreakerHoleHandicap
                };
            }

            CurrentTotalScore = existingScores.Sum(score => score.Score);
            CurrentStablefordPoints = existingScores.Sum(score => score.StablefordPoints);
        }
    }
}
