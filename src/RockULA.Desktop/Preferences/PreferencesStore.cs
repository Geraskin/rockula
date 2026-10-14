using System.Text.Json;
using Avalonia.Input;

namespace RockULA.Desktop.Preferences;

/// <summary>Bounded per-user host JSON; same-directory replacement, serialized writes.</summary>
public sealed class PreferencesStore
{
    public const int MaximumBytes = 32768;
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 8, WriteIndented = true };
    private readonly SemaphoreSlim _writes = new(1, 1);
    private bool _canSave = true;
    public PreferencesStore(string path) => FilePath = Path.GetFullPath(path);
    public string FilePath { get; }
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RockULA", "settings.json");
    public async Task<PreferencesLoad> LoadAsync()
    {
        try
        {
            _canSave = true;
            if (!File.Exists(FilePath)) return new(new(), null);
            byte[] bytes = await ReadBounded(FilePath).ConfigureAwait(false);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("Version", out var version)
                && version.TryGetInt32(out int number) && number > 1)
            {
                _canSave = false;
                return new(new(), "Settings use a newer version; defaults are active and the file will not be overwritten.", false);
            }
            var value = JsonSerializer.Deserialize<PlayerPreferences>(bytes, Json) ?? throw new ArgumentException("Settings are null.");
            Validate(value);
            _canSave = true;
            return new(value, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            return new(new(), "Settings ignored; using defaults: " + ex.Message);
        }
    }
    public async Task<string?> SaveAsync(PlayerPreferences value)
    {
        await _writes.WaitAsync().ConfigureAwait(false);
        string? temporary = null;
        try
        {
            if (!_canSave) return "Newer-version settings are read-only; current session changes were not saved.";
            Validate(value);
            if (File.Exists(FilePath))
            {
                try
                {
                    using var current = JsonDocument.Parse(await ReadBounded(FilePath).ConfigureAwait(false), new JsonDocumentOptions { MaxDepth = 8 });
                    if (current.RootElement.ValueKind == JsonValueKind.Object && current.RootElement.TryGetProperty("Version", out var schema)
                        && schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out int version) && version > 1)
                    {
                        _canSave = false;
                        return "Newer-version settings will not be overwritten.";
                    }
                }
                catch (JsonException) { } // Explicit user changes can replace corrupt preferences.
                catch (ArgumentException) { } // Oversized corrupt settings may likewise be repaired.
            }
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
            if (bytes.Length > MaximumBytes) throw new ArgumentException("Settings exceed the encoded size limit.");
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            temporary = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await output.WriteAsync(bytes).ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);
            }
            File.Move(temporary, FilePath, true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return "Could not save settings: " + ex.Message;
        }
        finally
        {
            if (temporary != null)
                try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            _writes.Release();
        }
    }
    private static async Task<byte[]> ReadBounded(string path)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        byte[] bytes = new byte[MaximumBytes + 1];
        int count = 0;
        while (count < bytes.Length)
        {
            int read = await input.ReadAsync(bytes.AsMemory(count)).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
        }
        if (count > MaximumBytes) throw new ArgumentException("Settings input exceeds 32768 bytes.");
        return bytes[..count];
    }
    private static void Validate(PlayerPreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Version != 1 || value.Volume is < 0 or > 100 || !Enum.IsDefined(value.Mode)
            || value.Mapping == null || value.Keyboard == null) throw new ArgumentException("Invalid settings schema, volume or controls.");
        if (value.RomPath?.Length > 4096 || value.SnapshotDirectory?.Length > 4096
            || value.RomPath?.Contains('\0') == true || value.SnapshotDirectory?.Contains('\0') == true
            || value.JoystickName == null || value.JoystickName.Length > 256 || value.JoystickGuid == null
            || (value.JoystickGuid.Length != 0 && (value.JoystickGuid.Length != 32 || !value.JoystickGuid.All(Uri.IsHexDigit))))
            throw new ArgumentException("Invalid saved path or joystick identity.");
        var m = value.Mapping;
        if (m.XAxis is < -1 or > 63 || m.YAxis is < -1 or > 63 || m.Hat is < -1 or > 63 || m.DeadZone is < 1 or > 32767
            || m.FireButton is < -1 or > 127 || m.LeftButton is < -1 or > 127 || m.RightButton is < -1 or > 127
            || m.UpButton is < -1 or > 127 || m.DownButton is < -1 or > 127) throw new ArgumentException("Invalid joystick mapping.");
        Key[] keys = [value.Keyboard.Up, value.Keyboard.Down, value.Keyboard.Left, value.Keyboard.Right, value.Keyboard.Fire];
        if (keys.Any(key => !Enum.IsDefined(key))) throw new ArgumentException("Invalid keyboard alias.");
    }
}
