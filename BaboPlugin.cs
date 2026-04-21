using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;

namespace BaboPlugin;

public partial class BaboPlugin : BasePlugin
{
    /// <summary>When true, .ready/.unready are ignored and .live does not require all players ready.</summary>
    private static readonly bool ReadyCommandTemporarilyDisabled = true;

    private bool isPractice = false;
    private bool isLive = false;
    private bool isWarmup = false;


    public record Position(Vector PlayerPosition, QAngle PlayerAngle);
     public Dictionary<byte, List<Position>> spawnsData = GetEmptySpawnsData();

   public static Dictionary<byte, List<Position>> GetEmptySpawnsData()
        {
            return new Dictionary<byte, List<Position>>
            {
                { (byte)CsTeam.CounterTerrorist, new List<Position>() },
                { (byte)CsTeam.Terrorist, new List<Position>() }
            };
        }

    public override string ModuleName => "BaboPlugin";
    public override string ModuleVersion => "1.0.28";
    public override string ModuleAuthor => "Babo";
    public override string ModuleDescription => "BaboPlugin";

    public override void Load(bool hotReload)
    {
        Console.WriteLine("BaboPlugin loaded, executing warmup config.");
        LoadAdmins();
        LoadSmokeLineupConfig();
        RegisterChatCommandListeners();
        ExecuteConfig("warmup.cfg");
    }

    public void GetSpawns()
        {
            // Resetting spawn data to avoid any glitches
            spawnsData = GetEmptySpawnsData();

            int minPriority = 1;

            var spawnsct = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_counterterrorist");
            foreach (var spawn in spawnsct)
            {
                if (spawn.IsValid && spawn.Enabled && spawn.Priority < minPriority)
                {
                    minPriority = spawn.Priority;
                }
            }

            foreach (var spawn in spawnsct)
            {
                if (spawn.IsValid && spawn.Enabled && spawn.Priority == minPriority)
                {
                    spawnsData[(byte)CsTeam.CounterTerrorist].Add(new Position(spawn.CBodyComponent?.SceneNode?.AbsOrigin!, spawn.CBodyComponent?.SceneNode?.AbsRotation!));
                }
            }

            var spawnst = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>("info_player_terrorist");
            foreach (var spawn in spawnst)
            {
                if (spawn.IsValid && spawn.Enabled && spawn.Priority == minPriority)
                {
                    spawnsData[(byte)CsTeam.Terrorist].Add(new Position(spawn.CBodyComponent?.SceneNode?.AbsOrigin!, spawn.CBodyComponent?.SceneNode?.AbsRotation!));
                }
            }

        }
    private void HandleSpawnCommand(CCSPlayerController? player, string commandArg, byte teamNum, string command)
    {
        if (!isPractice || !IsPlayerValid(player)) return;
        if (teamNum != 2 && teamNum != 3) return;
        if (!string.IsNullOrWhiteSpace(commandArg))
        {
            if (int.TryParse(commandArg, out int spawnNumber) && spawnNumber >= 1)
            {
                spawnNumber -= 1;
                if (!spawnsData.ContainsKey(teamNum) || spawnsData[teamNum].Count <= spawnNumber)
                {
                    ReplyToUserCommand(player, "Invalid spawn number.");
                    return;
                }
                var pos = spawnsData[teamNum][spawnNumber];
                player!.PlayerPawn.Value!.Teleport(pos.PlayerPosition, pos.PlayerAngle, new Vector(0, 0, 0));
                ReplyToUserCommand(player, $"Moved to spawn: {spawnNumber + 1}/{spawnsData[teamNum].Count}");
                AnnouncePracticeCommandUsage(player, $".{command} {spawnNumber + 1}");
            }
            else
            {
                ReplyToUserCommand(player, "Invalid value. Usage: .spawn <number>");
                return;
            }
        }
        else
        {
            ReplyToUserCommand(player, $"Usage: .{command} <number>");
        }
    }


    private static void ExecuteConfig(string configFile)
    {
        Server.ExecuteCommand($"exec BaboPlugin/{configFile}");
    }

    private void HandlePauseCommand(CCSPlayerController player, bool pause)
    {
        if (!isLive)
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 Pause is only available during live mode.");
            return;
        }

