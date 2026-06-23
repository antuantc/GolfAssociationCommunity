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

        [BindProperty]
        public string? FlightInput { get; set; }

        public List<Tournament> Tournaments { get; private set; } = new();
        public List<PlayerOption> Players { get; private set; } = new();
        public Tournament? SelectedTournament { get; private set; }
        public string? SelectedPlayerName { get; private set; }
        public string? SelectedFlight { get; private set; }
        public int CurrentTotalScore { get; private set; }
        public int CurrentStablefordPoints { get; private set; }
        public List<TournamentFlight> TournamentFlights { get; private set; } = new();

        public class PlayerOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class RoundScoreInput
        {
            public int? TotalScore { get; set; }
            public int? StablefordPoints { get; set; }
            public List<TiebreakerEntry> Tiebreakers { get; set; } =
                Enumerable.Range(0, PlayerScore.MaxTiebreakerEntries).Select(_ => new TiebreakerEntry()).ToList();

            public class TiebreakerEntry
            {
                public int? Score { get; set; }
            }
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
                    && registration.AssociationPlayerId == AssociationPlayerId.Value);

            // Auto-register the player if no registration exists yet
            if (registeredPlayer == null)
            {
                var assocPlayer = await Context.AssociationPlayers
                    .FirstOrDefaultAsync(p => p.Id == AssociationPlayerId.Value && p.GolfAssociationId == CurrentAssociation.Id);
                if (assocPlayer == null) return NotFound();

                var newReg = new Registration
                {
                    TournamentId = TournamentId.Value,
                    AssociationPlayerId = AssociationPlayerId.Value,
                    Status = RegistrationStatus.Registered,
                    PaymentConfirmed = true,
                    RegistrationDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                Context.Registrations.Add(newReg);
                await Context.SaveChangesAsync();
                registeredPlayer = await Context.Registrations
                    .Include(r => r.AssociationPlayer)
                    .FirstOrDefaultAsync(r => r.Id == newReg.Id);
            }
            else if (registeredPlayer.Status != RegistrationStatus.Registered)
            {
                registeredPlayer.Status = RegistrationStatus.Registered;
                registeredPlayer.UpdatedAt = DateTime.UtcNow;
                await Context.SaveChangesAsync();
                await Context.Entry(registeredPlayer).Reference(r => r.AssociationPlayer).LoadAsync();
            }

            if (registeredPlayer?.AssociationPlayer == null)
            {
                ModelState.AddModelError(string.Empty, "Could not load player record.");
                await LoadPageDataAsync();
                return Page();
            }

            // Load flights for this tournament (needed for flight name matching)
            TournamentFlights = await Context.TournamentFlights
                .Where(f => f.TournamentId == TournamentId.Value)
                .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
                .ToListAsync();

            // Save flight assignment on registration
            if (TournamentFlights.Count > 0)
            {
                // Flight dropdown mode — match by name
                var selectedFlight = TournamentFlights.FirstOrDefault(f => f.Name == FlightInput);
                registeredPlayer.TournamentFlightId = selectedFlight?.Id;
                registeredPlayer.Flight = selectedFlight?.Name ?? (string.IsNullOrWhiteSpace(FlightInput) ? null : FlightInput.Trim());
            }
            else
            {
                registeredPlayer.TournamentFlightId = null;
                registeredPlayer.Flight = string.IsNullOrWhiteSpace(FlightInput) ? null : FlightInput.Trim();
            }

            var existingScores = await Context.PlayerScores
                .Where(score => score.TournamentId == TournamentId.Value
                    && score.AssociationPlayerId == AssociationPlayerId.Value
                    && score.Round == Round)
                .ToListAsync();

            var existingRoundTotal = existingScores.FirstOrDefault(s => s.IsRoundTotalEntry);
            var existingTiebreakers = existingScores.Where(s => s.HoleNumber < 0).ToList();

            if (!Input.TotalScore.HasValue)
            {
                if (existingScores.Count > 0)
                {
                    Context.PlayerScores.RemoveRange(existingScores);
                }
            }
            else
            {
                // Round total entry
                var roundScore = existingRoundTotal ?? new PlayerScore
                {
                    TournamentId = TournamentId.Value,
                    AssociationPlayerId = AssociationPlayerId.Value,
                    Round = Round,
                    CreatedAt = DateTime.UtcNow
                };

                if (existingRoundTotal == null)
                {
                    Context.PlayerScores.Add(roundScore);
                }

                roundScore.HoleNumber = PlayerScore.RoundTotalEntryHoleNumber;
                roundScore.Score = Input.TotalScore.Value;
                roundScore.HolePar = 0;
                roundScore.HandicapStrokes = 0;
                roundScore.TiebreakerHoleHandicap = null;
                roundScore.StablefordPoints = Input.StablefordPoints ?? 0;
                roundScore.UpdatedAt = DateTime.UtcNow;

                // Remove any accidental duplicate round total records
                var extraRoundTotals = existingScores.Where(s => s.IsRoundTotalEntry && s != roundScore).ToList();
                if (extraRoundTotals.Count > 0)
                {
                    Context.PlayerScores.RemoveRange(extraRoundTotals);
                }

                // Tiebreaker entries: upsert each of the 4 slots
                for (int i = 0; i < PlayerScore.MaxTiebreakerEntries; i++)
                {
                    int holeNumber = -(i + 1);
                    var existing = existingTiebreakers.FirstOrDefault(s => s.HoleNumber == holeNumber);
                    var inputScore = Input.Tiebreakers[i].Score;

                    if (inputScore.HasValue)
                    {
                        if (existing == null)
                        {
                            existing = new PlayerScore
                            {
                                TournamentId = TournamentId.Value,
                                AssociationPlayerId = AssociationPlayerId.Value,
                                Round = Round,
                                HoleNumber = holeNumber,
                                CreatedAt = DateTime.UtcNow
                            };
                            Context.PlayerScores.Add(existing);
                        }

                        existing.Score = inputScore.Value;
                        existing.TiebreakerHoleHandicap = i + 1;
                        existing.HolePar = 0;
                        existing.HandicapStrokes = 0;
                        existing.StablefordPoints = 0;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (existing != null)
                    {
                        Context.PlayerScores.Remove(existing);
                    }
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
                        && registration.Tournament != null
                        && registration.Tournament.GolfAssociationId == CurrentAssociation.Id)
                    .OrderByDescending(registration => registration.Tournament!.StartDate)
                    .Select(registration => (int?)registration.TournamentId)
                    .FirstOrDefaultAsync();

                // Fall back to most recent tournament for this association even if no registration
                if (!TournamentId.HasValue)
                {
                    TournamentId = await Context.Tournaments
                        .Where(t => t.GolfAssociationId == CurrentAssociation.Id)
                        .OrderByDescending(t => t.StartDate)
                        .Select(t => (int?)t.Id)
                        .FirstOrDefaultAsync();
                }
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

            TournamentFlights = await Context.TournamentFlights
                .Where(f => f.TournamentId == TournamentId.Value)
                .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
                .ToListAsync();

            Players = await Context.AssociationPlayers
                .Where(p => p.GolfAssociationId == CurrentAssociation.Id && p.IsActive)
                .OrderBy(p => p.DisplayName)
                .ThenBy(p => p.Email)
                .Select(p => new PlayerOption { Id = p.Id, Name = p.DisplayName })
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

            // Load flight assignment from registration
            var registration = await Context.Registrations
                .Include(r => r.TournamentFlight)
                .FirstOrDefaultAsync(r => r.TournamentId == TournamentId.Value
                    && r.AssociationPlayerId == AssociationPlayerId.Value
                    && r.Status == RegistrationStatus.Registered);
            SelectedFlight = registration?.TournamentFlight?.Name ?? registration?.Flight;
            if (FlightInput == null)   // null = GET request (not yet bound by model binder)
                FlightInput = SelectedFlight;

            var existingScores = await Context.PlayerScores
                .Where(score => score.TournamentId == TournamentId.Value
                    && score.AssociationPlayerId == AssociationPlayerId.Value
                    && score.Round == Round)
                .ToListAsync();

            if (existingScores.Count > 0)
            {
                var roundTotalEntry = existingScores.FirstOrDefault(s => s.IsRoundTotalEntry);
                var tiebreakers = Enumerable.Range(0, PlayerScore.MaxTiebreakerEntries).Select(i =>
                {
                    var entry = existingScores.FirstOrDefault(s => s.HoleNumber == -(i + 1));
                    return new RoundScoreInput.TiebreakerEntry { Score = entry?.Score };
                }).ToList();

                Input = new RoundScoreInput
                {
                    TotalScore = roundTotalEntry?.Score,
                    StablefordPoints = roundTotalEntry?.StablefordPoints,
                    Tiebreakers = tiebreakers
                };
            }

            CurrentTotalScore = existingScores.Where(s => s.IsRoundTotalEntry).Sum(s => s.Score);
            CurrentStablefordPoints = existingScores.Where(s => s.IsRoundTotalEntry).Sum(s => s.StablefordPoints);
        }
    }
}
