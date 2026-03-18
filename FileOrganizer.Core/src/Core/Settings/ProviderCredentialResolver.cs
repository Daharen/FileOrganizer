namespace FileOrganizer.Core.Settings;

public sealed class ProviderCredentialResolver : IProviderCredentialResolver
{
    private readonly IAppSettingsStore _settingsStore;

    public ProviderCredentialResolver(IAppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public ResolvedProviderCredential Resolve(string providerId)
    {
        var settings = _settingsStore.Load();
        var provider = settings.Providers.FirstOrDefault(p =>
            string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
            p.Enabled);

        if (provider is not null && !string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            return new ResolvedProviderCredential
            {
                ProviderId = provider.ProviderId,
                ApiKey = provider.ApiKey,
                BaseUrl = provider.BaseUrl,
                ModelName = provider.ModelName,
                Source = "LocalSettings"
            };
        }

        var envPrefix = providerId.Trim().ToUpperInvariant().Replace("-", "_");
        var envKey = Environment.GetEnvironmentVariable($"{envPrefix}_API_KEY");
        var envBaseUrl = Environment.GetEnvironmentVariable($"{envPrefix}_BASE_URL");
        var envModel = Environment.GetEnvironmentVariable($"{envPrefix}_MODEL");

        return new ResolvedProviderCredential
        {
            ProviderId = providerId,
            ApiKey = envKey,
            BaseUrl = envBaseUrl,
            ModelName = envModel,
            Source = string.IsNullOrWhiteSpace(envKey) ? "None" : "UserEnvironment"
        };
    }
}
