using System.IO;
using System.Text.Json;
using Holo.Models;

namespace Holo.Services;

/// <summary>
/// Windows-native profile persistence.
/// Maps to macOS HoloCore.ProfileStore (file-based JSON persistence).
/// </summary>
public sealed class ProfileService
{
    private readonly string _profilesDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProfileService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _profilesDirectory = Path.Combine(appData, "Holo", "Profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public ProfileService(string customDirectory)
    {
        _profilesDirectory = customDirectory;
        Directory.CreateDirectory(_profilesDirectory);
    }

    public List<HoloProfile> LoadAll()
    {
        if (!Directory.Exists(_profilesDirectory)) return [];

        var profiles = new List<HoloProfile>();
        foreach (var file in Directory.GetFiles(_profilesDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<HoloProfile>(json, JsonOptions);
                if (profile != null && profile.Version == HoloProfile.CurrentVersion)
                    profiles.Add(profile);
            }
            catch
            {
                // Skip malformed profiles (matches macOS behavior)
            }
        }
        return profiles.OrderByDescending(p => p.UpdatedAt).ToList();
    }

    public void Save(HoloProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        var path = Path.Combine(_profilesDirectory, $"{profile.Id}.json");
        File.WriteAllText(path, json);
    }

    public void Delete(HoloProfile profile)
    {
        var path = Path.Combine(_profilesDirectory, $"{profile.Id}.json");
        if (File.Exists(path))
            File.Delete(path);
    }
}
