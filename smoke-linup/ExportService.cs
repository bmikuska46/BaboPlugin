using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace BaboPlugin;

/// <summary>
/// Handles all I/O: writes JSON + README, creates a ZIP, posts a rich embed to Discord.
/// </summary>
public class ExportService
{
    private readonly HttpClient _http = new();

    // ─────────────────────────────────────────────────────────
    //  PUBLIC ENTRY POINT
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Serialises lineup data, produces a ZIP on disk, and optionally POSTs to Discord.
    /// Returns (zipPath, discordOk).
    /// </summary>
    public async Task<(string zipPath, bool discordOk)> ExportAsync(
        LineupData data,
        string     exportRoot,
        string     discordWebhookUrl)
    {
        var safe   = MakeSafe(data.Name);
        var stamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var folder = Path.Combine(exportRoot, $"{data.Map}_{safe}_{stamp}");
        Directory.CreateDirectory(folder);

        // 1. JSON
        var json     = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(folder, "lineup.json"), json);

        // 2. Human-readable README
        await File.WriteAllTextAsync(Path.Combine(folder, "README.txt"), BuildReadme(data));

        // 3. ZIP
        var zipName = $"{data.Map}_{safe}.zip";
        var zipPath = Path.Combine(exportRoot, zipName);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(folder, zipPath);

        // 4. Discord
        bool discordOk = false;
        if (!string.IsNullOrWhiteSpace(discordWebhookUrl))
            discordOk = await PostToDiscordAsync(data, zipPath, discordWebhookUrl);

