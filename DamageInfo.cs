using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;


namespace BaboPlugin
{

    public partial class BaboPlugin
    {

        private void InitPlayerDamageInfo()
        {
            foreach (var key in playerData.Keys) {
                if (!playerData[key].IsValid) continue;
                int attackerId = key;
                foreach (var key2 in playerData.Keys) {
                    if (key == key2) continue;
                    if (!playerData[key2].IsValid) continue;
                    if (playerData[key].TeamNum == playerData[key2].TeamNum) continue;
                    if (playerData[key].TeamNum == 2) {
                        if (playerData[key2].TeamNum != 3) continue;
                        int targetId = key2;
                        if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
                            playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

                        if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
                            attackerInfo[targetId] = targetInfo = new DamagePlayerInfo();
                    } else if (playerData[key].TeamNum == 3) {
                        if (playerData[key2].TeamNum != 2) continue;
                        int targetId = key2;
                        if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
                            playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

                        if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
                            attackerInfo[targetId] = targetInfo = new DamagePlayerInfo(); 
                    }
                }
            }
        }

		public Dictionary<int, Dictionary<int, DamagePlayerInfo>> playerDamageInfo = new Dictionary<int, Dictionary<int, DamagePlayerInfo>>();
		private void UpdatePlayerDamageInfo(EventPlayerHurt @event, int targetId)
		{
            CCSPlayerController? attacker = @event.Attacker;

            if (!IsPlayerValid(attacker)) return;
			int attackerId = (int)attacker!.UserId!;
			if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
				playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

			if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
				attackerInfo[targetId] = targetInfo = new DamagePlayerInfo();

			targetInfo.DamageHP += @event.DmgHealth;
			targetInfo.Hits++;

            var weapon = (@event.Weapon ?? string.Empty).ToLowerInvariant();
            if (IsHeGrenadeWeapon(weapon))
            {
                targetInfo.Grenades.HeDamage += @event.DmgHealth;
            }
            else if (IsMolotovWeapon(weapon))
            {
                targetInfo.Grenades.MolotovDamage += @event.DmgHealth;
            }
		}

        private void UpdatePlayerFlashInfo(EventPlayerBlind @event, int targetId)
        {
            CCSPlayerController? attacker = @event.Attacker;
            if (!IsPlayerValid(attacker)) return;

            int attackerId = (int)attacker!.UserId!;
            if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
                playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

            if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
                attackerInfo[targetId] = targetInfo = new DamagePlayerInfo();

            targetInfo.Grenades.Flashes++;
            targetInfo.Grenades.FlashDurationSeconds += Math.Max(0f, @event.BlindDuration);
        }

        private static bool IsHeGrenadeWeapon(string weapon)
        {
            return weapon is "hegrenade" or "hegrenade_projectile";
        }

        private static bool IsMolotovWeapon(string weapon)
        {
            return weapon is "inferno" or "molotov" or "incgrenade";
        }

        private void ShowDamageInfo()
        {
            if (!enableDamageReport) return;
            try
            {
                HashSet<(int, int)> processedPairs = new HashSet<(int, int)>();

                foreach (var entry in playerDamageInfo)
                {
                    int attackerId = entry.Key;
                    foreach (var (targetId, targetEntry) in entry.Value)
                    {
                        if (processedPairs.Contains((attackerId, targetId)) || processedPairs.Contains((targetId, attackerId)))
                            continue;

                        // Access and use the damage information as needed.
                        int damageGiven = targetEntry.DamageHP;
                        int hitsGiven = targetEntry.Hits;
                        int heGiven = targetEntry.Grenades.HeDamage;
                        int molotovGiven = targetEntry.Grenades.MolotovDamage;
                        int flashGiven = targetEntry.Grenades.Flashes;
                        float flashDurationGiven = targetEntry.Grenades.FlashDurationSeconds;
                        int damageTaken = 0;
                        int hitsTaken = 0;
                        int heTaken = 0;
                        int molotovTaken = 0;
                        int flashTaken = 0;
                        float flashDurationTaken = 0;

                        if (playerDamageInfo.TryGetValue(targetId, out var targetInfo) && targetInfo.TryGetValue(attackerId, out var takenInfo))
                        {
                            damageTaken = takenInfo.DamageHP;
                            hitsTaken = takenInfo.Hits;
                            heTaken = takenInfo.Grenades.HeDamage;
                            molotovTaken = takenInfo.Grenades.MolotovDamage;
                            flashTaken = takenInfo.Grenades.Flashes;
                            flashDurationTaken = takenInfo.Grenades.FlashDurationSeconds;
                        }

                        if (!playerData.ContainsKey(attackerId) || !playerData.ContainsKey(targetId)) continue;

                        var attackerController = playerData[attackerId];
                        var targetController = playerData[targetId];

                        if (attackerController != null && targetController != null)
                        {
                            if (!attackerController.IsValid || !targetController.IsValid) continue;
                            if (attackerController.Connected != PlayerConnectedState.PlayerConnected) continue;
                            if (targetController.Connected != PlayerConnectedState.PlayerConnected) continue;
                            if (!attackerController.PlayerPawn.IsValid || !targetController.PlayerPawn.IsValid) continue;
                            if (attackerController.PlayerPawn.Value == null || targetController.PlayerPawn.Value == null) continue;

                            int attackerHP = attackerController.PlayerPawn.Value.Health < 0 ? 0 : attackerController.PlayerPawn.Value.Health;
                            string attackerName = attackerController.PlayerName;

                            int targetHP = targetController.PlayerPawn.Value.Health < 0 ? 0 : targetController.PlayerPawn.Value.Health;
                            string targetName = targetController.PlayerName;

                            ReplyToUserCommand(attackerController, $"{ChatColors.Green}To: [{damageGiven} / {hitsGiven} hits | HE {heGiven} | Molly {molotovGiven} | Flash {flashGiven} ({flashDurationGiven:0.0}s)] From: [{damageTaken} / {hitsTaken} hits | HE {heTaken} | Molly {molotovTaken} | Flash {flashTaken} ({flashDurationTaken:0.0}s)] - {targetName} - ({targetHP} hp){ChatColors.Default}");
                            ReplyToUserCommand(targetController, $"{ChatColors.Green}To: [{damageTaken} / {hitsTaken} hits | HE {heTaken} | Molly {molotovTaken} | Flash {flashTaken} ({flashDurationTaken:0.0}s)] From: [{damageGiven} / {hitsGiven} hits | HE {heGiven} | Molly {molotovGiven} | Flash {flashGiven} ({flashDurationGiven:0.0}s)] - {attackerName} - ({attackerHP} hp){ChatColors.Default}");
                        }

                        // Mark this pair as processed to avoid duplicates.
                        processedPairs.Add((attackerId, targetId));
                    }
                }
                playerDamageInfo.Clear();
            }
            catch (Exception e)
            {
                Server.PrintToConsole($"[ShowDamageInfo FATAL] An error occurred: {e.Message}");
            }

        }
    }

	public class DamagePlayerInfo
	{
		public int DamageHP { get; set; } = 0;
		public int Hits { get; set; } = 0;
        public GrenadeInfo Grenades { get; set; } = new GrenadeInfo();
	}
}