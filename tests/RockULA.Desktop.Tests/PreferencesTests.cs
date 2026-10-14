using RockULA.Core.Input;
using RockULA.Desktop.Devices;
using RockULA.Desktop.Preferences;

namespace RockULA.Desktop.Tests;

public sealed class PreferencesTests
{
    [Fact]
    public async Task MissingFileReturnsDefaultsAndRoundTripPreservesExplicitUserChoices()
    {
        string directory = Temporary();
        try
        {
            var store = new PreferencesStore(Path.Combine(directory, "settings.json"));
            var missing = await store.LoadAsync();
            Assert.Equal(50, missing.Value.Volume); Assert.Null(missing.Warning); Assert.True(missing.CanSave);
            var expected = new PlayerPreferences
            {
                RomPath = "C:/ゲーム/48.rom",
                SnapshotDirectory = "C:/ゲーム",
                Volume = 73,
                Muted = true,
                Mode = SpectrumJoystickMode.Sinclair12345,
                Mapping = new(XAxis: 2, YAxis: -1, FireButton: 7, DeadZone: 4000, InvertX: true, LeftButton: 65),
                Keyboard = new(Fire: Avalonia.Input.Key.F),
                JoystickGuid = "00112233445566778899aabbccddeeff",
                JoystickName = "USB joystick"
            };
            Assert.Null(await store.SaveAsync(expected));
            Assert.Equal(expected, (await store.LoadAsync()).Value);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"Volume\":101}")]
    [InlineData("{\"Mapping\":null}")]
    [InlineData("{\"Mode\":255}")]
    [InlineData("{\"Mapping\":{\"DeadZone\":0}}")]
    [InlineData("{\"Mapping\":{\"XAxis\":999}}")]
    [InlineData("{\"Keyboard\":{\"Fire\":99999}}")]
    public async Task MalformedOrInvalidSettingsFallBackWithWarning(string json)
    {
        string directory = Temporary();
        try
        {
            string path = Path.Combine(directory, "settings.json"); await File.WriteAllTextAsync(path, json);
            var loaded = await new PreferencesStore(path).LoadAsync();
            Assert.Equal(new PlayerPreferences(), loaded.Value);
            Assert.NotNull(loaded.Warning);
            Assert.Equal(json, await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task FutureSchemaIsNotOverwrittenAndOversizeFilesAreBounded()
    {
        string directory = Temporary();
        try
        {
            string path = Path.Combine(directory, "settings.json");
            var store = new PreferencesStore(path);
            await File.WriteAllTextAsync(path, "{\"Version\":2,\"Volume\":9}");
            Assert.NotNull(await store.SaveAsync(new())); // Protected even before LoadAsync.
            var loaded = await store.LoadAsync();
            Assert.False(loaded.CanSave); Assert.NotNull(loaded.Warning);
            Assert.NotNull(await store.SaveAsync(new()));
            Assert.Contains("\"Version\":2", await File.ReadAllTextAsync(path));
            await File.WriteAllBytesAsync(path, new byte[PreferencesStore.MaximumBytes + 1]);
            loaded = await new PreferencesStore(path).LoadAsync();
            Assert.NotNull(loaded.Warning); Assert.Equal(new PlayerPreferences(), loaded.Value);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task InvalidSavePreservesExistingFileAndConcurrentSavesLeaveValidJson()
    {
        string directory = Temporary();
        try
        {
            var store = new PreferencesStore(Path.Combine(directory, "settings.json"));
            Assert.Null(await store.SaveAsync(new() { Volume = 20 }));
            string before = await File.ReadAllTextAsync(store.FilePath);
            Assert.NotNull(await store.SaveAsync(new() { Volume = -1 }));
            Assert.Equal(before, await File.ReadAllTextAsync(store.FilePath));
            await Task.WhenAll(Enumerable.Range(0, 12).Select(x => store.SaveAsync(new() { Volume = x })));
            var loaded = await store.LoadAsync();
            Assert.Null(loaded.Warning); Assert.InRange(loaded.Value.Volume, 0, 11);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task FailedReplacementKeepsPriorDataAndCleansTemporaryFile()
    {
        string directory = Temporary();
        try
        {
            string path = Path.Combine(directory, "settings.json");
            Directory.CreateDirectory(path); // Deliberate invalid destination, portable across Windows/Linux.
            var store = new PreferencesStore(path);
            Assert.NotNull(await store.SaveAsync(new()));
            Assert.True(Directory.Exists(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void StableGuidSurvivesInstanceChangesAndAmbiguousIdenticalDevicesAreNotGuessed()
    {
        const string guid = "00112233445566778899aabbccddeeff";
        var prefs = new PlayerPreferences { JoystickGuid = guid, JoystickName = "USB" };
        Assert.Equal(99, prefs.FindJoystick([new(99, "USB", guid)])!.InstanceId);
        Assert.Null(prefs.FindJoystick([new(1, "USB", guid), new(2, "USB", guid)]));
        Assert.Null(prefs.FindJoystick([new(1, "Different", guid)]));
        Assert.Null(new PlayerPreferences().FindJoystick([new(1, "USB", guid)]));
    }
    private static string Temporary()
    {
        string path = Path.Combine(Path.GetTempPath(), "rockula-preferences-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
}
