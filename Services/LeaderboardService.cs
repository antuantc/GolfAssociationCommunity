using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    public class AssociationLeaderboardRow
    {
        public int AssociationPlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string PlayerEmail { get; set; } = string.Empty;
        public int OverallPosition { get; set; }
        public int TournamentPoints { get; set; }
        public int TournamentsPlayed { get; set; }
        public int Wins { get; set; }
        public decimal AveragePosition { get; set; }
        public int TotalScore { get; set; }
        public int TotalStablefordPoints { get; set; }
    }

    internal sealed class TournamentLeaderboardScoreRow
    {
        public int AssociationPlayerId { get; set; }
        public int TotalScore { get; set; }
        public int StablefordPoints { get; set; }
        public int? TiebreakerHoleHandicap { get; set; }
    }

    /// <summary>
    /// Service for managing tournament leaderboards
    /// </summary>
    public interface ILeaderboardService
    {
        Task<IEnumerable<Leaderboard>> GetTournamentLeaderboardAsync(int tournamentId);
        Task<IEnumerable<AssociationLeaderboardRow>> GetAssociationLeaderboardAsync(int associationId);
        Task<Leaderboard?> GetPlayerLeaderboardPositionAsync(int tournamentId, int associationPlayerId);
        Task UpdateLeaderboardAsync(int tournamentId);
        Task<bool> RecalculateLeaderboardAsync(int tournamentId);
        Task<bool> RecalculateAllLeaderboardsAsync();
    }

    public class LeaderboardService : ILeaderboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IScoreService _scoreService;
        private readonly ILogger<LeaderboardService> _logger;

        public LeaderboardService(
            ApplicationDbContext context,
            IScoreService scoreService,
            ILogger<LeaderboardService> logger)
        {
            _context = context;
            _scoreService = scoreService;
            _logger = logger;
        }

        public async Task<IEnumerable<Leaderboard>> GetTournamentLeaderboardAsync(int tournamentId)
        {
            try
            {
                return await _context.Leaderboards
                    .Where(l => l.TournamentId == tournamentId)
                    .Include(l => l.AssociationPlayer)
                    .Include(l => l.Tournament)
                    .OrderBy(l => l.Position)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard for tournament ID: {TournamentId}", tournamentId);
                throw;
            }
        }

        public async Task<IEnumerable<AssociationLeaderboardRow>> GetAssociationLeaderboardAsync(int associationId)
        {
            try
            {
                var rows = await _context.Leaderboards
                    .Where(l => l.Tournament != null && l.Tournament.GolfAssociationId == associationId)
                    .Include(l => l.AssociationPlayer)
                    .Include(l => l.Tournament)
                    .ToListAsync();

                var aggregated = rows
                    .GroupBy(l => new
                    {
                        l.AssociationPlayerId,
                        PlayerName = BuildPlayerName(l.AssociationPlayer),
                        PlayerEmail = l.AssociationPlayer?.Email ?? string.Empty
                    })
                    .Select(group => new AssociationLeaderboardRow
                    {
                        AssociationPlayerId = group.Key.AssociationPlayerId,
                        PlayerName = group.Key.PlayerName,
                        PlayerEmail = group.Key.PlayerEmail,
                        TournamentPoints = group.Sum(entry => CalculateTournamentPoints(entry.Position)),
                        TournamentsPlayed = group.Count(),
                        Wins = group.Count(entry => entry.Position == 1),
                        AveragePosition = Math.Round((decimal)group.Average(entry => entry.Position), 2),
                        TotalScore = group.Sum(entry => entry.TotalScore),
                        TotalStablefordPoints = group.Sum(entry => entry.StablefordPoints)
                    })
                    .OrderByDescending(row => row.TournamentPoints)
                    .ThenBy(row => row.AveragePosition)
                    .ThenByDescending(row => row.Wins)
                    .ThenBy(row => row.TotalScore)
                    .ThenBy(row => row.PlayerName)
                    .ToList();

                for (var index = 0; index < aggregated.Count; index++)
                {
                    aggregated[index].OverallPosition = index + 1;
                }

                return aggregated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving association leaderboard for association ID: {AssociationId}", associationId);
                throw;
            }
        }

        public async Task<Leaderboard?> GetPlayerLeaderboardPositionAsync(int tournamentId, int associationPlayerId)
        {
            try
            {
                return await _context.Leaderboards
                    .Include(l => l.AssociationPlayer)
                    .Include(l => l.Tournament)
                    .FirstOrDefaultAsync(l => l.TournamentId == tournamentId && l.AssociationPlayerId == associationPlayerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard position for tournament {TournamentId}, association player {AssociationPlayerId}",
                    tournamentId, associationPlayerId);
                throw;
            }
        }

        public async Task UpdateLeaderboardAsync(int tournamentId)
        {
            try
            {
                var tournament = await _context.Tournaments.FindAsync(tournamentId);
                if (tournament == null)
                {
                    _logger.LogWarning("Tournament not found with ID: {TournamentId}", tournamentId);
                    return;
                }

                // Get all players registered for the tournament
                var registrations = await _context.Registrations
                    .Where(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Registered && r.AssociationPlayerId != null)
                    .Include(r => r.AssociationPlayer)
                    .ToListAsync();

                var leaderboardData = new List<TournamentLeaderboardScoreRow>();

                // Calculate scores for each player
                foreach (var registration in registrations)
                {
                    int totalScore = 0;
                    int totalStablefordPoints = 0;
                    int? tiebreakerHoleHandicap = null;

                    var playerScores = await _context.PlayerScores
                        .Where(ps => ps.TournamentId == tournamentId && ps.AssociationPlayerId == registration.AssociationPlayerId)
                        .ToListAsync();

                    if (playerScores.Any())
                    {
                        totalScore = playerScores.Sum(ps => ps.Score);
                        totalStablefordPoints = playerScores.Sum(ps => ps.StablefordPoints);
                        tiebreakerHoleHandicap = playerScores
                            .Where(score => score.TiebreakerHoleHandicap.HasValue)
                            .OrderByDescending(score => score.Round)
                            .ThenByDescending(score => score.UpdatedAt)
                            .Select(score => score.TiebreakerHoleHandicap)
                            .FirstOrDefault();
                    }

                    leaderboardData.Add(new TournamentLeaderboardScoreRow
                    {
                        AssociationPlayerId = registration.AssociationPlayerId!.Value,
                        TotalScore = totalScore,
                        StablefordPoints = totalStablefordPoints,
                        TiebreakerHoleHandicap = tiebreakerHoleHandicap
                    });
                }

                // Lower tiebreaker handicap wins when totals are otherwise tied.
                IEnumerable<TournamentLeaderboardScoreRow> sortedData = tournament.Format switch
                {
                    TournamentFormat.Stableford => leaderboardData
                        .OrderByDescending(x => x.StablefordPoints)
                        .ThenBy(x => x.TiebreakerHoleHandicap ?? int.MaxValue)
                        .ThenBy(x => x.TotalScore)
                        .ThenBy(x => x.AssociationPlayerId),
                    TournamentFormat.BestBall => leaderboardData
                        .OrderBy(x => x.TotalScore)
                        .ThenBy(x => x.TiebreakerHoleHandicap ?? int.MaxValue)
                        .ThenByDescending(x => x.StablefordPoints)
                        .ThenBy(x => x.AssociationPlayerId),
                    TournamentFormat.Scramble => leaderboardData
                        .OrderBy(x => x.TotalScore)
                        .ThenBy(x => x.TiebreakerHoleHandicap ?? int.MaxValue)
                        .ThenByDescending(x => x.StablefordPoints)
                        .ThenBy(x => x.AssociationPlayerId),
                    _ => leaderboardData
                        .OrderBy(x => x.TotalScore)
                        .ThenBy(x => x.TiebreakerHoleHandicap ?? int.MaxValue)
                        .ThenByDescending(x => x.StablefordPoints)
                        .ThenBy(x => x.AssociationPlayerId)
                };

                // Clear existing leaderboard entries for this tournament
                var existingLeaderboard = await _context.Leaderboards
                    .Where(l => l.TournamentId == tournamentId)
                    .ToListAsync();

                _context.Leaderboards.RemoveRange(existingLeaderboard);
                await _context.SaveChangesAsync();

                // Create new leaderboard entries with positions
                int position = 1;
                foreach (var scoreRow in sortedData)
                {
                    var leaderboardEntry = new Leaderboard
                    {
                        TournamentId = tournamentId,
                        AssociationPlayerId = scoreRow.AssociationPlayerId,
                        Position = position,
                        TotalScore = scoreRow.TotalScore,
                        StablefordPoints = scoreRow.StablefordPoints,
                        ScoreDifferential = tournament.Format switch
                        {
                            TournamentFormat.Stroke => scoreRow.TotalScore - 72,
                            TournamentFormat.Stableford => scoreRow.StablefordPoints,
                            _ => scoreRow.TotalScore - 72
                        },
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Leaderboards.Add(leaderboardEntry);
                    position++;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Leaderboard updated for tournament ID: {TournamentId}", tournamentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating leaderboard for tournament ID: {TournamentId}", tournamentId);
                throw;
            }
        }

        public async Task<bool> RecalculateLeaderboardAsync(int tournamentId)
        {
            try
            {
                await UpdateLeaderboardAsync(tournamentId);
                _logger.LogInformation("Leaderboard recalculated for tournament ID: {TournamentId}", tournamentId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating leaderboard for tournament ID: {TournamentId}", tournamentId);
                throw;
            }
        }

        public async Task<bool> RecalculateAllLeaderboardsAsync()
        {
            try
            {
                var tournaments = await _context.Tournaments.ToListAsync();

                foreach (var tournament in tournaments)
                {
                    await UpdateLeaderboardAsync(tournament.Id);
                }

                _logger.LogInformation("All leaderboards recalculated. Total tournaments: {TournamentCount}", tournaments.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating all leaderboards");
                throw;
            }
        }

        private static string BuildPlayerName(AssociationPlayer? associationPlayer)
        {
            if (associationPlayer != null && !string.IsNullOrWhiteSpace(associationPlayer.DisplayName))
            {
                return associationPlayer.DisplayName;
            }

            return "Unknown Player";
        }

        private static int CalculateTournamentPoints(int position)
        {
            if (position <= 0)
            {
                return 0;
            }

            return Math.Max(1, 25 - (position - 1));
        }
    }
}
