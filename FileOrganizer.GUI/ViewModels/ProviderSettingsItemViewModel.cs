using CommunityToolkit.Mvvm.ComponentModel;

namespace FileOrganizer.GUI.ViewModels;

public partial class ProviderSettingsItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string providerId = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private string modelName = string.Empty;

    [ObservableProperty]
    private bool persistLocally = true;

    [ObservableProperty]
    private bool exportToUserEnvironment;

    [ObservableProperty]
    private bool enabled = true;

    [ObservableProperty]
    private string resolvedSource = "None";

    [ObservableProperty]
    private bool credentialAvailable;
}
