using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RogueCards.Domain;
namespace RogueCards.Persistence
{
    public sealed class TextGameDataRepository : IGameDataRepository
    {
        private readonly string _dataDir;
        private readonly string _savesDir;
        private readonly string _logsDir;
        private readonly DefaultDataSeeder _seeder;
        private readonly List<string> _parseErrors = new List<string>();
        public IReadOnlyList<string> ParseErrors
        {
            get
            {
                return _parseErrors;
            }
        }

        public TextGameDataRepository(string dataDir, string savesDir, string logsDir, DefaultDataSeeder seeder)
        {
            _dataDir = dataDir;
            _savesDir = savesDir;
            _logsDir = logsDir;
            _seeder = seeder;
        }

        public GameDatabase LoadAll()
        {
            _parseErrors.Clear();
            _seeder.EnsureWorkspaceDirectories(_dataDir, _savesDir, _logsDir);
            _seeder.EnsureDefaultFilesExist(_dataDir);

            List<WeaponDefinition> weapons = LoadWeapons();
            List<EnemyDefinition> enemies = LoadEnemies();
            List<WaveDefinition> waves = LoadWaves();
            List<PermanentUpgradeDefinition> upgrades = LoadUpgrades();
            List<CharacterDefinition> characters = LoadCharacters();
            List<AchievementDefinition> achievements = LoadAchievements();

            FlushParseErrorsToLog();

            return new GameDatabase(weapons, enemies, waves, upgrades, characters, achievements);
        }

        private List<WeaponDefinition> LoadWeapons()
        {
            const string fileName = "weapons.txt";
            List<WeaponDefinition> items = new List<WeaponDefinition>();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((int lineNo, string[] cols) in ReadRecords(fileName, 12))
            {
                WeaponDefinition? parsed = ParseWeapon(fileName, lineNo, cols, seenIds);
                if (parsed != null)
                {
                    items.Add(parsed);
                }
            }

            return items;
        }

        private WeaponDefinition? ParseWeapon(string fileName, int lineNo, string[] cols, HashSet<string> seenIds)
        {
            string id = cols[0].Trim();
            string name = cols[1].Trim();
            string description = cols[11].Trim();

            if (!ValidateUniqueId(fileName, lineNo, id, seenIds) || !ValidateNonEmpty(fileName, lineNo, name, "Name"))
            {
                return null;
            }

            bool valid = TryParseEnum(fileName, lineNo, cols[2], "Category", out WeaponCategory category)
                & TryParseInt(fileName, lineNo, cols[3], 0, 99, "BaseDamage", out int baseDamage)
                & TryParseInt(fileName, lineNo, cols[4], 1, 20, "BaseCooldown", out int baseCooldown)
                & TryParseInt(fileName, lineNo, cols[5], 0, 10, "Range", out int range)
                & TryParseEnum(fileName, lineNo, cols[6], "Targeting", out TargetingType targeting)
                & TryParseEnum(fileName, lineNo, cols[7], "EffectType", out WeaponEffectType effectType)
                & TryParseInt(fileName, lineNo, cols[8], 0, 9999, "UnlockCost", out int unlockCost)
                & TryParseInt(fileName, lineNo, cols[9], 0, 99, "UnlockOrder", out int unlockOrder)
                & TryParseBool(fileName, lineNo, cols[10], "IsDefaultUnlocked", out bool isDefault);

            if (!valid)
            {
                return null;
            }

            seenIds.Add(id);
            return new WeaponDefinition(
                id,
                name,
                category,
                baseDamage,
                baseCooldown,
                range,
                targeting,
                effectType,
                unlockCost,
                unlockOrder,
                isDefault,
                description);
        }

        private List<EnemyDefinition> LoadEnemies()
        {
            const string fileName = "enemies.txt";
            List<EnemyDefinition> items = new List<EnemyDefinition>();
            HashSet<EnemyType> seenTypes = new HashSet<EnemyType>();

            foreach ((int lineNo, string[] cols) in ReadRecords(fileName, 9))
            {
                EnemyDefinition? parsed = ParseEnemy(fileName, lineNo, cols, seenTypes);
                if (parsed != null)
                {
                    items.Add(parsed);
                }
            }

            return items;
        }

