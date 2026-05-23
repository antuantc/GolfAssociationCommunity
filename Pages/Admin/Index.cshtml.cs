using GolfAssociationCommunity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public int AssociationCount { get; private set; }
        public int TournamentCount { get; private set; }
        public int RegistrationCount { get; private set; }
        public int UserCount { get; private set; }
        public int PlayerCount { get; private set; }

        public async Task OnGetAsync()
        {
            AssociationCount = await _context.GolfAssociations.CountAsync();
            TournamentCount = await _context.Tournaments.CountAsync();
            RegistrationCount = await _context.Registrations.CountAsync();
            UserCount = await _context.Users.CountAsync();
            PlayerCount = await _context.AssociationPlayers.CountAsync();
        }
    }
}
