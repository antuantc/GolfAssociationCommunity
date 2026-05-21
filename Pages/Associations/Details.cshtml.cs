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

        public int MemberCount { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Association = await _associationService.GetAssociationByIdAsync(id);
            if (Association == null)
            {
                return NotFound();
            }

            ViewData["PublicAssociationId"] = Association.Id;
            ViewData["PublicAssociationName"] = Association.Name;
            MemberCount = await _associationService.GetMemberCountAsync(id);
            return Page();
        }
    }
}
