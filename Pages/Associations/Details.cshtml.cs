using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class DetailsModel : PageModel
    {
        private readonly IAssociationService _associationService;

        public DetailsModel(IAssociationService associationService)
        {
            _associationService = associationService;
        }

        public GolfAssociation? Association { get; set; }
        public List<SponsorshipPackage> ActiveSponsorshipPackages { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await LoadAssociationAsync(id))
            {
                return NotFound();
            }
            return Page();
        }

        private async Task<bool> LoadAssociationAsync(int id)
        {
            Association = await _associationService.GetAssociationByIdAsync(id);
            if (Association == null)
            {
                return false;
            }

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            ActiveSponsorshipPackages = Association.SponsorshipPackages
                .Where(sp => sp.IsActive)
                .OrderBy(sp => sp.DisplayOrder)
                .ThenByDescending(sp => sp.Amount)
                .ToList();
            return true;
        }
    }
}
