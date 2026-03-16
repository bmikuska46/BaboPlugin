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
            if (@event.DmgHealth <= 0) return;

            if (!playerData.TryGetValue(targetId, out var targetController) || !IsPlayerValid(targetController))
            {
                return;
            }

            string targetName = targetController.PlayerName;
            string rawWeapon = (@event.Weapon ?? "unknown").Trim();
            string weapon = rawWeapon.Length == 0 ? "unknown" : rawWeapon;

            ReplyToUserCommand(attacker, $"{ChatColors.Green}You dealt {@event.DmgHealth} damage to {targetName} with {weapon}.{ChatColors.Default}");
		}

        private void UpdatePlayerFlashInfo(EventPlayerBlind @event, int targetId)
        {
            CCSPlayerController? attacker = @event.Attacker;
            if (!IsPlayerValid(attacker)) return;
            float flashDuration = Math.Max(0f, @event.BlindDuration);
            if (flashDuration <= 0f) return;

            if (!playerData.TryGetValue(targetId, out var targetController) || !IsPlayerValid(targetController))
            {
                return;
            }

            string targetName = targetController.PlayerName;
            ReplyToUserCommand(attacker, $"{ChatColors.Green}You flashed {targetName} for {flashDuration:0.0}s.{ChatColors.Default}");
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
            playerDamageInfo.Clear();
        }
    }

	public class DamagePlayerInfo
	{
		public int DamageHP { get; set; } = 0;
		public int Hits { get; set; } = 0;
        public GrenadeInfo Grenades { get; set; } = new GrenadeInfo();
	}
}