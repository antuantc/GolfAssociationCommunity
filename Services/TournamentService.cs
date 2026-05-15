using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    /// <summary>
    /// Service for managing tournaments
    /// </summary>
    public interface ITournamentService
    {
        Task<Tournament?> GetTournamentByIdAsync(int id);
        Task<IEnumerable<Tournament>> GetAssociationTournamentsAsync(int associationId);
        Task<IEnumerable<Tournament>> GetUpcomingTournamentsAsync(int associationId);
        Task<IEnumerable<Tournament>> GetCompletedTournamentsAsync(int associationId);
        Task<Tournament> CreateTournamentAsync(Tournament tournament);
        Task<Tournament?> UpdateTournamentAsync(int id, Tournament tournament);
        Task<bool> UpdateTournamentStatusAsync(int id, TournamentStatus newStatus);
        Task<bool> DeleteTournamentAsync(int id);
        Task<int> GetRegistrationCountAsync(int tournamentId);
    }

    public class TournamentService : ITournamentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TournamentService> _logger;

        public TournamentService(ApplicationDbContext context, ILogger<TournamentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Tournament?> GetTournamentByIdAsync(int id)
        {
            try
            {
                return await _context.Tournaments
                    .Include(t => t.GolfAssociation)
                    .Include(t => t.Registrations)
                    .FirstOrDefaultAsync(t => t.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tournament with ID: {TournamentId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Tournament>> GetAssociationTournamentsAsync(int associationId)
        {
            try
            {
                return await _context.Tournaments
                    .Where(t => t.GolfAssociationId == associationId)
                    .OrderByDescending(t => t.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tournaments for association ID: {AssociationId}", associationId);
                throw;
            }
        }

        public async Task<IEnumerable<Tournament>> GetUpcomingTournamentsAsync(int associationId)
        {
            try
            {
                return await _context.Tournaments
                    .Where(t => t.GolfAssociationId == associationId && t.StartDate > DateTime.UtcNow)
                    .OrderBy(t => t.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving upcoming tournaments for association ID: {AssociationId}", associationId);
                throw;
            }
        }

        public async Task<IEnumerable<Tournament>> GetCompletedTournamentsAsync(int associationId)
        {
            try
            {
                return await _context.Tournaments
                    .Where(t => t.GolfAssociationId == associationId && 
                           (t.Status == TournamentStatus.Completed || t.Status == TournamentStatus.Cancelled))
                    .OrderByDescending(t => t.EndDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving completed tournaments for association ID: {AssociationId}", associationId);
                throw;
            }
        }

        public async Task<Tournament> CreateTournamentAsync(Tournament tournament)
        {
            try
            {
                tournament.CreatedAt = DateTime.UtcNow;
                tournament.UpdatedAt = DateTime.UtcNow;

                _context.Tournaments.Add(tournament);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tournament created successfully with ID: {TournamentId}", tournament.Id);
                return tournament;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tournament: {TournamentName}", tournament.Name);
                throw;
            }
        }

        public async Task<Tournament?> UpdateTournamentAsync(int id, Tournament tournament)
        {
            try
            {
                var existing = await _context.Tournaments.FindAsync(id);
                if (existing == null)
                {
                    _logger.LogWarning("Tournament not found with ID: {TournamentId}", id);
                    return null;
                }

                existing.Name = tournament.Name;
                existing.Description = tournament.Description;
                existing.StartDate = tournament.StartDate;
                existing.EndDate = tournament.EndDate;
                existing.RegistrationDeadline = tournament.RegistrationDeadline;
                existing.Format = tournament.Format;
                existing.Location = tournament.Location;
                existing.GolfCourse = tournament.GolfCourse;
                existing.EntryFee = tournament.EntryFee;
                existing.MaxPlayers = tournament.MaxPlayers;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Tournament updated successfully with ID: {TournamentId}", id);
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tournament with ID: {TournamentId}", id);
                throw;
            }
        }

        public async Task<bool> UpdateTournamentStatusAsync(int id, TournamentStatus newStatus)
        {
            try
            {
                var tournament = await _context.Tournaments.FindAsync(id);
                if (tournament == null)
                {
                    _logger.LogWarning("Tournament not found with ID: {TournamentId}", id);
                    return false;
                }

                tournament.Status = newStatus;
                tournament.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Tournament status updated to {NewStatus} for tournament ID: {TournamentId}",
                    newStatus, id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tournament status for tournament ID: {TournamentId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteTournamentAsync(int id)
        {
            try
            {
                var tournament = await _context.Tournaments.FindAsync(id);
                if (tournament == null)
                {
                    _logger.LogWarning("Tournament not found with ID: {TournamentId}", id);
                    return false;
                }

                _context.Tournaments.Remove(tournament);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tournament deleted successfully with ID: {TournamentId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tournament with ID: {TournamentId}", id);
                throw;
            }
        }

        public async Task<int> GetRegistrationCountAsync(int tournamentId)
        {
            try
            {
                return await _context.Registrations
                    .CountAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Registered);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration count for tournament ID: {TournamentId}", tournamentId);
                throw;
            }
        }
    }
}