        private EnemyDefinition? ParseEnemy(string fileName, int lineNo, string[] cols, HashSet<EnemyType> seenTypes)
        {
            if (!TryParseEnum(fileName, lineNo, cols[0], "EnemyType", out EnemyType type))
            {
                return null;
            }

            if (seenTypes.Contains(type))
            {
                RecordError(fileName, lineNo, $"Duplicate enemy type '{type}'.");
                return null;
            }

            string name = cols[1].Trim();
            string weaponLabel = cols[7].Trim();
            string description = cols[8].Trim();

            if (!ValidateNonEmpty(fileName, lineNo, name, "Name"))
            {
                return null;
            }

            bool valid = TryParseInt(fileName, lineNo, cols[2], 1, 999, "MaxHp", out int maxHp)
                & TryParseInt(fileName, lineNo, cols[3], 0, 99, "Damage", out int damage)
                & TryParseInt(fileName, lineNo, cols[4], 1, 10, "Range", out int range)
                & TryParseInt(fileName, lineNo, cols[5], 0, 9999, "CoinReward", out int coinReward)
                & TryParseBool(fileName, lineNo, cols[6], "SpawnOnEdge", out bool spawnOnEdge);

            if (!valid)
            {
                return null;
            }

            seenTypes.Add(type);
            return new EnemyDefinition(type, name, maxHp, damage, range, coinReward, spawnOnEdge, weaponLabel, description);
        }

        private List<WaveDefinition> LoadWaves()
        {
            const string fileName = "waves.txt";
            List<WaveDefinition> items = new List<WaveDefinition>();
            HashSet<int> seenWaves = new HashSet<int>();

            foreach ((int lineNo, string[] cols) in ReadRecords(fileName, 3))
            {
                WaveDefinition? parsed = ParseWave(fileName, lineNo, cols, seenWaves);
                if (parsed != null)
                {
                    items.Add(parsed);
                }
            }

            return items;
        }

        private WaveDefinition? ParseWave(string fileName, int lineNo, string[] cols, HashSet<int> seenWaves)
        {
            bool valid = TryParseInt(fileName, lineNo, cols[0], 1, 100, "WaveNumber", out int waveNumber)
                & TryParseEnum(fileName, lineNo, cols[2], "RewardType", out RewardType rewardType);

            if (!valid)
            {
                return null;
            }

            if (seenWaves.Contains(waveNumber))
            {
                RecordError(fileName, lineNo, $"Duplicate wave number '{waveNumber}'.");
                return null;
            }

            List<WaveEnemyEntry>? entries = ParseWaveEnemies(fileName, lineNo, cols[1]);
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            seenWaves.Add(waveNumber);
            return new WaveDefinition(waveNumber, entries, rewardType);
        }

        private List<WaveEnemyEntry>? ParseWaveEnemies(string fileName, int lineNo, string rawEntries)
        {
            List<WaveEnemyEntry> entries = new List<WaveEnemyEntry>();
            string[] tokens = rawEntries.Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
            {
                RecordError(fileName, lineNo, "Wave must define at least one enemy entry.");
                return null;
            }

            foreach (string rawToken in tokens)
            {
                string[] parts = rawToken.Trim().Split(':');
                if (parts.Length != 2)
                {
                    RecordError(fileName, lineNo, $"Invalid enemy spawn token '{rawToken.Trim()}'.");
                    return null;
                }

                bool ok = TryParseEnum(fileName, lineNo, parts[0], "WaveEnemyType", out EnemyType enemyType)
                    & TryParseInt(fileName, lineNo, parts[1], 1, 20, "EnemyCount", out int count);

                if (!ok)
                {
                    return null;
                }

                entries.Add(new WaveEnemyEntry(enemyType, count));
            }

            return entries;
        }

