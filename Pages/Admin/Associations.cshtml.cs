using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GolfAssociationCommunity.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AssociationsModel : PageModel
    {
        private readonly IAssociationService _associationService;

        public AssociationsModel(IAssociationService associationService)
        {
            _associationService = associationService;
        }

        public List<GolfAssociation> Associations { get; private set; } = new();

        public async Task OnGetAsync()
        {
            Associations = (await _associationService.GetAllActiveAssociationsAsync()).ToList();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var deleted = await _associationService.DeleteAssociationAsync(id);
            TempData["SuccessMessage"] = deleted
                ? "Association deleted successfully."
                : "Association not found.";

            return RedirectToPage();
        }
    }
}
