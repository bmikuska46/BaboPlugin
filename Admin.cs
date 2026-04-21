using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using System.Text.Json;

namespace BaboPlugin;

public partial class BaboPlugin
{
    private Dictionary<string, string> loadedAdmins = new();
    private readonly HashSet<string> normalizedAdminSteamIds = new(StringComparer.Ordinal);

    private void LoadAdmins()
    {
        string fileName = "BaboPlugin/admins.json";
        string filePath = Path.Join(Server.GameDirectory + "/csgo/cfg", fileName);

        if (File.Exists(filePath))
        {
            try
            {
                using (StreamReader fileReader = File.OpenText(filePath))
                {
                    string jsonContent = fileReader.ReadToEnd();
                    if (!string.IsNullOrEmpty(jsonContent))
                    {
                        JsonSerializerOptions options = new()
                        {
                            AllowTrailingCommas = true,
                        };
                        loadedAdmins = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent, options) ?? new Dictionary<string, string>();
                    }
                    else
                    {
                        loadedAdmins = new Dictionary<string, string>();
                    }
                }

                RebuildAdminSteamIdIndex();

                foreach (var kvp in loadedAdmins)
                {
                    Console.WriteLine($"[ADMIN] Username: {kvp.Key}, Role: {kvp.Value}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[LoadAdmins FATAL] An error occurred: {e.Message}");
            }
        }
        else
        {
            Console.WriteLine("[LoadAdmins] The JSON file does not exist. Creating one with default content");
            Dictionary<string, string> defaultAdmins = new()
            {
                { "steamid", "" }
            };

            try
            {
                JsonSerializerOptions options = new()
                {
                    WriteIndented = true,
                };
                string defaultJson = JsonSerializer.Serialize(defaultAdmins, options);
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (directoryPath != null)
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                }

                File.WriteAllText(filePath, defaultJson);

                Console.WriteLine("[LoadAdmins] Created a new JSON file with default content.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[LoadAdmins FATAL] Error creating the JSON file: {e.Message}");
            }
        }
    }

   
    private bool IsAdmin(CCSPlayerController? player)
    {
        // Sent via server, hence should be treated as an admin.
        if (player == null)
        {
            return true;
        }

        var steamId = NormalizeSteamId(player.SteamID.ToString());
        if (!string.IsNullOrEmpty(steamId) && normalizedAdminSteamIds.Contains(steamId))
        {
            return true;
        }

        var authorizedSteamId = NormalizeSteamId(player.AuthorizedSteamID?.ToString());
        if (!string.IsNullOrEmpty(authorizedSteamId) && normalizedAdminSteamIds.Contains(authorizedSteamId))
        {
            return true;
        }

        return false;
    }

    private void RebuildAdminSteamIdIndex()
    {
        normalizedAdminSteamIds.Clear();
        foreach (var (key, value) in loadedAdmins)
        {
            AddAdminSteamIdCandidate(key);
            AddAdminSteamIdCandidate(value);
        }
    }

    private void AddAdminSteamIdCandidate(string? candidate)
    {
        var normalized = NormalizeSteamId(candidate);
        // SteamID64 is 17 digits. Keep this strict to avoid false positives from role values.
        if (normalized.Length == 17)
        {
            normalizedAdminSteamIds.Add(normalized);
        }
    }

    private static string NormalizeSteamId(string? steamId)
    {
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return string.Empty;
        }
        return new string(steamId.Where(char.IsDigit).ToArray());
    }
}
