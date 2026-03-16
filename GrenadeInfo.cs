using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Events;

namespace BaboPlugin;

public partial class BaboPlugin
{
    private readonly Dictionary<int, CCSPlayerController> playerData = new();
    private bool enableDamageReport = true;

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!isPractice) return HookResult.Continue;
        RefreshPlayerData();
        InitPlayerDamageInfo();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!isPractice) return HookResult.Continue;
        RefreshPlayerData();
        ShowDamageInfo();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (!isPractice) return HookResult.Continue;
        RefreshPlayerData();

        var target = @event.Userid;
        if (!IsPlayerValid(target) || target!.UserId == null)
        {
            return HookResult.Continue;
        }

        UpdatePlayerDamageInfo(@event, (int)target.UserId);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerBlind(EventPlayerBlind @event, GameEventInfo info)
    {
        if (!isPractice) return HookResult.Continue;
        RefreshPlayerData();

        var target = @event.Userid;
        if (!IsPlayerValid(target) || target!.UserId == null)
        {
            return HookResult.Continue;
        }

        UpdatePlayerFlashInfo(@event, (int)target.UserId);
        return HookResult.Continue;
    }

    private void RefreshPlayerData()
    {
        playerData.Clear();
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.UserId == null)
            {
                continue;
            }

            if (player.Connected != PlayerConnectedState.PlayerConnected)
            {
                continue;
            }

            playerData[(int)player.UserId] = player;
        }
    }
}

public class GrenadeInfo
{
    public int HeDamage { get; set; }
    public int MolotovDamage { get; set; }
    public int Flashes { get; set; }
    public float FlashDurationSeconds { get; set; }
}
