using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IAssociationService _associationService;

        public IndexModel(IAssociationService associationService)
        {
            _associationService = associationService;
        }

        public List<GolfAssociation> Associations { get; set; } = new();

        public async Task OnGetAsync()
        {
            Associations = (await _associationService.GetAllActiveAssociationsAsync()).ToList();
        }
    }
}
