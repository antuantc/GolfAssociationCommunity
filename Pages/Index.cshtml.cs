using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
        private readonly IAssociationService _associationService;
        private readonly ILeaderboardService _leaderboardService;

        public IndexModel(IAssociationService associationService, ILeaderboardService leaderboardService)
        {
            _associationService = associationService;
            _leaderboardService = leaderboardService;
        }

        public List<GolfAssociation> Associations { get; set; } = new();
        public List<GlobalLeaderboardRow> GlobalLeaderboard { get; set; } = new();
        public List<AssociationCardStats> AssociationStats { get; set; } = new();
        public int TotalTournaments { get; set; }
        public int TotalAssociations { get; set; }

        public async Task OnGetAsync()
        {
            Associations = (await _associationService.GetAllActiveAssociationsAsync()).ToList();
            GlobalLeaderboard = (await _leaderboardService.GetGlobalLeaderboardAsync(10)).ToList();
            TotalAssociations = Associations.Count;

            // Build per-association card stats
            foreach (var assoc in Associations)
            {
                var fullAssoc = await _associationService.GetAssociationByIdAsync(assoc.Id);
                if (fullAssoc == null) continue;
                var upcoming = fullAssoc.Tournaments
                    .Where(t => t.StartDate >= DateTime.UtcNow && t.Status != TournamentStatus.Cancelled)
                    .OrderBy(t => t.StartDate)
                    .ToList();
                AssociationStats.Add(new AssociationCardStats
                {
                    Association = fullAssoc,
                    TournamentCount = fullAssoc.Tournaments.Count,
                    UpcomingCount = upcoming.Count,
                    NextTournament = upcoming.FirstOrDefault()
                });
                TotalTournaments += fullAssoc.Tournaments.Count;
            }
        }
    }
}
