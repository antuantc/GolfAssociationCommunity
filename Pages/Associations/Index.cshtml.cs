using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Associations
{
    public class IndexModel : PageModel
    {
        private readonly IAssociationService _associationService;

        public IndexModel(IAssociationService associationService)
        {
            _associationService = associationService;
        }

        public List<GolfAssociation> Associations { get; set; } = new();

        public Dictionary<int, int> MemberCounts { get; set; } = new();

        public IActionResult OnGet()
        {
            return RedirectToPage("/Index");
        }
    }
}
