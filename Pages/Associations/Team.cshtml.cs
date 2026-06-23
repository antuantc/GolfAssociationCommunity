using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class TeamModel : PageModel
    {
        private readonly IAssociationService _associationService;

        public TeamModel(IAssociationService associationService)
        {
            _associationService = associationService;
        }

        public GolfAssociation? Association { get; private set; }
        public List<AssociationOfficer> ActiveOfficers { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(int associationId)
        {
            Association = await _associationService.GetAssociationByIdAsync(associationId);
            if (Association == null)
                return NotFound();

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ViewData["PublicThemeKey"] = BrandingThemes.Normalize(Association.ThemeKey);
            ViewData["PublicAssociationLogoUrl"] = Association.LogoUrl;

            var nextTmmt = Association.Tournaments
                .Where(t => t.StartDate >= DateTime.UtcNow && t.Status != TournamentStatus.Cancelled)
                .OrderBy(t => t.StartDate)
                .FirstOrDefault();
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
