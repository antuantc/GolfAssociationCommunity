using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace GolfAssociationCommunity.Pages.AssociationAdmin
{
    public class CsvImportModel : AssociationAdminPageModel
    {
        private readonly ICsvImportService _csv;

        public CsvImportModel(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ICsvImportService csv)
            : base(userManager, context)
        {
            _csv = csv;
        }

        public ImportResult? Result { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var guard = await LoadAssociationContextAsync();
            return guard ?? Page();
        }

        // Template download: ?handler=Template&type=players
        public async Task<IActionResult> OnGetTemplateAsync(string type)
        {
            var guard = await LoadAssociationContextAsync();
            if (guard != null) return guard;

            var content = _csv.GetTemplate(type);
            if (string.IsNullOrEmpty(content)) return NotFound();
            var bytes = Encoding.UTF8.GetBytes(content);
            return File(bytes, "text/csv", $"{type}-template.csv");
        }

        public async Task<IActionResult> OnPostImportPlayersAsync(IFormFile? file)
            => await HandleImport(file, (stream, id) => _csv.ImportPlayersAsync(stream, id));

        public async Task<IActionResult> OnPostImportScoresAsync(IFormFile? file)
            => await HandleImport(file, (stream, id) => _csv.ImportScoresAsync(stream, id));

        public async Task<IActionResult> OnPostImportRegistrationsAsync(IFormFile? file)
            => await HandleImport(file, (stream, id) => _csv.ImportRegistrationsAsync(stream, id));

        public async Task<IActionResult> OnPostImportLeaderboardAsync(IFormFile? file)
            => await HandleImport(file, (stream, id) => _csv.ImportLeaderboardAsync(stream, id));

        private async Task<IActionResult> HandleImport(
            IFormFile? file,
            Func<Stream, int, Task<ImportResult>> importFn)
        {
            var guard = await LoadAssociationContextAsync();
            if (guard != null) return guard;

            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please select a CSV file.");
                return Page();
            }
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Only .csv files are accepted.");
                return Page();
            }

            using var stream = file.OpenReadStream();
            Result = await importFn(stream, CurrentAssociation.Id);
            return Page();
        }
    }
}
