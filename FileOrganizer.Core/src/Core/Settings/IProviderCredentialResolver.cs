namespace FileOrganizer.Core.Settings;

public interface IProviderCredentialResolver
{
    ResolvedProviderCredential Resolve(string providerId);
}
