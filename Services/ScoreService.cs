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
        Task<IEnumerable<PlayerScore>> GetPlayerScoresAsync(int tournamentId, int associationPlayerId);
        Task<IEnumerable<PlayerScore>> GetTournamentScoresAsync(int tournamentId);
        Task<PlayerScore> RecordScoreAsync(PlayerScore score);
        Task<PlayerScore?> UpdateScoreAsync(int id, PlayerScore score);
        Task<bool> DeleteScoreAsync(int id);
        Task<int> CalculateTotalScoreAsync(int tournamentId, int associationPlayerId);
        Task<IEnumerable<PlayerScore>> GetPlayerRoundScoresAsync(int tournamentId, int associationPlayerId, int round);
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
                    .Include(ps => ps.AssociationPlayer)
                    .Include(ps => ps.Tournament)
                    .FirstOrDefaultAsync(ps => ps.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving player score with ID: {ScoreId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<PlayerScore>> GetPlayerScoresAsync(int tournamentId, int associationPlayerId)
        {
            try
            {
                return await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId && ps.AssociationPlayerId == associationPlayerId)
                    .Include(ps => ps.AssociationPlayer)
                    .OrderBy(ps => ps.Round)
                        .ThenBy(ps => ps.IsRoundTotalEntry ? -1 : ps.HoleNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scores for tournament {TournamentId}, association player {AssociationPlayerId}",
                    tournamentId, associationPlayerId);
                throw;
            }
        }

        public async Task<IEnumerable<PlayerScore>> GetTournamentScoresAsync(int tournamentId)
        {
            try
            {
                return await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId)
                    .Include(ps => ps.AssociationPlayer)
                    .OrderBy(ps => ps.AssociationPlayerId)
                    .ThenBy(ps => ps.Round)
                    .ThenBy(ps => ps.IsRoundTotalEntry ? -1 : ps.HoleNumber)
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
                if (score.HoleNumber < PlayerScore.RoundTotalEntryHoleNumber || score.HoleNumber > 18)
                {
                    throw new ArgumentException("Hole number must be between 0 and 18");
                }

                score.CreatedAt = DateTime.UtcNow;
                score.UpdatedAt = DateTime.UtcNow;

                _context.PlayerScores.Add(score);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Score recorded for association player {AssociationPlayerId} at entry {EntryType}, round {Round}",
                    score.AssociationPlayerId, score.IsRoundTotalEntry ? "round-total" : $"hole-{score.HoleNumber}", score.Round);
                return score;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording score for tournament {TournamentId}, association player {AssociationPlayerId}",
                    score.TournamentId, score.AssociationPlayerId);
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
                existing.TiebreakerHoleHandicap = score.TiebreakerHoleHandicap;
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

        public async Task<int> CalculateTotalScoreAsync(int tournamentId, int associationPlayerId)
        {
            try
            {
                var scores = await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId && ps.AssociationPlayerId == associationPlayerId)
                    .ToListAsync();

                return scores.Sum(ps => ps.Score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total score for tournament {TournamentId}, association player {AssociationPlayerId}",
                    tournamentId, associationPlayerId);
                throw;
            }
        }

        public async Task<IEnumerable<PlayerScore>> GetPlayerRoundScoresAsync(int tournamentId, int associationPlayerId, int round)
        {
            try
            {
                return await _context.PlayerScores
                    .Where(ps => ps.TournamentId == tournamentId && ps.AssociationPlayerId == associationPlayerId && ps.Round == round)
                    .Include(ps => ps.AssociationPlayer)
                    .OrderBy(ps => ps.IsRoundTotalEntry ? -1 : ps.HoleNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving round scores for tournament {TournamentId}, association player {AssociationPlayerId}, round {Round}",
                    tournamentId, associationPlayerId, round);
                throw;
            }
        }
    }
}
