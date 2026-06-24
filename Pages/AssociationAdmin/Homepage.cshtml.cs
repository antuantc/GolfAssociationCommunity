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
    [RequestSizeLimit(100 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    public class HomepageModel : AssociationAdminPageModel
    {
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".webm", ".mov" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;
        private const long MaxVideoSizeBytes = 100 * 1024 * 1024;

        private readonly IWebHostEnvironment _env;
        private readonly UploadSettings _uploads;

        public HomepageModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IWebHostEnvironment env, IOptions<UploadSettings> uploads)
            : base(userManager, context)
        {
            _env = env;
            _uploads = uploads.Value;
        }

        // Hero & branding
        [BindProperty] public HeroSettingsInput HeroSettings { get; set; } = new();
        [BindProperty] public IFormFile? HeroImage { get; set; }
        [BindProperty] public IFormFile? HeroVideo { get; set; }
        [BindProperty] public bool ClearHeroVideo { get; set; }

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
        [BindProperty] public SponsorInput EditSponsor { get; set; } = new();
        [BindProperty] public IFormFile? EditSponsorLogo { get; set; }
        public int? EditSponsorId { get; private set; }

        // Charities
        [BindProperty] public CharityInput NewCharity { get; set; } = new();
        [BindProperty] public IFormFile? CharityImage { get; set; }
        [BindProperty] public CharityInput EditCharity { get; set; } = new();
        [BindProperty] public IFormFile? EditCharityImage { get; set; }
        public int? EditCharityId { get; private set; }

        // Display data
        public List<AssociationMedia> MediaItems { get; private set; } = new();
        public List<AssociationSponsor> Sponsors { get; private set; } = new();
        public List<AssociationCharity> Charities { get; private set; } = new();
        public GolfAssociation? AssociationData { get; private set; }

        // ── Input models ─────────────────────────────────────────────────

        public class HeroSettingsInput
        {
            [StringLength(120)] public string? Tagline { get; set; }
            [StringLength(500)] public string? Motto { get; set; }
            [Range(1800, 2100)]  public int? EstYear { get; set; }
            [StringLength(1000)] public string? Description { get; set; }
            public string? ExistingHeroImageUrl { get; set; }
            public string? ExistingHeroVideoUrl { get; set; }
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
            [Required][StringLength(160)] public string Name { get; set; } = string.Empty;
            [StringLength(1000)] public string? Description { get; set; }
            [StringLength(500)] public string? Url { get; set; }
            [Range(0, 1000)] public int DisplayOrder { get; set; }
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
                ExistingHeroImageUrl = AssociationData.HeroImageUrl,
                ExistingHeroVideoUrl = AssociationData.HeroVideoUrl
            };

            // Auto-increment display order for new sponsor
            NewSponsor.DisplayOrder = Sponsors.Count > 0 ? Sponsors.Max(s => s.DisplayOrder) + 1 : 0;

            // Populate edit form if a sponsor edit is requested
            if (Request.Query.TryGetValue("editId", out var editIdStr) && int.TryParse(editIdStr, out var editId))
            {
                EditSponsorId = editId;
                var editing = Sponsors.FirstOrDefault(s => s.Id == editId);
                if (editing != null)
                {
                    EditSponsor = new SponsorInput
                    {
                        Name = editing.Name,
                        Category = editing.Category,
                        Website = editing.Website,
                        DisplayOrder = editing.DisplayOrder
                    };
                }
            }

            // Auto-increment display order for new charity
            NewCharity.DisplayOrder = Charities.Count > 0 ? Charities.Max(c => c.DisplayOrder) + 1 : 0;

            // Populate edit form if a charity edit is requested
            if (Request.Query.TryGetValue("editCharityId", out var editCharityIdStr) && int.TryParse(editCharityIdStr, out var editCharityId))
            {
                EditCharityId = editCharityId;
                var editingCharity = Charities.FirstOrDefault(c => c.Id == editCharityId);
                if (editingCharity != null)
                {
                    EditCharity = new CharityInput
                    {
                        Name = editingCharity.Name,
                        Description = editingCharity.Description,
                        Url = editingCharity.Url,
                        DisplayOrder = editingCharity.DisplayOrder
                    };
                }
            }

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

            if (HeroVideo != null)
            {
                if (HeroVideo.Length > MaxVideoSizeBytes)
                    ModelState.AddModelError(nameof(HeroVideo), "Video must be 100 MB or smaller.");
                var ext = Path.GetExtension(HeroVideo.FileName);
                if (!AllowedVideoExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(HeroVideo), "Only MP4, WebM, or MOV videos are allowed.");
            }

            // Only validate fields belonging to this form; other [BindProperty] fields
            // (e.g. NewSponsor.Name which is [Required]) are not submitted here and
            // would otherwise cause ModelState to be invalid.
            var heroRelevantPrefixes = new[] { "HeroSettings", nameof(HeroImage), nameof(HeroVideo), nameof(ClearHeroVideo) };
            foreach (var key in ModelState.Keys.Where(k => !heroRelevantPrefixes.Any(p => k.StartsWith(p))).ToList())
                ModelState.Remove(key);

            if (!ModelState.IsValid) { await LoadPageDataAsync(); return Page(); }

            var assoc = await Context.GolfAssociations.FindAsync(CurrentAssociation.Id);
            if (assoc == null) return NotFound();

            if (HeroImage != null && HeroImage.Length > 0)
            {
                DeleteFile(assoc.HeroImageUrl);
                assoc.HeroImageUrl = await SaveFileAsync(HeroImage, "hero");
            }

            if (ClearHeroVideo)
            {
                DeleteFile(assoc.HeroVideoUrl);
                assoc.HeroVideoUrl = null;
            }
            else if (HeroVideo != null && HeroVideo.Length > 0)
            {
                DeleteFile(assoc.HeroVideoUrl);
                assoc.HeroVideoUrl = await SaveFileAsync(HeroVideo, "hero");
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

            // Only validate fields belonging to this form
            var addSponsorPrefixes = new[] { "NewSponsor", nameof(SponsorLogo) };
            foreach (var key in ModelState.Keys.Where(k => !addSponsorPrefixes.Any(p => k.StartsWith(p))).ToList())
                ModelState.Remove(key);

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
                DisplayOrder = Sponsors.Count > 0 ? Sponsors.Max(s => s.DisplayOrder) + 1 : 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{NewSponsor.Name} added as sponsor.";
            return LocalRedirect(Url.Page("/AssociationAdmin/Homepage") + "#sponsors");
        }

        // ── Edit sponsor ─────────────────────────────────────────────────

        public async Task<IActionResult> OnPostEditSponsorAsync(int id)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (EditSponsorLogo != null)
            {
                if (EditSponsorLogo.Length > MaxFileSizeBytes)
                    ModelState.AddModelError(nameof(EditSponsorLogo), "Logo must be 5 MB or smaller.");
                var ext = Path.GetExtension(EditSponsorLogo.FileName);
                if (!AllowedImageExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(EditSponsorLogo), "Only JPG, PNG, GIF, or WebP images are allowed.");
            }

            // Only validate fields belonging to this form
            var editSponsorPrefixes = new[] { "EditSponsor", nameof(EditSponsorLogo) };
            foreach (var key in ModelState.Keys.Where(k => !editSponsorPrefixes.Any(p => k.StartsWith(p))).ToList())
                ModelState.Remove(key);

            if (!ModelState.IsValid)
            {
                EditSponsorId = id;
                await LoadPageDataAsync();
                return Page();
            }

            var sponsor = await Context.AssociationSponsors
                .FirstOrDefaultAsync(s => s.Id == id && s.GolfAssociationId == CurrentAssociation.Id);
            if (sponsor is null) return NotFound();

            if (EditSponsorLogo != null && EditSponsorLogo.Length > 0)
            {
                DeleteFile(sponsor.LogoUrl);
                sponsor.LogoUrl = await SaveFileAsync(EditSponsorLogo, "sponsors");
            }

            sponsor.Name = EditSponsor.Name.Trim();
            sponsor.Category = string.IsNullOrWhiteSpace(EditSponsor.Category) ? null : EditSponsor.Category.Trim().ToUpperInvariant();
            sponsor.Website = string.IsNullOrWhiteSpace(EditSponsor.Website) ? null : EditSponsor.Website.Trim();
            sponsor.DisplayOrder = EditSponsor.DisplayOrder;

            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{sponsor.Name} updated.";
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

        // ── Add charity ──────────────────────────────────────────────────

        public async Task<IActionResult> OnPostAddCharityAsync()
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (CharityImage != null)
            {
                if (CharityImage.Length > MaxFileSizeBytes)
                    ModelState.AddModelError(nameof(CharityImage), "Image must be 5 MB or smaller.");
                var ext = Path.GetExtension(CharityImage.FileName);
                if (!AllowedImageExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(CharityImage), "Only JPG, PNG, GIF, or WebP images are allowed.");
            }

            var addCharityPrefixes = new[] { "NewCharity", nameof(CharityImage) };
            foreach (var key in ModelState.Keys.Where(k => !addCharityPrefixes.Any(p => k.StartsWith(p))).ToList())
                ModelState.Remove(key);

            if (!ModelState.IsValid) { await LoadPageDataAsync(); return Page(); }

            string? imageUrl = null;
            if (CharityImage != null && CharityImage.Length > 0)
                imageUrl = await SaveFileAsync(CharityImage, "charities");

            Context.AssociationCharities.Add(new AssociationCharity
            {
                GolfAssociationId = CurrentAssociation.Id,
                Name = NewCharity.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(NewCharity.Description) ? null : NewCharity.Description.Trim(),
                Url = string.IsNullOrWhiteSpace(NewCharity.Url) ? null : NewCharity.Url.Trim(),
                ImageUrl = imageUrl,
                DisplayOrder = Charities.Count > 0 ? Charities.Max(c => c.DisplayOrder) + 1 : 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{NewCharity.Name} added."
;
            return LocalRedirect(Url.Page("/AssociationAdmin/Homepage") + "#charities");
        }

        // ── Edit charity ───────────────────────────────────────────────

        public async Task<IActionResult> OnPostEditCharityAsync(int id)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            if (EditCharityImage != null)
            {
                if (EditCharityImage.Length > MaxFileSizeBytes)
                    ModelState.AddModelError(nameof(EditCharityImage), "Image must be 5 MB or smaller.");
                var ext = Path.GetExtension(EditCharityImage.FileName);
                if (!AllowedImageExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(EditCharityImage), "Only JPG, PNG, GIF, or WebP images are allowed.");
            }

            var editCharityPrefixes = new[] { "EditCharity", nameof(EditCharityImage) };
            foreach (var key in ModelState.Keys.Where(k => !editCharityPrefixes.Any(p => k.StartsWith(p))).ToList())
                ModelState.Remove(key);

            if (!ModelState.IsValid)
            {
                EditCharityId = id;
                await LoadPageDataAsync();
                return Page();
            }

            var charity = await Context.AssociationCharities
                .FirstOrDefaultAsync(c => c.Id == id && c.GolfAssociationId == CurrentAssociation.Id);
            if (charity is null) return NotFound();

            if (EditCharityImage != null && EditCharityImage.Length > 0)
            {
                DeleteFile(charity.ImageUrl);
                charity.ImageUrl = await SaveFileAsync(EditCharityImage, "charities");
            }

            charity.Name = EditCharity.Name.Trim();
            charity.Description = string.IsNullOrWhiteSpace(EditCharity.Description) ? null : EditCharity.Description.Trim();
            charity.Url = string.IsNullOrWhiteSpace(EditCharity.Url) ? null : EditCharity.Url.Trim();
            charity.DisplayOrder = EditCharity.DisplayOrder;

            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{charity.Name} updated.";
            return LocalRedirect(Url.Page("/AssociationAdmin/Homepage") + "#charities");
        }

        // ── Delete charity ──────────────────────────────────────────────

        public async Task<IActionResult> OnPostDeleteCharityAsync(int id)
        {
            var ctx = await LoadAssociationContextAsync();
            if (ctx is not null) return ctx;

            var charity = await Context.AssociationCharities
                .FirstOrDefaultAsync(c => c.Id == id && c.GolfAssociationId == CurrentAssociation.Id);
            if (charity == null) return NotFound();

            DeleteFile(charity.ImageUrl);
            Context.AssociationCharities.Remove(charity);
            await Context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{charity.Name} removed.";
            return LocalRedirect(Url.Page("/AssociationAdmin/Homepage") + "#charities");
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
            Charities = await Context.AssociationCharities
                .Where(c => c.GolfAssociationId == CurrentAssociation.Id)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        private async Task<string> SaveFileAsync(IFormFile file, string subfolder)
        {
            var dir = Path.Combine(_uploads.PhysicalRoot, subfolder);
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{CurrentAssociation.Id}_{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(dir, fileName);
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"{_uploads.RequestPath}/{subfolder}/{fileName}";
        }

        private void DeleteFile(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            var relative = url.StartsWith(_uploads.RequestPath)
                ? url.Substring(_uploads.RequestPath.Length).TrimStart('/')
                : url.TrimStart('/');
            var path = Path.Combine(_uploads.PhysicalRoot, relative.Replace('/', Path.DirectorySeparatorChar));
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
