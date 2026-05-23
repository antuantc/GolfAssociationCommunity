namespace GolfAssociationCommunity.Services
{
    /// <summary>
    /// Controls where user-uploaded files are stored and served from.
    /// Set UploadsPath in appsettings.json (or appsettings.Production.json) to an
    /// absolute directory OUTSIDE the application publish folder so that files survive
    /// deployments. If left empty the app defaults to a "persistent-uploads" folder
    /// next to the application content root.
    /// </summary>
    public class UploadSettings
    {
        /// <summary>Absolute physical path to the uploads root directory.</summary>
        public string PhysicalRoot { get; set; } = string.Empty;

        /// <summary>URL request path prefix under which uploads are served.</summary>
        public string RequestPath { get; set; } = "/uploads";
    }
}
