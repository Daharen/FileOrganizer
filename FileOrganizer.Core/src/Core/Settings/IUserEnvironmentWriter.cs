namespace FileOrganizer.Core.Settings;

public interface IUserEnvironmentWriter
{
    void SetUserVariable(string name, string? value);
    string? GetUserVariable(string name);
}
