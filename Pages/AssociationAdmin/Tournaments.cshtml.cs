using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class TournamentsModel : AssociationAdminPageModel
    {
        private readonly ITournamentService _tournamentService;

        public TournamentsModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ITournamentService tournamentService)
            : base(userManager, context)
        {
            _tournamentService = tournamentService;
        }

        public List<Tournament> Tournaments { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            Tournaments = await Context.Tournaments
                .Where(t => t.GolfAssociationId == CurrentAssociation.Id)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var tournament = await Context.Tournaments.FirstOrDefaultAsync(t => t.Id == id && t.GolfAssociationId == CurrentAssociation.Id);
            if (tournament is null)
            {
                return NotFound();
            }

            await _tournamentService.DeleteTournamentAsync(id);
            TempData["SuccessMessage"] = "Tournament deleted successfully.";
            return RedirectToPage();
        }
    }
}
