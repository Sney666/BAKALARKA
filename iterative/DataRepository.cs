#nullable disable
using System.Diagnostics;
using System.Globalization;

namespace RogueCards
{
    public class DataRepository
    {
        private readonly string _dataDirectory;
        private readonly string _weaponsFilePath;
        private readonly string _enemiesFilePath;
        private readonly string _wavesFilePath;
        private readonly string _upgradesFilePath;
        private readonly string _charactersFilePath;
        private readonly string _achievementsFilePath;
        private readonly string _parserErrorLogPath;

        public string SavesDirectory { get; }
        public string LogsDirectory { get; }
        public string ActiveRunFilePath { get; }
        public List<string> ParseErrors { get; } = [];

        public DataRepository()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _dataDirectory = Path.Combine(baseDirectory, "Data");
            SavesDirectory = Path.Combine(baseDirectory, "Saves");
            LogsDirectory = Path.Combine(baseDirectory, "Logs");

            _weaponsFilePath = Path.Combine(_dataDirectory, "weapons.txt");
            _enemiesFilePath = Path.Combine(_dataDirectory, "enemies.txt");
            _wavesFilePath = Path.Combine(_dataDirectory, "waves.txt");
            _upgradesFilePath = Path.Combine(_dataDirectory, "upgrades.txt");
            _charactersFilePath = Path.Combine(_dataDirectory, "characters.txt");
            _achievementsFilePath = Path.Combine(_dataDirectory, "achievements.txt");
            ActiveRunFilePath = Path.Combine(SavesDirectory, "active_run.txt");
            _parserErrorLogPath = Path.Combine(LogsDirectory, "data_parser_errors.txt");
        }

        public GameDatabase InitializeAndLoad()
        {
            ParseErrors.Clear();
            EnsureDirectories();
            EnsureDefaultDataFiles();

            GameDatabase database = new();
            LoadWeapons(database);
            LoadEnemies(database);
            LoadWaves(database);
            LoadUpgrades(database);
            LoadCharacters(database);
            LoadAchievements(database);

            FlushParseErrorsToDisk();
            return database;
        }

