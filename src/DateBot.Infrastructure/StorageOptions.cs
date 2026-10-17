namespace DateBot.Infrastructure;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>
    /// Empty = the user profile (<c>%LOCALAPPDATA%\DateBot</c> on Windows), outside the repository, so the
    /// session and database survive rebuilds and <c>git clean</c>. Relative paths are resolved against the app folder.
    /// </summary>
    public string DataDirectory { get; init; } = string.Empty;

    public string DatabaseFile { get; init; } = "datebot.db";

    public string PhotosDirectory { get; init; } = "photos";

    public string ResolveDataDirectory() =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DateBot")
            : Path.IsPathRooted(DataDirectory) ? DataDirectory : Path.Combine(AppContext.BaseDirectory, DataDirectory);

    public string DatabasePath => Path.Combine(ResolveDataDirectory(), DatabaseFile);

    public string PhotosPath => Path.Combine(ResolveDataDirectory(), PhotosDirectory);
}
