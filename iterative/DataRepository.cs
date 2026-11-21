#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RogueCards
{
    public class DataRepository
    {
        public string BaseDirectory { get; }
        public string DataDirectory { get; }
        public string SavesDirectory { get; }
        public string LogsDirectory { get; }

        public string WeaponsFilePath { get; }
        public string EnemiesFilePath { get; }
        public string WavesFilePath { get; }
        public string UpgradesFilePath { get; }
        public string CharactersFilePath { get; }
        public string AchievementsFilePath { get; }
        public string ActiveRunFilePath { get; }
        public string ParserErrorLogPath { get; }

        public List<string> ParseErrors { get; } = new List<string>();

        public DataRepository()
        {
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            DataDirectory = Path.Combine(BaseDirectory, "Data");
            SavesDirectory = Path.Combine(BaseDirectory, "Saves");
            LogsDirectory = Path.Combine(BaseDirectory, "Logs");

            WeaponsFilePath = Path.Combine(DataDirectory, "weapons.txt");
            EnemiesFilePath = Path.Combine(DataDirectory, "enemies.txt");
            WavesFilePath = Path.Combine(DataDirectory, "waves.txt");
            UpgradesFilePath = Path.Combine(DataDirectory, "upgrades.txt");
            CharactersFilePath = Path.Combine(DataDirectory, "characters.txt");
            AchievementsFilePath = Path.Combine(DataDirectory, "achievements.txt");
            ActiveRunFilePath = Path.Combine(SavesDirectory, "active_run.txt");
            ParserErrorLogPath = Path.Combine(LogsDirectory, "data_parser_errors.txt");
        }

        public GameDatabase InitializeAndLoad()
        {
            ParseErrors.Clear();
            EnsureDirectories();
            EnsureDefaultDataFiles();

            GameDatabase database = new GameDatabase();
            LoadWeapons(database);
            LoadEnemies(database);
            LoadWaves(database);
            LoadUpgrades(database);
            LoadCharacters(database);
            LoadAchievements(database);

            FlushParseErrorsToDisk();
            return database;
        }

        public bool HasActiveRunSave()
        {
            return File.Exists(ActiveRunFilePath);
        }

        public PlayerProfile CreateDefaultProfile(GameDatabase database)
        {
            PlayerProfile profile = new PlayerProfile();
            profile.Coins = 0;
            profile.Area2Unlocked = false;
            profile.SelectedCharacterId = "viking";

            if (database != null)
            {
                foreach (WeaponDefinition weapon in database.WeaponList)
                {
                    if (weapon.UnlockedByDefault)
                    {
                        profile.UnlockedWeaponIds.Add(weapon.Id);
                    }
                }

                foreach (CharacterDefinition character in database.CharacterList)
                {
                    if (character.UnlockedByDefault)
                    {
                        profile.UnlockedCharacterIds.Add(character.Id);
                    }
                }

                foreach (AchievementDefinition achievement in database.AchievementList)
                {
                    profile.AchievementProgress[achievement.Id] = 0;
                }
            }

            return profile;
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(SavesDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }

        private void EnsureDefaultDataFiles()
        {
            if (!File.Exists(WeaponsFilePath))
            {
                WriteDefaultWeaponsFile();
            }

            if (!File.Exists(EnemiesFilePath))
            {
                WriteDefaultEnemiesFile();
            }

            if (!File.Exists(WavesFilePath))
            {
                WriteDefaultWavesFile();
            }

            if (!File.Exists(UpgradesFilePath))
            {
                WriteDefaultUpgradesFile();
            }

            if (!File.Exists(CharactersFilePath))
            {
                WriteDefaultCharactersFile();
            }

            if (!File.Exists(AchievementsFilePath))
            {
                WriteDefaultAchievementsFile();
            }
        }

        private void WriteDefaultWeaponsFile()
        {
            string[] lines = new string[]
            {
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
            };

            File.WriteAllLines(WeaponsFilePath, lines);
        }

        private void WriteDefaultEnemiesFile()
        {
            string[] lines = new string[]
            {
                "# Rogue Cards - Enemies Definition File",
                "# Format: Id|EnemyType|Name|MaxHp|AttackDamage|AttackRange|CoinDrop|SpawnNearEdge|IsBoss|WeaponName|Description",
                "pawn|Pawn|Pawn|3|2|1|2|false|false|Axe|Foot soldier. Moves orthogonally toward the player and strikes at range 1.",
                "archer|Archer|Archer|2|2|5|3|true|false|Bow|Hooded ranger. Keeps distance greater than 2 and fires along row or column.",
                "spearman|Spearman|Spearman|4|2|2|3|false|false|Spear|Armored guard. Moves toward the player and strikes up to 2 tiles orthogonally.",
                "shaman|Shaman|Shaman|3|1|3|4|true|false|Potion|Tribal mystic. Throws a poison potion at distance 3 leaving toxic hazards.",
                "viking_chief|VikingChief|Viking Chief|30|3|5|30|false|true|Dual Arms|Wave 10 Boss. Cycles Throwing Axe, Hook + Club with summon, and Evolved Viking Axe."
            };

            File.WriteAllLines(EnemiesFilePath, lines);
        }

        private void WriteDefaultWavesFile()
        {
            string[] lines = new string[]
            {
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
            };

            File.WriteAllLines(WavesFilePath, lines);
        }

        private void WriteDefaultUpgradesFile()
        {
            string[] lines = new string[]
            {
                "# Rogue Cards - Permanent Upgrades Queue File",
                "# Format: Id|Name|Cost|UnlockOrder|EffectType|EffectValue|Description",
                "durability_1|Durability Training I|20|1|MaxHp|1|Increases max character health by +1 HP.",
                "swift_boots|Swift Boots|30|2|MovementCooldown|1|Reduces the cooldown of all Movement abilities by 1 turn.",
                "charisma|Charisma|40|3|FreeReroll|1|Player gets 1 free reroll per run.",
                "melee_training_1|Melee Training I|50|4|MeleeDamage|1|Adds +1 dmg to all Melee weapons.",
                "tactical_master|Tactical Master|60|5|WaitEquippedCooldown|1|The Wait action reduces the cooldown of equipped weapons by 1 extra turn.",
                "ranged_training_1|Ranged Training I|70|6|RangedDamage|1|Adds +1 dmg to all Ranged weapons.",
                "speciality_master|Speciality Master|80|7|SpecialDamage|1|Character Special attack now deals 1 dmg."
            };

            File.WriteAllLines(UpgradesFilePath, lines);
        }

        private void WriteDefaultCharactersFile()
        {
            string[] lines = new string[]
            {
                "# Rogue Cards - Playable Characters Definition File",
                "# Format: Id|Name|BaseHp|SpecialName|SpecialCooldown|UnlockedByDefault|UnlockAchievementId|SpecialDescription",
                "viking|Viking|5|Viking Scream|6|true|none|Knocks back all enemies on adjacent tiles (orthogonally and diagonally) by 1 tile.",
                "shieldmaiden|Shieldmaiden|5|Iron Bulwark|6|false|kingslayer|Grants 1 turn of full damage immunity and reduces cooldown of all equipped cards by 1 turn."
            };

            File.WriteAllLines(CharactersFilePath, lines);
        }

        private void WriteDefaultAchievementsFile()
        {
            string[] lines = new string[]
            {
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
            };

            File.WriteAllLines(AchievementsFilePath, lines);
        }

        private void LoadWeapons(GameDatabase database)
        {
            string fileName = "weapons.txt";
            string[] lines = ReadFileLinesSafe(WeaponsFilePath, fileName);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                int lineNumber = i + 1;

                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = SplitAndTrim(raw);
                if (parts.Length != 12)
                {
                    LogParseError(fileName, lineNumber, $"Expected 12 columns, found {parts.Length}.");
                    continue;
                }

                string id = parts[0];
                string name = parts[1];

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    LogParseError(fileName, lineNumber, "Weapon Id and Name cannot be empty.");
                    continue;
                }

                if (database.Weapons.ContainsKey(id))
                {
                    LogParseError(fileName, lineNumber, $"Duplicate weapon Id '{id}'.");
                    continue;
                }

                if (!Enum.TryParse(parts[2], true, out CardCategory category))
                {
                    LogParseError(fileName, lineNumber, $"Invalid CardCategory '{parts[2]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[3], 0, 99, out int baseDamage))
                {
                    LogParseError(fileName, lineNumber, $"Invalid BaseDamage '{parts[3]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[4], 1, 20, out int baseCooldown))
                {
                    LogParseError(fileName, lineNumber, $"Invalid BaseCooldown '{parts[4]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[5], 0, 10, out int range))
                {
                    LogParseError(fileName, lineNumber, $"Invalid Range '{parts[5]}'.");
                    continue;
                }

                if (!Enum.TryParse(parts[6], true, out TargetingType targeting))
                {
                    LogParseError(fileName, lineNumber, $"Invalid TargetingType '{parts[6]}'.");
                    continue;
                }

                if (!Enum.TryParse(parts[7], true, out WeaponEffectType effect))
                {
                    LogParseError(fileName, lineNumber, $"Invalid WeaponEffectType '{parts[7]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[8], 0, 9999, out int unlockCost))
                {
                    LogParseError(fileName, lineNumber, $"Invalid UnlockCost '{parts[8]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[9], 0, 999, out int unlockOrder))
                {
                    LogParseError(fileName, lineNumber, $"Invalid UnlockOrder '{parts[9]}'.");
                    continue;
                }

                if (!bool.TryParse(parts[10], out bool unlockedByDefault))
                {
                    LogParseError(fileName, lineNumber, $"Invalid UnlockedByDefault boolean '{parts[10]}'.");
                    continue;
                }

                string description = parts[11];
                if (string.IsNullOrWhiteSpace(description))
                {
                    LogParseError(fileName, lineNumber, "Weapon Description cannot be empty.");
                    continue;
                }

                WeaponDefinition weapon = new WeaponDefinition
                {
                    Id = id,
                    Name = name,
                    Category = category,
                    BaseDamage = baseDamage,
                    BaseCooldown = baseCooldown,
                    Range = range,
                    Targeting = targeting,
                    Effect = effect,
                    UnlockCost = unlockCost,
                    UnlockOrder = unlockOrder,
                    UnlockedByDefault = unlockedByDefault,
                    Description = description
                };

                database.Weapons[weapon.Id] = weapon;
                database.WeaponList.Add(weapon);
            }

            database.WeaponList = database.WeaponList
                .OrderBy(w => w.UnlockOrder)
                .ThenBy(w => w.Name)
                .ToList();
        }

        private void LoadEnemies(GameDatabase database)
        {
            string fileName = "enemies.txt";
            string[] lines = ReadFileLinesSafe(EnemiesFilePath, fileName);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                int lineNumber = i + 1;

                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = SplitAndTrim(raw);
                if (parts.Length != 11)
                {
                    LogParseError(fileName, lineNumber, $"Expected 11 columns, found {parts.Length}.");
                    continue;
                }

                string id = parts[0];
                if (!Enum.TryParse(parts[1], true, out EnemyType enemyType))
                {
                    LogParseError(fileName, lineNumber, $"Invalid EnemyType '{parts[1]}'.");
                    continue;
                }

                if (database.Enemies.ContainsKey(enemyType))
                {
                    LogParseError(fileName, lineNumber, $"Duplicate EnemyType '{enemyType}'.");
                    continue;
                }

                string name = parts[2];
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    LogParseError(fileName, lineNumber, "Enemy Id and Name cannot be empty.");
                    continue;
                }

                if (!TryParseIntRange(parts[3], 1, 500, out int maxHp))
                {
                    LogParseError(fileName, lineNumber, $"Invalid MaxHp '{parts[3]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[4], 0, 100, out int attackDamage))
                {
                    LogParseError(fileName, lineNumber, $"Invalid AttackDamage '{parts[4]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[5], 1, 10, out int attackRange))
                {
                    LogParseError(fileName, lineNumber, $"Invalid AttackRange '{parts[5]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[6], 0, 500, out int coinDrop))
                {
                    LogParseError(fileName, lineNumber, $"Invalid CoinDrop '{parts[6]}'.");
                    continue;
                }

                if (!bool.TryParse(parts[7], out bool spawnNearEdge))
                {
                    LogParseError(fileName, lineNumber, $"Invalid SpawnNearEdge '{parts[7]}'.");
                    continue;
                }

                if (!bool.TryParse(parts[8], out bool isBoss))
                {
                    LogParseError(fileName, lineNumber, $"Invalid IsBoss '{parts[8]}'.");
                    continue;
                }

                string weaponName = parts[9];
                string description = parts[10];

                EnemyDefinition enemy = new EnemyDefinition
                {
                    Id = id,
                    Type = enemyType,
                    Name = name,
                    MaxHp = maxHp,
                    AttackDamage = attackDamage,
                    AttackRange = attackRange,
                    CoinDrop = coinDrop,
                    SpawnNearEdge = spawnNearEdge,
                    IsBoss = isBoss,
                    WeaponName = weaponName,
                    Description = description
                };

                database.Enemies[enemy.Type] = enemy;
            }
        }

        private void LoadWaves(GameDatabase database)
        {
            string fileName = "waves.txt";
            string[] lines = ReadFileLinesSafe(WavesFilePath, fileName);
            HashSet<int> seenWaves = new HashSet<int>();

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                int lineNumber = i + 1;

                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = SplitAndTrim(raw);
                if (parts.Length != 3)
                {
                    LogParseError(fileName, lineNumber, $"Expected 3 columns, found {parts.Length}.");
                    continue;
                }

                if (!TryParseIntRange(parts[0], 1, 100, out int waveNumber))
                {
                    LogParseError(fileName, lineNumber, $"Invalid WaveNumber '{parts[0]}'.");
                    continue;
                }

                if (seenWaves.Contains(waveNumber))
                {
                    LogParseError(fileName, lineNumber, $"Duplicate WaveNumber '{waveNumber}'.");
                    continue;
                }

                if (!Enum.TryParse(parts[2], true, out RewardType rewardType))
                {
                    LogParseError(fileName, lineNumber, $"Invalid RewardAfterClear '{parts[2]}'.");
                    continue;
                }

                List<WaveSpawnEntry> spawns = new List<WaveSpawnEntry>();
                string[] spawnTokens = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                bool spawnValid = spawnTokens.Length > 0;

                foreach (string token in spawnTokens)
                {
                    string[] pair = token.Trim().Split(':');
                    if (pair.Length != 2)
                    {
                        LogParseError(fileName, lineNumber, $"Malformed spawn entry '{token}'. Expected EnemyType:Count.");
                        spawnValid = false;
                        break;
                    }

                    if (!Enum.TryParse(pair[0].Trim(), true, out EnemyType enemyType))
                    {
                        LogParseError(fileName, lineNumber, $"Invalid EnemyType '{pair[0]}' in wave spawn.");
                        spawnValid = false;
                        break;
                    }

                    if (!TryParseIntRange(pair[1].Trim(), 1, 20, out int count))
                    {
                        LogParseError(fileName, lineNumber, $"Invalid spawn count '{pair[1]}' for '{enemyType}'.");
                        spawnValid = false;
                        break;
                    }

                    spawns.Add(new WaveSpawnEntry
                    {
                        EnemyType = enemyType,
                        Count = count
                    });
                }

                if (!spawnValid)
                {
                    continue;
                }

                seenWaves.Add(waveNumber);
                database.Waves.Add(new WaveDefinition
                {
                    WaveNumber = waveNumber,
                    Spawns = spawns,
                    RewardAfterClear = rewardType
                });
            }

            database.Waves = database.Waves.OrderBy(w => w.WaveNumber).ToList();
        }

        private void LoadUpgrades(GameDatabase database)
        {
            string fileName = "upgrades.txt";
            string[] lines = ReadFileLinesSafe(UpgradesFilePath, fileName);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                int lineNumber = i + 1;

                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = SplitAndTrim(raw);
                if (parts.Length != 7)
                {
                    LogParseError(fileName, lineNumber, $"Expected 7 columns, found {parts.Length}.");
                    continue;
                }

                string id = parts[0];
                string name = parts[1];

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    LogParseError(fileName, lineNumber, "Upgrade Id and Name cannot be empty.");
                    continue;
                }

                if (database.Upgrades.ContainsKey(id))
                {
                    LogParseError(fileName, lineNumber, $"Duplicate upgrade Id '{id}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[2], 1, 9999, out int cost))
                {
                    LogParseError(fileName, lineNumber, $"Invalid upgrade Cost '{parts[2]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[3], 1, 999, out int unlockOrder))
                {
                    LogParseError(fileName, lineNumber, $"Invalid UnlockOrder '{parts[3]}'.");
                    continue;
                }

                if (!Enum.TryParse(parts[4], true, out PermanentUpgradeEffectType effectType))
                {
                    LogParseError(fileName, lineNumber, $"Invalid EffectType '{parts[4]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[5], 1, 50, out int effectValue))
                {
                    LogParseError(fileName, lineNumber, $"Invalid EffectValue '{parts[5]}'.");
                    continue;
                }

                string description = parts[6];
                if (string.IsNullOrWhiteSpace(description))
                {
                    LogParseError(fileName, lineNumber, "Upgrade Description cannot be empty.");
                    continue;
                }

                PermanentUpgradeDefinition upgrade = new PermanentUpgradeDefinition
                {
                    Id = id,
                    Name = name,
                    Cost = cost,
                    UnlockOrder = unlockOrder,
                    EffectType = effectType,
                    EffectValue = effectValue,
                    Description = description
                };

                database.Upgrades[upgrade.Id] = upgrade;
                database.UpgradeList.Add(upgrade);
            }

            database.UpgradeList = database.UpgradeList
                .OrderBy(u => u.UnlockOrder)
                .ToList();
        }

        private void LoadCharacters(GameDatabase database)
        {
            string fileName = "characters.txt";
            string[] lines = ReadFileLinesSafe(CharactersFilePath, fileName);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                int lineNumber = i + 1;

                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = SplitAndTrim(raw);
                if (parts.Length != 8)
                {
                    LogParseError(fileName, lineNumber, $"Expected 8 columns, found {parts.Length}.");
                    continue;
                }

                string id = parts[0];
                string name = parts[1];

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    LogParseError(fileName, lineNumber, "Character Id and Name cannot be empty.");
                    continue;
                }

                if (database.Characters.ContainsKey(id))
                {
                    LogParseError(fileName, lineNumber, $"Duplicate character Id '{id}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[2], 1, 50, out int baseHp))
                {
                    LogParseError(fileName, lineNumber, $"Invalid BaseHp '{parts[2]}'.");
                    continue;
                }

                string specialName = parts[3];
                if (!TryParseIntRange(parts[4], 1, 20, out int specialCooldown))
                {
                    LogParseError(fileName, lineNumber, $"Invalid SpecialCooldown '{parts[4]}'.");
                    continue;
                }

                if (!bool.TryParse(parts[5], out bool unlockedByDefault))
                {
                    LogParseError(fileName, lineNumber, $"Invalid UnlockedByDefault '{parts[5]}'.");
                    continue;
                }

                string unlockAchId = parts[6].Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : parts[6];

                string specialDesc = parts[7];

                CharacterDefinition character = new CharacterDefinition
                {
                    Id = id,
                    Name = name,
                    BaseHp = baseHp,
                    SpecialName = specialName,
                    SpecialCooldown = specialCooldown,
                    UnlockedByDefault = unlockedByDefault,
                    UnlockAchievementId = unlockAchId,
                    SpecialDescription = specialDesc
                };

                database.Characters[character.Id] = character;
                database.CharacterList.Add(character);
            }
        }

        private void LoadAchievements(GameDatabase database)
        {
            string fileName = "achievements.txt";
            string[] lines = ReadFileLinesSafe(AchievementsFilePath, fileName);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                int lineNumber = i + 1;

                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = SplitAndTrim(raw);
                if (parts.Length != 6)
                {
                    LogParseError(fileName, lineNumber, $"Expected 6 columns, found {parts.Length}.");
                    continue;
                }

                string id = parts[0];
                string title = parts[1];

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                {
                    LogParseError(fileName, lineNumber, "Achievement Id and Title cannot be empty.");
                    continue;
                }

                if (database.Achievements.ContainsKey(id))
                {
                    LogParseError(fileName, lineNumber, $"Duplicate achievement Id '{id}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[2], 1, 10000, out int targetValue))
                {
                    LogParseError(fileName, lineNumber, $"Invalid TargetValue '{parts[2]}'.");
                    continue;
                }

                if (!TryParseIntRange(parts[3], 0, 5000, out int coinReward))
                {
                    LogParseError(fileName, lineNumber, $"Invalid CoinReward '{parts[3]}'.");
                    continue;
                }

                string unlockCharId = parts[4].Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : parts[4];

                string description = parts[5];

                AchievementDefinition achievement = new AchievementDefinition
                {
                    Id = id,
                    Title = title,
                    TargetValue = targetValue,
                    CoinReward = coinReward,
                    UnlockCharacterId = unlockCharId,
                    Description = description
                };

                database.Achievements[achievement.Id] = achievement;
                database.AchievementList.Add(achievement);
            }
        }

        private string[] ReadFileLinesSafe(string path, string fileName)
        {
            try
            {
                if (!File.Exists(path))
                {
                    LogParseError(fileName, 0, "File not found.");
                    return Array.Empty<string>();
                }

                return File.ReadAllLines(path);
            }
            catch (Exception ex)
            {
                LogParseError(fileName, 0, $"Unable to read file: {ex.Message}");
                return Array.Empty<string>();
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

        private static bool TryParseIntRange(string text, int min, int max, out int value)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value >= min && value <= max;
            }

            return false;
        }

        private void LogParseError(string fileName, int lineNumber, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string entry = $"[{timestamp}] [{fileName}:Line {lineNumber}] {message}";
            ParseErrors.Add(entry);
        }

        private void FlushParseErrorsToDisk()
        {
            if (ParseErrors.Count == 0)
            {
                return;
            }

            try
            {
                File.AppendAllLines(ParserErrorLogPath, ParseErrors);
            }
            catch
            {
            }
        }
    }
}