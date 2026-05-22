using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class IndexModel : AssociationAdminPageModel
    {
        public IndexModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
            : base(userManager, context)
        {
        }

        public int TournamentCount { get; private set; }
        public int UpcomingTournamentCount { get; private set; }
        public int RegistrationCount { get; private set; }
        public int PlayerCount { get; private set; }
        public int ScoreCount { get; private set; }
        public int SponsorshipPackageCount { get; private set; }

        public async Task<Microsoft.AspNetCore.Mvc.IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null)
            {
                return contextResult;
            }

            var associationId = CurrentAssociation.Id;
            TournamentCount = await Context.Tournaments.CountAsync(t => t.GolfAssociationId == associationId);
            UpcomingTournamentCount = await Context.Tournaments.CountAsync(t => t.GolfAssociationId == associationId && t.StartDate > DateTime.UtcNow);
            RegistrationCount = await Context.Registrations.CountAsync(r => r.Tournament != null && r.Tournament.GolfAssociationId == associationId);
            PlayerCount = await Context.AssociationPlayers.CountAsync(player => player.GolfAssociationId == associationId && player.IsActive);
            ScoreCount = await Context.PlayerScores.CountAsync(s => s.Tournament != null && s.Tournament.GolfAssociationId == associationId);
            SponsorshipPackageCount = await Context.SponsorshipPackages.CountAsync(sp => sp.GolfAssociationId == associationId);

            return Page();
        }
    }
}
