namespace FileOrganizer.Core.Settings;

public interface IAppSettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
    string GetSettingsPath();
}
