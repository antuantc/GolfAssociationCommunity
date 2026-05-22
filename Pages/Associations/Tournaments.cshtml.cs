using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class TournamentsModel : PageModel
    {
        private readonly IAssociationService _associationService;
        private readonly ITournamentService _tournamentService;

        public TournamentsModel(IAssociationService associationService, ITournamentService tournamentService)
        {
            _associationService = associationService;
            _tournamentService = tournamentService;
        }

        public GolfAssociation? Association { get; private set; }
        public List<Tournament> UpcomingTournaments { get; private set; } = new();
        public List<Tournament> CompletedTournaments { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(int associationId)
        {
            Association = await _associationService.GetAssociationByIdAsync(associationId);
            if (Association == null)
            {
                return NotFound();
            }

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Association.ThemeKey);
            UpcomingTournaments = (await _tournamentService.GetUpcomingTournamentsAsync(associationId)).ToList();
            CompletedTournaments = (await _tournamentService.GetCompletedTournamentsAsync(associationId)).ToList();
            return Page();
        }
    }
}