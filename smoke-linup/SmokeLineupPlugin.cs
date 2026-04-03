using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;

namespace BaboPlugin;

public partial class BaboPlugin
{
    private readonly Dictionary<ulong, RecordingSession> smokeLineupSessions = new();
    private readonly Dictionary<ulong, CPointWorldText> smokeLineupMarkers = new();
    private readonly ExportService smokeLineupExport = new();

    private SmokeLineupConfig smokeLineupConfig = new();
    private string smokeLineupMapName = string.Empty;

    private bool IsSmokeLineupAvailable()
    {
        return isPractice && !isWarmup;
    }

    private static bool IsSmokeLineupCommand(string text)
    {
        return text == ".record_smoke" ||
               text.StartsWith(".record_smoke ", StringComparison.Ordinal) ||
               text == ".cancel_smoke" ||
               text == ".cancel" ||
               text == ".done" ||
               text == ".finish" ||
               text == ".snap" ||
               text == ".snap_start" ||
               text == ".snap_end" ||
               text.StartsWith(".throw ", StringComparison.Ordinal) ||
               text.StartsWith(".t ", StringComparison.Ordinal);
    }

    private void LoadSmokeLineupConfig()
    {
        var configPath = GetSmokeLineupConfigPath();
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        try
        {
            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    smokeLineupConfig = JsonSerializer.Deserialize<SmokeLineupConfig>(json, options) ?? new SmokeLineupConfig();
                }
            }
            else
            {
                File.WriteAllText(configPath, JsonSerializer.Serialize(smokeLineupConfig, options));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmokeLineup] Failed to load config: {ex.Message}");
            smokeLineupConfig = new SmokeLineupConfig();
        }

        smokeLineupConfig.ExportDirectory = ResolveSmokeLineupPath(smokeLineupConfig.ExportDirectory);
        Directory.CreateDirectory(smokeLineupConfig.ExportDirectory);
        smokeLineupMapName = Server.MapName ?? string.Empty;
    }

    public override void Unload(bool hotReload)
    {
        PurgeAllSmokeLineupState();
    }

    [GameEventHandler]
    public HookResult OnSmokeLineupWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (!IsSmokeLineupAvailable())
        {
            return HookResult.Continue;
        }

        PrepareSmokeLineupState();

        var player = @event.Userid;
        if (!IsPlayerValid(player))
        {
            return HookResult.Continue;
        }

        if (!smokeLineupSessions.TryGetValue(player!.SteamID, out var session))
        {
            return HookResult.Continue;
        }

        if (session.CurrentStep != RecordingStep.WaitingForTestThrow)
        {
            return HookResult.Continue;
        }

        var weapon = (@event.Weapon ?? string.Empty).ToLowerInvariant();
        if (!weapon.Contains("smoke", StringComparison.Ordinal))
        {
            return HookResult.Continue;
        }

        session.ThrewGrenade = true;
        SmokeLineupChat(player, "Smoke thrown. Waiting for detonation...");
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnSmokeLineupDetonate(EventSmokegrenadeDetonate @event, GameEventInfo info)
    {
        if (!IsSmokeLineupAvailable())
        {
            return HookResult.Continue;
        }

        PrepareSmokeLineupState();

        var player = @event.Userid;
        if (!IsPlayerValid(player))
        {
            return HookResult.Continue;
        }

        if (!smokeLineupSessions.TryGetValue(player!.SteamID, out var session))
        {
            return HookResult.Continue;
        }

        if (session.CurrentStep != RecordingStep.WaitingForTestThrow || !session.ThrewGrenade)
        {
            return HookResult.Continue;
        }

        session.ThrewGrenade = false;

        var impact = new Vec3(@event.X, @event.Y, @event.Z);
        session.Data.SmokeImpactPosition = impact;

        SmokeLineupChat(player, $"Smoke landed at {impact}.");
        SmokeLineupChat(player, "Taking impact screenshots. Do not move.");

        TakeSmokeImpactScreenshots(player, session, impact);
        session.CurrentStep = RecordingStep.ReadyToExport;

        var totalDelay = smokeLineupConfig.ScreenshotDelay * 2f +
                         (smokeLineupConfig.EnableThirdPersonScreenshots ? smokeLineupConfig.ScreenshotDelay + 0.5f : 0.5f);

        AddTimer(totalDelay + 0.5f, () =>
        {
            if (!smokeLineupSessions.ContainsKey(player.SteamID))
            {
                return;
            }

            SmokeLineupChat(player, "All data captured.");
            SmokeLineupChat(player, "Type .done to export or .cancel_smoke to discard.");
        });

        return HookResult.Continue;
    }

    private bool TryHandleSmokeLineupCommand(CCSPlayerController player, string rawText, string text)
    {
        PrepareSmokeLineupState();

        var hasSession = smokeLineupSessions.TryGetValue(player.SteamID, out var session);
        if (!IsSmokeLineupCommand(text) && !(hasSession && text.StartsWith(".", StringComparison.Ordinal)))
        {
            return false;
        }

        if (!IsSmokeLineupAvailable())
        {
            if (IsSmokeLineupCommand(text))
            {
                SmokeLineupChat(player, "Smoke lineup commands are only available in .prac mode.");
                return true;
            }

            return false;
        }

        if (text == ".record_smoke")
        {
            SmokeLineupChat(player, "Usage: .record_smoke <lineup name>");
            return true;
        }

        if (text.StartsWith(".record_smoke ", StringComparison.Ordinal))
        {
            var name = rawText.Length > 14 ? rawText[14..].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                SmokeLineupChat(player, "Usage: .record_smoke <lineup name>");
                return true;
            }

            StartSmokeLineupRecording(player, name);
            return true;
        }

        if (!hasSession)
        {
            if (IsSmokeLineupCommand(text))
            {
                SmokeLineupChat(player, "No active recording. Use .record_smoke <lineup name>.");
                return true;
            }

            return false;
        }

        switch (text)
        {
            case ".cancel_smoke":
            case ".cancel":
                CleanupSmokeLineupSession(player, silent: false);
                return true;
            case ".done":
            case ".finish":
                if (session!.CurrentStep == RecordingStep.ReadyToExport)
                {
                    _ = FinalizeSmokeLineupAsync(player, session);
                }
                else
                {
                    SmokeLineupChat(player, "The lineup is not ready yet. Follow the current step first.");
                }

                return true;
            case ".snap":
                HandleSmokeThrowPositionSnap(player, session!);
                return true;
            case ".snap_start":
                HandleSmokeMovementSnap(player, session!, true);
                return true;
            case ".snap_end":
                HandleSmokeMovementSnap(player, session!, false);
                return true;
            case ".throw normal":
            case ".t normal":
                HandleSmokeThrowType(player, session!, ThrowType.Normal);
                return true;
            case ".throw middle":
            case ".t middle":
                HandleSmokeThrowType(player, session!, ThrowType.MiddleClick);
                return true;
            case ".throw jump":
            case ".t jump":
                HandleSmokeThrowType(player, session!, ThrowType.JumpThrow);
                return true;
            case ".throw wjump":
            case ".t wjump":
                HandleSmokeThrowType(player, session!, ThrowType.WJumpThrow);
                return true;
        }

        if (text.StartsWith(".throw ", StringComparison.Ordinal) || text.StartsWith(".t ", StringComparison.Ordinal))
        {
            SmokeLineupChat(player, "Throw type must be one of: normal, middle, jump, wjump.");
            return true;
        }

        if (text.StartsWith(".", StringComparison.Ordinal))
        {
            SmokeLineupChat(player, "Unknown smoke command. Use .cancel_smoke to abort the recording.");
            return true;
        }

        return false;
    }

    private void StartSmokeLineupRecording(CCSPlayerController player, string name)
    {
        if (!player.PawnIsAlive || !IsPlayerValid(player))
        {
            SmokeLineupChat(player, "You must be alive to record a smoke lineup.");
            return;
        }

        if (smokeLineupSessions.ContainsKey(player.SteamID))
        {
            SmokeLineupChat(player, "You already have an active recording. Use .cancel_smoke first.");
            return;
        }

        if (smokeLineupSessions.Count >= smokeLineupConfig.MaxConcurrentSessions)
        {
            SmokeLineupChat(player, "The server is at the smoke-recording limit. Try again later.");
            return;
        }

        var currentMap = Server.MapName ?? string.Empty;
        var session = new RecordingSession
        {
            LineupName = name,
            CurrentStep = RecordingStep.WaitingForThrowPosition,
            Data = new LineupData
            {
                Name = name,
                Map = currentMap,
                RecordedBy = player.PlayerName,
                SteamId = player.SteamID.ToString(),
                RecordedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            }
        };

        smokeLineupSessions[player.SteamID] = session;

        SmokeLineupChat(player, $"Recording started: {name}");
        SmokeLineupChat(player, "Step 1/4: stand at the throw position and aim at the target.");
        SmokeLineupChat(player, "Type .snap when ready, or .cancel_smoke to abort.");
    }

    private void HandleSmokeThrowPositionSnap(CCSPlayerController player, RecordingSession session)
    {
        if (session.CurrentStep != RecordingStep.WaitingForThrowPosition)
        {
            SmokeLineupChat(player, ".snap is only used for step 1.");
            return;
        }

        if (!TryGetPlayerTransform(player, out var position, out var angles))
        {
            SmokeLineupChat(player, "You must be alive to capture the throw position.");
            return;
        }

        session.Data.ThrowPosition = new Vec3(position.X, position.Y, position.Z);
        session.Data.ThrowAngles = new Vec3(angles.X, angles.Y, angles.Z);
        session.Data.TeleportCommand =
            $"setpos {position.X:F2} {position.Y:F2} {position.Z:F2}; setang {angles.X:F2} {angles.Y:F2} {angles.Z:F2}";

        TakePositionScreenshotPair(player, session, "throw_pos");
        session.CurrentStep = RecordingStep.WaitingForThrowType;

        SmokeLineupChat(player, $"Throw position saved at {session.Data.ThrowPosition}.");
        SmokeLineupChat(player, "Step 2/4: choose .throw normal, .throw middle, .throw jump, or .throw wjump.");
    }

    private void HandleSmokeThrowType(CCSPlayerController player, RecordingSession session, ThrowType type)
    {
        if (session.CurrentStep != RecordingStep.WaitingForThrowType)
        {
            SmokeLineupChat(player, "Complete step 1 first with .snap.");
            return;
        }

        session.Data.ThrowType = type.ToString();
        session.Data.ThrowDescription = DescribeSmokeThrow(type);

        if (type == ThrowType.WJumpThrow)
        {
            session.Data.RequiresMovement = true;
            session.CurrentStep = RecordingStep.WaitingForMoveStart;

            SmokeLineupChat(player, "Throw type saved: wjump.");
            SmokeLineupChat(player, "Step 3/4: move to the start of the run-up and use .snap_start.");
            return;
        }

        session.Data.RequiresMovement = false;
        SmokeLineupChat(player, $"Throw type saved: {type}.");
        EnterSmokeTestThrowStep(player, session);
    }

    private void HandleSmokeMovementSnap(CCSPlayerController player, RecordingSession session, bool isStart)
    {
        var expectedStep = isStart ? RecordingStep.WaitingForMoveStart : RecordingStep.WaitingForMoveEnd;
        if (session.CurrentStep != expectedStep)
        {
            SmokeLineupChat(player, isStart
                ? "You are not at the run-up start step."
                : "You are not at the run-up end step.");
            return;
        }

        if (!TryGetPlayerTransform(player, out var position, out var angles))
        {
            SmokeLineupChat(player, "You must be alive to capture movement points.");
            return;
        }

        if (isStart)
        {
            session.Data.MoveStartPosition = new Vec3(position.X, position.Y, position.Z);
            session.Data.CrosshairAtMoveStart = new Vec3(angles.X, angles.Y, angles.Z);
            TakePositionScreenshotPair(player, session, "move_start");

            session.CurrentStep = RecordingStep.WaitingForMoveEnd;
            SmokeLineupChat(player, $"Run-up start saved at {session.Data.MoveStartPosition}.");
            SmokeLineupChat(player, "Move to the throw point and use .snap_end.");
            return;
        }

        session.Data.MoveEndPosition = new Vec3(position.X, position.Y, position.Z);
        session.Data.CrosshairAtMoveEnd = new Vec3(angles.X, angles.Y, angles.Z);

        var start = session.Data.MoveStartPosition!;
        var dx = position.X - start.X;
        var dy = position.Y - start.Y;
        session.Data.MoveDistanceUnits = MathF.Sqrt(dx * dx + dy * dy);

        TakePositionScreenshotPair(player, session, "move_end");

        SmokeLineupChat(player, $"Run-up end saved at {session.Data.MoveEndPosition}.");
        SmokeLineupChat(player, $"Run-up distance: {session.Data.MoveDistanceUnits:F1} units.");

        EnterSmokeTestThrowStep(player, session);
    }

    private void EnterSmokeTestThrowStep(CCSPlayerController player, RecordingSession session)
    {
        session.CurrentStep = RecordingStep.WaitingForTestThrow;

        var pawn = player.PlayerPawn?.Value;
        if (pawn != null)
        {
            var throwPosition = session.Data.ThrowPosition;
            var throwAngles = session.Data.ThrowAngles;

            pawn.Teleport(
                new Vector(throwPosition.X, throwPosition.Y, throwPosition.Z),
                new QAngle(throwAngles.X, throwAngles.Y, throwAngles.Z),
                new Vector(0f, 0f, 0f));

            SmokeLineupChat(player, "Teleported back to the throw position.");
        }

        SmokeLineupChat(player, "Step 4/4: throw the smoke exactly as recorded.");
        SmokeLineupChat(player, "The plugin will capture the smoke landing automatically.");
    }

    private void TakePositionScreenshotPair(CCSPlayerController player, RecordingSession session, string label)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null)
        {
            return;
        }

        var idx = ++session.ScreenshotIndex;

        if (smokeLineupConfig.EnableThirdPersonScreenshots)
        {
            CPointWorldText? marker = null;
            if (smokeLineupConfig.SpawnPositionMarker)
            {
                marker = SpawnSmokeLineupMarker(pawn.AbsOrigin);
                RememberSmokeLineupMarker(player.SteamID, marker);
            }

            player.ExecuteClientCommandFromServer("thirdperson");
            AddTimer(smokeLineupConfig.ScreenshotDelay, () =>
            {
                player.ExecuteClientCommandFromServer("screenshot");
                session.ScreenshotLabels.Add($"[{idx:00}] {label} - third-person position");

                AddTimer(0.25f, () =>
                {
                    player.ExecuteClientCommandFromServer("firstperson");
                    RemoveSmokeLineupMarker(player.SteamID, marker);

                    AddTimer(smokeLineupConfig.ScreenshotDelay, () =>
                    {
                        player.ExecuteClientCommandFromServer("screenshot");
                        session.ScreenshotLabels.Add($"[{idx:00}] {label} - first-person crosshair");
                    });
                });
            });
            return;
        }

        AddTimer(smokeLineupConfig.ScreenshotDelay, () =>
        {
            player.ExecuteClientCommandFromServer("screenshot");
            session.ScreenshotLabels.Add($"[{idx:00}] {label} - first-person crosshair");
        });
    }

    private void TakeSmokeImpactScreenshots(CCSPlayerController player, RecordingSession session, Vec3 impact)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null || pawn.EyeAngles == null)
        {
            return;
        }

        var originalPosition = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z);
        var originalAngles = new QAngle(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z);

        var overheadPosition = new Vector(impact.X, impact.Y, impact.Z + smokeLineupConfig.ImpactCameraHeight);
        var lookDownAngles = new QAngle(89f, 0f, 0f);

        pawn.Teleport(overheadPosition, lookDownAngles, new Vector(0f, 0f, 0f));
        player.ExecuteClientCommandFromServer("firstperson");

        var idx = ++session.ScreenshotIndex;
        AddTimer(smokeLineupConfig.ScreenshotDelay, () =>
        {
            player.ExecuteClientCommandFromServer("screenshot");
            session.ScreenshotLabels.Add($"[{idx:00}] smoke_impact - overhead first-person");

            if (smokeLineupConfig.EnableThirdPersonScreenshots)
            {
                player.ExecuteClientCommandFromServer("thirdperson");
                AddTimer(smokeLineupConfig.ScreenshotDelay, () =>
                {
                    player.ExecuteClientCommandFromServer("screenshot");
                    session.ScreenshotLabels.Add($"[{idx:00}] smoke_impact - overhead third-person");

                    AddTimer(0.3f, () =>
                    {
                        player.ExecuteClientCommandFromServer("firstperson");
                        player.PlayerPawn?.Value?.Teleport(originalPosition, originalAngles, new Vector(0f, 0f, 0f));
                    });
                });

                return;
            }

            AddTimer(0.3f, () =>
            {
                player.PlayerPawn?.Value?.Teleport(originalPosition, originalAngles, new Vector(0f, 0f, 0f));
            });
        });
    }

    private async Task FinalizeSmokeLineupAsync(CCSPlayerController player, RecordingSession session)
    {
        SmokeLineupChat(player, "Exporting lineup...");

        try
        {
            var (zipPath, discordOk) = await smokeLineupExport.ExportAsync(
                session.Data,
                smokeLineupConfig.ExportDirectory,
                smokeLineupConfig.DiscordWebhookUrl);

            SmokeLineupChat(player, $"Saved export: {Path.GetFileName(zipPath)}");

            if (!string.IsNullOrWhiteSpace(smokeLineupConfig.DiscordWebhookUrl))
            {
                SmokeLineupChat(player, discordOk ? "Discord upload succeeded." : "Discord upload failed.");
            }

            if (session.ScreenshotLabels.Count > 0)
            {
                SmokeLineupChat(player, $"{session.ScreenshotLabels.Count} screenshot(s) were taken in CS2/screenshots.");
                foreach (var label in session.ScreenshotLabels)
                {
                    SmokeLineupChat(player, label);
                }
            }

            SmokeLineupChat(player, $"Practice command: {session.Data.TeleportCommand}");
        }
        catch (Exception ex)
        {
            SmokeLineupChat(player, $"Export failed: {ex.Message}");
            Console.WriteLine($"[SmokeLineup] Export failed: {ex}");
        }
        finally
        {
            CleanupSmokeLineupSession(player, silent: true);
        }
    }

    private void PrepareSmokeLineupState()
    {
        EnsureSmokeLineupMapState();
        PruneSmokeLineupSessions();
    }

    private void EnsureSmokeLineupMapState()
    {
        var currentMap = Server.MapName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(smokeLineupMapName))
        {
            smokeLineupMapName = currentMap;
            return;
        }

        if (string.Equals(smokeLineupMapName, currentMap, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PurgeAllSmokeLineupState();
        smokeLineupMapName = currentMap;
    }

    private void PruneSmokeLineupSessions()
    {
        if (smokeLineupSessions.Count == 0)
        {
            return;
        }

        var connectedSteamIds = Utilities.GetPlayers()
            .Where(p => p.IsValid)
            .Select(p => p.SteamID)
            .ToHashSet();

        var staleSteamIds = smokeLineupSessions.Keys
            .Where(steamId => !connectedSteamIds.Contains(steamId))
            .ToList();

        foreach (var steamId in staleSteamIds)
        {
            CleanupSmokeLineupSession(steamId, null, true);
        }
    }

    private void CleanupSmokeLineupSession(CCSPlayerController player, bool silent)
    {
        CleanupSmokeLineupSession(player.SteamID, player, silent);
    }

    private void CleanupSmokeLineupSession(ulong steamId, CCSPlayerController? player, bool silent)
    {
        RemoveSmokeLineupMarker(steamId);
        if (smokeLineupSessions.Remove(steamId) && !silent && player != null)
        {
            SmokeLineupChat(player, "Recording cancelled.");
        }
    }

    private void PurgeAllSmokeLineupState()
    {
        foreach (var marker in smokeLineupMarkers.Values)
        {
            if (marker.IsValid)
            {
                marker.Remove();
            }
        }

        smokeLineupMarkers.Clear();
        smokeLineupSessions.Clear();
    }

    private void RememberSmokeLineupMarker(ulong steamId, CPointWorldText? marker)
    {
        if (marker == null)
        {
            return;
        }

        RemoveSmokeLineupMarker(steamId);
        smokeLineupMarkers[steamId] = marker;
    }

    private void RemoveSmokeLineupMarker(ulong steamId, CPointWorldText? expectedMarker = null)
    {
        if (!smokeLineupMarkers.TryGetValue(steamId, out var marker))
        {
            return;
        }

        if (expectedMarker != null && !ReferenceEquals(marker, expectedMarker))
        {
            return;
        }

        if (marker.IsValid)
        {
            marker.Remove();
        }

        smokeLineupMarkers.Remove(steamId);
    }

    private static CPointWorldText? SpawnSmokeLineupMarker(Vector origin)
    {
        try
        {
            var marker = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
            if (marker == null)
            {
                return null;
            }

            marker.MessageText = "THROW SPOT";
            marker.FontSize = 28;
            marker.Color = System.Drawing.Color.OrangeRed;
            marker.WorldUnitsPerPx = 0.05f;
            marker.DrawBackground = false;
            marker.Teleport(
                new Vector(origin.X, origin.Y, origin.Z + 90f),
                new QAngle(0f, 0f, 0f),
                new Vector(0f, 0f, 0f));
            marker.DispatchSpawn();

            return marker;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmokeLineup] Failed to spawn marker: {ex.Message}");
            return null;
        }
    }

    private static bool TryGetPlayerTransform(CCSPlayerController player, out Vector position, out QAngle angles)
    {
        position = new Vector(0f, 0f, 0f);
        angles = new QAngle(0f, 0f, 0f);

        var pawn = player.PlayerPawn?.Value;
        if (!player.PawnIsAlive || pawn?.AbsOrigin == null || pawn.EyeAngles == null)
        {
            return false;
        }

        position = pawn.AbsOrigin;
        angles = pawn.EyeAngles;
        return true;
    }

    private static string DescribeSmokeThrow(ThrowType type)
    {
        return type switch
        {
            ThrowType.Normal => "Stand still and left-click throw.",
            ThrowType.MiddleClick => "Stand still and use middle mouse throw.",
            ThrowType.JumpThrow => "Use a jump-throw bind.",
            ThrowType.WJumpThrow => "Run the marked path, then jump-throw at the end.",
            _ => string.Empty
        };
    }

    private static string GetSmokeLineupConfigPath()
    {
        return Path.Join(Server.GameDirectory, "csgo", "cfg", "BaboPlugin", "smoke-lineup.json");
    }

    private static string ResolveSmokeLineupPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Join(Server.GameDirectory, "csgo", path);
    }

    private void SmokeLineupChat(CCSPlayerController player, string message)
    {
        player.PrintToChat($" \x04[BaboPlugin]\x01 [Smoke] {message}");
    }
}
