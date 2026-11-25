using System;
using System.Collections.Generic;
using System.Linq;
using RogueCards.Domain;
namespace RogueCards.Application
{
    public sealed class AchievementService
    {
        private const string IdFirstBlood = "first_blood";
        private const string IdWaveSurvivor = "wave_survivor";
        private const string IdFriendlyBetrayal = "friendly_betrayal";
        private const string IdTrapMaster = "trap_master";
        private const string IdComboStriker = "combo_striker";
        private const string IdUntouchable = "untouchable";
        private const string IdWellEquipped = "well_equipped";
        private const string IdVeteranSlayer = "veteran_slayer";
        private const string IdNoSecondChances = "no_second_chances";
        private const string IdKingslayer = "kingslayer";
        private readonly GameDatabase _database;
        private readonly PlayerProfile _profile;
        private readonly IRunPersistence _persistence;
        private readonly Queue<AchievementToast> _toastQueue = new Queue<AchievementToast>();

        public GameStatistics Stats { get; }

        public AchievementToast? ActiveToast { get; private set; }

        public AchievementService(GameDatabase database, PlayerProfile profile, IRunPersistence persistence)
        {
            _database = database;
            _profile = profile;
            _persistence = persistence;
            Stats = _persistence.LoadStatistics();

            SyncProgressFromExistingData();
        }

        public void UpdateToasts(float deltaSeconds)
        {
            if (ActiveToast == null)
            {
                PromoteNextToast();
                return;
            }

            ActiveToast.RemainingSeconds -= deltaSeconds;
            if (ActiveToast.RemainingSeconds <= 0f)
            {
                ActiveToast = null;
                PromoteNextToast();
            }
        }

        public int GetProgress(string achievementId)
        {
            if (_profile.CompletedAchievementIds.Contains(achievementId)
                && _database.Achievements.TryGetValue(achievementId, out AchievementDefinition? def))
            {
                return def.TargetValue;
            }

            return _profile.AchievementProgress.TryGetValue(achievementId, out int value) ? value : 0;
        }

        public bool IsCompleted(string achievementId)
        {
            return _profile.CompletedAchievementIds.Contains(achievementId);
        }

        public void RecordRunStarted()
        {
            Stats.TotalRunsPlayed += 1;
            if (Stats.HighestWaveReached < 1)
            {
                Stats.HighestWaveReached = 1;
            }

            _persistence.SaveStatistics(Stats);
        }

        public void RecordTurnCompleted()
        {
            Stats.TotalTurnsTaken += 1;
            _persistence.SaveStatistics(Stats);
        }

        public void RecordDamageDealt(int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            Stats.TotalDamageDealt += damage;
            _persistence.SaveStatistics(Stats);
        }

        public void RecordWeaponUsed(string weaponId)
        {
            Stats.TotalCardsPlayed += 1;

            if (Stats.WeaponUseCounts.TryGetValue(weaponId, out int current))
            {
                Stats.WeaponUseCounts[weaponId] = current + 1;
            }
            else
            {
                Stats.WeaponUseCounts[weaponId] = 1;
            }

            _persistence.SaveStatistics(Stats);
        }

        public void RecordEnemyDefeated(EnemyType type, int coins, bool friendlyFire)
        {
            Stats.TotalEnemiesDefeated += 1;
            Stats.TotalCoinsEarned += Math.Max(0, coins);

            if (Stats.EnemiesDefeatedByType.TryGetValue(type, out int current))
            {
                Stats.EnemiesDefeatedByType[type] = current + 1;
            }
            else
            {
                Stats.EnemiesDefeatedByType[type] = 1;
            }

            if (friendlyFire)
            {
                Stats.TotalFriendlyFireKills += 1;
                UpdateProgress(IdFriendlyBetrayal, Stats.TotalFriendlyFireKills);
            }

            UpdateProgress(IdFirstBlood, Stats.TotalEnemiesDefeated);
            UpdateProgress(IdVeteranSlayer, Stats.TotalEnemiesDefeated);

            if (type == EnemyType.VikingChief)
            {
                UpdateProgress(IdKingslayer, 1);
            }

            _persistence.SaveStatistics(Stats);
            _persistence.SaveProfile(_profile);
        }

        public void RecordWaveReached(int wave)
        {
            if (wave > Stats.HighestWaveReached)
            {
                Stats.HighestWaveReached = wave;
            }

            UpdateProgress(IdWaveSurvivor, Stats.HighestWaveReached);
            _persistence.SaveStatistics(Stats);
            _persistence.SaveProfile(_profile);
        }

        public void RecordWaveCleared(int wave, bool tookDamage)
        {
            if (wave > Stats.HighestWaveReached)
            {
                Stats.HighestWaveReached = wave;
            }

            UpdateProgress(IdWaveSurvivor, Stats.HighestWaveReached);

            if (!tookDamage)
            {
                UpdateProgress(IdUntouchable, 1);
            }

            _persistence.SaveStatistics(Stats);
            _persistence.SaveProfile(_profile);
        }

