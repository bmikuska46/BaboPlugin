using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

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
            case ".bot_place":
                Server.ExecuteCommand("bot_place");
                player.PrintToChat(" \x04[BaboPlugin]\x01 Placed a bot at your crosshair.");
                return true;
            default:
                return false;
        }
    }
}

