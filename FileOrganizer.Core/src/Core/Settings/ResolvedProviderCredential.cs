namespace FileOrganizer.Core.Settings;

public sealed class ResolvedProviderCredential
{
    public string ProviderId { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
    public string? BaseUrl { get; init; }
    public string? ModelName { get; init; }
    public string Source { get; init; } = "None";
    public bool IsAvailable => !string.IsNullOrWhiteSpace(ApiKey);
}
