using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    /// <summary>
    /// Service for managing player registrations for tournaments
    /// </summary>
    public interface IRegistrationService
    {
        Task<Registration?> GetRegistrationByIdAsync(int id);
        Task<Registration?> GetPlayerTournamentRegistrationAsync(int tournamentId, string playerId);
        Task<Registration?> GetGuestTournamentRegistrationAsync(int tournamentId, string guestEmail);
        Task<IEnumerable<Registration>> GetTournamentRegistrationsAsync(int tournamentId);
        Task<IEnumerable<Registration>> GetPlayerRegistrationsAsync(string playerId);
        Task<Registration> CreateRegistrationAsync(Registration registration);
        Task<Registration?> UpdateRegistrationAsync(int id, Registration registration);
        Task<bool> ConfirmPaymentAsync(int id, string authorizeNetTransactionId);
        Task<bool> WithdrawRegistrationAsync(int id, string reason);
        Task<bool> CanPlayerRegisterAsync(int tournamentId, string playerId);
        Task<bool> CanGuestRegisterAsync(int tournamentId, string guestEmail);
        Task<int> GetRegistrationCountAsync(int tournamentId, RegistrationStatus status);
    }

    public class RegistrationService : IRegistrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RegistrationService> _logger;

        public RegistrationService(ApplicationDbContext context, ILogger<RegistrationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Registration?> GetRegistrationByIdAsync(int id)
        {
            try
            {
                return await _context.Registrations
                    .Include(r => r.Tournament)
                    .Include(r => r.Player)
                    .FirstOrDefaultAsync(r => r.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration with ID: {RegistrationId}", id);
                throw;
            }
        }

        public async Task<Registration?> GetPlayerTournamentRegistrationAsync(int tournamentId, string playerId)
        {
            try
            {
                return await _context.Registrations
                    .Include(r => r.Tournament)
                    .Include(r => r.Player)
                    .FirstOrDefaultAsync(r => r.TournamentId == tournamentId && r.PlayerId == playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration for tournament ID: {TournamentId}, player ID: {PlayerId}",
                    tournamentId, playerId);
                throw;
            }
        }

        public async Task<Registration?> GetGuestTournamentRegistrationAsync(int tournamentId, string guestEmail)
        {
            try
            {
                var normalizedEmail = guestEmail.Trim().ToUpperInvariant();
                return await _context.Registrations
                    .Include(r => r.Tournament)
                    .FirstOrDefaultAsync(r =>
                        r.TournamentId == tournamentId &&
                        r.GuestEmail.ToUpper() == normalizedEmail &&
                        r.Status != RegistrationStatus.Cancelled &&
                        r.Status != RegistrationStatus.Withdrew);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving guest registration for tournament ID: {TournamentId}, email: {GuestEmail}",
                    tournamentId, guestEmail);
                throw;
            }
        }

        public async Task<IEnumerable<Registration>> GetTournamentRegistrationsAsync(int tournamentId)
        {
            try
            {
                return await _context.Registrations
                    .Where(r => r.TournamentId == tournamentId)
                    .Include(r => r.Player)
                    .OrderBy(r => r.RegistrationDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for tournament ID: {TournamentId}", tournamentId);
                throw;
            }
        }

        public async Task<IEnumerable<Registration>> GetPlayerRegistrationsAsync(string playerId)
        {
            try
            {
                return await _context.Registrations
                    .Where(r => r.PlayerId == playerId)
                    .Include(r => r.Tournament)
                    .OrderByDescending(r => r.Tournament!.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for player ID: {PlayerId}", playerId);
                throw;
            }
        }

        public async Task<Registration> CreateRegistrationAsync(Registration registration)
        {
            try
            {
                registration.RegistrationDate = DateTime.UtcNow;
                registration.UpdatedAt = DateTime.UtcNow;

                _context.Registrations.Add(registration);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Registration created successfully with ID: {RegistrationId}", registration.Id);
                return registration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating registration for tournament ID: {TournamentId}",
                    registration.TournamentId);
                throw;
            }
        }

        public async Task<Registration?> UpdateRegistrationAsync(int id, Registration registration)
        {
            try
            {
                var existing = await _context.Registrations.FindAsync(id);
                if (existing == null)
                {
                    _logger.LogWarning("Registration not found with ID: {RegistrationId}", id);
                    return null;
                }

                existing.Status = registration.Status;
                existing.RegistrationFee = registration.RegistrationFee;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Registration updated successfully with ID: {RegistrationId}", id);
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating registration with ID: {RegistrationId}", id);
                throw;
            }
        }

        public async Task<bool> ConfirmPaymentAsync(int id, string authorizeNetTransactionId)
        {
            try
            {
                var registration = await _context.Registrations.FindAsync(id);
                if (registration == null)
                {
                    _logger.LogWarning("Registration not found with ID: {RegistrationId}", id);
                    return false;
                }

                registration.PaymentConfirmed = true;
                registration.AuthorizeNetTransactionId = authorizeNetTransactionId;
                registration.PaymentDate = DateTime.UtcNow;
                registration.Status = RegistrationStatus.Registered;
                registration.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment confirmed for registration ID: {RegistrationId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming payment for registration ID: {RegistrationId}", id);
                throw;
            }
        }

        public async Task<bool> WithdrawRegistrationAsync(int id, string reason)
        {
            try
            {
                var registration = await _context.Registrations.FindAsync(id);
                if (registration == null)
                {
                    _logger.LogWarning("Registration not found with ID: {RegistrationId}", id);
                    return false;
                }

                registration.Status = RegistrationStatus.Withdrew;
                registration.WithdrawalReason = reason;
                registration.WithdrawalDate = DateTime.UtcNow;
                registration.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Registration withdrawn with ID: {RegistrationId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error withdrawing registration with ID: {RegistrationId}", id);
                throw;
            }
        }

        public async Task<bool> CanPlayerRegisterAsync(int tournamentId, string playerId)
        {
            try
            {
                // Check if player already registered
                var existingRegistration = await GetPlayerTournamentRegistrationAsync(tournamentId, playerId);
                if (existingRegistration != null && existingRegistration.Status == RegistrationStatus.Registered)
                {
                    _logger.LogWarning("Player {PlayerId} already registered for tournament {TournamentId}",
                        playerId, tournamentId);
                    return false;
                }

                // Check if tournament has available slots
                var tournament = await _context.Tournaments.FindAsync(tournamentId);
                if (tournament == null)
                {
                    _logger.LogWarning("Tournament not found with ID: {TournamentId}", tournamentId);
                    return false;
                }

                var registeredCount = await GetRegistrationCountAsync(tournamentId, RegistrationStatus.Registered);
                if (registeredCount >= tournament.MaxPlayers)
                {
                    _logger.LogWarning("Tournament {TournamentId} is full", tournamentId);
                    return false;
                }

                // Check registration deadline
                if (tournament.RegistrationDeadline.HasValue && 
                    tournament.RegistrationDeadline.Value < DateTime.UtcNow)
                {
                    _logger.LogWarning("Registration deadline has passed for tournament {TournamentId}", tournamentId);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking registration eligibility for player ID: {PlayerId}, tournament ID: {TournamentId}",
                    playerId, tournamentId);
                throw;
            }
        }

        public async Task<bool> CanGuestRegisterAsync(int tournamentId, string guestEmail)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(guestEmail))
                {
                    return false;
                }

                // Check if the guest email already has an active registration for this tournament.
                var existingRegistration = await GetGuestTournamentRegistrationAsync(tournamentId, guestEmail);
                if (existingRegistration != null)
                {
                    _logger.LogWarning("Guest email {GuestEmail} already has an active registration for tournament {TournamentId}",
                        guestEmail, tournamentId);
                    return false;
                }

                var tournament = await _context.Tournaments.FindAsync(tournamentId);
                if (tournament == null)
                {
                    _logger.LogWarning("Tournament not found with ID: {TournamentId}", tournamentId);
                    return false;
                }

                var registeredCount = await GetRegistrationCountAsync(tournamentId, RegistrationStatus.Registered);
                if (registeredCount >= tournament.MaxPlayers)
                {
                    _logger.LogWarning("Tournament {TournamentId} is full", tournamentId);
                    return false;
                }

                if (tournament.RegistrationDeadline.HasValue &&
                    tournament.RegistrationDeadline.Value < DateTime.UtcNow)
                {
                    _logger.LogWarning("Registration deadline has passed for tournament {TournamentId}", tournamentId);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking guest registration eligibility for email: {GuestEmail}, tournament ID: {TournamentId}",
                    guestEmail, tournamentId);
                throw;
            }
        }

        public async Task<int> GetRegistrationCountAsync(int tournamentId, RegistrationStatus status)
        {
            try
            {
                return await _context.Registrations
                    .CountAsync(r => r.TournamentId == tournamentId && r.Status == status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration count for tournament ID: {TournamentId}", tournamentId);
                throw;
            }
        }
    }
}
