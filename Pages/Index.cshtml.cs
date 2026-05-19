using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IAssociationService _associationService;
        private readonly ITournamentService _tournamentService;

        public IndexModel(
            IAssociationService associationService,
            ITournamentService tournamentService)
        {
            _associationService = associationService;
            _tournamentService = tournamentService;
        }

        public List<GolfAssociation> Associations { get; set; } = new();
        public List<Tournament> UpcomingTournaments { get; set; } = new();

        public async Task OnGetAsync()
        {
            Associations = (await _associationService.GetAllActiveAssociationsAsync()).ToList();
            UpcomingTournaments = (await _tournamentService.GetAllUpcomingTournamentsAsync()).Take(5).ToList();
        }
    }
}
