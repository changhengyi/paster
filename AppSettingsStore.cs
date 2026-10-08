using System.IO;
using System.Text.Json;

namespace Paster;

public sealed class AppSettingsData
{
    public string? ClipsJsonPath { get; set; }
}

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string PasterAppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Paster");

    public static string SettingsFilePath => Path.Combine(PasterAppDataRoot, "settings.json");

    public static string DefaultClipsPath => Path.Combine(PasterAppDataRoot, "clips.json");

    public static AppSettingsData Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new AppSettingsData();
        }

        var raw = File.ReadAllText(SettingsFilePath);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new AppSettingsData();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettingsData>(raw) ?? new AppSettingsData();
        }
        catch (JsonException)
        {
            return new AppSettingsData();
        }
    }

    public static void Save(AppSettingsData data)
    {
        Directory.CreateDirectory(PasterAppDataRoot);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    public static string ResolveClipsJsonPath(AppSettingsData? settings = null)
    {
        settings ??= Load();
        var path = settings.ClipsJsonPath?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            return DefaultClipsPath;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return DefaultClipsPath;
        }
    }
}
