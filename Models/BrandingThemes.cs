namespace GolfAssociationCommunity.Models
{
    public record BrandingThemeOption(string Key, string Name, string Description);

    public static class BrandingThemes
    {
        public const string DefaultKey = "links-classic";

        public static IReadOnlyList<BrandingThemeOption> Options { get; } =
            new List<BrandingThemeOption>
            {
                new("links-classic", "Links Classic", "Traditional greens and navy inspired by historic links courses."),
                new("sunrise-tee", "Sunrise Tee", "Warm morning fairway tones with bright sky accents."),
                new("sand-wedge", "Sand Wedge", "Coastal bunker palette with natural sand and deep blue."),
                new("ocean-fairway", "Ocean Fairway", "Sea breeze blues and rich turf greens."),
                new("pine-bogey", "Pine Bogey", "Evergreen clubhouse style with earthy neutrals."),
                new("sunset-backnine", "Sunset Back Nine", "Late-round sunset oranges over cool greens."),
                new("masters-azalea", "Masters Azalea", "Tournament-week greens with azalea pink accents."),
                new("st-andrews", "St Andrews Stone", "Stone, slate, and moss tones inspired by old course architecture."),
                new("desert-dunes", "Desert Dunes", "Desert course palette with amber sand and cactus green."),
                new("midnight-drivingrange", "Midnight Driving Range", "Evening range look with deep navy and electric highlights."),
                new("iowa-asian-golf", "Iowa Asian Golf", "Deep Iowa fairway greens with IAGA signature red — matching iowaasiangolf.com.")
            };

        public static bool IsValid(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return Options.Any(option => option.Key == key.Trim());
        }

        public static string Normalize(string? key)
        {
            if (!IsValid(key))
            {
                return DefaultKey;
            }

            return key!.Trim();
        }
    }
}