        public static PlayerProfile CreateDefaultProfile(GameDatabase database)
        {
            PlayerProfile profile = new()
            {
                Coins = 0,
                Area2Unlocked = false,
                SelectedCharacterId = "viking"
            };

            if (database == null)
            {
                return profile;
            }

            foreach (WeaponDefinition weapon in database.WeaponList.Where(w => w.UnlockedByDefault))
            {
                profile.UnlockedWeaponIds.Add(weapon.Id);
            }

            foreach (CharacterDefinition character in database.CharacterList.Where(c => c.UnlockedByDefault))
            {
                profile.UnlockedCharacterIds.Add(character.Id);
            }

            foreach (AchievementDefinition achievement in database.AchievementList)
            {
                profile.AchievementProgress[achievement.Id] = 0;
            }

            return profile;
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory(_dataDirectory);
            Directory.CreateDirectory(SavesDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }

        private void EnsureDefaultDataFiles()
        {
            EnsureFileExists(_weaponsFilePath, GetDefaultWeaponsLines);
            EnsureFileExists(_enemiesFilePath, GetDefaultEnemiesLines);
            EnsureFileExists(_wavesFilePath, GetDefaultWavesLines);
            EnsureFileExists(_upgradesFilePath, GetDefaultUpgradesLines);
            EnsureFileExists(_charactersFilePath, GetDefaultCharactersLines);
            EnsureFileExists(_achievementsFilePath, GetDefaultAchievementsLines);
        }

        private static void EnsureFileExists(string path, Func<string[]> defaultLinesProvider)
        {
            if (!File.Exists(path))
            {
                File.WriteAllLines(path, defaultLinesProvider());
            }
        }

        private static string[] GetDefaultWeaponsLines() =>
        [
            "# Rogue Cards - Weapons and Cards Definition File",
            "# Format: Id|Name|Category|BaseDamage|BaseCooldown|Range|Targeting|Effect|UnlockCost|UnlockOrder|UnlockedByDefault|Description",
            "viking_axe|Viking Axe|Melee|3|4|1|Orthogonal|SingleAdjacent|0|0|true|Hits one enemy on an adjacent orthogonal tile (range 1).",
            "wooden_club|Wooden Club|Melee|2|5|1|Orthogonal|KnockbackAdjacent|15|1|false|Hits one enemy on an adjacent orthogonal tile and knocks them back 1 tile.",
            "two_handed_hammer|Two-handed Hammer|Melee|4|6|1|Orthogonal|ArcSweep|30|5|false|Hits 3 tiles at once in a sweeping arc in the chosen orthogonal direction.",
            "throwing_axe|Throwing Axe|Ranged|2|3|5|Orthogonal|LineProjectile|0|0|true|Fires a projectile in a straight orthogonal line. Hits the first enemy in its path.",
            "hunting_bow|Hunting Bow|Ranged|3|5|5|Diagonal|DiagonalProjectile|20|2|false|Fires a projectile diagonally. Hits the first enemy in its path.",
            "heavy_spear|Heavy Spear|Ranged|4|6|5|Orthogonal|PierceTwoLine|35|6|false|Fires in a straight orthogonal line. Pierces the first enemy and stops at the second.",
            "bear_trap|Bear Trap|Special|2|6|0|Self|PlaceBearTrap|0|0|true|Places a trap on your current tile. First enemy stepping on it takes damage and is stunned.",
            "boat_hook|Boat Hook|Special|1|5|3|Orthogonal|HarpoonPull|25|3|false|Fires a hook up to 3 tiles orthogonally. Deals damage and pulls the enemy adjacent to you.",
            "diagonal_dash|Diagonal Dash|Movement|0|4|1|Diagonal|DiagonalDash|0|0|true|Dashes 1 tile diagonally if the target tile is free.",
            "slide|Slide|Movement|0|5|2|Orthogonal|SlideTwo|25|4|false|Moves 2 tiles in a straight orthogonal line. Stops before any blocking enemy."
        ];

        private static string[] GetDefaultEnemiesLines() =>
        [
            "# Rogue Cards - Enemies Definition File",
            "# Format: Id|EnemyType|Name|MaxHp|AttackDamage|AttackRange|CoinDrop|SpawnNearEdge|IsBoss|WeaponName|Description",
            "pawn|Pawn|Pawn|3|2|1|2|false|false|Axe|Foot soldier. Moves orthogonally toward the player and strikes at range 1.",
            "archer|Archer|Archer|2|2|5|3|true|false|Bow|Hooded ranger. Keeps distance greater than 2 and fires along row or column.",
            "spearman|Spearman|Spearman|4|2|2|3|false|false|Spear|Armored guard. Moves toward the player and strikes up to 2 tiles orthogonally.",
            "shaman|Shaman|Shaman|3|1|3|4|true|false|Potion|Tribal mystic. Throws a poison potion at distance 3 leaving toxic hazards.",
            "viking_chief|VikingChief|Viking Chief|30|3|5|30|false|true|Dual Arms|Wave 10 Boss. Cycles Throwing Axe, Hook + Club with summon, and Evolved Viking Axe."
        ];

        private static string[] GetDefaultWavesLines() =>
        [
            "# Rogue Cards - Wave Progression File (Level 1)",
            "# Format: WaveNumber|EnemyType:Count,EnemyType:Count|RewardAfterClear",
            "1|Pawn:1|None",
            "2|Pawn:1,Archer:1|PickCard",
            "3|Spearman:1,Pawn:1|None",
            "4|Spearman:1,Archer:1|Upgrade",
            "5|Pawn:2,Shaman:1|None",
            "6|Spearman:1,Archer:2|PickCard",
            "7|Pawn:3,Archer:2|None",
            "8|Pawn:2,Spearman:1,Shaman:1|Upgrade",
            "9|Pawn:1,Shaman:1,Spearman:1,Archer:2|None",
            "10|VikingChief:1|None"
        ];

        private static string[] GetDefaultUpgradesLines() =>
        [
            "# Rogue Cards - Permanent Upgrades Queue File",
            "# Format: Id|Name|Cost|UnlockOrder|EffectType|EffectValue|Description",
            "durability_1|Durability Training I|20|1|MaxHp|1|Increases max character health by +1 HP.",
            "swift_boots|Swift Boots|30|2|MovementCooldown|1|Reduces the cooldown of all Movement abilities by 1 turn.",
            "charisma|Charisma|40|3|FreeReroll|1|Player gets 1 free reroll per run.",
            "melee_training_1|Melee Training I|50|4|MeleeDamage|1|Adds +1 dmg to all Melee weapons.",
            "tactical_master|Tactical Master|60|5|WaitEquippedCooldown|1|The Wait action reduces the cooldown of equipped weapons by 1 extra turn.",
            "ranged_training_1|Ranged Training I|70|6|RangedDamage|1|Adds +1 dmg to all Ranged weapons.",
            "speciality_master|Speciality Master|80|7|SpecialDamage|1|Character Special attack now deals 1 dmg."
        ];

        private static string[] GetDefaultCharactersLines() =>
        [
            "# Rogue Cards - Playable Characters Definition File",
            "# Format: Id|Name|BaseHp|SpecialName|SpecialCooldown|UnlockedByDefault|UnlockAchievementId|SpecialDescription",
            "viking|Viking|5|Viking Scream|6|true|none|Knocks back all enemies on adjacent tiles (orthogonally and diagonally) by 1 tile.",
            "shieldmaiden|Shieldmaiden|5|Iron Bulwark|6|false|kingslayer|Grants 1 turn of full damage immunity and reduces cooldown of all equipped cards by 1 turn."
        ];

        private static string[] GetDefaultAchievementsLines() =>
        [
            "# Rogue Cards - Achievements Definition File",
            "# Format: Id|Title|TargetValue|CoinReward|UnlockCharacterId|Description",
            "first_blood|First Blood|1|10|none|Defeat your first enemy.",
            "wave_survivor|Wave Survivor|5|15|none|Reach Wave 5 in a single run.",
            "friendly_betrayal|Friendly Betrayal|1|20|none|Cause an enemy to kill another enemy via friendly fire.",
            "trap_master|Trap Master|5|20|none|Damage and stun 5 enemies using Bear Traps across all runs.",
            "combo_striker|Combo Striker|3|25|none|Play 3 or more equipped cards in a single turn.",
            "untouchable|Untouchable|1|25|none|Clear an entire wave without taking any damage.",
            "well_equipped|Well Equipped|3|30|none|Unlock at least 3 permanent upgrades or weapons in the Menu.",
            "veteran_slayer|Veteran Slayer|50|35|none|Defeat 50 enemies in total across all runs.",
            "no_second_chances|No Second Chances|1|40|none|Win a full 10-wave run without using any Rerolls.",
            "kingslayer|Kingslayer|1|50|shieldmaiden|Defeat the Viking Chief (Wave 10 Boss)."
        ];

        private void ParseDelimitedFile(
            string filePath,
            string fileName,
            int expectedColumns,
            Action<string[], int> rowParser)
        {
            string[] lines = ReadFileLinesSafe(filePath, fileName);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                int lineNumber = i + 1;

                if (string.IsNullOrEmpty(raw) || raw.StartsWith('#'))
                {
                    continue;
                }

                string[] parts = SplitAndTrim(raw);
                if (parts.Length != expectedColumns)
                {
                    LogParseError(fileName, lineNumber, $"Expected {expectedColumns} columns, found {parts.Length}.");
                    continue;
                }

                rowParser(parts, lineNumber);
            }
        }

