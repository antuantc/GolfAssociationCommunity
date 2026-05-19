using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Tournaments
{
    public class IndexModel : PageModel
    {
        private readonly ITournamentService _tournamentService;

        public IndexModel(ITournamentService tournamentService)
        {
            _tournamentService = tournamentService;
        }

        public List<Tournament> UpcomingTournaments { get; set; } = new();
        public List<Tournament> CompletedTournaments { get; set; } = new();

        public async Task OnGetAsync()
        {
            UpcomingTournaments = (await _tournamentService.GetAllUpcomingTournamentsAsync()).ToList();
            CompletedTournaments = (await _tournamentService.GetAllCompletedTournamentsAsync()).ToList();
        }
    }
}
