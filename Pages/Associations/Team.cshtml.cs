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

            ActiveOfficers = Association.OfficersAndMembers
                .Where(o => o.IsActive)
                .OrderBy(o => o.DisplayOrder)
                .ThenBy(o => o.Name)
                .ToList();

            return Page();
        }
    }
}
