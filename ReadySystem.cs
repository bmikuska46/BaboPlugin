using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace BaboPlugin;

public partial class BaboPlugin
{
    private readonly HashSet<string> readyPlayers = new();

    private void HandleReadyCommand(CCSPlayerController player)
    {
        var steamId = GetSteamId(player);
        if (steamId == null)
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 Could not resolve your SteamID.");
            return;
        }

        if (!readyPlayers.Add(steamId))
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 You are already marked as ready.");
            return;
        }

        AreAllConnectedPlayersReady(out var readyCount, out var totalCount);
        Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {player.PlayerName} is ready ({readyCount}/{totalCount}).");
    }

    private void HandleUnreadyCommand(CCSPlayerController player)
    {
        var steamId = GetSteamId(player);
        if (steamId == null)
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 Could not resolve your SteamID.");
            return;
        }

        if (!readyPlayers.Remove(steamId))
        {
            player.PrintToChat(" \x04[BaboPlugin]\x01 You are already not ready.");
            return;
        }

        AreAllConnectedPlayersReady(out var readyCount, out var totalCount);
        Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {player.PlayerName} is no longer ready ({readyCount}/{totalCount}).");
    }

    private void ResetReadyPlayers()
    {
        readyPlayers.Clear();
    }

    private bool AreAllConnectedPlayersReady(out int readyCount, out int totalCount)
    {
        var connectedPlayers = GetConnectedPlayersForReady();
        totalCount = connectedPlayers.Count;
        readyCount = connectedPlayers.Count(player =>
        {
            var steamId = GetSteamId(player);
            return steamId != null && readyPlayers.Contains(steamId);
        });

        return totalCount > 0 && readyCount == totalCount;
    }

    private static List<CCSPlayerController> GetConnectedPlayersForReady()
    {
        return Utilities.GetPlayers()
            .Where(player => player.IsValid && player.AuthorizedSteamID != null)
            .ToList();
    }

    private static string? GetSteamId(CCSPlayerController player)
    {
        return player.AuthorizedSteamID?.ToString();
    }
}