        private List<PermanentUpgradeDefinition> LoadUpgrades()
        {
            const string fileName = "upgrades.txt";
            List<PermanentUpgradeDefinition> items = new List<PermanentUpgradeDefinition>();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((int lineNo, string[] cols) in ReadRecords(fileName, 6))
            {
                PermanentUpgradeDefinition? parsed = ParseUpgrade(fileName, lineNo, cols, seenIds);
                if (parsed != null)
                {
                    items.Add(parsed);
                }
            }

            return items;
        }

        private PermanentUpgradeDefinition? ParseUpgrade(
            string fileName,
            int lineNo,
            string[] cols,
            HashSet<string> seenIds)
        {
            string id = cols[0].Trim();
            string name = cols[1].Trim();
            string description = cols[5].Trim();

            if (!ValidateUniqueId(fileName, lineNo, id, seenIds) || !ValidateNonEmpty(fileName, lineNo, name, "Name"))
            {
                return null;
            }

            bool valid = TryParseEnum(fileName, lineNo, cols[2], "UpgradeType", out PermanentUpgradeType type)
                & TryParseInt(fileName, lineNo, cols[3], 0, 9999, "Cost", out int cost)
                & TryParseInt(fileName, lineNo, cols[4], 1, 99, "UnlockOrder", out int unlockOrder);

            if (!valid)
            {
                return null;
            }

            seenIds.Add(id);
            return new PermanentUpgradeDefinition(id, name, type, cost, unlockOrder, description);
        }

        private List<CharacterDefinition> LoadCharacters()
        {
            const string fileName = "characters.txt";
            List<CharacterDefinition> items = new List<CharacterDefinition>();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((int lineNo, string[] cols) in ReadRecords(fileName, 9))
            {
                CharacterDefinition? parsed = ParseCharacter(fileName, lineNo, cols, seenIds);
                if (parsed != null)
                {
                    items.Add(parsed);
                }
            }

            return items;
        }

        private CharacterDefinition? ParseCharacter(
            string fileName,
            int lineNo,
            string[] cols,
            HashSet<string> seenIds)
        {
            string id = cols[0].Trim();
            string name = cols[1].Trim();
            string specialName = cols[3].Trim();
            string unlockAchievementId = cols[7].Trim();
            string description = cols[8].Trim();

            if (!ValidateUniqueId(fileName, lineNo, id, seenIds)
                || !ValidateNonEmpty(fileName, lineNo, name, "Name")
                || !ValidateNonEmpty(fileName, lineNo, specialName, "SpecialName"))
            {
                return null;
            }

            bool valid = TryParseInt(fileName, lineNo, cols[2], 1, 99, "BaseMaxHp", out int baseHp)
                & TryParseEnum(fileName, lineNo, cols[4], "SpecialType", out CharacterSpecialType specialType)
                & TryParseInt(fileName, lineNo, cols[5], 1, 20, "SpecialCooldown", out int specialCd)
                & TryParseBool(fileName, lineNo, cols[6], "IsDefaultUnlocked", out bool isDefault);

            if (!valid)
            {
                return null;
            }

            seenIds.Add(id);
            return new CharacterDefinition(
                id,
                name,
                baseHp,
                specialName,
                specialType,
                specialCd,
                isDefault,
                unlockAchievementId,
                description);
        }

        private List<AchievementDefinition> LoadAchievements()
        {
            const string fileName = "achievements.txt";
            List<AchievementDefinition> items = new List<AchievementDefinition>();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((int lineNo, string[] cols) in ReadRecords(fileName, 6))
            {
                AchievementDefinition? parsed = ParseAchievement(fileName, lineNo, cols, seenIds);
                if (parsed != null)
                {
                    items.Add(parsed);
                }
            }

            return items;
        }

