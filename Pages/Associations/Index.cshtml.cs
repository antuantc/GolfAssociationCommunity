using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
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

        public async Task OnGetAsync()
        {
            Associations = (await _associationService.GetAllActiveAssociationsAsync()).ToList();
            foreach (var association in Associations)
            {
                MemberCounts[association.Id] = await _associationService.GetMemberCountAsync(association.Id);
            }
        }
    }
}
