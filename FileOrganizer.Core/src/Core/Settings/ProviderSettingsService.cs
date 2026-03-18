namespace FileOrganizer.Core.Settings;

public sealed class ProviderSettingsService
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IUserEnvironmentWriter _environmentWriter;

    public ProviderSettingsService(
        IAppSettingsStore settingsStore,
        IUserEnvironmentWriter environmentWriter)
    {
        _settingsStore = settingsStore;
        _environmentWriter = environmentWriter;
    }

    public AppSettings Load()
    {
        return _settingsStore.Load();
    }

    public void Save(AppSettings settings)
    {
        _settingsStore.Save(settings);

        foreach (var provider in settings.Providers.Where(p => p.ExportToUserEnvironment))
        {
            var prefix = provider.ProviderId.Trim().ToUpperInvariant().Replace("-", "_");

            _environmentWriter.SetUserVariable($"{prefix}_API_KEY", provider.ApiKey);
            _environmentWriter.SetUserVariable($"{prefix}_BASE_URL", provider.BaseUrl);
            _environmentWriter.SetUserVariable($"{prefix}_MODEL", provider.ModelName);
        }
    }

    public string GetSettingsPath()
    {
        return _settingsStore.GetSettingsPath();
    }
}