        private void LoadWeapons(GameDatabase database)
        {
            const string fileName = "weapons.txt";
            ParseDelimitedFile(_weaponsFilePath, fileName, 12, (parts, line) => ParseWeaponRow(database, fileName, parts, line));
            database.WeaponList.Sort((a, b) =>
            {
                int cmp = a.UnlockOrder.CompareTo(b.UnlockOrder);
                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
        }

        private void ParseWeaponRow(GameDatabase database, string fileName, string[] parts, int line)
        {
            if (!ValidateUniqueIdAndName(parts[0], parts[1], database.Weapons.ContainsKey(parts[0]), "Weapon", fileName, line))
            {
                return;
            }

            if (!TryParseEnumField(parts[2], "CardCategory", fileName, line, out CardCategory category) ||
                !TryParseIntField(parts[3], 0, 99, "BaseDamage", fileName, line, out int baseDamage) ||
                !TryParseIntField(parts[4], 1, 20, "BaseCooldown", fileName, line, out int baseCooldown) ||
                !TryParseIntField(parts[5], 0, 10, "Range", fileName, line, out int range) ||
                !TryParseEnumField(parts[6], "TargetingType", fileName, line, out TargetingType targeting) ||
                !TryParseEnumField(parts[7], "WeaponEffectType", fileName, line, out WeaponEffectType effect) ||
                !TryParseIntField(parts[8], 0, 9999, "UnlockCost", fileName, line, out int unlockCost) ||
                !TryParseIntField(parts[9], 0, 999, "UnlockOrder", fileName, line, out int unlockOrder) ||
                !TryParseBoolField(parts[10], "UnlockedByDefault", fileName, line, out bool unlockedByDefault))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(parts[11]))
            {
                LogParseError(fileName, line, "Weapon Description cannot be empty.");
                return;
            }

            WeaponDefinition weapon = new()
            {
                Id = parts[0],
                Name = parts[1],
                Category = category,
                BaseDamage = baseDamage,
                BaseCooldown = baseCooldown,
                Range = range,
                Targeting = targeting,
                Effect = effect,
                UnlockCost = unlockCost,
                UnlockOrder = unlockOrder,
                UnlockedByDefault = unlockedByDefault,
                Description = parts[11]
            };

            database.Weapons[weapon.Id] = weapon;
            database.WeaponList.Add(weapon);
        }

        private void LoadEnemies(GameDatabase database)
        {
            const string fileName = "enemies.txt";
            ParseDelimitedFile(_enemiesFilePath, fileName, 11, (parts, line) => ParseEnemyRow(database, fileName, parts, line));
        }

        private void ParseEnemyRow(GameDatabase database, string fileName, string[] parts, int line)
        {
            if (!TryParseEnumField(parts[1], "EnemyType", fileName, line, out EnemyType enemyType))
            {
                return;
            }

            if (database.Enemies.ContainsKey(enemyType))
            {
                LogParseError(fileName, line, $"Duplicate EnemyType '{enemyType}'.");
                return;
            }

            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[2]))
            {
                LogParseError(fileName, line, "Enemy Id and Name cannot be empty.");
                return;
            }

