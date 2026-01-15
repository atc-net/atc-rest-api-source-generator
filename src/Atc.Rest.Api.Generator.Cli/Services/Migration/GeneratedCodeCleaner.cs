namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Cleans old generated code files from projects before source generator migration.
/// </summary>
internal static class GeneratedCodeCleaner
{
    /// <summary>
    /// Folders to delete from Api.Generated project.
    /// </summary>
    private static readonly string[] ServerFoldersToDelete =
    [
        "Contracts",
        "Endpoints",
        "Resources",
    ];

    /// <summary>
    /// Files to delete from Api.Generated project.
    /// </summary>
    private static readonly string[] ServerFilesToDelete =
    [
        "GlobalUsings.cs",
        "IApiContractAssemblyMarker.cs",
    ];

    /// <summary>
    /// Folders to delete from ApiClient.Generated project.
    /// </summary>
    private static readonly string[] ClientFoldersToDelete =
    [
        "Contracts",
        "Endpoints",
    ];

    /// <summary>
    /// Files to delete from ApiClient.Generated project.
    /// </summary>
    private static readonly string[] ClientFilesToDelete =
    [
        "GlobalUsings.cs",
    ];

    /// <summary>
    /// Cleans old generated code from the Api.Generated project.
    /// </summary>
    /// <param name="projectDirectory">The directory of the Api.Generated project.</param>
    /// <param name="dryRun">If true, only returns what would be deleted.</param>
    /// <returns>List of deleted items.</returns>
    public static CleanupResult CleanServerProject(
        string projectDirectory,
        bool dryRun = false)
        => CleanProject(projectDirectory, ServerFoldersToDelete, ServerFilesToDelete, dryRun);

    /// <summary>
    /// Cleans old generated code from the ApiClient.Generated project.
    /// </summary>
    /// <param name="projectDirectory">The directory of the ApiClient.Generated project.</param>
    /// <param name="dryRun">If true, only returns what would be deleted.</param>
    /// <returns>List of deleted items.</returns>
    public static CleanupResult CleanClientProject(
        string projectDirectory,
        bool dryRun = false)
        => CleanProject(projectDirectory, ClientFoldersToDelete, ClientFilesToDelete, dryRun);

    private static CleanupResult CleanProject(
        string projectDirectory,
        string[] foldersToDelete,
        string[] filesToDelete,
        bool dryRun)
    {
        var result = new CleanupResult();

        // Delete folders
        foreach (var folder in foldersToDelete)
        {
            var folderPath = Path.Combine(projectDirectory, folder);
            if (Directory.Exists(folderPath))
            {
                var fileCount = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).Length;
                result.DeletedFolders.Add(new DeletedItem
                {
                    Path = folderPath,
                    ItemCount = fileCount,
                });

                if (!dryRun)
                {
                    Directory.Delete(folderPath, recursive: true);
                }
            }
        }

        // Delete files
        foreach (var file in filesToDelete)
        {
            var filePath = Path.Combine(projectDirectory, file);
            if (File.Exists(filePath))
            {
                result.DeletedFiles.Add(filePath);

                if (!dryRun)
                {
                    File.Delete(filePath);
                }
            }
        }

        return result;
    }
}