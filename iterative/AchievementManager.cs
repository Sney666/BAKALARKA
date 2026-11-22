#nullable disable
using System.Diagnostics;
using System.Globalization;

namespace RogueCards
{
    public class AchievementManager
    {
        private readonly DataRepository _dataRepository;
        private readonly GameDatabase _database;
        private readonly PlayerProfile _profile;
        private readonly PersistenceManager _persistenceManager;
        private readonly Queue<AchievementToast> _toastQueue = new();
        private readonly string _statsFilePath;

        public GameStatistics Stats { get; private set; }
        public AchievementToast ActiveToast { get; private set; }

        public AchievementManager(
            DataRepository dataRepository,
            GameDatabase database,
            PlayerProfile profile,
            PersistenceManager persistenceManager)
        {
            _dataRepository = dataRepository;
            _database = database;
            _profile = profile;
            _persistenceManager = persistenceManager;
            _statsFilePath = Path.Combine(dataRepository.SavesDirectory, "stats.txt");

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

            if (ActiveToast == null && _toastQueue.Count > 0)
            {
                ActiveToast = _toastQueue.Dequeue();
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
                Stats.WeaponUseCounts[weaponName] = Stats.WeaponUseCounts.GetValueOrDefault(weaponName) + 1;
            }

            SaveStatistics();
        }

        public void RecordCardsPlayedInSingleTurn(int count)
        {
            if (count > 0)
            {
                UpdateMaxProgress("combo_striker", count);
            }
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
            Stats.EnemiesDefeatedByType[enemyType] = Stats.EnemiesDefeatedByType.GetValueOrDefault(enemyType) + 1;

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
            int purchasedWeapons = _database.WeaponList.Count(
                w => !w.UnlockedByDefault && _profile.UnlockedWeaponIds.Contains(w.Id));

            int totalMenuUnlocks = purchasedWeapons + _profile.UnlockedUpgradeIds.Count;
            UpdateMaxProgress("well_equipped", totalMenuUnlocks);
        }

        public int GetAchievementProgress(string achievementId) =>
            _profile.AchievementProgress.GetValueOrDefault(achievementId);

        public bool IsAchievementUnlocked(string achievementId) =>
            _profile.CompletedAchievementIds.Contains(achievementId);

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
            if (!_database.Achievements.TryGetValue(achievementId, out AchievementDefinition def))
            {
                return;
            }

            int current = GetAchievementProgress(achievementId);
            int clampedNew = Math.Min(def.TargetValue, Math.Max(current, value));
            bool changed = clampedNew != current;
            _profile.AchievementProgress[achievementId] = clampedNew;

            if (!_profile.CompletedAchievementIds.Contains(achievementId) && clampedNew >= def.TargetValue)
            {
                UnlockAchievement(def);
                return;
            }

            if (changed)
            {
                _persistenceManager.SaveProfile(_profile);
            }
        }

        private void UnlockAchievement(AchievementDefinition def)
        {
            _profile.CompletedAchievementIds.Add(def.Id);
            _profile.AchievementProgress[def.Id] = def.TargetValue;

            if (def.CoinReward > 0)
            {
                _profile.Coins += def.CoinReward;
                Stats.TotalCoinsEarned += def.CoinReward;
            }

            string unlockExtra = string.Empty;
            if (!string.IsNullOrEmpty(def.UnlockCharacterId) &&
                _database.Characters.TryGetValue(def.UnlockCharacterId, out CharacterDefinition unlockedChar))
            {
                _profile.UnlockedCharacterIds.Add(unlockedChar.Id);
                unlockExtra = $" | Unlocked Hero: {unlockedChar.Name}!";
            }

            _persistenceManager.SaveProfile(_profile);
            SaveStatistics();

            _toastQueue.Enqueue(new AchievementToast
            {
                Title = $"ACHIEVEMENT UNLOCKED: {def.Title}",
                Subtitle = $"+{def.CoinReward} Coins{unlockExtra}",
                RemainingSeconds = 4.2f
            });
        }

        private void EnsureEnemyTypeKeys()
        {
            foreach (EnemyType enemyType in Enum.GetValues<EnemyType>())
            {
                Stats.EnemiesDefeatedByType.TryAdd(enemyType, 0);
            }
        }

        private void LoadStatistics()
        {
            Stats = new GameStatistics();
            EnsureEnemyTypeKeys();

            if (!File.Exists(_statsFilePath))
            {
                SaveStatistics();
                return;
            }

            try
            {
                foreach (string raw in File.ReadAllLines(_statsFilePath))
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    int sep = line.IndexOf('=');
                    if (sep <= 0)
                    {
                        continue;
                    }

                    ApplyStatEntry(line.Substring(0, sep).Trim(), line.Substring(sep + 1).Trim());
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to load statistics: {ex.Message}");
                Stats = new GameStatistics();
                EnsureEnemyTypeKeys();
                SaveStatistics();
            }
        }

        private void ApplyStatEntry(string key, string value)
        {
            switch (key)
            {
                case "TotalRunsPlayed" when TryParseNonNegative(value, out int runs):
                    Stats.TotalRunsPlayed = runs;
                    break;
                case "RunsWon" when TryParseNonNegative(value, out int won):
                    Stats.RunsWon = won;
                    break;
                case "HighestWaveReached" when TryParseNonNegative(value, out int highWave):
                    Stats.HighestWaveReached = highWave;
                    break;
                case "TotalTurnsTaken" when TryParseNonNegative(value, out int turns):
                    Stats.TotalTurnsTaken = turns;
                    break;
                case "TotalEnemiesDefeated" when TryParseNonNegative(value, out int kills):
                    Stats.TotalEnemiesDefeated = kills;
                    break;
                case "TotalFriendlyFireKills" when TryParseNonNegative(value, out int ff):
                    Stats.TotalFriendlyFireKills = ff;
                    break;
                case "TotalDamageDealt" when TryParseNonNegative(value, out int dmg):
                    Stats.TotalDamageDealt = dmg;
                    break;
                case "TotalCoinsEarned" when TryParseNonNegative(value, out int coins):
                    Stats.TotalCoinsEarned = coins;
                    break;
                case "TotalCardsPlayed" when TryParseNonNegative(value, out int cards):
                    Stats.TotalCardsPlayed = cards;
                    break;
                case "BearTrapStunsTriggered" when TryParseNonNegative(value, out int traps):
                    Stats.BearTrapStunsTriggered = traps;
                    break;
                case "EnemiesByType":
                    ParseEnemyCounts(value);
                    break;
                case "WeaponUseCounts":
                    ParseWeaponCounts(value);
                    break;
            }
        }

        private void SaveStatistics()
        {
            try
            {
                Directory.CreateDirectory(_dataRepository.SavesDirectory);

                string enemiesByType = string.Join(
                    ",",
                    Stats.EnemiesDefeatedByType
                        .OrderBy(kv => (int)kv.Key)
                        .Select(kv => $"{kv.Key}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));

                string weaponCounts = string.Join(
                    ";",
                    Stats.WeaponUseCounts
                        .OrderBy(kv => kv.Key)
                        .Select(kv => $"{kv.Key.Replace(";", " ").Replace(":", " ")}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));

                List<string> lines =
                [
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
                ];

                File.WriteAllLines(_statsFilePath, lines);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to save statistics: {ex.Message}");
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