using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace BaboPlugin;

public partial class BaboPlugin
{
    private readonly Dictionary<string, Action<CCSPlayerController?, CommandInfo?>> chatCommandHandlers = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AdminOnlyChatCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ".prac",
        ".warmup",
        ".live",
        ".move",
        ".map",
        ".god"
    };

    private void RegisterChatCommandListeners()
    {
        chatCommandHandlers.Clear();

        RegisterChatCommand(".ready", OnReadyCommand);
        RegisterChatCommand(".unready", OnUnreadyCommand);
        RegisterChatCommand(".pause", OnPauseCommand);
        RegisterChatCommand(".unpause", OnUnpauseCommand);
        RegisterChatCommand(".help", OnHelpCommand);
        RegisterChatCommand(".prac", OnPracCommand);
        RegisterChatCommand(".warmup", OnWarmupCommand);
        RegisterChatCommand(".live", OnLiveCommand);
        RegisterChatCommand(".move", OnMoveCommand);
        RegisterChatCommand(".map", OnMapCommand);
        RegisterChatCommand(".god", OnGodCommand);
        RegisterChatCommand(".spawn", OnSpawnCommand);

        RegisterChatCommand(".glock", OnGlockCommand);
        RegisterChatCommand(".ak", OnAkCommand);
        RegisterChatCommand(".m4a1s", OnM4a1sCommand);
        RegisterChatCommand(".m4a4", OnM4a4Command);
        RegisterChatCommand(".mp9", OnMp9Command);
        RegisterChatCommand(".ak47", OnAk47Command);
        RegisterChatCommand(".hkp2000", OnHkp2000Command);
        RegisterChatCommand(".mac10", OnMac10Command);
        RegisterChatCommand(".m4a1_silencer", OnM4a1SilencerCommand);
        RegisterChatCommand(".usp_silencer", OnUspSilencerCommand);
        RegisterChatCommand(".mp7", OnMp7Command);
        RegisterChatCommand(".m4a1", OnM4a1Command);
        RegisterChatCommand(".p250", OnP250Command);
        RegisterChatCommand(".ump45", OnUmp45Command);
        RegisterChatCommand(".awp", OnAwpCommand);
        RegisterChatCommand(".deagle", OnDeagleCommand);
        RegisterChatCommand(".p90", OnP90Command);
        RegisterChatCommand(".galilar", OnGalilarCommand);
        RegisterChatCommand(".fiveseven", OnFivesevenCommand);
        RegisterChatCommand(".nova", OnNovaCommand);
        RegisterChatCommand(".famas", OnFamasCommand);
        RegisterChatCommand(".elite", OnEliteCommand);
        RegisterChatCommand(".xm1014", OnXm1014Command);
        RegisterChatCommand(".ssg08", OnSsg08Command);
        RegisterChatCommand(".cz75", OnCz75Command);
        RegisterChatCommand(".negev", OnNegevCommand);
        RegisterChatCommand(".sg556", OnSg556Command);
        RegisterChatCommand(".scout", OnScoutCommand);
        RegisterChatCommand(".57", OnFiveSevenAliasCommand);
        RegisterChatCommand(".dual_elite", OnDualEliteCommand);
        RegisterChatCommand(".tec9", OnTec9Command);
        RegisterChatCommand(".m249", OnM249Command);
        RegisterChatCommand(".revolver", OnRevolverCommand);

        RegisterChatCommand(".bot_place", OnBotPlaceCommand);
        RegisterChatCommand(".bot_add_ct", OnBotAddCtCommand);
        RegisterChatCommand(".bot_add_t", OnBotAddTCommand);
        RegisterChatCommand(".bot_crouch", OnBotCrouchCommand);
        RegisterChatCommand(".bot_stand", OnBotStandCommand);
        RegisterChatCommand(".rethrow", OnRethrowCommand);

        RegisterChatCommand(".record_smoke", OnRecordSmokeCommand);
        RegisterChatCommand(".cancel_smoke", OnCancelSmokeCommand);
        RegisterChatCommand(".cancel", OnCancelCommand);
        RegisterChatCommand(".done", OnDoneCommand);
        RegisterChatCommand(".finish", OnFinishCommand);
        RegisterChatCommand(".snap", OnSnapCommand);
        RegisterChatCommand(".snap_start", OnSnapStartCommand);
        RegisterChatCommand(".snap_end", OnSnapEndCommand);
        RegisterChatCommand(".throw", OnThrowCommand);
        RegisterChatCommand(".t", OnTCommand);

        AddCommandListener("say", OnChatCommandSay, HookMode.Pre);
        AddCommandListener("say_team", OnChatCommandSay, HookMode.Pre);
    }

    private void RegisterChatCommand(string name, Action<CCSPlayerController?, CommandInfo?> handler)
    {
        chatCommandHandlers[name] = handler;
    }

    private HookResult OnChatCommandSay(CCSPlayerController? player, CommandInfo info)
    {
        var rawText = GetChatCommandText(info);
        if (string.IsNullOrWhiteSpace(rawText) || !rawText.StartsWith(".", StringComparison.Ordinal))
        {
            return HookResult.Continue;
        }

        var commandName = GetChatCommandName(rawText);
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return HookResult.Continue;
        }

        if (!chatCommandHandlers.TryGetValue(commandName, out var handler))
        {
            if (player != null && player.IsValid && smokeLineupSessions.ContainsKey(player.SteamID))
            {
                SmokeLineupChat(player, "Unknown smoke command. Use .cancel_smoke to abort the recording.");
                return HookResult.Handled;
            }

            return HookResult.Continue;
        }

        if (AdminOnlyChatCommands.Contains(commandName) && !IsAdmin(player))
        {
            ReplyToUserCommand(player, "You are not allowed to use this command.");
            return HookResult.Handled;
        }

        handler(player, info);
        return HookResult.Handled;
    }

    private static string GetChatCommandText(CommandInfo? info)
    {
        if (info == null)
        {
            return string.Empty;
        }

        return (info.GetArg(1) ?? string.Empty).Trim().Trim('"');
    }

    private static string GetChatCommandName(string rawText)
    {
        return rawText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .ToLowerInvariant() ?? string.Empty;
    }

    private static string GetChatCommandArguments(string rawText)
    {
        var separatorIndex = rawText.IndexOf(' ');
        return separatorIndex >= 0 ? rawText[(separatorIndex + 1)..].Trim() : string.Empty;
    }

    private static string GetCommandActorName(CCSPlayerController? player)
    {
        return player?.PlayerName ?? "Server";
    }

    private static CCSPlayerController? GetUsablePlayer(CCSPlayerController? player)
    {
        return player != null && player.IsValid ? player : null;
    }

    private void ExecuteModeSwitch(CCSPlayerController? player, string configFile, bool practice, bool live, bool warmup, bool loadSpawns, string announcement)
    {
        isPractice = practice;
        isLive = live;
        isWarmup = warmup;

        ResetReadyPlayers();
        PurgeAllSmokeLineupState();

        if (loadSpawns)
        {
            GetSpawns();
        }

        ExecuteConfig(configFile);
        Server.PrintToChatAll($" \x04[BaboPlugin]\x01 {GetCommandActorName(player)} {announcement}");
    }

    private void OnReadyCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        if (ReadyCommandTemporarilyDisabled)
        {
            ReplyToUserCommand(caller, ".ready is temporarily disabled. An admin can start with .live.");
            return;
        }

        HandleReadyCommand(caller);
    }

    private void OnUnreadyCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        if (ReadyCommandTemporarilyDisabled)
        {
            ReplyToUserCommand(caller, ".unready is temporarily disabled.");
            return;
        }

        HandleUnreadyCommand(caller);
    }

    private void OnPauseCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        HandlePauseCommand(caller, true);
    }

    private void OnUnpauseCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        HandlePauseCommand(caller, false);
    }

    private void OnHelpCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        HandlePracticeHelpCommand(caller);
    }

    private void OnPracCommand(CCSPlayerController? player, CommandInfo? info)
    {
        ExecuteModeSwitch(player, "prac.cfg", practice: true, live: false, warmup: false, loadSpawns: true, "loaded practice config.");
    }

    private void OnWarmupCommand(CCSPlayerController? player, CommandInfo? info)
    {
        ExecuteModeSwitch(player, "warmup.cfg", practice: true, live: false, warmup: true, loadSpawns: false, "loaded warmup config.");
    }

    private void OnLiveCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller != null &&
            !ReadyCommandTemporarilyDisabled &&
            !AreAllConnectedPlayersReady(out var readyCount, out var totalCount))
        {
            ReplyToUserCommand(caller, $"Cannot start live. Ready: {readyCount}/{totalCount}. Players must type .ready");
            return;
        }

        ExecuteModeSwitch(player, "live.cfg", practice: false, live: true, warmup: false, loadSpawns: false, "loaded live config.");
    }

    private void OnMoveCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        HandleMoveCommand(caller, GetChatCommandText(info));
    }

    private void OnMapCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        isPractice = false;
        isLive = false;
        isWarmup = false;
        PurgeAllSmokeLineupState();

        HandleMapCommand(caller, GetChatCommandText(info));
    }

    private void OnGodCommand(CCSPlayerController? player, CommandInfo? info) => HandleGodCommand(player);

    private void OnSpawnCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        var spawnArg = GetChatCommandArguments(GetChatCommandText(info))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        HandleSpawnCommand(caller, spawnArg, (byte)caller.TeamNum, "spawn");
    }

    private void OnGlockCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".glock");
    private void OnAkCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".ak");
    private void OnM4a1sCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".m4a1s");
    private void OnM4a4Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".m4a4");
    private void OnMp9Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".mp9");
    private void OnAk47Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".ak47");
    private void OnHkp2000Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".hkp2000");
    private void OnMac10Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".mac10");
    private void OnM4a1SilencerCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".m4a1_silencer");
    private void OnUspSilencerCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".usp_silencer");
    private void OnMp7Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".mp7");
    private void OnM4a1Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".m4a1");
    private void OnP250Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".p250");
    private void OnUmp45Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".ump45");
    private void OnAwpCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".awp");
    private void OnDeagleCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".deagle");
    private void OnP90Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".p90");
    private void OnGalilarCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".galilar");
    private void OnFivesevenCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".fiveseven");
    private void OnNovaCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".nova");
    private void OnFamasCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".famas");
    private void OnEliteCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".elite");
    private void OnXm1014Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".xm1014");
    private void OnSsg08Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".ssg08");
    private void OnCz75Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".cz75");
    private void OnNegevCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".negev");
    private void OnSg556Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".sg556");
    private void OnScoutCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".scout");
    private void OnFiveSevenAliasCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".57");
    private void OnDualEliteCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".dual_elite");
    private void OnTec9Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".tec9");
    private void OnM249Command(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".m249");
    private void OnRevolverCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeWeaponCommand(player, ".revolver");

    private void OnBotPlaceCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeBotCommand(player, GetChatCommandText(info));
    private void OnBotAddCtCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeBotCommand(player, ".bot_add_ct");
    private void OnBotAddTCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeBotCommand(player, ".bot_add_t");
    private void OnBotCrouchCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeBotCommand(player, ".bot_crouch");
    private void OnBotStandCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeBotCommand(player, ".bot_stand");
    private void OnRethrowCommand(CCSPlayerController? player, CommandInfo? info) => ExecutePracticeBotCommand(player, ".rethrow");

    private void OnRecordSmokeCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return;
        }

        PrepareSmokeLineupState();
        if (!IsSmokeLineupAvailable())
        {
            SmokeLineupChat(caller, "Smoke lineup commands are only available in .prac mode.");
            return;
        }

        var lineupName = GetChatCommandArguments(GetChatCommandText(info));
        if (string.IsNullOrWhiteSpace(lineupName))
        {
            SmokeLineupChat(caller, "Usage: .record_smoke <lineup name>");
            return;
        }

        StartSmokeLineupRecording(caller, lineupName);
    }

    private void OnCancelSmokeCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (!TryGetSmokeSession(caller, out _))
        {
            return;
        }

        CleanupSmokeLineupSession(caller!, silent: false);
    }

    private void OnCancelCommand(CCSPlayerController? player, CommandInfo? info) => OnCancelSmokeCommand(player, info);

    private void OnDoneCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (!TryGetSmokeSession(caller, out var session))
        {
            return;
        }

        if (session.CurrentStep != RecordingStep.ReadyToExport)
        {
            SmokeLineupChat(caller!, "The lineup is not ready yet. Follow the current step first.");
            return;
        }

        _ = FinalizeSmokeLineupAsync(caller!, session);
    }

    private void OnFinishCommand(CCSPlayerController? player, CommandInfo? info) => OnDoneCommand(player, info);

    private void OnSnapCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (!TryGetSmokeSession(caller, out var session))
        {
            return;
        }

        HandleSmokeThrowPositionSnap(caller!, session);
    }

    private void OnSnapStartCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (!TryGetSmokeSession(caller, out var session))
        {
            return;
        }

        HandleSmokeMovementSnap(caller!, session, isStart: true);
    }

    private void OnSnapEndCommand(CCSPlayerController? player, CommandInfo? info)
    {
        var caller = GetUsablePlayer(player);
        if (!TryGetSmokeSession(caller, out var session))
        {
            return;
        }

        HandleSmokeMovementSnap(caller!, session, isStart: false);
    }

    private void OnThrowCommand(CCSPlayerController? player, CommandInfo? info)
    {
        ExecuteSmokeThrowCommand(player, GetChatCommandArguments(GetChatCommandText(info)));
    }

    private void OnTCommand(CCSPlayerController? player, CommandInfo? info)
    {
        ExecuteSmokeThrowCommand(player, GetChatCommandArguments(GetChatCommandText(info)));
    }

    private void ExecuteSmokeThrowCommand(CCSPlayerController? player, string rawArguments)
    {
        var caller = GetUsablePlayer(player);
        if (!TryGetSmokeSession(caller, out var session))
        {
            return;
        }

        var throwArg = rawArguments
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .ToLowerInvariant() ?? string.Empty;

        switch (throwArg)
        {
            case "normal":
                HandleSmokeThrowType(caller!, session, ThrowType.Normal);
                return;
            case "middle":
                HandleSmokeThrowType(caller!, session, ThrowType.MiddleClick);
                return;
            case "jump":
                HandleSmokeThrowType(caller!, session, ThrowType.JumpThrow);
                return;
            case "wjump":
                HandleSmokeThrowType(caller!, session, ThrowType.WJumpThrow);
                return;
            default:
                SmokeLineupChat(caller!, "Throw type must be one of: normal, middle, jump, wjump.");
                return;
        }
    }

    private bool TryGetSmokeSession(CCSPlayerController? player, out RecordingSession session)
    {
        session = null!;

        var caller = GetUsablePlayer(player);
        if (caller == null)
        {
            return false;
        }

        PrepareSmokeLineupState();
        if (!IsSmokeLineupAvailable())
        {
            SmokeLineupChat(caller, "Smoke lineup commands are only available in .prac mode.");
            return false;
        }

        if (!smokeLineupSessions.TryGetValue(caller.SteamID, out var activeSession))
        {
            SmokeLineupChat(caller, "No active recording. Use .record_smoke <lineup name>.");
            return false;
        }

        session = activeSession;
        return true;
    }
}
