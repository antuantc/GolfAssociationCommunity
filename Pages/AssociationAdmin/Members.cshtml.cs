using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class MembersModel : AssociationAdminPageModel
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        private readonly IWebHostEnvironment _env;
        private readonly UploadSettings _uploads;

        public MembersModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IWebHostEnvironment env, IOptions<UploadSettings> uploads)
            : base(userManager, context)
        {
            _env = env;
            _uploads = uploads.Value;
        }

        [BindProperty(SupportsGet = true)]
        public int? EditId { get; set; }

        [BindProperty]
        public OfficerInput Input { get; set; } = new();

        [BindProperty]
        public IFormFile? Picture { get; set; }

        public List<AssociationOfficer> Officers { get; private set; } = new();
        public bool IsEditing => EditId.HasValue;

        public class OfficerInput
        {
            [Required]
            [StringLength(160)]
            public string Name { get; set; } = string.Empty;

            [Required]
            [StringLength(100)]
            public string Role { get; set; } = string.Empty;

            [StringLength(500)]
            public string? Bio { get; set; }

            public int DisplayOrder { get; set; }

            public bool IsActive { get; set; } = true;

            public string? ExistingPictureUrl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            await LoadPageDataAsync();

            if (EditId.HasValue)
            {
                var officer = Officers.FirstOrDefault(o => o.Id == EditId.Value);
                if (officer == null) return NotFound();

                Input = new OfficerInput
                {
                    Name = officer.Name,
                    Role = officer.Role,
                    Bio = officer.Bio,
                    DisplayOrder = officer.DisplayOrder,
                    IsActive = officer.IsActive,
                    ExistingPictureUrl = officer.PictureUrl
                };
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            if (Picture != null)
            {
                if (Picture.Length > MaxFileSizeBytes)
                    ModelState.AddModelError(nameof(Picture), "Image must be 5 MB or smaller.");

                var ext = Path.GetExtension(Picture.FileName);
                if (!AllowedExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(Picture), "Only JPG, PNG, GIF, or WebP images are allowed.");
            }

            if (!ModelState.IsValid)
            {
                await LoadPageDataAsync();
                return Page();
            }

            string? pictureUrl = Input.ExistingPictureUrl;
            if (Picture != null && Picture.Length > 0)
            {
                pictureUrl = await SavePictureAsync(Picture);
            }

            if (EditId.HasValue)
            {
                var existing = await Context.AssociationOfficers
                    .FirstOrDefaultAsync(o => o.Id == EditId.Value && o.GolfAssociationId == CurrentAssociation.Id);
                if (existing == null) return NotFound();

                // Delete old picture if a new one was uploaded
                if (pictureUrl != null && pictureUrl != existing.PictureUrl)
                    DeletePicture(existing.PictureUrl);

                existing.Name = Input.Name.Trim();
                existing.Role = Input.Role.Trim();
                existing.Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim();
                existing.PictureUrl = pictureUrl;
                existing.DisplayOrder = Input.DisplayOrder;
                existing.IsActive = Input.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;

                await Context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"{existing.Name} updated.";
            }
            else
            {
                var officer = new AssociationOfficer
                {
                    GolfAssociationId = CurrentAssociation.Id,
                    Name = Input.Name.Trim(),
                    Role = Input.Role.Trim(),
                    Bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim(),
                    PictureUrl = pictureUrl,
                    DisplayOrder = Input.DisplayOrder,
                    IsActive = Input.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                Context.AssociationOfficers.Add(officer);
                await Context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"{officer.Name} added.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var contextResult = await LoadAssociationContextAsync();
            if (contextResult is not null) return contextResult;

            var officer = await Context.AssociationOfficers
                .FirstOrDefaultAsync(o => o.Id == id && o.GolfAssociationId == CurrentAssociation.Id);
            if (officer == null) return NotFound();

            DeletePicture(officer.PictureUrl);
            Context.AssociationOfficers.Remove(officer);
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{officer.Name} removed.";
            return RedirectToPage();
        }

        private async Task LoadPageDataAsync()
        {
            Officers = await Context.AssociationOfficers
                .Where(o => o.GolfAssociationId == CurrentAssociation.Id)
                .OrderBy(o => o.DisplayOrder)
                .ThenBy(o => o.Name)
                .ToListAsync();
        }

        private async Task<string?> SavePictureAsync(IFormFile file)
        {
            var uploadsDir = Path.Combine(_uploads.PhysicalRoot, "members");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{CurrentAssociation.Id}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"{_uploads.RequestPath}/members/{fileName}";
        }

        private void DeletePicture(string? pictureUrl)
        {
            if (string.IsNullOrWhiteSpace(pictureUrl)) return;
            var relative = pictureUrl.StartsWith(_uploads.RequestPath)
                ? pictureUrl.Substring(_uploads.RequestPath.Length).TrimStart('/')
                : pictureUrl.TrimStart('/');
            var filePath = Path.Combine(_uploads.PhysicalRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }
}