        if (pause)
        {
            Server.ExecuteCommand("mp_pause_match 1");
            Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {player.PlayerName} paused the match.");
        }
        else
        {
            Server.ExecuteCommand("mp_pause_match 0");
            Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {player.PlayerName} unpaused the match.");
        }
    }

    private static void HandleMoveCommand(CCSPlayerController caller, string rawText)
    {
        var parts = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            caller.PrintToChat(" \x04[BaboPlugin]\x01 Usage: .move STEAM_64_ID [t/ct]");
            return;
        }

        var steamIdInput = parts[1];
        var teamInput = parts[2].ToLowerInvariant();

        if (teamInput != "t" && teamInput != "ct")
        {
            caller.PrintToChat(" \x04[BaboPlugin]\x01 Invalid team. Use t or ct.");
            return;
        }

        var target = Utilities.GetPlayers().FirstOrDefault(p =>
            p.IsValid && p.AuthorizedSteamID != null && p.AuthorizedSteamID.ToString() == steamIdInput);

        if (target == null)
        {
            caller.PrintToChat($" \x04[BaboPlugin]\x01 Player with SteamID {steamIdInput} not found.");
            return;
        }

        var targetTeam = teamInput == "ct" ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
        target.SwitchTeam(targetTeam);

        Server.PrintToChatAll(
            $" \x04[BaboPlugin]\x01 {caller.PlayerName} moved {target.PlayerName} to {(teamInput == "ct" ? "CT" : "T")}.");
    }

    private static void HandleMapCommand(CCSPlayerController caller, string rawText)
    {
        var parts = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            caller.PrintToChat(" \x04[BaboPlugin]\x01 Usage: .map MAP_NAME");
            return;
        }

        var mapName = parts[1];
        if (mapName.Contains(';') || mapName.Contains('\"'))
        {
            caller.PrintToChat(" \x04[BaboPlugin]\x01 Invalid map name.");
            return;
        }
        var isValidMap = mapName.StartsWith("de_") || mapName.StartsWith("cs_");


        Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {caller.PlayerName} changed map to {mapName}.");
        Server.ExecuteCommand($"map {(isValidMap ? mapName : "de_" + mapName)}");
    }

    private void ReplyToUserCommand(CCSPlayerController? player, string message, bool console = false)
        {
            if (player == null)
            {
                Server.PrintToConsole($" \x04[BaboPlugin]\x01 {message}");
            }
            else
            {
                if (console)
                {
                    player.PrintToConsole($" \x04[BaboPlugin]\x01 {message}");
                }
                else
                {
                    player.PrintToChat($" \x04[BaboPlugin]\x01 {message}");
                }
            }
        }

          public bool IsPlayerValid(CCSPlayerController? player)
        {
            return (
                player != null &&
                player.IsValid &&
                player.PlayerPawn.IsValid &&
                player.PlayerPawn.Value != null
            );
        }

    private void HandleGodCommand(CCSPlayerController? player)
        {
            if (!isPractice || player == null || !IsPlayerValid(player)) return;
	    
			int currentHP = player!.PlayerPawn!.Value!.Health;
			
			if(currentHP > 100)
			{
				player.PlayerPawn.Value.Health = 100;
				// ReplyToUserCommand(player, $"God mode disabled!");
                		ReplyToUserCommand(player, "God mode disabled!");
                AnnouncePracticeCommandUsage(player, ".god (disabled)");
				return;
			}
			else
			{
				player.PlayerPawn.Value.Health = 2147483647; // max 32bit int
				// ReplyToUserCommand(player, $"God mode enabled!");
                		ReplyToUserCommand(player, "God mode enabled!");
                AnnouncePracticeCommandUsage(player, ".god (enabled)");
				return;
			}
        }

    private void HandlePracticeHelpCommand(CCSPlayerController player)
    {
        if (!isPractice)
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 .help is available in practice mode.");
            return;
        }

        var helpLines = new[]
        {
            "Practice commands:",
            ".help | .spawn <number> | .god | .rethrow",
            ".bot_add_ct | .bot_add_t | .bot_place <index> | .bot_crouch | .bot_stand | .bot_strafe <0|1>",
            ".map <name>",
            ".weapon <weapon>",
             };

        var smokeHelpLines = new[]
        {
            "Smoke lineup commands:",
            ".record_smoke <name> | .snap | .throw normal|middle|jump|wjump",
            ".snap_start | .snap_end | .done | .cancel_smoke"
        };

        foreach (var line in helpLines)
        {
            Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {line}");
        }

        if (!isWarmup)
        {
            foreach (var line in smokeHelpLines)
            {
                Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {line}");
            }
        }

        AnnouncePracticeCommandUsage(player, ".help");
    }

    private void AnnouncePracticeCommandUsage(CCSPlayerController? player, string commandText)
    {
        if (!isPractice || player == null || !player.IsValid)
        {
            return;
        }

        Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {player.PlayerName} used {commandText}");
    }
}
