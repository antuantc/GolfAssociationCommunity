using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages
{
    public class AssociationCardStats
    {
        public GolfAssociation Association { get; set; } = default!;
        public int TournamentCount { get; set; }
        public int UpcomingCount { get; set; }
        public Tournament? NextTournament { get; set; }
    }

    public class IndexModel : PageModel
    {
        private readonly ILeaderboardService _leaderboardService;
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context, ILeaderboardService leaderboardService)
        {
            _context = context;
            _leaderboardService = leaderboardService;
        }

        public List<GlobalLeaderboardRow> GlobalLeaderboard { get; set; } = new();
        public List<AssociationCardStats> AssociationStats { get; set; } = new();
        public int TotalTournaments { get; set; }
        public int TotalAssociations { get; set; }

        public async Task OnGetAsync()
        {
            // Single query: associations + their tournaments only — no N+1
            var associations = await _context.GolfAssociations
                .Where(ga => ga.IsActive)
                .Include(ga => ga.Tournaments)
                .OrderBy(ga => ga.Name)
                .ToListAsync();

            GlobalLeaderboard = (await _leaderboardService.GetGlobalLeaderboardAsync(10)).ToList();
            TotalAssociations = associations.Count;

            var now = DateTime.UtcNow;
            foreach (var assoc in associations)
            {
                var upcoming = assoc.Tournaments
                    .Where(t => t.StartDate >= now && t.Status != TournamentStatus.Cancelled)
                    .OrderBy(t => t.StartDate)
                    .ToList();

                AssociationStats.Add(new AssociationCardStats
                {
                    Association = assoc,
                    TournamentCount = assoc.Tournaments.Count,
                    UpcomingCount = upcoming.Count,
                    NextTournament = upcoming.FirstOrDefault()
                });
                TotalTournaments += assoc.Tournaments.Count;
            }
        }
    }
}
