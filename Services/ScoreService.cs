using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    /// <summary>
    /// Service for managing player scores and scoring operations
    /// </summary>
    public interface IScoreService
    {
        Task<PlayerScore?> GetScoreByIdAsync(int id);
        Task<IEnumerable<PlayerScore>> GetPlayerScoresAsync(int tournamentId, string playerId);
        Task<IEnumerable<PlayerScore>> GetTournamentScoresAsync(int tournamentId);
        Task<PlayerScore> RecordScoreAsync(PlayerScore score);
        Task<PlayerScore?> UpdateScoreAsync(int id, PlayerScore score);
        Task<bool> DeleteScoreAsync(int id);
        Task<int> CalculateStablefordPointsAsync(int holeScore, int holePar, int handicapStrokes);
        Task<int> CalculateTotalStablefordAsync(int tournamentId, string playerId);
        Task<int> CalculateTotalScoreAsync(int tournamentId, string playerId);
        Task<IEnumerable<PlayerScore>> GetPlayerRoundScoresAsync(int tournamentId, string playerId, int round);
    }

    public class ScoreService : IScoreService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ScoreService> _logger;

        public ScoreService(ApplicationDbContext context, ILogger<ScoreService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PlayerScore?> GetScoreByIdAsync(int id)
        {
            try
            {
                return await _context.PlayerScores
                    .Include(ps => ps.Player)
                    .Include(ps => ps.Tournament)
                    .FirstOrDefaultAsync(ps => ps.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving player score with ID: {ScoreId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<PlayerScore>> GetPlayerScoresAsync(int tournamentId, string playerId)
        {
            try
            {
                return await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId && ps.PlayerId == playerId)
                    .OrderBy(ps => ps.Round)
                    .ThenBy(ps => ps.HoleNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scores for tournament {TournamentId}, player {PlayerId}",
                    tournamentId, playerId);
                throw;
            }
        }

        public async Task<IEnumerable<PlayerScore>> GetTournamentScoresAsync(int tournamentId)
        {
            try
            {
                return await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId)
                    .Include(ps => ps.Player)
                    .OrderBy(ps => ps.PlayerId)
                    .ThenBy(ps => ps.Round)
                    .ThenBy(ps => ps.HoleNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tournament scores for tournament ID: {TournamentId}", tournamentId);
                throw;
            }
        }

        public async Task<PlayerScore> RecordScoreAsync(PlayerScore score)
        {
            try
            {
                // Validate hole number (1-18)
                if (score.HoleNumber < 1 || score.HoleNumber > 18)
                {
                    throw new ArgumentException("Hole number must be between 1 and 18");
                }

                score.CreatedAt = DateTime.UtcNow;
                score.UpdatedAt = DateTime.UtcNow;

                // Calculate Stableford points
                score.StablefordPoints = await CalculateStablefordPointsAsync(
                    score.Score,
                    score.HolePar,
                    score.HandicapStrokes);

                _context.PlayerScores.Add(score);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Score recorded for player {PlayerId} at hole {HoleNumber}, round {Round}",
                    score.PlayerId, score.HoleNumber, score.Round);
                return score;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording score for tournament {TournamentId}, player {PlayerId}",
                    score.TournamentId, score.PlayerId);
                throw;
            }
        }

        public async Task<PlayerScore?> UpdateScoreAsync(int id, PlayerScore score)
        {
            try
            {
                var existing = await _context.PlayerScores.FindAsync(id);
                if (existing == null)
                {
                    _logger.LogWarning("Score not found with ID: {ScoreId}", id);
                    return null;
                }

                existing.Score = score.Score;
                existing.HolePar = score.HolePar;
                existing.HandicapStrokes = score.HandicapStrokes;
                existing.StablefordPoints = await CalculateStablefordPointsAsync(
                    score.Score,
                    score.HolePar,
                    score.HandicapStrokes);
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Score updated successfully with ID: {ScoreId}", id);
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating score with ID: {ScoreId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteScoreAsync(int id)
        {
            try
            {
                var score = await _context.PlayerScores.FindAsync(id);
                if (score == null)
                {
                    _logger.LogWarning("Score not found with ID: {ScoreId}", id);
                    return false;
                }

                _context.PlayerScores.Remove(score);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Score deleted successfully with ID: {ScoreId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting score with ID: {ScoreId}", id);
                throw;
            }
        }

        public Task<int> CalculateStablefordPointsAsync(int holeScore, int holePar, int handicapStrokes)
        {
            try
            {
                // Stableford scoring: 2 points for par, +1 per stroke under par, -1 per stroke over par
                // Adjusted for handicap strokes
                int adjustedScore = holeScore - handicapStrokes;
                int adjustedPar = holePar;

                int diff = adjustedScore - adjustedPar;
                int points = 2 + diff;

                // Minimum points is 0
                return Task.FromResult(Math.Max(0, points));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Stableford points");
                throw;
            }
        }

        public async Task<int> CalculateTotalStablefordAsync(int tournamentId, string playerId)
        {
            try
            {
                var scores = await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId && ps.PlayerId == playerId)
                    .ToListAsync();

                return scores.Sum(ps => ps.StablefordPoints);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total Stableford score for tournament {TournamentId}, player {PlayerId}",
                    tournamentId, playerId);
                throw;
            }
        }

        public async Task<int> CalculateTotalScoreAsync(int tournamentId, string playerId)
        {
            try
            {
                var scores = await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId && ps.PlayerId == playerId)
                    .ToListAsync();

                return scores.Sum(ps => ps.Score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total score for tournament {TournamentId}, player {PlayerId}",
                    tournamentId, playerId);
                throw;
            }
        }

        public async Task<IEnumerable<PlayerScore>> GetPlayerRoundScoresAsync(int tournamentId, string playerId, int round)
        {
            try
            {
                return await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId && ps.PlayerId == playerId && ps.Round == round)
                    .OrderBy(ps => ps.HoleNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving round scores for tournament {TournamentId}, player {PlayerId}, round {Round}",
                    tournamentId, playerId, round);
                throw;
            }
        }
    }
}