            if (!TryParseIntField(parts[3], 1, 500, "MaxHp", fileName, line, out int maxHp) ||
                !TryParseIntField(parts[4], 0, 100, "AttackDamage", fileName, line, out int attackDamage) ||
                !TryParseIntField(parts[5], 1, 10, "AttackRange", fileName, line, out int attackRange) ||
                !TryParseIntField(parts[6], 0, 500, "CoinDrop", fileName, line, out int coinDrop) ||
                !TryParseBoolField(parts[7], "SpawnNearEdge", fileName, line, out bool spawnNearEdge) ||
                !TryParseBoolField(parts[8], "IsBoss", fileName, line, out bool isBoss))
            {
                return;
            }

            database.Enemies[enemyType] = new EnemyDefinition
            {
                Id = parts[0],
                Type = enemyType,
                Name = parts[2],
                MaxHp = maxHp,
                AttackDamage = attackDamage,
                AttackRange = attackRange,
                CoinDrop = coinDrop,
                SpawnNearEdge = spawnNearEdge,
                IsBoss = isBoss,
                WeaponName = parts[9]
            };
        }

        private void LoadWaves(GameDatabase database)
        {
            const string fileName = "waves.txt";
            HashSet<int> seenWaves = [];
            ParseDelimitedFile(_wavesFilePath, fileName, 3, (parts, line) => ParseWaveRow(database, seenWaves, fileName, parts, line));
            database.Waves.Sort((a, b) => a.WaveNumber.CompareTo(b.WaveNumber));
        }

        private void ParseWaveRow(
            GameDatabase database,
            HashSet<int> seenWaves,
            string fileName,
            string[] parts,
            int line)
        {
            if (!TryParseIntField(parts[0], 1, 100, "WaveNumber", fileName, line, out int waveNumber))
            {
                return;
            }

            if (!seenWaves.Add(waveNumber))
            {
                LogParseError(fileName, line, $"Duplicate WaveNumber '{waveNumber}'.");
                return;
            }

            if (!TryParseEnumField(parts[2], "RewardAfterClear", fileName, line, out RewardType rewardType))
            {
                return;
            }

            List<WaveSpawnEntry> spawns = ParseWaveSpawns(parts[1], fileName, line);
            if (spawns == null)
            {
                seenWaves.Remove(waveNumber);
                return;
            }

            database.Waves.Add(new WaveDefinition
            {
                WaveNumber = waveNumber,
                Spawns = spawns,
                RewardAfterClear = rewardType
            });
        }

        private List<WaveSpawnEntry> ParseWaveSpawns(string rawSpawns, string fileName, int line)
        {
            string[] spawnTokens = rawSpawns.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (spawnTokens.Length == 0)
            {
                return null;
            }

            List<WaveSpawnEntry> spawns = [];
            foreach (string token in spawnTokens)
            {
                string[] pair = token.Trim().Split(':');
                if (pair.Length != 2)
                {
                    LogParseError(fileName, line, $"Malformed spawn entry '{token}'. Expected EnemyType:Count.");
                    return null;
                }

                if (!TryParseEnumField(pair[0].Trim(), "EnemyType", fileName, line, out EnemyType enemyType) ||
                    !TryParseIntField(pair[1].Trim(), 1, 20, $"spawn count for '{enemyType}'", fileName, line, out int count))
                {
                    return null;
                }

                spawns.Add(new WaveSpawnEntry { EnemyType = enemyType, Count = count });
            }

            return spawns;
        }

        private void LoadUpgrades(GameDatabase database)
        {
            const string fileName = "upgrades.txt";
            ParseDelimitedFile(_upgradesFilePath, fileName, 7, (parts, line) => ParseUpgradeRow(database, fileName, parts, line));
            database.UpgradeList.Sort((a, b) => a.UnlockOrder.CompareTo(b.UnlockOrder));
        }

        private void ParseUpgradeRow(GameDatabase database, string fileName, string[] parts, int line)
        {
            if (!ValidateUniqueIdAndName(parts[0], parts[1], database.Upgrades.ContainsKey(parts[0]), "Upgrade", fileName, line))
            {
                return;
            }

            if (!TryParseIntField(parts[2], 1, 9999, "Cost", fileName, line, out int cost) ||
                !TryParseIntField(parts[3], 1, 999, "UnlockOrder", fileName, line, out int unlockOrder) ||
                !TryParseEnumField(parts[4], "EffectType", fileName, line, out PermanentUpgradeEffectType effectType) ||
                !TryParseIntField(parts[5], 1, 50, "EffectValue", fileName, line, out int effectValue))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(parts[6]))
            {
                LogParseError(fileName, line, "Upgrade Description cannot be empty.");
                return;
            }

            PermanentUpgradeDefinition upgrade = new()
            {
                Id = parts[0],
                Name = parts[1],
                Cost = cost,
                UnlockOrder = unlockOrder,
                EffectType = effectType,
                EffectValue = effectValue,
                Description = parts[6]
            };

            database.Upgrades[upgrade.Id] = upgrade;
            database.UpgradeList.Add(upgrade);
        }

        private void LoadCharacters(GameDatabase database)
        {
            const string fileName = "characters.txt";
            ParseDelimitedFile(_charactersFilePath, fileName, 8, (parts, line) => ParseCharacterRow(database, fileName, parts, line));
        }

        private void ParseCharacterRow(GameDatabase database, string fileName, string[] parts, int line)
        {
            if (!ValidateUniqueIdAndName(parts[0], parts[1], database.Characters.ContainsKey(parts[0]), "Character", fileName, line))
            {
                return;
            }

            if (!TryParseIntField(parts[2], 1, 50, "BaseHp", fileName, line, out int baseHp) ||
                !TryParseIntField(parts[4], 1, 20, "SpecialCooldown", fileName, line, out int specialCooldown) ||
                !TryParseBoolField(parts[5], "UnlockedByDefault", fileName, line, out bool unlockedByDefault))
            {
                return;
            }

            CharacterDefinition character = new()
            {
                Id = parts[0],
                Name = parts[1],
                BaseHp = baseHp,
                SpecialName = parts[3],
                SpecialCooldown = specialCooldown,
                UnlockedByDefault = unlockedByDefault,
                UnlockAchievementId = NormalizeOptionalId(parts[6]),
                SpecialDescription = parts[7]
            };

            database.Characters[character.Id] = character;
            database.CharacterList.Add(character);
        }

        private void LoadAchievements(GameDatabase database)
        {
            const string fileName = "achievements.txt";
            ParseDelimitedFile(_achievementsFilePath, fileName, 6, (parts, line) => ParseAchievementRow(database, fileName, parts, line));
        }

        private void ParseAchievementRow(GameDatabase database, string fileName, string[] parts, int line)
        {
            if (!ValidateUniqueIdAndName(parts[0], parts[1], database.Achievements.ContainsKey(parts[0]), "Achievement", fileName, line))
            {
                return;
            }

            if (!TryParseIntField(parts[2], 1, 10000, "TargetValue", fileName, line, out int targetValue) ||
                !TryParseIntField(parts[3], 0, 5000, "CoinReward", fileName, line, out int coinReward))
            {
                return;
            }

            AchievementDefinition achievement = new()
            {
                Id = parts[0],
                Title = parts[1],
                TargetValue = targetValue,
                CoinReward = coinReward,
                UnlockCharacterId = NormalizeOptionalId(parts[4]),
                Description = parts[5]
            };

            database.Achievements[achievement.Id] = achievement;
            database.AchievementList.Add(achievement);
        }

        private bool ValidateUniqueIdAndName(
            string id,
            string name,
            bool isDuplicate,
            string entityLabel,
            string fileName,
            int line)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                LogParseError(fileName, line, $"{entityLabel} Id and Name cannot be empty.");
                return false;
            }

            if (isDuplicate)
            {
                LogParseError(fileName, line, $"Duplicate {entityLabel} Id '{id}'.");
                return false;
            }

            return true;
        }

        private bool TryParseEnumField<TEnum>(
            string text,
            string fieldName,
            string fileName,
            int line,
            out TEnum value) where TEnum : struct
        {
            if (Enum.TryParse(text, true, out value))
            {
                return true;
            }

            LogParseError(fileName, line, $"Invalid {fieldName} '{text}'.");
            return false;
        }

        private bool TryParseIntField(
            string text,
            int min,
            int max,
            string fieldName,
            string fileName,
            int line,
            out int value)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                value >= min &&
                value <= max)
            {
                return true;
            }

            LogParseError(fileName, line, $"Invalid {fieldName} '{text}'.");
            return false;
        }

        private bool TryParseBoolField(
            string text,
            string fieldName,
            string fileName,
            int line,
            out bool value)
        {
            if (bool.TryParse(text, out value))
            {
                return true;
            }

            LogParseError(fileName, line, $"Invalid {fieldName} boolean '{text}'.");
            return false;
        }

        private static string NormalizeOptionalId(string value) =>
            value.Equals("none", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;

        private string[] ReadFileLinesSafe(string path, string fileName)
        {
            try
            {
                if (!File.Exists(path))
                {
                    LogParseError(fileName, 0, "File not found.");
                    return [];
                }

                return File.ReadAllLines(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogParseError(fileName, 0, $"Unable to read file: {ex.Message}");
                return [];
            }
        }

        private static string[] SplitAndTrim(string line)
        {
            string[] rawParts = line.Split('|');
            for (int i = 0; i < rawParts.Length; i++)
            {
                rawParts[i] = rawParts[i].Trim();
            }

            return rawParts;
        }

        private void LogParseError(string fileName, int lineNumber, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            ParseErrors.Add($"[{timestamp}] [{fileName}:Line {lineNumber}] {message}");
        }

        private void FlushParseErrorsToDisk()
        {
            if (ParseErrors.Count == 0)
            {
                return;
            }

            try
            {
                File.AppendAllLines(_parserErrorLogPath, ParseErrors);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to write parser errors: {ex.Message}");
            }
        }
    }
}