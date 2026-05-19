using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    [Authorize(Roles = "AssociationAdmin")]
    public abstract class AssociationAdminPageModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        protected readonly ApplicationDbContext Context;

        protected AssociationAdminPageModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            Context = context;
        }

        protected ApplicationUser CurrentUser { get; private set; } = null!;
        protected GolfAssociation CurrentAssociation { get; private set; } = null!;

        protected async Task<IActionResult?> LoadAssociationContextAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (!user.GolfAssociationId.HasValue)
            {
                return Forbid();
            }

            var association = await Context.GolfAssociations
                .FirstOrDefaultAsync(a => a.Id == user.GolfAssociationId.Value && a.IsActive);

            if (association is null)
            {
                return NotFound();
            }

            CurrentUser = user;
            CurrentAssociation = association;
            ViewData["AssociationName"] = association.Name;
            return null;
        }
    }
}