        public void RecordCardsPlayedInTurn(int count)
        {
            if (count <= 0)
            {
                return;
            }

            int currentBest = GetProgress(IdComboStriker);
            if (count > currentBest)
            {
                UpdateProgress(IdComboStriker, count);
                _persistence.SaveProfile(_profile);
            }
        }

        public void RecordBearTrapStun()
        {
            Stats.BearTrapStunsTriggered += 1;
            UpdateProgress(IdTrapMaster, Stats.BearTrapStunsTriggered);
            _persistence.SaveStatistics(Stats);
            _persistence.SaveProfile(_profile);
        }

        public void RecordRunWon(bool usedReroll)
        {
            Stats.RunsWon += 1;
            if (Stats.HighestWaveReached < _database.Waves.Count)
            {
                Stats.HighestWaveReached = _database.Waves.Count;
            }

            UpdateProgress(IdKingslayer, 1);

            if (!usedReroll)
            {
                UpdateProgress(IdNoSecondChances, 1);
            }

            _persistence.SaveStatistics(Stats);
            _persistence.SaveProfile(_profile);
        }

        public void EvaluateMenuUnlocks()
        {
            int purchasedWeapons = _profile.UnlockedWeaponIds.Count(
                id => _database.Weapons.TryGetValue(id, out WeaponDefinition? weapon) && !weapon.IsDefaultUnlocked);
            int purchasedUpgrades = _profile.UnlockedUpgradeIds.Count;
            int totalMenuUnlocks = purchasedWeapons + purchasedUpgrades;

            UpdateProgress(IdWellEquipped, totalMenuUnlocks);
            _persistence.SaveProfile(_profile);
        }

        private void SyncProgressFromExistingData()
        {
            SetProgressSilent(IdFirstBlood, Stats.TotalEnemiesDefeated);
            SetProgressSilent(IdVeteranSlayer, Stats.TotalEnemiesDefeated);
            SetProgressSilent(IdFriendlyBetrayal, Stats.TotalFriendlyFireKills);
            SetProgressSilent(IdTrapMaster, Stats.BearTrapStunsTriggered);
            SetProgressSilent(IdWaveSurvivor, Stats.HighestWaveReached);

            int purchasedWeapons = _profile.UnlockedWeaponIds.Count(
                id => _database.Weapons.TryGetValue(id, out WeaponDefinition? weapon) && !weapon.IsDefaultUnlocked);
            SetProgressSilent(IdWellEquipped, purchasedWeapons + _profile.UnlockedUpgradeIds.Count);
        }

        private void SetProgressSilent(string achievementId, int value)
        {
            if (!_database.Achievements.TryGetValue(achievementId, out AchievementDefinition? def))
            {
                return;
            }

            int clamped = Math.Clamp(value, 0, def.TargetValue);
            int existing = _profile.AchievementProgress.TryGetValue(achievementId, out int cur) ? cur : 0;
            if (clamped > existing)
            {
                _profile.AchievementProgress[achievementId] = clamped;
            }
        }

        private void UpdateProgress(string achievementId, int currentValue)
        {
            if (!_database.Achievements.TryGetValue(achievementId, out AchievementDefinition? def))
            {
                return;
            }

            int clamped = Math.Clamp(currentValue, 0, def.TargetValue);
            int previous = _profile.AchievementProgress.TryGetValue(achievementId, out int existing) ? existing : 0;
            if (clamped > previous)
            {
                _profile.AchievementProgress[achievementId] = clamped;
            }

            if (_profile.CompletedAchievementIds.Contains(achievementId))
            {
                return;
            }

            if (clamped >= def.TargetValue)
            {
                CompleteAchievement(def);
            }
        }

        private void CompleteAchievement(AchievementDefinition def)
        {
            _profile.CompletedAchievementIds.Add(def.Id);
            _profile.AchievementProgress[def.Id] = def.TargetValue;

            if (def.CoinReward > 0)
            {
                _profile.Coins += def.CoinReward;
                Stats.TotalCoinsEarned += def.CoinReward;
            }

            string toastDetail = def.Description;
            if (!string.IsNullOrWhiteSpace(def.UnlockCharacterId)
                && _database.Characters.TryGetValue(def.UnlockCharacterId, out CharacterDefinition? unlockedChar))
            {
                _profile.UnlockedCharacterIds.Add(unlockedChar.Id);
                toastDetail = $"Unlocked {unlockedChar.Name}!";
            }

            EnqueueToast(new AchievementToast(def.Name, toastDetail, def.CoinReward));
        }

        private void EnqueueToast(AchievementToast toast)
        {
            if (ActiveToast == null)
            {
                ActiveToast = toast;
                return;
            }

            _toastQueue.Enqueue(toast);
        }

        private void PromoteNextToast()
        {
            if (_toastQueue.Count > 0)
            {
                ActiveToast = _toastQueue.Dequeue();
            }
        }
    }
}