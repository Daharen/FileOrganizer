namespace FileOrganizer.Core.Settings;

public sealed class LlmProviderSettings
{
    public string ProviderId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
    public string? BaseUrl { get; init; }
    public string? ModelName { get; init; }
    public bool PersistLocally { get; init; }
    public bool ExportToUserEnvironment { get; init; }
    public bool Enabled { get; init; }
}
