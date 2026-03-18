namespace FileOrganizer.Core.Settings;

public sealed class UserEnvironmentWriter : IUserEnvironmentWriter
{
    public void SetUserVariable(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
    }

    public string? GetUserVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    }
}
