using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Services
{
    public interface IAdminAuditService
    {
        Task WriteAsync(string action, string actor, IDictionary<string, string?> details);
    }

    public class AdminAuditService : IAdminAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminAuditService> _logger;

        public AdminAuditService(ApplicationDbContext context, ILogger<AdminAuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task WriteAsync(string action, string actor, IDictionary<string, string?> details)
        {
            var detailText = string.Join(" | ", details
                .Where(d => !string.IsNullOrWhiteSpace(d.Value))
                .Select(d => $"{d.Key}: {d.Value}"));

            var eventEntity = new AdminAuditEvent
            {
                Action = action,
                Actor = actor,
                Details = string.IsNullOrWhiteSpace(detailText) ? "-" : detailText,
                AtUtc = DateTime.UtcNow
            };

            _context.AdminAuditEvents.Add(eventEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "AUDIT AdminAction={Action} Actor={Actor} Details={Details} AtUtc={AtUtc}",
                action,
                actor,
                eventEntity.Details,
                eventEntity.AtUtc);
        }
    }
}
