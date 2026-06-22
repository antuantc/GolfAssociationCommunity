using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    public class RecentTournamentLeaderboard
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public string TournamentDates { get; set; } = string.Empty;
        public List<Leaderboard> TopEntries { get; set; } = new();
    }

    public class GlobalLeaderboardRow
    {
        public int OverallPosition { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string PlayerEmail { get; set; } = string.Empty;
        public int TournamentPoints { get; set; }
        public int TournamentsPlayed { get; set; }
        public int Wins { get; set; }
        public decimal AveragePosition { get; set; }
        public int TotalScore { get; set; }
    }

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
    }

    internal sealed class TournamentLeaderboardScoreRow
    {
        public int AssociationPlayerId { get; set; }
        public int TotalScore { get; set; }
        public List<int> TiebreakerScores { get; set; } = new();
        public string? Flight { get; set; }
    }

    /// <summary>
    /// Service for managing tournament leaderboards
    /// </summary>
    public interface ILeaderboardService
    {
        Task<IEnumerable<Leaderboard>> GetTournamentLeaderboardAsync(int tournamentId);
        Task<Dictionary<int, List<int>>> GetTournamentTiebreakersAsync(int tournamentId);
        Task<IEnumerable<AssociationLeaderboardRow>> GetAssociationLeaderboardAsync(int associationId);
        Task<IEnumerable<RecentTournamentLeaderboard>> GetRecentTournamentLeaderboardsAsync(int associationId, int tournamentCount = 3, int topN = 5);
        Task<IEnumerable<GlobalLeaderboardRow>> GetGlobalLeaderboardAsync(int topN = 10);
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
                var leaderboards = await _context.Leaderboards
                    .Where(l => l.TournamentId == tournamentId)
                    .Include(l => l.AssociationPlayer)
                    .Include(l => l.Tournament)
                    .OrderBy(l => l.Position)
                    .ToListAsync();

                // Load tiebreaker hole scores (HoleNumber -1 to -4, ordered hardest first)
                var tiebreakerScores = await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId && ps.HoleNumber < 0)
                    .Select(ps => new { ps.AssociationPlayerId, ps.HoleNumber, ps.Score })
                    .ToListAsync();

                var tiebreakersByPlayer = tiebreakerScores
                    .GroupBy(ps => ps.AssociationPlayerId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(ps => ps.HoleNumber).Select(ps => ps.Score).ToList());

                foreach (var entry in leaderboards)
                {
                    if (tiebreakersByPlayer.TryGetValue(entry.AssociationPlayerId, out var scores))
                        entry.TiebreakerScores = scores;
                }

                return leaderboards;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard for tournament ID: {TournamentId}", tournamentId);
                throw;
            }
        }

        public async Task<Dictionary<int, List<int>>> GetTournamentTiebreakersAsync(int tournamentId)
        {
            var rows = await _context.PlayerScores
                .Where(ps => ps.TournamentId == tournamentId && ps.HoleNumber < 0)
                .Select(ps => new { ps.AssociationPlayerId, ps.HoleNumber, ps.Score })
                .ToListAsync();

            return rows
                .GroupBy(ps => ps.AssociationPlayerId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(ps => ps.HoleNumber).Select(ps => ps.Score).ToList());
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
                        TotalScore = group.Sum(entry => entry.TotalScore)
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

        public async Task<IEnumerable<RecentTournamentLeaderboard>> GetRecentTournamentLeaderboardsAsync(int associationId, int tournamentCount = 3, int topN = 5)
        {
            try
            {
                var recentTournamentIds = await _context.Tournaments
                    .Where(t => t.GolfAssociationId == associationId)
                    .OrderByDescending(t => t.StartDate)
                    .Select(t => new { t.Id, t.Name, t.StartDate, t.EndDate })
                    .Take(tournamentCount)
                    .ToListAsync();

                var result = new List<RecentTournamentLeaderboard>();
                foreach (var t in recentTournamentIds)
                {
                    var entries = await _context.Leaderboards
                        .Where(l => l.TournamentId == t.Id)
                        .Include(l => l.AssociationPlayer)
                        .OrderBy(l => l.Position)
                        .Take(topN)
                        .ToListAsync();

                    if (entries.Count == 0) continue;

                    result.Add(new RecentTournamentLeaderboard
                    {
                        TournamentId = t.Id,
                        TournamentName = t.Name,
                        TournamentDates = $"{t.StartDate:MMM d, yyyy} \u2013 {t.EndDate:MMM d, yyyy}",
                        TopEntries = entries
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent tournament leaderboards for association ID: {AssociationId}", associationId);
                throw;
            }
        }

        public async Task<IEnumerable<GlobalLeaderboardRow>> GetGlobalLeaderboardAsync(int topN = 10)
        {
            try
            {
                var rows = await _context.Leaderboards
                    .Include(l => l.AssociationPlayer)
                    .ToListAsync();

                var aggregated = rows
                    .GroupBy(l => string.IsNullOrWhiteSpace(l.AssociationPlayer?.Email)
                        ? $"name:{BuildPlayerName(l.AssociationPlayer)}"
                        : l.AssociationPlayer!.Email.Trim().ToLowerInvariant())
                    .Select(group => new GlobalLeaderboardRow
                    {
                        PlayerName = group
                            .Select(e => e.AssociationPlayer?.DisplayName)
                            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? group.Key,
                        PlayerEmail = group.Key.StartsWith("name:") ? string.Empty : group.Key,
                        TournamentPoints = group.Sum(e => CalculateTournamentPoints(e.Position)),
                        TournamentsPlayed = group.Count(),
                        Wins = group.Count(e => e.Position == 1),
                        AveragePosition = Math.Round((decimal)group.Average(e => e.Position), 2),
                        TotalScore = group.Sum(e => e.TotalScore)
                    })
                    .OrderByDescending(r => r.TournamentPoints)
                    .ThenBy(r => r.AveragePosition)
                    .ThenByDescending(r => r.Wins)
                    .ThenBy(r => r.TotalScore)
                    .ThenBy(r => r.PlayerName)
                    .Take(topN)
                    .ToList();

                for (int i = 0; i < aggregated.Count; i++)
                    aggregated[i].OverallPosition = i + 1;

                return aggregated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving global leaderboard");
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

                // Load all scores for this tournament up front
                var allScores = await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId)
                    .ToListAsync();

                // Determine the full set of player IDs to rank:
                // - players with a Registered registration, OR
                // - players who have any score entries (covers CSV-imported players without registrations)
                var registrations = await _context.Registrations
                    .Where(r => r.TournamentId == tournamentId && r.AssociationPlayerId != null)
                    .Include(r => r.TournamentFlight)
                    .ToListAsync();

                var registeredPlayerIds = registrations
                    .Where(r => r.Status == RegistrationStatus.Registered)
                    .Select(r => r.AssociationPlayerId!.Value)
                    .ToHashSet();

                var allRegById = registrations
                    .Where(r => r.AssociationPlayerId != null)
                    .ToDictionary(r => r.AssociationPlayerId!.Value);

                var scoredPlayerIds = allScores
                    .Select(s => s.AssociationPlayerId)
                    .Distinct()
                    .ToHashSet();

                // Union: registered players + anyone with a score
                var allPlayerIds = registeredPlayerIds.Union(scoredPlayerIds).ToList();

                var leaderboardData = new List<TournamentLeaderboardScoreRow>();

                foreach (var playerId in allPlayerIds)
                {
                    var playerScores = allScores
                        .Where(ps => ps.AssociationPlayerId == playerId)
                        .ToList();

                    // Prefer explicit round-total rows (HoleNumber=0); fall back to summing individual holes
                    var roundTotals = playerScores.Where(ps => ps.IsRoundTotalEntry).ToList();
                    int totalScore = roundTotals.Count > 0
                        ? roundTotals.Sum(ps => ps.Score)
                        : playerScores.Where(ps => ps.HoleNumber > 0).Sum(ps => ps.Score);

                    var tiebreakerScores = playerScores
                        .Where(ps => ps.HoleNumber >= -PlayerScore.MaxTiebreakerEntries && ps.HoleNumber < 0)
                        .OrderByDescending(ps => ps.HoleNumber)
                        .Select(ps => ps.Score)
                        .ToList();

                    // Resolve flight from registration if present
                    allRegById.TryGetValue(playerId, out var reg);
                    var flight = reg?.TournamentFlight?.Name ?? reg?.Flight;

                    leaderboardData.Add(new TournamentLeaderboardScoreRow
                    {
                        AssociationPlayerId = playerId,
                        TotalScore = totalScore,
                        TiebreakerScores = tiebreakerScores,
                        Flight = flight
                    });
                }

                // Clear existing leaderboard entries for this tournament
                var existingLeaderboard = await _context.Leaderboards
                    .Where(l => l.TournamentId == tournamentId)
                    .ToListAsync();

                _context.Leaderboards.RemoveRange(existingLeaderboard);
                await _context.SaveChangesAsync();

                // Rank within each flight group; players with no flight compete together
                var byFlight = leaderboardData
                    .GroupBy(x => x.Flight ?? string.Empty)
                    .OrderBy(g => g.Key);

                foreach (var flightGroup in byFlight)
                {
                    int position = 1;
                    var sortedFlight = flightGroup
                        .OrderBy(x => x.TotalScore)
                        .ThenBy(x => x, new TiebreakerScoreComparer())
                        .ThenBy(x => x.AssociationPlayerId);

                    foreach (var scoreRow in sortedFlight)
                    {
                        _context.Leaderboards.Add(new Leaderboard
                        {
                            TournamentId = tournamentId,
                            AssociationPlayerId = scoreRow.AssociationPlayerId,
                            Position = position,
                            TotalScore = scoreRow.TotalScore,
                            Flight = scoreRow.Flight,
                            StablefordPoints = 0,
                            ScoreDifferential = 0,
                            UpdatedAt = DateTime.UtcNow
                        });
                        position++;
                    }
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

    internal sealed class TiebreakerScoreComparer : IComparer<TournamentLeaderboardScoreRow>
    {
        public int Compare(TournamentLeaderboardScoreRow? x, TournamentLeaderboardScoreRow? y)
        {
            var xs = x?.TiebreakerScores ?? new List<int>();
            var ys = y?.TiebreakerScores ?? new List<int>();
            int len = Math.Max(xs.Count, ys.Count);
            for (int i = 0; i < len; i++)
            {
                int xv = i < xs.Count ? xs[i] : int.MaxValue;
                int yv = i < ys.Count ? ys[i] : int.MaxValue;
                if (xv != yv) return xv.CompareTo(yv);
            }
            return 0;
        }
    }
}
