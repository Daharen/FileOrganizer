namespace FileOrganizer.Core.Settings;

public sealed class AppSettings
{
    public List<LlmProviderSettings> Providers { get; init; } = new();
}