        return (zipPath, discordOk);
    }

    // ─────────────────────────────────────────────────────────
    //  DISCORD
    // ─────────────────────────────────────────────────────────

    private async Task<bool> PostToDiscordAsync(
        LineupData data,
        string     zipPath,
        string     webhookUrl)
    {
        try
        {
            int embedColor = data.ThrowType switch
            {
                "Normal"      => 3066993,    // green
                "MiddleClick" => 3447003,    // blue
                "JumpThrow"   => 16776960,   // yellow
                "WJumpThrow"  => 15158332,   // red
                _             => 9807270
            };
            string throwEmoji = data.ThrowType switch
            {
                "Normal"      => "🟢",
                "MiddleClick" => "🔵",
                "JumpThrow"   => "🟡",
                "WJumpThrow"  => "🔴",
                _             => "⚪"
            };

            var fields = new List<object>
            {
                F("🗺️ Map",         data.Map,        true),
                F("👤 Recorded By",  data.RecordedBy, true),
                F("📅 Date (UTC)",   data.RecordedAt, false),

                F($"{throwEmoji} Throw Type",
                  $"**{data.ThrowType}**\n{data.ThrowDescription}",
                  false),

                F("📍 Throw Position",
                  $"```X: {data.ThrowPosition.X:F2}\nY: {data.ThrowPosition.Y:F2}\nZ: {data.ThrowPosition.Z:F2}```",
                  true),

                F("🎯 Crosshair Aim",
                  $"```Pitch: {data.ThrowAngles.X:F2}°\nYaw:   {data.ThrowAngles.Y:F2}°```",
                  true),
            };

            // Movement block
            if (data.RequiresMovement
                && data.MoveStartPosition != null
                && data.MoveEndPosition   != null)
            {
                var distStr = data.MoveDistanceUnits.HasValue
                    ? $"\n**Distance:** `{data.MoveDistanceUnits:F1} units`"
                    : "";
                fields.Add(F("🏃 Run-up Path",
                    $"**Start:** `{data.MoveStartPosition}`\n**End:** `{data.MoveEndPosition}`{distStr}",
                    false));

                fields.Add(F("🎯 Crosshair During Run-up",
                    $"**At start —** Pitch `{data.CrosshairAtMoveStart?.X:F2}°` / Yaw `{data.CrosshairAtMoveStart?.Y:F2}°`\n" +
                    $"**At throw —** Pitch `{data.CrosshairAtMoveEnd?.X:F2}°` / Yaw `{data.CrosshairAtMoveEnd?.Y:F2}°`",
                    false));
            }

            // Smoke impact block
            if (data.SmokeImpactPosition != null)
            {
                fields.Add(F("💨 Smoke Impact Position",
                    $"```X: {data.SmokeImpactPosition.X:F2}\nY: {data.SmokeImpactPosition.Y:F2}\nZ: {data.SmokeImpactPosition.Z:F2}```",
                    false));
            }

            fields.Add(F("⌨️ Practice Teleport (sv_cheats 1)",
                $"```{data.TeleportCommand}```",
                false));

            fields.Add(F("📸 Screenshots",
                "Saved **client-side** — check your `CS2/screenshots/` folder.\n" +
                "Attach them to this message manually.",
                false));

            var embed = new
            {
                title       = $"💨 Smoke Lineup: **{data.Name}**",
                description = $"New lineup recorded on **{data.Map}**",
                color       = embedColor,
                fields,
                footer      = new { text = $"SteamID: {data.SteamId}" },
                timestamp   = DateTime.UtcNow.ToString("o")
            };

            var payloadJson = JsonSerializer.Serialize(new
            {
                username = "Smoke Lineup Bot",
                embeds   = new[] { embed }
            });

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");

            // Attach ZIP (<25 MB Discord limit)
            var fi = new FileInfo(zipPath);
            if (fi.Exists && fi.Length < 25L * 1024 * 1024)
            {
                var bytes   = await File.ReadAllBytesAsync(zipPath);
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                form.Add(content, "files[0]", fi.Name);
            }

            var resp = await _http.PostAsync(webhookUrl, form);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmokeLineup] Discord error: {ex.Message}");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  README
    // ─────────────────────────────────────────────────────────

    private static string BuildReadme(LineupData d)
    {
        const string HR = "═══════════════════════════════════════════════════";
        var sb = new StringBuilder();

        sb.AppendLine(HR);
        sb.AppendLine($"  SMOKE LINEUP  ·  {d.Name.ToUpperInvariant()}");
        sb.AppendLine(HR);
        sb.AppendLine();
        sb.AppendLine($"  Map         : {d.Map}");
        sb.AppendLine($"  Recorded by : {d.RecordedBy}  ({d.SteamId})");
        sb.AppendLine($"  Date (UTC)  : {d.RecordedAt}");
        sb.AppendLine();

        sb.AppendLine("  ── HOW TO THROW ──────────────────────────────────");
        sb.AppendLine($"  Type   : {d.ThrowType}");
        sb.AppendLine($"  Method : {d.ThrowDescription}");
        sb.AppendLine();

        sb.AppendLine("  ── THROW POSITION ────────────────────────────────");
        sb.AppendLine($"  X : {d.ThrowPosition.X,10:F2}");
        sb.AppendLine($"  Y : {d.ThrowPosition.Y,10:F2}");
        sb.AppendLine($"  Z : {d.ThrowPosition.Z,10:F2}");
        sb.AppendLine();

        sb.AppendLine("  ── CROSSHAIR AIM (at moment of throw) ────────────");
        sb.AppendLine($"  Pitch (up/down)    : {d.ThrowAngles.X,8:F2}°");
        sb.AppendLine($"  Yaw   (left/right) : {d.ThrowAngles.Y,8:F2}°");
        sb.AppendLine();

        if (d.RequiresMovement && d.MoveStartPosition != null && d.MoveEndPosition != null)
        {
            sb.AppendLine("  ── RUN-UP PATH ───────────────────────────────────");
            sb.AppendLine($"  Start position  : {d.MoveStartPosition}");
            sb.AppendLine($"  Start crosshair : Pitch {d.CrosshairAtMoveStart?.X:F2}°  Yaw {d.CrosshairAtMoveStart?.Y:F2}°");
            sb.AppendLine();
            sb.AppendLine($"  End position    : {d.MoveEndPosition}");
            sb.AppendLine($"  End crosshair   : Pitch {d.CrosshairAtMoveEnd?.X:F2}°  Yaw {d.CrosshairAtMoveEnd?.Y:F2}°");
            if (d.MoveDistanceUnits.HasValue)
                sb.AppendLine($"  Run distance    : {d.MoveDistanceUnits:F1} units");
            sb.AppendLine();
        }

        if (d.SmokeImpactPosition != null)
        {
            sb.AppendLine("  ── SMOKE LANDING POSITION ────────────────────────");
            sb.AppendLine($"  X : {d.SmokeImpactPosition.X,10:F2}");
            sb.AppendLine($"  Y : {d.SmokeImpactPosition.Y,10:F2}");
            sb.AppendLine($"  Z : {d.SmokeImpactPosition.Z,10:F2}");
            sb.AppendLine();
        }

        sb.AppendLine("  ── PRACTICE COMMANDS (sv_cheats 1) ───────────────");
        sb.AppendLine($"  {d.TeleportCommand}");
        sb.AppendLine();
        sb.AppendLine("  ── SCREENSHOTS ───────────────────────────────────");
        sb.AppendLine("  Saved client-side in CS2/screenshots/");
        sb.AppendLine("  Each .snap and the smoke impact produce two shots:");
        sb.AppendLine("    *_3p.jpg  =  third-person view with ▼ THROW SPOT ▼ marker");
        sb.AppendLine("    *_1p.jpg  =  first-person crosshair placement");
        sb.AppendLine("  Impact shots:");
        sb.AppendLine("    *_impact_overhead_1p.jpg  =  first-person straight down");
        sb.AppendLine("    *_impact_overhead_3p.jpg  =  third-person overhead (if enabled)");
        sb.AppendLine();
        sb.AppendLine(HR);

        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────
    //  TINY HELPERS
    // ─────────────────────────────────────────────────────────

    private static object F(string name, string value, bool inline) =>
        new { name, value, inline };

    private static string MakeSafe(string s) =>
        string.Concat(s.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
}
