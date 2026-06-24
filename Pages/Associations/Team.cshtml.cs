using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class TeamModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TeamModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public GolfAssociation? Association { get; private set; }
        public List<AssociationOfficer> ActiveOfficers { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(int associationId)
        {
            // Only load officers — no Members, Players, Tournaments, etc.
            Association = await _context.GolfAssociations
                .Include(ga => ga.OfficersAndMembers)
                .FirstOrDefaultAsync(ga => ga.Id == associationId && ga.IsActive);
            if (Association == null)
                return NotFound();

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Association.ThemeKey);
            ViewData["PublicAssociationLogoUrl"] = Association.LogoUrl;

            // Scalar query for next tournament header — avoids loading all tournaments
            var nextTmmt = await _context.Tournaments
                .Where(t => t.GolfAssociationId == associationId
                         && t.StartDate >= DateTime.UtcNow
                         && t.Status != TournamentStatus.Cancelled)
                .OrderBy(t => t.StartDate)
                .Select(t => new { t.Id, t.Name, t.StartDate, t.GolfCourse, t.Location })
                .FirstOrDefaultAsync();
            if (nextTmmt != null)
            {
                ViewData["NextTournamentName"] = nextTmmt.Name;
                ViewData["NextTournamentDate"] = nextTmmt.StartDate.ToString("MMMM d, yyyy");
                ViewData["NextTournamentCourse"] = nextTmmt.GolfCourse;
                ViewData["NextTournamentLocation"] = nextTmmt.Location;
                ViewData["NextTournamentId"] = nextTmmt.Id;
            }

            ActiveOfficers = Association.OfficersAndMembers
                .Where(o => o.IsActive)
                .OrderBy(o => o.DisplayOrder)
                .ThenBy(o => o.Name)
                .ToList();

            return Page();
        }
    }
}