        private AchievementDefinition? ParseAchievement(
            string fileName,
            int lineNo,
            string[] cols,
            HashSet<string> seenIds)
        {
            string id = cols[0].Trim();
            string name = cols[1].Trim();
            string description = cols[2].Trim();
            string unlockCharacterId = cols[5].Trim();

            if (!ValidateUniqueId(fileName, lineNo, id, seenIds) || !ValidateNonEmpty(fileName, lineNo, name, "Name"))
            {
                return null;
            }

            bool valid = TryParseInt(fileName, lineNo, cols[3], 1, 100000, "TargetValue", out int targetValue)
                & TryParseInt(fileName, lineNo, cols[4], 0, 9999, "CoinReward", out int coinReward);

            if (!valid)
            {
                return null;
            }

            seenIds.Add(id);
            return new AchievementDefinition(id, name, description, targetValue, coinReward, unlockCharacterId);
        }

        private IEnumerable<(int LineNumber, string[] Columns)> ReadRecords(string fileName, int expectedColumns)
        {
            string filePath = Path.Combine(_dataDir, fileName);
            if (!File.Exists(filePath))
            {
                RecordError(fileName, 0, "Data file is missing.");
                yield break;
            }

            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNumber = i + 1;
                string trimmed = lines[i].Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] columns = lines[i].Split('|');
                if (columns.Length != expectedColumns)
                {
                    RecordError(
                        fileName,
                        lineNumber,
                        $"Expected {expectedColumns} columns, found {columns.Length}.");
                    continue;
                }

                yield return (lineNumber, columns);
            }
        }

        private bool ValidateUniqueId(string fileName, int lineNo, string id, HashSet<string> seenIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                RecordError(fileName, lineNo, "ID cannot be empty.");
                return false;
            }

            if (seenIds.Contains(id))
            {
                RecordError(fileName, lineNo, $"Duplicate ID '{id}'.");
                return false;
            }

            return true;
        }

        private bool ValidateNonEmpty(string fileName, int lineNo, string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                RecordError(fileName, lineNo, $"Field '{fieldName}' cannot be empty.");
                return false;
            }

            return true;
        }

        private bool TryParseInt(
            string fileName,
            int lineNo,
            string raw,
            int min,
            int max,
            string fieldName,
            out int result)
        {
            string trimmed = raw.Trim();
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                RecordError(fileName, lineNo, $"Field '{fieldName}' has invalid integer '{trimmed}'.");
                return false;
            }

            if (result < min || result > max)
            {
                RecordError(fileName, lineNo, $"Field '{fieldName}' value {result} is outside [{min}..{max}].");
                return false;
            }

            return true;
        }

        private bool TryParseBool(string fileName, int lineNo, string raw, string fieldName, out bool result)
        {
            string trimmed = raw.Trim();
            if (!bool.TryParse(trimmed, out result))
            {
                RecordError(fileName, lineNo, $"Field '{fieldName}' has invalid boolean '{trimmed}'.");
                return false;
            }

            return true;
        }

        private bool TryParseEnum<TEnum>(string fileName, int lineNo, string raw, string fieldName, out TEnum result)
            where TEnum : struct, Enum
        {
            string trimmed = raw.Trim();
            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                result = default;
                RecordError(fileName, lineNo, $"Field '{fieldName}' cannot be numeric '{trimmed}'.");
                return false;
            }

            if (!Enum.TryParse(trimmed, true, out result) || !Enum.IsDefined(typeof(TEnum), result))
            {
                RecordError(fileName, lineNo, $"Field '{fieldName}' has invalid enum value '{trimmed}'.");
                return false;
            }

            return true;
        }

        private void RecordError(string fileName, int lineNumber, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            _parseErrors.Add($"[{timestamp}] File: {fileName}, Line: {lineNumber} - {message}");
        }

        private void FlushParseErrorsToLog()
        {
            if (_parseErrors.Count == 0)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_logsDir);
                string logPath = Path.Combine(_logsDir, "data_parser_errors.txt");
                File.AppendAllLines(logPath, _parseErrors, Encoding.UTF8);
            }
            catch (IOException)
            {
                // Ignore logging I/O error to keep initialization non-blocking.
            }
        }
    }
}