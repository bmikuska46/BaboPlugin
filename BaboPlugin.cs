using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;

namespace BaboPlugin;

public partial class BaboPlugin : BasePlugin
{
    private bool isPractice = false;
    public override string ModuleName => "BaboPlugin";
    public override string ModuleVersion => "1.0.8";
    public override string ModuleAuthor => "Babo";
    public override string ModuleDescription => "BaboPlugin";

    public override void Load(bool hotReload)
    {
        Console.WriteLine("BaboPlugin loaded, executing warmup config.");
        LoadAdmins();
        ExecuteConfig("warmup.cfg");
    }

    [GameEventHandler]
    public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
    {
        var player = Utilities.GetPlayerFromSlot(@event.Userid);
        if (player == null || !player.IsValid)
        {
            return HookResult.Continue;
        }

        var rawText = (@event.Text ?? string.Empty).Trim();
        var text = rawText.ToLowerInvariant();
        var isRestrictedCommand =
            text == ".prac" ||
            text == ".warmup" ||
            text == ".live" ||
            text.StartsWith(".move ") ||
            text.StartsWith(".map") ||
            text == ".god";

        if (text == ".ready")
        {
            HandleReadyCommand(player);
            return HookResult.Continue;
        }

        if (text == ".unready")
        {
            HandleUnreadyCommand(player);
            return HookResult.Continue;
        }

        if (isRestrictedCommand && !IsAdmin(player))
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 You are not allowed to use this command.");
            return HookResult.Continue;
        }

        if (text == ".prac")
        {
            isPractice = true;
            ResetReadyPlayers();
            ExecuteConfig("prac.cfg");
            Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {player.PlayerName} loaded practice config.");
            return HookResult.Continue;
        }

        if (text == ".warmup")
        {
            isPractice = true;
            ResetReadyPlayers();
            ExecuteConfig("warmup.cfg");
            Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {player.PlayerName} loaded warmup config.");
            return HookResult.Continue;
        }

        if (text == ".live")
        {   
            if (!AreAllConnectedPlayersReady(out var readyCount, out var totalCount))
            {
                player.PrintToChat($" \x04[BaboPlugin]\x01 Cannot start live. Ready: {readyCount}/{totalCount}. Players must type .ready");
                return HookResult.Continue;
            }
            isPractice = false;

            ResetReadyPlayers();
            ExecuteConfig("live.cfg");
            Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {player.PlayerName} loaded live config.");
            return HookResult.Continue;
        }

        if (text.StartsWith(".move "))
        {
            HandleMoveCommand(player, rawText);
            return HookResult.Continue;
        }

        if (text.StartsWith(".map"))
        {
            isPractice = false;
            HandleMapCommand(player, rawText);
            return HookResult.Continue;
        }

        if (text == ".god")
        {
            HandleGodCommand(player);
            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    // No css_* console commands registered - only .prac / .warmup / .live in chat (no !)

    private static void ExecuteConfig(string configFile)
    {
        Server.ExecuteCommand($"exec BaboPlugin/{configFile}");
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

        Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {caller.PlayerName} changed map to {mapName}.");
        Server.ExecuteCommand($"map {mapName}");
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
				return;
			}
			else
			{
				player.PlayerPawn.Value.Health = 2147483647; // max 32bit int
				// ReplyToUserCommand(player, $"God mode enabled!");
                		ReplyToUserCommand(player, "God mode enabled!");
				return;
			}
        }
}