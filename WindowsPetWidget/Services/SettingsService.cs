using System.IO;
using System.Text.Json;
using WindowsPetWidget.Models;

namespace WindowsPetWidget.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsPetWidget");
        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public PetSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new PetSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<PetSettings>(json, JsonOptions) ?? new PetSettings();
        }
        catch (JsonException)
        {
            return new PetSettings();
        }
        catch (IOException)
        {
            return new PetSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new PetSettings();
        }
    }

    public void Save(PetSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (IOException)
        {
            // Settings persistence is best-effort; the pet should keep running.
        }
        catch (UnauthorizedAccessException)
        {
            // Settings persistence is best-effort; the pet should keep running.
        }
    }
}
