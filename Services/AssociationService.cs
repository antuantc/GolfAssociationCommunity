using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    /// <summary>
    /// Service for managing golf associations
    /// </summary>
    public interface IAssociationService
    {
        Task<GolfAssociation?> GetAssociationByIdAsync(int id);
        Task<IEnumerable<GolfAssociation>> GetAllActiveAssociationsAsync();
        Task<GolfAssociation> CreateAssociationAsync(GolfAssociation association);
        Task<GolfAssociation?> UpdateAssociationAsync(int id, GolfAssociation association);
        Task<bool> DeleteAssociationAsync(int id);
        Task<IEnumerable<ApplicationUser>> GetAssociationMembersAsync(int associationId);
        Task<bool> AddMemberToAssociationAsync(int associationId, string userId);
        Task<bool> RemoveMemberFromAssociationAsync(int associationId, string userId);
        Task<IEnumerable<Tournament>> GetAssociationTournamentsAsync(int associationId);
        Task<int> GetMemberCountAsync(int associationId);
    }

    public class AssociationService : IAssociationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AssociationService> _logger;

        public AssociationService(ApplicationDbContext context, ILogger<AssociationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<GolfAssociation?> GetAssociationByIdAsync(int id)
        {
            try
            {
                return await _context.GolfAssociations
                    .Include(ga => ga.Members)
                    .Include(ga => ga.Tournaments)
                    .Include(ga => ga.SponsorshipPackages)
                    .Include(ga => ga.SponsorshipPayments)
                    .Include(ga => ga.OfficersAndMembers)
                    .Include(ga => ga.MediaItems)
                    .Include(ga => ga.Sponsors)
                    .FirstOrDefaultAsync(ga => ga.Id == id && ga.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving association with ID: {AssociationId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<GolfAssociation>> GetAllActiveAssociationsAsync()
        {
            try
            {
                return await _context.GolfAssociations
                    .Where(ga => ga.IsActive)
                    .OrderBy(ga => ga.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all active associations");
                throw;
            }
        }

        public async Task<GolfAssociation> CreateAssociationAsync(GolfAssociation association)
        {
            try
            {
                association.ThemeKey = BrandingThemes.Normalize(association.ThemeKey);
                association.CreatedAt = DateTime.UtcNow;
                association.UpdatedAt = DateTime.UtcNow;
                association.IsActive = true;

                _context.GolfAssociations.Add(association);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Association created successfully with ID: {AssociationId}", association.Id);
                return association;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating association: {AssociationName}", association.Name);
                throw;
            }
        }

        public async Task<GolfAssociation?> UpdateAssociationAsync(int id, GolfAssociation association)
        {
            try
            {
                var existing = await _context.GolfAssociations.FindAsync(id);
                if (existing == null)
                {
                    _logger.LogWarning("Association not found with ID: {AssociationId}", id);
                    return null;
                }

                existing.Name = association.Name;
                existing.Description = association.Description;
                existing.ContactEmail = association.ContactEmail;
                existing.ContactPhone = association.ContactPhone;
                existing.Street = association.Street;
                existing.City = association.City;
                existing.State = association.State;
                existing.ZipCode = association.ZipCode;
                existing.Country = association.Country;
                existing.ThemeKey = string.IsNullOrWhiteSpace(association.ThemeKey)
                    ? existing.ThemeKey
                    : BrandingThemes.Normalize(association.ThemeKey);
                existing.Website = association.Website;
                existing.LogoUrl = association.LogoUrl;
                existing.AdminUserId = association.AdminUserId;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Association updated successfully with ID: {AssociationId}", id);
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating association with ID: {AssociationId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteAssociationAsync(int id)
        {
            try
            {
                var association = await _context.GolfAssociations.FindAsync(id);
                if (association == null)
                {
                    _logger.LogWarning("Association not found with ID: {AssociationId}", id);
                    return false;
                }

                association.IsActive = false;
                association.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Association soft deleted with ID: {AssociationId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting association with ID: {AssociationId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<ApplicationUser>> GetAssociationMembersAsync(int associationId)
        {
            try
            {
                return await _context.Users
                    .Where(u => u.GolfAssociationId == associationId && !u.LockoutEnabled)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving members for association ID: {AssociationId}", associationId);
                throw;
            }
        }

        public async Task<bool> AddMemberToAssociationAsync(int associationId, string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    return false;
                }

                var association = await _context.GolfAssociations.FindAsync(associationId);
                if (association == null)
                {
                    _logger.LogWarning("Association not found with ID: {AssociationId}", associationId);
                    return false;
                }

                user.GolfAssociationId = associationId;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} added to association {AssociationId}", userId, associationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member to association");
                throw;
            }
        }

        public async Task<bool> RemoveMemberFromAssociationAsync(int associationId, string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    return false;
                }

                if (user.GolfAssociationId != associationId)
                {
                    _logger.LogWarning("User not member of association");
                    return false;
                }

                user.GolfAssociationId = null;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} removed from association {AssociationId}", userId, associationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member from association");
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

        public async Task<int> GetMemberCountAsync(int associationId)
        {
            try
            {
                return await _context.Users
                    .CountAsync(u => u.GolfAssociationId == associationId && !u.LockoutEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving member count for association ID: {AssociationId}", associationId);
                throw;
            }
        }
    }
}
