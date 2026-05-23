using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class HomepageModel : AssociationAdminPageModel
    {
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        private readonly IWebHostEnvironment _env;

        public HomepageModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IWebHostEnvironment env)
            : base(userManager, context)
        {
            _env = env;
        }

        // Hero & branding
        [BindProperty] public HeroSettingsInput HeroSettings { get; set; } = new();
        [BindProperty] public IFormFile? HeroImage { get; set; }

        // Gallery – photo upload
        [BindProperty] public IFormFile? PhotoUpload { get; set; }
        [BindProperty] public string? PhotoCaption { get; set; }

        // Gallery – video embed
        [BindProperty] public string? VideoUrl { get; set; }
        [BindProperty] public string? VideoCaption { get; set; }
        [BindProperty] public bool VideoAutoPlay { get; set; }

        // Sponsors
        [BindProperty] public SponsorInput NewSponsor { get; set; } = new();
        [BindProperty] public IFormFile? SponsorLogo { get; set; }

        // Charity
        [BindProperty] public CharityInput Charity { get; set; } = new();

        // Display data
        public List<AssociationMedia> MediaItems { get; private set; } = new();
        public List<AssociationSponsor> Sponsors { get; private set; } = new();
        public GolfAssociation? AssociationData { get; private set; }

        // ── Input models ─────────────────────────────────────────────────

        public class HeroSettingsInput
        {
            [StringLength(120)] public string? Tagline { get; set; }
            [StringLength(500)] public string? Motto { get; set; }
            [Range(1800, 2100)]  public int? EstYear { get; set; }
            [StringLength(1000)] public string? Description { get; set; }
            public string? ExistingHeroImageUrl { get; set; }
        }

        public class SponsorInput
        {
            [Required][StringLength(160)] public string Name { get; set; } = string.Empty;
            [StringLength(80)]  public string? Category { get; set; }
            [StringLength(500)] public string? Website { get; set; }
            public int DisplayOrder { get; set; }
        }

        public class CharityInput
        {
            [StringLength(160)] public string? CharityName { get; set; }
            [StringLength(1000)] public string? CharityDescription { get; set; }
            [StringLength(500)] public string? CharityUrl { get; set; }
        }

        // ── Handlers ─────────────────────────────────────────────────────

        public async Task<IActionResult> OnGetAsync()
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;
            await LoadPageDataAsync();

            HeroSettings = new HeroSettingsInput
            {
                Tagline = AssociationData!.Tagline,
                Motto = AssociationData.Motto,
                EstYear = AssociationData.EstYear,
                Description = AssociationData.Description,
                ExistingHeroImageUrl = AssociationData.HeroImageUrl
            };
            Charity = new CharityInput
            {
                CharityName = AssociationData.CharityName,
                CharityDescription = AssociationData.CharityDescription,
                CharityUrl = AssociationData.CharityUrl
            };
            return Page();
        }

        // ── Save hero ────────────────────────────────────────────────────

        public async Task<IActionResult> OnPostSaveHeroAsync()
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (HeroImage != null)
            {
                if (HeroImage.Length > MaxFileSizeBytes)
                    ModelState.AddModelError(nameof(HeroImage), "Image must be 5 MB or smaller.");
                var ext = Path.GetExtension(HeroImage.FileName);
                if (!AllowedImageExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(HeroImage), "Only JPG, PNG, GIF, or WebP images are allowed.");
            }

            if (!ModelState.IsValid) { await LoadPageDataAsync(); return Page(); }

            var assoc = await Context.GolfAssociations.FindAsync(CurrentAssociation.Id);
            if (assoc == null) return NotFound();

            if (HeroImage != null && HeroImage.Length > 0)
            {
                DeleteFile(assoc.HeroImageUrl);
                assoc.HeroImageUrl = await SaveFileAsync(HeroImage, "hero");
            }

            assoc.Tagline = string.IsNullOrWhiteSpace(HeroSettings.Tagline) ? null : HeroSettings.Tagline.Trim();
            assoc.Motto = string.IsNullOrWhiteSpace(HeroSettings.Motto) ? null : HeroSettings.Motto.Trim();
            assoc.EstYear = HeroSettings.EstYear;
            assoc.Description = string.IsNullOrWhiteSpace(HeroSettings.Description) ? null : HeroSettings.Description.Trim();
            assoc.UpdatedAt = DateTime.UtcNow;

            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Hero & branding saved.";
            return RedirectToPage();
        }

        // ── Add photo ────────────────────────────────────────────────────

        public async Task<IActionResult> OnPostAddPhotoAsync()
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (PhotoUpload == null || PhotoUpload.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a photo file.";
                return RedirectToPage();
            }
            if (PhotoUpload.Length > MaxFileSizeBytes)
            {
                TempData["ErrorMessage"] = "Photo must be 5 MB or smaller.";
                return RedirectToPage();
            }
            var ext = Path.GetExtension(PhotoUpload.FileName);
            if (!AllowedImageExtensions.Contains(ext))
            {
                TempData["ErrorMessage"] = "Only JPG, PNG, GIF, or WebP images are allowed.";
                return RedirectToPage();
            }

            var url = await SaveFileAsync(PhotoUpload, "gallery");
            var nextOrder = await Context.AssociationMedia
                .Where(m => m.GolfAssociationId == CurrentAssociation.Id)
                .Select(m => (int?)m.DisplayOrder).MaxAsync() ?? 0;

            Context.AssociationMedia.Add(new AssociationMedia
            {
                GolfAssociationId = CurrentAssociation.Id,
                MediaType = MediaType.Photo,
                Url = url,
                Caption = string.IsNullOrWhiteSpace(PhotoCaption) ? null : PhotoCaption.Trim(),
                DisplayOrder = nextOrder + 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Photo added.";
            return RedirectToPage();
        }

        // ── Add video ────────────────────────────────────────────────────

        public async Task<IActionResult> OnPostAddVideoAsync()
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (string.IsNullOrWhiteSpace(VideoUrl))
            {
                TempData["ErrorMessage"] = "Please enter a video URL.";
                return RedirectToPage();
            }

            var embedUrl = BuildEmbedUrl(VideoUrl.Trim());
            if (embedUrl == null)
            {
                TempData["ErrorMessage"] = "Only YouTube and Vimeo URLs are supported.";
                return RedirectToPage();
            }

            var nextOrder = await Context.AssociationMedia
                .Where(m => m.GolfAssociationId == CurrentAssociation.Id)
                .Select(m => (int?)m.DisplayOrder).MaxAsync() ?? 0;

            Context.AssociationMedia.Add(new AssociationMedia
            {
                GolfAssociationId = CurrentAssociation.Id,
                MediaType = MediaType.Video,
                Url = embedUrl,
                Caption = string.IsNullOrWhiteSpace(VideoCaption) ? null : VideoCaption.Trim(),
                DisplayOrder = nextOrder + 1,
                IsActive = true,
                AutoPlay = VideoAutoPlay,
                CreatedAt = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Video added.";
            return RedirectToPage();
        }

        // ── Delete media ─────────────────────────────────────────────────

        public async Task<IActionResult> OnPostDeleteMediaAsync(int id)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            var item = await Context.AssociationMedia
                .FirstOrDefaultAsync(m => m.Id == id && m.GolfAssociationId == CurrentAssociation.Id);
            if (item == null) return NotFound();

            if (item.MediaType == MediaType.Photo)
                DeleteFile(item.Url);

            Context.AssociationMedia.Remove(item);
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Media removed.";
            return RedirectToPage();
        }

        // ── Add sponsor ──────────────────────────────────────────────────

        public async Task<IActionResult> OnPostAddSponsorAsync()
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (SponsorLogo != null)
            {
                if (SponsorLogo.Length > MaxFileSizeBytes)
                    ModelState.AddModelError(nameof(SponsorLogo), "Logo must be 5 MB or smaller.");
                var ext = Path.GetExtension(SponsorLogo.FileName);
                if (!AllowedImageExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(SponsorLogo), "Only JPG, PNG, GIF, or WebP images are allowed.");
            }

            if (!ModelState.IsValid) { await LoadPageDataAsync(); return Page(); }

            string? logoUrl = null;
            if (SponsorLogo != null && SponsorLogo.Length > 0)
                logoUrl = await SaveFileAsync(SponsorLogo, "sponsors");

            Context.AssociationSponsors.Add(new AssociationSponsor
            {
                GolfAssociationId = CurrentAssociation.Id,
                Name = NewSponsor.Name.Trim(),
                Category = string.IsNullOrWhiteSpace(NewSponsor.Category) ? null : NewSponsor.Category.Trim().ToUpperInvariant(),
                Website = string.IsNullOrWhiteSpace(NewSponsor.Website) ? null : NewSponsor.Website.Trim(),
                LogoUrl = logoUrl,
                DisplayOrder = NewSponsor.DisplayOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{NewSponsor.Name} added as sponsor.";
            return RedirectToPage();
        }

        // ── Delete sponsor ───────────────────────────────────────────────

        public async Task<IActionResult> OnPostDeleteSponsorAsync(int id)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            var sponsor = await Context.AssociationSponsors
                .FirstOrDefaultAsync(s => s.Id == id && s.GolfAssociationId == CurrentAssociation.Id);
            if (sponsor == null) return NotFound();

            DeleteFile(sponsor.LogoUrl);
            Context.AssociationSponsors.Remove(sponsor);
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{sponsor.Name} removed.";
            return RedirectToPage();
        }

        // ── Save charity ─────────────────────────────────────────────────

        public async Task<IActionResult> OnPostSaveCharityAsync()
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            var assoc = await Context.GolfAssociations.FindAsync(CurrentAssociation.Id);
            if (assoc == null) return NotFound();

            assoc.CharityName = string.IsNullOrWhiteSpace(Charity.CharityName) ? null : Charity.CharityName.Trim();
            assoc.CharityDescription = string.IsNullOrWhiteSpace(Charity.CharityDescription) ? null : Charity.CharityDescription.Trim();
            assoc.CharityUrl = string.IsNullOrWhiteSpace(Charity.CharityUrl) ? null : Charity.CharityUrl.Trim();
            assoc.UpdatedAt = DateTime.UtcNow;

            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Charity details saved.";
            return RedirectToPage();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private async Task LoadPageDataAsync()
        {
            AssociationData = await Context.GolfAssociations.FindAsync(CurrentAssociation.Id);
            MediaItems = await Context.AssociationMedia
                .Where(m => m.GolfAssociationId == CurrentAssociation.Id)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();
            Sponsors = await Context.AssociationSponsors
                .Where(s => s.GolfAssociationId == CurrentAssociation.Id)
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }

        private async Task<string> SaveFileAsync(IFormFile file, string subfolder)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", subfolder);
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{CurrentAssociation.Id}_{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(dir, fileName);
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/{subfolder}/{fileName}";
        }

        private void DeleteFile(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var path = Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        internal static string? BuildEmbedUrl(string url)
        {
            // YouTube: watch?v=ID, youtu.be/ID, embed/ID
            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                string? id = null;
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    if (uri.Host.Contains("youtu.be"))
                    {
                        id = uri.AbsolutePath.TrimStart('/').Split('?')[0];
                    }
                    else
                    {
                        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                        id = query["v"];
                        if (string.IsNullOrEmpty(id))
                        {
                            var seg = uri.AbsolutePath.Split('/');
                            var embedIdx = Array.IndexOf(seg, "embed");
                            if (embedIdx >= 0 && embedIdx + 1 < seg.Length)
                                id = seg[embedIdx + 1];
                        }
                    }
                }
                return string.IsNullOrEmpty(id) ? null : $"https://www.youtube.com/embed/{id}";
            }
            // Vimeo
            if (url.Contains("vimeo.com"))
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var id = uri.AbsolutePath.TrimStart('/').Split('/').LastOrDefault(s => s.All(char.IsDigit));
                    return string.IsNullOrEmpty(id) ? null : $"https://player.vimeo.com/video/{id}";
                }
            }
            return null;
        }
    }
}
