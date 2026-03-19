
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace BaboPlugin;

public partial class BaboPlugin
{
    private static readonly Dictionary<string, string> PracticeWeaponCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        [".glock"] = "weapon_glock",
        [".ak"] = "weapon_ak47",
        [".m4a1s"] = "weapon_m4a1_silencer",
        [".m4a4"] = "weapon_m4a1",
        [".mp9"] = "weapon_mp9",
        [".ak47"] = "weapon_ak47",
        [".hkp2000"] = "weapon_hkp2000",
        [".mac10"] = "weapon_mac10",
        [".m4a1_silencer"] = "weapon_m4a1_silencer",
        [".usp_silencer"] = "weapon_usp_silencer",
        [".mp7"] = "weapon_mp7",
        [".m4a1"] = "weapon_m4a1",
        [".p250"] = "weapon_p250",
        [".ump45"] = "weapon_ump45",
        [".awp"] = "weapon_awp",
        [".deagle"] = "weapon_deagle",
        [".p90"] = "weapon_p90",
        [".galilar"] = "weapon_galilar",
        [".fiveseven"] = "weapon_fiveseven",
        [".nova"] = "weapon_nova",
        [".famas"] = "weapon_famas",
        [".elite"] = "weapon_elite",
        [".xm1014"] = "weapon_xm1014",
        [".ssg08"] = "weapon_ssg08",
        [".cz75"] = "weapon_cz75a",
        [".negev"] = "weapon_negev",
        [".sg556"] = "weapon_sg556",
        [".scout"] = "weapon_ssg08",
        [".57"] = "weapon_fiveseven",
        [".dual_elite"] = "weapon_elite",
        [".tec9"] = "weapon_tec9",
        [".m249"] = "weapon_m249",
        [".revolver"] = "weapon_revolver",

    };

    private bool TryHandlePracticeWeaponCommand(CCSPlayerController player, string text)
    {
        if (!isPractice || !IsPlayerValid(player))
        {
            return false;
        }

        if (!PracticeWeaponCommands.TryGetValue(text, out var weaponName))
        {
            return false;
        }

        GivePlayerWeapon(player, weaponName);
        return true;
    }

    private static void GivePlayerWeapon(CCSPlayerController player, string weaponName)
    {
        try
        {
            player.GiveNamedItem(weaponName);
            player.PrintToChat($" \x04[BaboPlugin]\x01 Gave you {weaponName}.");
        }
        catch
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 Failed to give weapon.");
        }
    }

    private bool TryHandlePracticeBotCommand(CCSPlayerController player, string text)
    {
        if (!isPractice)
        {
            return false;
        }

        if (text.StartsWith(".bot_place", StringComparison.Ordinal))
        {
            if (!player.PawnIsAlive)
            {
                player.PrintToChat(" \x04[BaboPlugin]\x01 You must be alive to use .bot_place.");
                return true;
            }

            var botIndex = 1;
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 2)
            {
                player.PrintToChat(" \x04[BaboPlugin]\x01 Usage: .bot_place <index>");
                return true;
            }

            if (parts.Length == 2 && (!int.TryParse(parts[1], out botIndex) || botIndex < 1))
            {
                player.PrintToChat(" \x04[BaboPlugin]\x01 Invalid bot index. Use 1, 2, 3...");
                return true;
            }

            PlaceBotAtPlayerPosition(player, botIndex);
            return true;
        }

        switch (text)
        {
            case ".bot_add_ct":
                Server.ExecuteCommand("bot_add_ct");
                player.PrintToChat(" \x04[BaboPlugin]\x01 Added CT bot.");
                return true;
            case ".bot_add_t":
                Server.ExecuteCommand("bot_add_t");
                player.PrintToChat(" \x04[BaboPlugin]\x01 Added T bot.");
                return true;
            case ".bot_crouch":
                Server.ExecuteCommand("bot_crouch 1");
                player.PrintToChat(" \x04[BaboPlugin]\x01 All bots set to crouch.");
                return true;
            case ".bot_stand":
                Server.ExecuteCommand("bot_crouch 0");
                player.PrintToChat(" \x04[BaboPlugin]\x01 All bots set to stand.");
                return true;
            case ".rethrow":
                if (!player.PawnIsAlive)
                {
                    player.PrintToChat(" \x04[BaboPlugin]\x01 You must be alive to use .rethrow.");
                    return true;
                }

                try
                {
                    player.ExecuteClientCommandFromServer("sv_rethrow_last_grenade");
                }
                catch
                {
                    Server.ExecuteCommand("sv_rethrow_last_grenade");
                }

                player.PrintToChat(" \x04[BaboPlugin]\x01 Rethrew the last grenade.");
                return true;
            default:
                return false;
        }
    }

    private static void EnsureBotAvailableForPlacement(CCSPlayerController player, int minimumBotCount = 1)
    {
        var botCount = Utilities.GetPlayers().Count(p => p.IsValid && p.IsBot);
        if (botCount >= minimumBotCount)
        {
            return;
        }

        // With bot_quota 0, practice can have no bots available for placement.
        Server.ExecuteCommand("bot_quota_mode normal");
        Server.ExecuteCommand($"bot_quota {minimumBotCount}");

        var addCommand = player.TeamNum == 3 ? "bot_add_t" : "bot_add_ct";
        for (var i = botCount; i < minimumBotCount; i++)
        {
            Server.ExecuteCommand(addCommand);
        }
    }

    private static void PlaceBotAtPlayerPosition(CCSPlayerController player, int botIndex)
    {
        EnsureBotAvailableForPlacement(player, botIndex);

        var bots = Utilities.GetPlayers().Where(p =>
            p.IsValid &&
            p.IsBot &&
            p.PlayerPawn.IsValid &&
            p.PlayerPawn.Value != null)
            .ToList();

        if (bots.Count < botIndex)
        {
            player.PrintToChat($" \x04[BaboPlugin]\x01 Could not find bot index {botIndex}. Current bot count: {bots.Count}.");
            return;
        }

        var bot = bots[botIndex - 1];
        var playerPawn = player.PlayerPawn.Value;
        var botPawn = bot.PlayerPawn.Value;
        if (playerPawn == null || botPawn == null)
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 Failed to place bot.");
            return;
        }

        var destination = playerPawn.AbsOrigin;
        var angle = playerPawn.AbsRotation;
        botPawn.Teleport(destination, angle, new Vector(0, 0, 0));
        player.PrintToChat($" \x04[BaboPlugin]\x01 Teleported bot #{botIndex} ({bot.PlayerName}) to your position.");
    }
}

