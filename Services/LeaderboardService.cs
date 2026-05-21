using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    /// <summary>
    /// Service for managing tournament leaderboards
    /// </summary>
    public interface ILeaderboardService
    {
        Task<IEnumerable<Leaderboard>> GetTournamentLeaderboardAsync(int tournamentId);
        Task<Leaderboard?> GetPlayerLeaderboardPositionAsync(int tournamentId, string playerId);
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
                    .Include(l => l.Player)
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

        public async Task<Leaderboard?> GetPlayerLeaderboardPositionAsync(int tournamentId, string playerId)
        {
            try
            {
                return await _context.Leaderboards
                    .Include(l => l.Player)
                    .Include(l => l.Tournament)
                    .FirstOrDefaultAsync(l => l.TournamentId == tournamentId && l.PlayerId == playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard position for tournament {TournamentId}, player {PlayerId}",
                    tournamentId, playerId);
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
                    .Where(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Registered && r.PlayerId != null)
                    .Include(r => r.Player)
                    .ToListAsync();

                var leaderboardData = new List<(string PlayerId, int TotalScore, int StablefordPoints)>();

                // Calculate scores for each player
                foreach (var registration in registrations)
                {
                    int totalScore = 0;
                    int totalStablefordPoints = 0;

                    var playerScores = await _context.PlayerScores
                        .Where(ps => ps.TournamentId == tournamentId && ps.PlayerId == registration.PlayerId)
                        .ToListAsync();

                    if (playerScores.Any())
                    {
                        totalScore = playerScores.Sum(ps => ps.Score);
                        totalStablefordPoints = playerScores.Sum(ps => ps.StablefordPoints);
                    }

                    leaderboardData.Add((registration.PlayerId!, totalScore, totalStablefordPoints));
                }

                // Sort by Stableford points (descending) or total score (ascending) depending on format
                IEnumerable<(string PlayerId, int TotalScore, int StablefordPoints)> sortedData = tournament.Format switch
                {
                    TournamentFormat.Stableford => leaderboardData.OrderByDescending(x => x.StablefordPoints),
                    TournamentFormat.BestBall => leaderboardData.OrderBy(x => x.TotalScore),
                    TournamentFormat.Scramble => leaderboardData.OrderBy(x => x.TotalScore),
                    _ => leaderboardData.OrderBy(x => x.TotalScore) // Stroke play and Fourball default to lowest score
                };

                // Clear existing leaderboard entries for this tournament
                var existingLeaderboard = await _context.Leaderboards
                    .Where(l => l.TournamentId == tournamentId)
                    .ToListAsync();

                _context.Leaderboards.RemoveRange(existingLeaderboard);
                await _context.SaveChangesAsync();

                // Create new leaderboard entries with positions
                int position = 1;
                foreach (var (playerId, totalScore, stablefordPoints) in sortedData)
                {
                    var leaderboardEntry = new Leaderboard
                    {
                        TournamentId = tournamentId,
                        PlayerId = playerId,
                        Position = position,
                        TotalScore = totalScore,
                        StablefordPoints = stablefordPoints,
                        ScoreDifferential = tournament.Format switch
                        {
                            TournamentFormat.Stroke => totalScore - 72, // Assuming par 72
                            TournamentFormat.Stableford => stablefordPoints,
                            _ => totalScore - 72
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
    }
}
