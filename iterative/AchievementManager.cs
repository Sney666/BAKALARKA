#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RogueCards
{
    public class AchievementManager
    {
        private readonly DataRepository dataRepository;
        private readonly GameDatabase database;
        private readonly PlayerProfile profile;
        private readonly PersistenceManager persistenceManager;
        private readonly Queue<AchievementToast> toastQueue = new Queue<AchievementToast>();

        public string StatsFilePath { get; }
        public GameStatistics Stats { get; private set; }
        public AchievementToast ActiveToast { get; private set; }

        public AchievementManager(
            DataRepository dataRepository,
            GameDatabase database,
            PlayerProfile profile,
            PersistenceManager persistenceManager)
        {
            this.dataRepository = dataRepository;
            this.database = database;
            this.profile = profile;
            this.persistenceManager = persistenceManager;
            StatsFilePath = Path.Combine(dataRepository.SavesDirectory, "stats.txt");

            LoadStatistics();
            EnsureEnemyTypeKeys();
            SyncAchievementProgressFromStats();
        }

        public void UpdateToasts(float deltaSeconds)
        {
            if (ActiveToast != null)
            {
                ActiveToast.RemainingSeconds -= deltaSeconds;
                if (ActiveToast.RemainingSeconds <= 0f)
                {
                    ActiveToast = null;
                }
            }

            if (ActiveToast == null && toastQueue.Count > 0)
            {
                ActiveToast = toastQueue.Dequeue();
            }
        }

        public string GetMostUsedWeaponDisplay()
        {
            if (Stats.WeaponUseCounts == null || Stats.WeaponUseCounts.Count == 0)
            {
                return "None";
            }

            KeyValuePair<string, int> top = Stats.WeaponUseCounts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .First();

            return $"{top.Key} ({top.Value})";
        }

        public void RecordRunStarted()
        {
            Stats.TotalRunsPlayed++;
            if (Stats.HighestWaveReached < 1)
            {
                Stats.HighestWaveReached = 1;
            }

            SaveStatistics();
        }

        public void RecordTurnTaken()
        {
            Stats.TotalTurnsTaken++;
            SaveStatistics();
        }

        public void RecordDamageDealt(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Stats.TotalDamageDealt += amount;
            SaveStatistics();
        }

        public void RecordCardPlayed(string weaponName)
        {
            Stats.TotalCardsPlayed++;
            if (!string.IsNullOrWhiteSpace(weaponName))
            {
                if (!Stats.WeaponUseCounts.ContainsKey(weaponName))
                {
                    Stats.WeaponUseCounts[weaponName] = 0;
                }

                Stats.WeaponUseCounts[weaponName]++;
            }

            SaveStatistics();
        }

        public void RecordCardsPlayedInSingleTurn(int count)
        {
            if (count <= 0)
            {
                return;
            }

            UpdateMaxProgress("combo_striker", count);
        }

        public void RecordBearTrapStun()
        {
            Stats.BearTrapStunsTriggered++;
            SaveStatistics();
            UpdateMaxProgress("trap_master", Stats.BearTrapStunsTriggered);
        }

        public void RecordEnemyDefeated(EnemyType enemyType, int coinReward, bool isFriendlyFire)
        {
            Stats.TotalEnemiesDefeated++;

            if (!Stats.EnemiesDefeatedByType.ContainsKey(enemyType))
            {
                Stats.EnemiesDefeatedByType[enemyType] = 0;
            }

            Stats.EnemiesDefeatedByType[enemyType]++;

            if (coinReward > 0)
            {
                Stats.TotalCoinsEarned += coinReward;
            }

            if (isFriendlyFire)
            {
                Stats.TotalFriendlyFireKills++;
                UpdateMaxProgress("friendly_betrayal", Stats.TotalFriendlyFireKills);
            }

            SaveStatistics();

            UpdateMaxProgress("first_blood", Stats.TotalEnemiesDefeated);
            UpdateMaxProgress("veteran_slayer", Stats.TotalEnemiesDefeated);

            if (enemyType == EnemyType.VikingChief)
            {
                UpdateMaxProgress("kingslayer", 1);
            }
        }

        public void RecordWaveCleared(int clearedWave, bool tookDamageInWave)
        {
            if (clearedWave > Stats.HighestWaveReached)
            {
                Stats.HighestWaveReached = clearedWave;
                SaveStatistics();
            }

            if (!tookDamageInWave)
            {
                UpdateMaxProgress("untouchable", 1);
            }
        }

        public void RecordWaveReached(int waveNumber)
        {
            if (waveNumber > Stats.HighestWaveReached)
            {
                Stats.HighestWaveReached = waveNumber;
                SaveStatistics();
            }

            UpdateMaxProgress("wave_survivor", waveNumber);
        }

        public void RecordRunWon(bool usedRerollInRun)
        {
            Stats.RunsWon++;
            if (Stats.HighestWaveReached < 10)
            {
                Stats.HighestWaveReached = 10;
            }

            SaveStatistics();

            if (!usedRerollInRun)
            {
                UpdateMaxProgress("no_second_chances", 1);
            }
        }

        public void EvaluateWellEquipped()
        {
            int purchasedWeapons = database.WeaponList.Count(
                w => !w.UnlockedByDefault && profile.UnlockedWeaponIds.Contains(w.Id));

            int purchasedUpgrades = profile.UnlockedUpgradeIds.Count;
            int totalMenuUnlocks = purchasedWeapons + purchasedUpgrades;

            UpdateMaxProgress("well_equipped", totalMenuUnlocks);
        }

        public int GetAchievementProgress(string achievementId)
        {
            if (profile.AchievementProgress.TryGetValue(achievementId, out int val))
            {
                return val;
            }

            return 0;
        }

        public bool IsAchievementUnlocked(string achievementId)
        {
            return profile.CompletedAchievementIds.Contains(achievementId);
        }

        private void SyncAchievementProgressFromStats()
        {
            UpdateMaxProgress("first_blood", Stats.TotalEnemiesDefeated);
            UpdateMaxProgress("wave_survivor", Stats.HighestWaveReached);
            UpdateMaxProgress("friendly_betrayal", Stats.TotalFriendlyFireKills);
            UpdateMaxProgress("trap_master", Stats.BearTrapStunsTriggered);
            UpdateMaxProgress("veteran_slayer", Stats.TotalEnemiesDefeated);
            EvaluateWellEquipped();
        }

        private void UpdateMaxProgress(string achievementId, int value)
        {
            if (!database.Achievements.TryGetValue(achievementId, out AchievementDefinition def))
            {
                return;
            }

            int current = GetAchievementProgress(achievementId);
            int clampedNew = Math.Min(def.TargetValue, Math.Max(current, value));

            bool changed = (clampedNew != current);
            profile.AchievementProgress[achievementId] = clampedNew;

            if (!profile.CompletedAchievementIds.Contains(achievementId) && clampedNew >= def.TargetValue)
            {
                UnlockAchievement(def);
                return;
            }

            if (changed)
            {
                persistenceManager.SaveProfile(profile);
            }
        }

        private void UnlockAchievement(AchievementDefinition def)
        {
            profile.CompletedAchievementIds.Add(def.Id);
            profile.AchievementProgress[def.Id] = def.TargetValue;

            if (def.CoinReward > 0)
            {
                profile.Coins += def.CoinReward;
                Stats.TotalCoinsEarned += def.CoinReward;
            }

            string unlockExtra = string.Empty;
            if (!string.IsNullOrEmpty(def.UnlockCharacterId) &&
                database.Characters.TryGetValue(def.UnlockCharacterId, out CharacterDefinition unlockedChar))
            {
                profile.UnlockedCharacterIds.Add(unlockedChar.Id);
                unlockExtra = $" | Unlocked Hero: {unlockedChar.Name}!";
            }

            persistenceManager.SaveProfile(profile);
            SaveStatistics();

            toastQueue.Enqueue(new AchievementToast
            {
                Title = $"ACHIEVEMENT UNLOCKED: {def.Title}",
                Subtitle = $"+{def.CoinReward} Coins{unlockExtra}",
                RemainingSeconds = 4.2f
            });
        }

        private void EnsureEnemyTypeKeys()
        {
            foreach (EnemyType enemyType in Enum.GetValues(typeof(EnemyType)))
            {
                if (!Stats.EnemiesDefeatedByType.ContainsKey(enemyType))
                {
                    Stats.EnemiesDefeatedByType[enemyType] = 0;
                }
            }
        }

        private void LoadStatistics()
        {
            Stats = new GameStatistics();
            EnsureEnemyTypeKeys();

            if (!File.Exists(StatsFilePath))
            {
                SaveStatistics();
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(StatsFilePath);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    int sep = line.IndexOf('=');
                    if (sep <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, sep).Trim();
                    string value = line.Substring(sep + 1).Trim();

                    if (key == "TotalRunsPlayed" && TryParseNonNegative(value, out int runs))
                    {
                        Stats.TotalRunsPlayed = runs;
                    }
                    else if (key == "RunsWon" && TryParseNonNegative(value, out int won))
                    {
                        Stats.RunsWon = won;
                    }
                    else if (key == "HighestWaveReached" && TryParseNonNegative(value, out int highWave))
                    {
                        Stats.HighestWaveReached = highWave;
                    }
                    else if (key == "TotalTurnsTaken" && TryParseNonNegative(value, out int turns))
                    {
                        Stats.TotalTurnsTaken = turns;
                    }
                    else if (key == "TotalEnemiesDefeated" && TryParseNonNegative(value, out int kills))
                    {
                        Stats.TotalEnemiesDefeated = kills;
                    }
                    else if (key == "TotalFriendlyFireKills" && TryParseNonNegative(value, out int ff))
                    {
                        Stats.TotalFriendlyFireKills = ff;
                    }
                    else if (key == "TotalDamageDealt" && TryParseNonNegative(value, out int dmg))
                    {
                        Stats.TotalDamageDealt = dmg;
                    }
                    else if (key == "TotalCoinsEarned" && TryParseNonNegative(value, out int coins))
                    {
                        Stats.TotalCoinsEarned = coins;
                    }
                    else if (key == "TotalCardsPlayed" && TryParseNonNegative(value, out int cards))
                    {
                        Stats.TotalCardsPlayed = cards;
                    }
                    else if (key == "BearTrapStunsTriggered" && TryParseNonNegative(value, out int traps))
                    {
                        Stats.BearTrapStunsTriggered = traps;
                    }
                    else if (key == "EnemiesByType")
                    {
                        ParseEnemyCounts(value);
                    }
                    else if (key == "WeaponUseCounts")
                    {
                        ParseWeaponCounts(value);
                    }
                }
            }
            catch
            {
                Stats = new GameStatistics();
                EnsureEnemyTypeKeys();
                SaveStatistics();
            }
        }

        public void SaveStatistics()
        {
            try
            {
                Directory.CreateDirectory(dataRepository.SavesDirectory);

                string enemiesByType = string.Join(
                    ",",
                    Stats.EnemiesDefeatedByType.OrderBy(kv => (int)kv.Key).Select(kv => $"{kv.Key}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));

                string weaponCounts = string.Join(
                    ";",
                    Stats.WeaponUseCounts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key.Replace(";", " ").Replace(":", " ")}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));

                List<string> lines = new List<string>
                {
                    "# Rogue Cards - Persistent Lifetime Statistics",
                    $"TotalRunsPlayed={Stats.TotalRunsPlayed.ToString(CultureInfo.InvariantCulture)}",
                    $"RunsWon={Stats.RunsWon.ToString(CultureInfo.InvariantCulture)}",
                    $"HighestWaveReached={Stats.HighestWaveReached.ToString(CultureInfo.InvariantCulture)}",
                    $"TotalTurnsTaken={Stats.TotalTurnsTaken.ToString(CultureInfo.InvariantCulture)}",
                    $"TotalEnemiesDefeated={Stats.TotalEnemiesDefeated.ToString(CultureInfo.InvariantCulture)}",
                    $"TotalFriendlyFireKills={Stats.TotalFriendlyFireKills.ToString(CultureInfo.InvariantCulture)}",
                    $"TotalDamageDealt={Stats.TotalDamageDealt.ToString(CultureInfo.InvariantCulture)}",
                    $"TotalCoinsEarned={Stats.TotalCoinsEarned.ToString(CultureInfo.InvariantCulture)}",
                    $"TotalCardsPlayed={Stats.TotalCardsPlayed.ToString(CultureInfo.InvariantCulture)}",
                    $"BearTrapStunsTriggered={Stats.BearTrapStunsTriggered.ToString(CultureInfo.InvariantCulture)}",
                    $"EnemiesByType={enemiesByType}",
                    $"WeaponUseCounts={weaponCounts}"
                };

                File.WriteAllLines(StatsFilePath, lines);
            }
            catch
            {
            }
        }

        private void ParseEnemyCounts(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (string token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = token.Split(':');
                if (parts.Length == 2 &&
                    Enum.TryParse(parts[0].Trim(), true, out EnemyType type) &&
                    TryParseNonNegative(parts[1].Trim(), out int count))
                {
                    Stats.EnemiesDefeatedByType[type] = count;
                }
            }
        }

        private void ParseWeaponCounts(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (string token in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = token.Split(':');
                if (parts.Length == 2 &&
                    !string.IsNullOrWhiteSpace(parts[0]) &&
                    TryParseNonNegative(parts[1].Trim(), out int count))
                {
                    Stats.WeaponUseCounts[parts[0].Trim()] = count;
                }
            }
        }

        private static bool TryParseNonNegative(string text, out int value)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0)
            {
                return true;
            }

            value = 0;
            return false;
        }
    }
}