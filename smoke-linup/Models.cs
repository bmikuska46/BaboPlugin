using System.Text.Json.Serialization;

namespace BaboPlugin;

// ─────────────────────────────────────────────────────────────
//  ENUMS
// ─────────────────────────────────────────────────────────────

public enum ThrowType
{
    Normal,       // stand still, left-click
    MiddleClick,  // hold smoke with middle mouse (no release on trigger)
    JumpThrow,    // jump-throw bind
    WJumpThrow    // walk/run forward, then jump-throw at end
}

public enum RecordingStep
{
    WaitingForThrowPosition,   // 1  – player stands at throw spot & aims
    WaitingForThrowType,       // 2  – pick throw type
    WaitingForMoveStart,       // 3a – walk to START of run-up  (WJumpThrow only)
    WaitingForMoveEnd,         // 3b – walk to END of run-up    (WJumpThrow only)
    WaitingForTestThrow,       // 4  – test-throw so the plugin can photograph the landing
    ReadyToExport              //     all done, awaiting .done
}

// ─────────────────────────────────────────────────────────────
//  VALUE OBJECTS
// ─────────────────────────────────────────────────────────────

public class Vec3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vec3() { }
    public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

// ─────────────────────────────────────────────────────────────
//  LINEUP DATA  (serialised → JSON + README + Discord embed)
// ─────────────────────────────────────────────────────────────

public class LineupData
{
    // ── Identity ─────────────────────────────────────────────
    public string Name       { get; set; } = "";
    public string Map        { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public string SteamId    { get; set; } = "";
    public string RecordedAt { get; set; } = "";

    // ── Throw origin & crosshair ──────────────────────────────
    public Vec3 ThrowPosition { get; set; } = new();
    public Vec3 ThrowAngles   { get; set; } = new();   // Pitch = up/down, Yaw = left/right

    // ── Throw mechanics ───────────────────────────────────────
    public string ThrowType        { get; set; } = "";
    public string ThrowDescription { get; set; } = "";

    // ── Movement data (WJumpThrow only) ───────────────────────
    public bool RequiresMovement { get; set; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vec3? MoveStartPosition { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vec3? MoveEndPosition { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vec3? CrosshairAtMoveStart { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vec3? CrosshairAtMoveEnd { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? MoveDistanceUnits { get; set; }

    // ── Smoke impact (captured from EventSmokeGrenadeDetonate) ─
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vec3? SmokeImpactPosition { get; set; }

    // ── Convenience ───────────────────────────────────────────
    /// <summary>setpos + setang command the player can paste in-game.</summary>
    public string TeleportCommand { get; set; } = "";
}

// ─────────────────────────────────────────────────────────────
//  RECORDING SESSION  (in-memory wizard state per player)
// ─────────────────────────────────────────────────────────────

public class RecordingSession
{
    public string        LineupName   { get; set; } = "";
    public RecordingStep CurrentStep  { get; set; } = RecordingStep.WaitingForThrowPosition;
    public LineupData    Data         { get; set; } = new();

    /// True after the player fires a smoke grenade in WaitingForTestThrow,
    /// prevents accidentally capturing someone else's grenade.
    public bool ThrewGrenade { get; set; } = false;

    /// Human-readable labels for every screenshot taken (shown at export time).
    public List<string> ScreenshotLabels { get; set; } = new();
    public int          ScreenshotIndex  { get; set; } = 0;
}

// ─────────────────────────────────────────────────────────────
//  CONFIG
// ─────────────────────────────────────────────────────────────

public class SmokeLineupConfig
{
    public int Version { get; set; } = 1;

    [JsonPropertyName("DiscordBotToken")]
    public string DiscordBotToken { get; set; } = "";

    [JsonPropertyName("DiscordChannelId")]
    public string DiscordChannelId { get; set; } = "1073237888814288976";

    /// Server-side path (relative to game root) where lineup ZIPs are written.
    [JsonPropertyName("ExportDirectory")]
    public string ExportDirectory { get; set; } =
        "addons/counterstrikesharp/plugins/BaboPlugin/lineups";

    /// Seconds to wait after switching camera mode before the screenshot fires.
    [JsonPropertyName("ScreenshotDelay")]
    public float ScreenshotDelay { get; set; } = 0.8f;

    /// Requires sv_cheats 1 – switches the player to third-person for position shots.
    [JsonPropertyName("EnableThirdPersonScreenshots")]
    public bool EnableThirdPersonScreenshots { get; set; } = true;

    /// Units above the smoke impact point for the overhead impact camera.
    [JsonPropertyName("ImpactCameraHeight")]
    public float ImpactCameraHeight { get; set; } = 400f;

    /// Spawn a floating ▼ THROW SPOT ▼ world-text label during third-person shots.
    [JsonPropertyName("SpawnPositionMarker")]
    public bool SpawnPositionMarker { get; set; } = true;

    [JsonPropertyName("MaxConcurrentSessions")]
    public int MaxConcurrentSessions { get; set; } = 5;

}
