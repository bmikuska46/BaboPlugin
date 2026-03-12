using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Text.Json;

namespace BaboPlugin;

public partial class BaboPlugin
{
    private Dictionary<string, string> loadedAdmins = new();

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

    public bool IsAdmin(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.AuthorizedSteamID == null)
        {
            return false;
        }

        string steamId = player.AuthorizedSteamID.ToString();
        return loadedAdmins.ContainsKey(steamId);
    }
}
