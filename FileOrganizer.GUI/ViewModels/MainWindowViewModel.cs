using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileOrganizer.Core.Settings;

namespace FileOrganizer.GUI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ProviderSettingsService _providerSettingsService;
    private readonly IProviderCredentialResolver _credentialResolver;

    public MainWindowViewModel()
        : this(
            new ProviderSettingsService(new JsonAppSettingsStore(), new UserEnvironmentWriter()),
            new ProviderCredentialResolver(new JsonAppSettingsStore()))
    {
    }

    public MainWindowViewModel(
        ProviderSettingsService providerSettingsService,
        IProviderCredentialResolver credentialResolver)
    {
        _providerSettingsService = providerSettingsService;
        _credentialResolver = credentialResolver;

        ProviderSettings = new ObservableCollection<ProviderSettingsItemViewModel>(CreateDefaultProviders());
        LoadSettingsCommand = new RelayCommand(LoadSettings);
        SaveSettingsCommand = new RelayCommand(SaveSettings);

        LoadSettings();
    }

    public ObservableCollection<ProviderSettingsItemViewModel> ProviderSettings { get; }

    public IRelayCommand LoadSettingsCommand { get; }

    public IRelayCommand SaveSettingsCommand { get; }

    [ObservableProperty]
    private string providerSettingsStatus = "Provider settings ready.";

    private void LoadSettings()
    {
        var settings = _providerSettingsService.Load();

        foreach (var item in ProviderSettings)
        {
            var stored = settings.Providers.FirstOrDefault(p => p.ProviderId == item.ProviderId);
            if (stored is not null)
            {
                item.DisplayName = string.IsNullOrWhiteSpace(stored.DisplayName) ? item.DisplayName : stored.DisplayName;
                item.ApiKey = stored.ApiKey ?? string.Empty;
                item.BaseUrl = stored.BaseUrl ?? string.Empty;
                item.ModelName = stored.ModelName ?? string.Empty;
                item.PersistLocally = stored.PersistLocally;
                item.ExportToUserEnvironment = stored.ExportToUserEnvironment;
                item.Enabled = stored.Enabled;
            }

            UpdateResolvedState(item);
        }

        ProviderSettingsStatus = $"Provider settings loaded from {_providerSettingsService.GetSettingsPath()}.";
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            Providers = ProviderSettings.Select(item => new LlmProviderSettings
            {
                ProviderId = item.ProviderId,
                DisplayName = item.DisplayName,
                ApiKey = item.PersistLocally ? NullIfWhiteSpace(item.ApiKey) : null,
                BaseUrl = NullIfWhiteSpace(item.BaseUrl),
                ModelName = NullIfWhiteSpace(item.ModelName),
                PersistLocally = item.PersistLocally,
                ExportToUserEnvironment = item.ExportToUserEnvironment,
                Enabled = item.Enabled
            }).ToList()
        };

        _providerSettingsService.Save(settings);

        foreach (var item in ProviderSettings)
        {
            UpdateResolvedState(item);
        }

        ProviderSettingsStatus = $"Provider settings saved to {_providerSettingsService.GetSettingsPath()}.";
    }

    private void UpdateResolvedState(ProviderSettingsItemViewModel item)
    {
        var resolved = _credentialResolver.Resolve(item.ProviderId);
        item.ResolvedSource = resolved.Source;
        item.CredentialAvailable = resolved.IsAvailable;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProviderSettingsItemViewModel[] CreateDefaultProviders()
        =>
        [
            new ProviderSettingsItemViewModel
            {
                ProviderId = "openai",
                DisplayName = "OpenAI",
                PersistLocally = true,
                Enabled = true
            },
            new ProviderSettingsItemViewModel
            {
                ProviderId = "local",
                DisplayName = "Local",
                PersistLocally = true,
                Enabled = true
            }
        ];
}
