namespace RockULA.Desktop.Preferences;

public sealed class PreferencesStore(string path)
{
    public const int MaximumBytes = 32768;
    public string FilePath { get; } = path;
    public Task<PreferencesLoad> LoadAsync() => throw new NotImplementedException();
    public Task<string?> SaveAsync(PlayerPreferences value) => throw new NotImplementedException();
}
