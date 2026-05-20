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
        private readonly IAdminAuditService _adminAuditService;

        public AssociationsModel(IAssociationService associationService, IAdminAuditService adminAuditService)
        {
            _associationService = associationService;
            _adminAuditService = adminAuditService;
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

            await _adminAuditService.WriteAsync(
                deleted ? "Deleted association" : "Failed to delete association",
                User?.Identity?.Name ?? "anonymous",
                new Dictionary<string, string?>
                {
                    ["AssociationId"] = id.ToString()
                });

            return RedirectToPage();
        }
    }
}
