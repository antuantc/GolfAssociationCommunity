using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages
{
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

        public async Task OnGetAsync()
        {
            Associations = (await _associationService.GetAllActiveAssociationsAsync()).ToList();
            GlobalLeaderboard = (await _leaderboardService.GetGlobalLeaderboardAsync(10)).ToList();
        }
    }
}
