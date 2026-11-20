#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace RogueCards
{
    public enum ScreenMode
    {
        MainMenu,
        Gameplay,
        AchievementsAndStats
    }

    public enum GamePhase
    {
        StartingDraft,
        PlayerTurn,
        AimingCard,
        RewardPickCard,
        RewardUpgrade,
        Victory,
        Defeat
    }

    public enum CardCategory
    {
        Melee,
        Ranged,
        Special,
        Movement
    }

    public enum AimMode
    {
        Orthogonal,
        Diagonal,
        SelfTile
    }

    public enum EnemyAIState
    {
        Unarmed,
        Armed,
        Charging
    }

    public enum HazardType
    {
        BearTrap,
        Poison
    }

    public enum ModifierType
    {
        DamagePlus1,
        DamagePlus2,
        CooldownMinus1,
        CooldownMinus2,
        Poison
    }

    public static class SecureRandom
    {
        public static int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1)
            {
                return 0;
            }
            return RandomNumberGenerator.GetInt32(maxExclusive);
        }

        public static float NextFloat(float min, float max)
        {
            int sample = RandomNumberGenerator.GetInt32(0, 10000);
            float t = sample / 10000f;
            return min + t * (max - min);
        }

        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    public readonly struct GridPos : IEquatable<GridPos>
    {
        public const int GridSize = 5;

        public int X { get; }
        public int Y { get; }

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsInBounds()
        {
            return X >= 0 && X < GridSize && Y >= 0 && Y < GridSize;
        }

        public int ManhattanDistance(GridPos other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public GridPos Offset(int dx, int dy)
        {
            return new GridPos(X + dx, Y + dy);
        }

        public static bool TryParse(string raw, out GridPos pos, bool requireInBounds)
        {
            pos = new GridPos(0, 0);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }
            string[] parts = raw.Split(',');
            if (parts.Length != 2)
            {
                return false;
            }
            bool okX = int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x);
            bool okY = int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y);
            if (!okX || !okY)
            {
                return false;
            }
            pos = new GridPos(x, y);
            return !requireInBounds || pos.IsInBounds();
        }

        public bool Equals(GridPos other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPos other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(GridPos a, GridPos b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(GridPos a, GridPos b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            char col = (char)('A' + Math.Clamp(X, 0, GridSize - 1));
            return col.ToString() + (Y + 1).ToString(CultureInfo.InvariantCulture);
        }
    }

    public class WeaponDef
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public CardCategory Category { get; init; }
        public AimMode AimMode { get; init; }
        public int BaseDamage { get; init; }
        public int BaseCooldown { get; init; }
        public bool DefaultUnlocked { get; init; }
        public int UnlockOrder { get; init; }
        public int UnlockCost { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    public class EnemyDef
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int MaxHp { get; init; }
        public int Damage { get; init; }
        public int CoinDrop { get; init; }
        public bool IsBoss { get; init; }
        public bool SpawnNearEdge { get; init; }
        public string WeaponName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    public class WaveDef
    {
        public int WaveNumber { get; init; }
        public List<string> EnemyIds { get; init; } = [];
        public string RewardAfter { get; init; } = "None";
    }

    public class UpgradeDef
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int UnlockOrder { get; init; }
        public int Cost { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    public class CharacterDef
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int BaseHp { get; init; }
        public string SpecialName { get; init; } = string.Empty;
        public int SpecialCooldown { get; init; }
        public bool DefaultUnlocked { get; init; }
        public string UnlockConditionText { get; init; } = string.Empty;
        public string SpecialDescription { get; init; } = string.Empty;
    }

    public class AchievementDef
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int TargetValue { get; init; }
        public int CoinReward { get; init; }
        public string SpecialUnlock { get; init; } = "None";
    }

    public class GameDatabase
    {
        public Dictionary<string, WeaponDef> Weapons { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<WeaponDef> WeaponList { get; set; } = [];
        public Dictionary<string, EnemyDef> Enemies { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<WaveDef> Waves { get; set; } = [];
        public Dictionary<string, UpgradeDef> Upgrades { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<UpgradeDef> UpgradeList { get; set; } = [];
        public Dictionary<string, CharacterDef> Characters { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<CharacterDef> CharacterList { get; } = [];
        public Dictionary<string, AchievementDef> Achievements { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<AchievementDef> AchievementList { get; } = [];
        public List<string> LoadWarnings { get; } = [];

        public WeaponDef GetWeaponOrFallback(string id)
        {
            if (!string.IsNullOrEmpty(id) && Weapons.TryGetValue(id, out WeaponDef def))
            {
                return def;
            }
            return WeaponList.Count > 0 ? WeaponList[0] : new WeaponDef();
        }

        public EnemyDef GetEnemyOrFallback(string id)
        {
            if (!string.IsNullOrEmpty(id) && Enemies.TryGetValue(id, out EnemyDef def))
            {
                return def;
            }
            return new EnemyDef
            {
                Id = id ?? string.Empty,
                Name = id ?? "Enemy",
                MaxHp = 1
            };
        }

        public CharacterDef GetCharacterOrFallback(string id)
        {
            if (!string.IsNullOrEmpty(id) && Characters.TryGetValue(id, out CharacterDef def))
            {
                return def;
            }
            return CharacterList.Count > 0 ? CharacterList[0] : new CharacterDef();
        }
    }

    public class CardInstance
    {
        public string InstanceUid { get; } = Guid.NewGuid().ToString("N")[..8];
        public string WeaponId { get; set; } = string.Empty;
        public int CurrentCooldown { get; set; }
        public int BonusDamage { get; set; }
        public int CooldownModifier { get; set; }
        public bool HasPoison { get; set; }
        public bool UsedThisTurn { get; set; }

        public int GetEffectiveDamage(GameDatabase db, ProfileData profile)
        {
            WeaponDef def = db.GetWeaponOrFallback(WeaponId);
            if (def.Category == CardCategory.Movement)
            {
                return 0;
            }
            int dmg = def.BaseDamage + BonusDamage;
            if (def.Category == CardCategory.Melee && profile.UnlockedUpgrades.Contains("melee_1"))
            {
                dmg += 1;
            }
            if (def.Category == CardCategory.Ranged && profile.UnlockedUpgrades.Contains("ranged_1"))
            {
                dmg += 1;
            }
            return Math.Max(0, dmg);
        }

        public int GetEffectiveMaxCooldown(GameDatabase db, ProfileData profile)
        {
            WeaponDef def = db.GetWeaponOrFallback(WeaponId);
            int cd = def.BaseCooldown + CooldownModifier;
            if (HasPoison)
            {
                cd += 1;
            }
            if (def.Category == CardCategory.Movement && profile.UnlockedUpgrades.Contains("swift_boots"))
            {
                cd -= 1;
            }
            return Math.Max(1, cd);
        }

        public string GetModifiersSummary()
        {
            List<string> parts = [];
            if (BonusDamage > 0)
            {
                parts.Add("+" + BonusDamage.ToString(CultureInfo.InvariantCulture) + " DMG");
            }
            if (CooldownModifier < 0)
            {
                parts.Add(CooldownModifier.ToString(CultureInfo.InvariantCulture) + " CD");
            }
            else if (CooldownModifier > 0)
            {
                parts.Add("+" + CooldownModifier.ToString(CultureInfo.InvariantCulture) + " CD");
            }
            if (HasPoison)
            {
                parts.Add("POISON");
            }
            return string.Join(" | ", parts);
        }
    }

    public class EnemyInstance
    {
        public string Uid { get; } = Guid.NewGuid().ToString("N")[..8];
        public string EnemyId { get; set; } = string.Empty;
        public GridPos Pos { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public EnemyAIState State { get; set; }
        public int ChargeTurnsRemaining { get; set; }
        public List<GridPos> LockedTiles { get; set; } = [];
        public GridPos AimDir { get; set; }
        public int BossCycleIndex { get; set; }
        public int PoisonTurns { get; set; }
        public int StunTurns { get; set; }
        public string IntentLabel { get; set; } = "Equipping";
        public bool SpawnedMidTurn { get; set; }
    }

    public class HazardInstance
    {
        public HazardType Type { get; init; }
        public GridPos Pos { get; init; }
        public int RemainingTurns { get; set; }
        public bool IsArmed { get; set; }
        public int Damage { get; init; }
        public bool AppliesPoison { get; init; }
    }

    public class PlayerState
    {
        public string CharacterId { get; set; } = "viking";
        public GridPos Pos { get; set; } = new GridPos(2, 2);
        public int Hp { get; set; } = 5;
        public int MaxHp { get; set; } = 5;
        public int SpecialCooldown { get; set; }
        public bool SpecialUsedThisTurn { get; set; }
        public int ImmuneTurns { get; set; }
        public int FreeRerollsLeft { get; set; }
        public int RerollsUsedThisRun { get; set; }
        public bool TookDamageThisWave { get; set; }
        public int RunCoinsEarned { get; set; }
        public int RunEnemiesDefeated { get; set; }
    }

    public class RewardState
    {
        public List<string> OfferedWeaponIds { get; } = [];
        public List<ModifierType> OfferedModifiers { get; } = [];
        public ModifierType? SelectedModifier { get; set; }
        public int RerollCost { get; set; } = 5;
        public List<int> StartingDraftSelectedIndices { get; } = [];
    }

    public class FloatingText
    {
        public GridPos Pos { get; init; }
        public string Text { get; init; } = string.Empty;
        public Color Color { get; init; }
        public float Progress { get; set; }
        public float OffsetX { get; init; }
    }

    public class ToastMessage
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public Color AccentColor { get; init; }
        public float RemainingSeconds { get; set; }
    }

    public class ProfileData
    {
        public int Coins { get; set; }
        public HashSet<string> UnlockedWeapons { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> UnlockedUpgrades { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> UnlockedCharacters { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CompletedAchievements { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> AchievementProgress { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool Area2Unlocked { get; set; }
    }

    public class StatsData
    {
        public int TotalRunsPlayed { get; set; }
        public int RunsWon { get; set; }
        public int HighestWaveReached { get; set; }
        public int TotalTurnsTaken { get; set; }
        public int TotalEnemiesDefeated { get; set; }
        public Dictionary<string, int> EnemiesDefeatedByType { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int TotalFriendlyFireKills { get; set; }
        public int TotalDamageDealt { get; set; }
        public int TotalCoinsEarned { get; set; }
        public int TotalCardsPlayed { get; set; }
        public Dictionary<string, int> WeaponUsageCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int BearTrapStunsTriggered { get; set; }

        public string GetMostUsedWeaponName(GameDatabase db)
        {
            string bestId = string.Empty;
            int bestCount = 0;
            foreach (KeyValuePair<string, int> kv in WeaponUsageCounts)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    bestId = kv.Key;
                }
            }
            if (bestCount <= 0)
            {
                return "None";
            }
            string displayName = db.Weapons.TryGetValue(bestId, out WeaponDef wdef) ? wdef.Name : bestId;
            return displayName + " (" + bestCount.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }

    public class CombatLogEntry
    {
        public string Timestamp { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public Color Color { get; init; }
    }

    public static class DefaultDataInitializer
    {
        public static void EnsureDefaultFiles(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            WriteIfMissing(Path.Combine(dataDir, "weapons.txt"), GetDefaultWeapons());
            WriteIfMissing(Path.Combine(dataDir, "enemies.txt"), GetDefaultEnemies());
            WriteIfMissing(Path.Combine(dataDir, "waves.txt"), GetDefaultWaves());
            WriteIfMissing(Path.Combine(dataDir, "upgrades.txt"), GetDefaultUpgrades());
            WriteIfMissing(Path.Combine(dataDir, "characters.txt"), GetDefaultCharacters());
            WriteIfMissing(Path.Combine(dataDir, "achievements.txt"), GetDefaultAchievements());
        }

        private static void WriteIfMissing(string path, string content)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content, Encoding.UTF8);
            }
        }

        private static string GetDefaultWeapons()
        {
            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards - Weapons Definitions");
            sb.AppendLine("# Format: id|name|category|aimMode|damage|cooldown|defaultUnlocked|unlockOrder|unlockCost|description");
            sb.AppendLine("viking_axe|Viking Axe|Melee|Orthogonal|3|4|true|0|0|Hits one enemy on an adjacent orthogonal tile.");
            sb.AppendLine("wooden_club|Wooden Club|Melee|Orthogonal|2|5|false|1|15|Hits an adjacent orthogonal enemy and knocks them back 1 tile.");
            sb.AppendLine("two_handed_hammer|Two-handed Hammer|Melee|Orthogonal|4|6|false|5|30|Sweeps 3 tiles in a front arc in the chosen orthogonal direction.");
            sb.AppendLine("throwing_axe|Throwing Axe|Ranged|Orthogonal|2|3|true|0|0|Fires in a straight orthogonal line, hitting the first enemy.");
            sb.AppendLine("hunting_bow|Hunting Bow|Ranged|Diagonal|3|5|false|2|20|Fires an arrow diagonally, hitting the first enemy in its path.");
            sb.AppendLine("heavy_spear|Heavy Spear|Ranged|Orthogonal|4|6|false|6|35|Fires orthogonally, piercing the first enemy and stopping at the second.");
            sb.AppendLine("bear_trap|Bear Trap|Special|SelfTile|2|6|true|0|0|Places a trap on your tile. Arms when you leave; damages and stuns the first enemy.");
            sb.AppendLine("boat_hook|Boat Hook|Special|Orthogonal|1|5|false|3|25|Fires up to 3 tiles orthogonally, damaging and pulling the enemy adjacent to you.");
            sb.AppendLine("diagonal_dash|Diagonal Dash|Movement|Diagonal|0|4|true|0|0|Dashes 1 tile diagonally onto a free tile.");
            sb.AppendLine("slide|Slide|Movement|Orthogonal|0|5|false|4|25|Slides up to 2 tiles orthogonally, stopping before any enemy.");
            return sb.ToString();
        }

        private static string GetDefaultEnemies()
        {
            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards - Enemies Definitions");
            sb.AppendLine("# Format: id|name|maxHp|damage|coinDrop|isBoss|spawnNearEdge|weaponName|description");
            sb.AppendLine("pawn|Pawn|3|2|2|false|false|Hand Axe|Foot soldier. Moves orthogonally and strikes adjacent tiles.");
            sb.AppendLine("archer|Archer|2|2|3|false|true|Longbow|Ranged hunter. Retreats if close; aligns and fires straight arrows.");
            sb.AppendLine("spearman|Spearman|4|2|3|false|false|Long Spear|Armored guard. Strikes up to 2 tiles orthogonally.");
            sb.AppendLine("shaman|Shaman|3|1|4|false|true|Poison Vial|Tribal mystic. Keeps distance and throws poison in an X pattern.");
            sb.AppendLine("viking_chief|Viking Chief|30|3|30|true|false|Warlord Arsenal|Wave 10 Boss. Cycles Throwing Axe, Hook+Club+Spawn, and Evolved Axe.");
            return sb.ToString();
        }

        private static string GetDefaultWaves()
        {
            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards - Level 1 Wave Progression");
            sb.AppendLine("# Format: waveNumber|enemyIdsCommaSeparated|rewardAfter");
            sb.AppendLine("1|pawn|None");
            sb.AppendLine("2|pawn,archer|PickCard");
            sb.AppendLine("3|spearman,pawn|None");
            sb.AppendLine("4|spearman,archer|Upgrade");
            sb.AppendLine("5|pawn,pawn,shaman|None");
            sb.AppendLine("6|spearman,archer,archer|PickCard");
            sb.AppendLine("7|pawn,pawn,pawn,archer,archer|None");
            sb.AppendLine("8|pawn,pawn,spearman,shaman|Upgrade");
            sb.AppendLine("9|pawn,shaman,spearman,archer,archer|None");
            sb.AppendLine("10|viking_chief|Victory");
            return sb.ToString();
        }

        private static string GetDefaultUpgrades()
        {
            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards - Permanent Upgrades");
            sb.AppendLine("# Format: id|name|unlockOrder|cost|description");
            sb.AppendLine("durability_1|Durability Training I|1|20|Increases maximum hero health by +1 HP.");
            sb.AppendLine("swift_boots|Swift Boots|2|30|Reduces cooldown of all Movement cards by 1 turn.");
            sb.AppendLine("charisma|Charisma|3|40|Grants 1 free reward reroll per run.");
            sb.AppendLine("melee_1|Melee Training I|4|50|Adds +1 damage to all Melee weapons.");
            sb.AppendLine("tactical_master|Tactical Master|5|60|The Wait action reduces active weapon cooldowns by 1 extra turn.");
            sb.AppendLine("ranged_1|Ranged Training I|6|70|Adds +1 damage to all Ranged weapons.");
            sb.AppendLine("speciality_master|Speciality Master|7|80|Hero Special ability also deals 1 damage to affected enemies.");
            return sb.ToString();
        }

        private static string GetDefaultCharacters()
        {
            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards - Playable Characters");
            sb.AppendLine("# Format: id|name|baseHp|specialName|specialCooldown|defaultUnlocked|unlockCondition|specialDescription");
            sb.AppendLine("viking|Viking|5|Viking Scream|6|true|Unlocked by default|Knocks back all adjacent enemies (8 directions) by 1 tile. Deals 0 dmg (1 with Speciality Master).");
            sb.AppendLine("shieldmaiden|Shieldmaiden|5|Iron Bulwark|6|false|Complete Kingslayer Achievement|Grants 1 turn of full damage immunity and reduces card cooldowns by 1. (+1 dmg to orthogonal foes with Speciality Master).");
            return sb.ToString();
        }

        private static string GetDefaultAchievements()
        {
            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards - Achievements");
            sb.AppendLine("# Format: id|title|description|targetValue|coinReward|specialUnlock");
            sb.AppendLine("first_blood|First Blood|Defeat your first enemy.|1|10|None");
            sb.AppendLine("wave_survivor|Wave Survivor|Reach Wave 5 in a single run.|5|15|None");
            sb.AppendLine("friendly_betrayal|Friendly Betrayal|Cause an enemy to kill another enemy via friendly fire.|1|20|None");
            sb.AppendLine("trap_master|Trap Master|Damage and stun 5 enemies using Bear Traps across all runs.|5|20|None");
            sb.AppendLine("combo_striker|Combo Striker|Play 3 or more equipped cards in a single turn.|3|25|None");
            sb.AppendLine("untouchable|Untouchable|Clear an entire wave without taking any damage.|1|25|None");
            sb.AppendLine("well_equipped|Well Equipped|Unlock at least 3 permanent upgrades or weapons in the Menu.|3|30|None");
            sb.AppendLine("veteran_slayer|Veteran Slayer|Defeat 50 enemies in total across all runs.|50|35|None");
            sb.AppendLine("no_second_chances|No Second Chances|Win a full 10-wave run without using any Rerolls.|1|40|None");
            sb.AppendLine("kingslayer|Kingslayer|Defeat the Viking Chief (Wave 10 Boss).|1|50|Shieldmaiden");
            return sb.ToString();
        }
    }

    public static class DataParser
    {
        private static readonly HashSet<string> ValidRewardTypes = new(StringComparer.Ordinal)
        {
            "None",
            "PickCard",
            "Upgrade",
            "Victory"
        };

        public static GameDatabase LoadAll(string dataDir, string logsDir)
        {
            DefaultDataInitializer.EnsureDefaultFiles(dataDir);
            Directory.CreateDirectory(logsDir);

            GameDatabase db = new();
            List<string> errors = [];

            ParsePipeRecords(Path.Combine(dataDir, "weapons.txt"), "weapons.txt", 10, errors, (lineNum, parts) => ParseWeaponRecord(lineNum, parts, db, errors));
            ParsePipeRecords(Path.Combine(dataDir, "enemies.txt"), "enemies.txt", 9, errors, (lineNum, parts) => ParseEnemyRecord(lineNum, parts, db, errors));
            HashSet<int> seenWaves = [];
            ParsePipeRecords(Path.Combine(dataDir, "waves.txt"), "waves.txt", 3, errors, (lineNum, parts) => ParseWaveRecord(lineNum, parts, db, seenWaves, errors));
            ParsePipeRecords(Path.Combine(dataDir, "upgrades.txt"), "upgrades.txt", 5, errors, (lineNum, parts) => ParseUpgradeRecord(lineNum, parts, db, errors));
            ParsePipeRecords(Path.Combine(dataDir, "characters.txt"), "characters.txt", 8, errors, (lineNum, parts) => ParseCharacterRecord(lineNum, parts, db, errors));
            ParsePipeRecords(Path.Combine(dataDir, "achievements.txt"), "achievements.txt", 6, errors, (lineNum, parts) => ParseAchievementRecord(lineNum, parts, db, errors));

            db.WeaponList = db.WeaponList.OrderBy(w => w.UnlockOrder).ThenBy(w => w.Name).ToList();
            db.UpgradeList = db.UpgradeList.OrderBy(u => u.UnlockOrder).ToList();
            db.Waves = db.Waves.OrderBy(w => w.WaveNumber).ToList();

            WriteValidationLog(logsDir, db, errors);
            return db;
        }

        private static void WriteValidationLog(string logsDir, GameDatabase db, List<string> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }
            string logPath = Path.Combine(logsDir, "data_validation.log");
            StringBuilder sb = new();
            sb.AppendLine("=== Data Validation Run: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " ===");
            foreach (string err in errors)
            {
                sb.AppendLine(err);
                db.LoadWarnings.Add(err);
            }
            File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
        }

        private static void ParsePipeRecords(string path, string fileName, int expectedColumns, List<string> errors, Action<int, string[]> handleRecord)
        {
            if (!File.Exists(path))
            {
                errors.Add(fileName + ": File missing.");
                return;
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                string raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith('#'))
                {
                    continue;
                }
                string[] parts = raw.Split('|');
                if (parts.Length != expectedColumns)
                {
                    errors.Add(FormatErr(fileName, lineNum, "Expected " + expectedColumns + " columns, found " + parts.Length + "."));
                    continue;
                }
                handleRecord(lineNum, parts);
            }
        }

        private static void ParseWeaponRecord(int lineNum, string[] parts, GameDatabase db, List<string> errors)
        {
            string id = parts[0].Trim();
            string name = parts[1].Trim();
            string desc = parts[9].Trim();
            if (!ValidateIdAndName("weapons.txt", lineNum, id, name, db.Weapons.ContainsKey(id), errors))
            {
                return;
            }
            if (!TryParseEnum("weapons.txt", lineNum, "category", parts[2], out CardCategory cat, errors) ||
                !TryParseEnum("weapons.txt", lineNum, "aimMode", parts[3], out AimMode aim, errors) ||
                !TryParseBoundedInt("weapons.txt", lineNum, "damage", parts[4], 0, 50, out int dmg, errors) ||
                !TryParseBoundedInt("weapons.txt", lineNum, "cooldown", parts[5], 1, 20, out int cd, errors) ||
                !TryParseBool("weapons.txt", lineNum, "defaultUnlocked", parts[6], out bool defUnlocked, errors) ||
                !TryParseBoundedInt("weapons.txt", lineNum, "unlockOrder", parts[7], 0, 100, out int order, errors) ||
                !TryParseBoundedInt("weapons.txt", lineNum, "unlockCost", parts[8], 0, 9999, out int cost, errors))
            {
                return;
            }
            if (string.IsNullOrEmpty(desc))
            {
                errors.Add(FormatErr("weapons.txt", lineNum, "Empty description."));
                return;
            }

            WeaponDef w = new()
            {
                Id = id,
                Name = name,
                Category = cat,
                AimMode = aim,
                BaseDamage = dmg,
                BaseCooldown = cd,
                DefaultUnlocked = defUnlocked,
                UnlockOrder = order,
                UnlockCost = cost,
                Description = desc
            };
            db.Weapons[id] = w;
            db.WeaponList.Add(w);
        }

        private static void ParseEnemyRecord(int lineNum, string[] parts, GameDatabase db, List<string> errors)
        {
            string id = parts[0].Trim();
            string name = parts[1].Trim();
            if (!ValidateIdAndName("enemies.txt", lineNum, id, name, db.Enemies.ContainsKey(id), errors))
            {
                return;
            }
            if (!TryParseBoundedInt("enemies.txt", lineNum, "maxHp", parts[2], 1, 500, out int hp, errors) ||
                !TryParseBoundedInt("enemies.txt", lineNum, "damage", parts[3], 0, 50, out int dmg, errors) ||
                !TryParseBoundedInt("enemies.txt", lineNum, "coinDrop", parts[4], 0, 500, out int coins, errors) ||
                !TryParseBool("enemies.txt", lineNum, "isBoss", parts[5], out bool isBoss, errors) ||
                !TryParseBool("enemies.txt", lineNum, "spawnNearEdge", parts[6], out bool edge, errors))
            {
                return;
            }

            db.Enemies[id] = new EnemyDef
            {
                Id = id,
                Name = name,
                MaxHp = hp,
                Damage = dmg,
                CoinDrop = coins,
                IsBoss = isBoss,
                SpawnNearEdge = edge,
                WeaponName = parts[7].Trim(),
                Description = parts[8].Trim()
            };
        }

        private static void ParseWaveRecord(int lineNum, string[] parts, GameDatabase db, HashSet<int> seenWaves, List<string> errors)
        {
            if (!TryParseBoundedInt("waves.txt", lineNum, "waveNumber", parts[0], 1, 50, out int waveNum, errors))
            {
                return;
            }
            if (!seenWaves.Add(waveNum))
            {
                errors.Add(FormatErr("waves.txt", lineNum, "Duplicate waveNumber " + waveNum + "."));
                return;
            }
            List<string> validEnemyIds = ParseWaveEnemyList(lineNum, parts[1], db, errors);
            if (validEnemyIds.Count == 0)
            {
                errors.Add(FormatErr("waves.txt", lineNum, "Wave has no valid enemies."));
                return;
            }
            string reward = parts[2].Trim();
            if (!ValidRewardTypes.Contains(reward))
            {
                errors.Add(FormatErr("waves.txt", lineNum, "Invalid rewardAfter '" + reward + "'."));
                return;
            }
            db.Waves.Add(new WaveDef
            {
                WaveNumber = waveNum,
                EnemyIds = validEnemyIds,
                RewardAfter = reward
            });
        }

        private static List<string> ParseWaveEnemyList(int lineNum, string rawList, GameDatabase db, List<string> errors)
        {
            List<string> validEnemyIds = [];
            foreach (string tok in rawList.Split(','))
            {
                string eid = tok.Trim();
                if (string.IsNullOrEmpty(eid))
                {
                    continue;
                }
                if (!db.Enemies.ContainsKey(eid))
                {
                    errors.Add(FormatErr("waves.txt", lineNum, "References unknown enemy id '" + eid + "'."));
                }
                else
                {
                    validEnemyIds.Add(eid);
                }
            }
            return validEnemyIds;
        }

        private static void ParseUpgradeRecord(int lineNum, string[] parts, GameDatabase db, List<string> errors)
        {
            string id = parts[0].Trim();
            string name = parts[1].Trim();
            if (!ValidateIdAndName("upgrades.txt", lineNum, id, name, db.Upgrades.ContainsKey(id), errors))
            {
                return;
            }
            if (!TryParseBoundedInt("upgrades.txt", lineNum, "unlockOrder", parts[2], 1, 100, out int order, errors) ||
                !TryParseBoundedInt("upgrades.txt", lineNum, "cost", parts[3], 0, 9999, out int cost, errors))
            {
                return;
            }
            UpgradeDef u = new()
            {
                Id = id,
                Name = name,
                UnlockOrder = order,
                Cost = cost,
                Description = parts[4].Trim()
            };
            db.Upgrades[id] = u;
            db.UpgradeList.Add(u);
        }

        private static void ParseCharacterRecord(int lineNum, string[] parts, GameDatabase db, List<string> errors)
        {
            string id = parts[0].Trim();
            string name = parts[1].Trim();
            if (!ValidateIdAndName("characters.txt", lineNum, id, name, db.Characters.ContainsKey(id), errors))
            {
                return;
            }
            if (!TryParseBoundedInt("characters.txt", lineNum, "baseHp", parts[2], 1, 50, out int hp, errors) ||
                !TryParseBoundedInt("characters.txt", lineNum, "specialCooldown", parts[4], 1, 20, out int cd, errors) ||
                !TryParseBool("characters.txt", lineNum, "defaultUnlocked", parts[5], out bool defUnlocked, errors))
            {
                return;
            }
            CharacterDef c = new()
            {
                Id = id,
                Name = name,
                BaseHp = hp,
                SpecialName = parts[3].Trim(),
                SpecialCooldown = cd,
                DefaultUnlocked = defUnlocked,
                UnlockConditionText = parts[6].Trim(),
                SpecialDescription = parts[7].Trim()
            };
            db.Characters[id] = c;
            db.CharacterList.Add(c);
        }

        private static void ParseAchievementRecord(int lineNum, string[] parts, GameDatabase db, List<string> errors)
        {
            string id = parts[0].Trim();
            string title = parts[1].Trim();
            if (!ValidateIdAndName("achievements.txt", lineNum, id, title, db.Achievements.ContainsKey(id), errors))
            {
                return;
            }
            if (!TryParseBoundedInt("achievements.txt", lineNum, "targetValue", parts[3], 1, 10000, out int target, errors) ||
                !TryParseBoundedInt("achievements.txt", lineNum, "coinReward", parts[4], 0, 5000, out int reward, errors))
            {
                return;
            }
            AchievementDef a = new()
            {
                Id = id,
                Title = title,
                Description = parts[2].Trim(),
                TargetValue = target,
                CoinReward = reward,
                SpecialUnlock = parts[5].Trim()
            };
            db.Achievements[id] = a;
            db.AchievementList.Add(a);
        }

        private static bool ValidateIdAndName(string fileName, int lineNum, string id, string name, bool isDuplicate, List<string> errors)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
            {
                errors.Add(FormatErr(fileName, lineNum, "Empty id or name."));
                return false;
            }
            if (isDuplicate)
            {
                errors.Add(FormatErr(fileName, lineNum, "Duplicate id '" + id + "'."));
                return false;
            }
            return true;
        }

        private static bool TryParseBoundedInt(string fileName, int lineNum, string fieldName, string raw, int min, int max, out int value, List<string> errors)
        {
            if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min && value <= max)
            {
                return true;
            }
            errors.Add(FormatErr(fileName, lineNum, "Invalid " + fieldName + " '" + raw + "' (range " + min + ".." + max + ")."));
            return false;
        }

        private static bool TryParseBool(string fileName, int lineNum, string fieldName, string raw, out bool value, List<string> errors)
        {
            if (bool.TryParse(raw.Trim(), out value))
            {
                return true;
            }
            errors.Add(FormatErr(fileName, lineNum, "Invalid " + fieldName + " boolean '" + raw + "'."));
            return false;
        }

        private static bool TryParseEnum<TEnum>(string fileName, int lineNum, string fieldName, string raw, out TEnum value, List<string> errors) where TEnum : struct
        {
            if (Enum.TryParse(raw.Trim(), true, out value))
            {
                return true;
            }
            errors.Add(FormatErr(fileName, lineNum, "Invalid " + fieldName + " '" + raw + "'."));
            return false;
        }

        private static string FormatErr(string fileName, int lineNum, string message)
        {
            return fileName + " line " + lineNum.ToString(CultureInfo.InvariantCulture) + ": " + message;
        }
    }

    public class CombatLogger
    {
        private readonly string _logsDir;
        private string _currentLogFile = string.Empty;
        public List<CombatLogEntry> RecentEntries { get; } = [];

        public CombatLogger(string logsDir)
        {
            _logsDir = logsDir;
            Directory.CreateDirectory(_logsDir);
        }

        public void StartNewRunLog(string characterName)
        {
            RecentEntries.Clear();
            CreateLogFile();
            Log("New run started as " + characterName + ".", Color.FromArgb(140, 210, 255));
        }

        public void ResumeRunLog(string characterName, int wave, int turn)
        {
            RecentEntries.Clear();
            CreateLogFile();
            Log("Resumed saved run as " + characterName + " (Wave " + wave + ", Turn " + turn + ").", Color.FromArgb(140, 210, 255));
        }

        private void CreateLogFile()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _currentLogFile = Path.Combine(_logsDir, "combat_log_" + stamp + ".txt");
        }

        public void Log(string message, Color color)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            RecentEntries.Add(new CombatLogEntry
            {
                Timestamp = ts,
                Text = message,
                Color = color
            });
            while (RecentEntries.Count > 8)
            {
                RecentEntries.RemoveAt(0);
            }

            if (string.IsNullOrEmpty(_currentLogFile))
            {
                return;
            }
            try
            {
                File.AppendAllText(_currentLogFile, "[" + ts + "] " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Ignore non-fatal file lock during combat log append
            }
        }
    }

    public class SaveManager
    {
        private static readonly char[] CommaSeparator = [','];
        private static readonly char[] SemicolonSeparator = [';'];

        private readonly string _logsDir;
        private readonly string _profilePath;
        private readonly string _statsPath;
        private readonly string _activeRunPath;

        private sealed class ActiveRunLoadContext
        {
            public bool VersionChecked { get; set; }
            public GamePhase Phase { get; set; } = GamePhase.PlayerTurn;
            public int Wave { get; set; } = 1;
            public int Turn { get; set; } = 1;
            public PlayerState Player { get; } = new() { CharacterId = string.Empty, Hp = 0, MaxHp = 0 };
            public List<CardInstance> Hand { get; } = [];
            public List<CardInstance> Equipped { get; } = [];
            public List<EnemyInstance> Enemies { get; } = [];
            public List<HazardInstance> Hazards { get; } = [];
            public RewardState Reward { get; } = new();
        }

        public SaveManager(string savesDir, string logsDir)
        {
            _logsDir = logsDir;
            Directory.CreateDirectory(savesDir);
            Directory.CreateDirectory(_logsDir);
            _profilePath = Path.Combine(savesDir, "profile.txt");
            _statsPath = Path.Combine(savesDir, "stats.txt");
            _activeRunPath = Path.Combine(savesDir, "active_run.txt");
        }

        public bool HasActiveRunSave()
        {
            return File.Exists(_activeRunPath);
        }

        public void DeleteActiveRunSave()
        {
            try
            {
                if (File.Exists(_activeRunPath))
                {
                    File.Delete(_activeRunPath);
                }
            }
            catch
            {
                // Ignore non-fatal delete error
            }
        }

        public ProfileData LoadProfile(GameDatabase db)
        {
            ProfileData profile = CreateDefaultProfile(db);
            if (!File.Exists(_profilePath))
            {
                SaveProfile(profile);
                return profile;
            }
            try
            {
                foreach ((string key, string val) in ReadKeyValueEntries(_profilePath))
                {
                    ApplyProfileEntry(profile, db, key, val);
                }
            }
            catch (Exception ex)
            {
                LogSaveError("profile.txt load error: " + ex.Message);
            }
            return profile;
        }

        private static ProfileData CreateDefaultProfile(GameDatabase db)
        {
            ProfileData profile = new();
            foreach (WeaponDef w in db.WeaponList.Where(w => w.DefaultUnlocked))
            {
                profile.UnlockedWeapons.Add(w.Id);
            }
            foreach (CharacterDef c in db.CharacterList.Where(c => c.DefaultUnlocked))
            {
                profile.UnlockedCharacters.Add(c.Id);
            }
            foreach (AchievementDef a in db.AchievementList)
            {
                profile.AchievementProgress[a.Id] = 0;
            }
            return profile;
        }

        private static void ApplyProfileEntry(ProfileData profile, GameDatabase db, string key, string val)
        {
            if (key == "Coins" && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int coins))
            {
                profile.Coins = Math.Max(0, coins);
            }
            else if (key == "Area2Unlocked" && bool.TryParse(val, out bool a2))
            {
                profile.Area2Unlocked = a2;
            }
            else if (key == "UnlockedWeapons")
            {
                AddValidIds(val, id => db.Weapons.ContainsKey(id), profile.UnlockedWeapons);
            }
            else if (key == "UnlockedUpgrades")
            {
                AddValidIds(val, id => db.Upgrades.ContainsKey(id), profile.UnlockedUpgrades);
            }
            else if (key == "UnlockedCharacters")
            {
                AddValidIds(val, id => db.Characters.ContainsKey(id), profile.UnlockedCharacters);
            }
            else if (key == "CompletedAchievements")
            {
                AddValidIds(val, id => db.Achievements.ContainsKey(id), profile.CompletedAchievements);
            }
            else if (key.StartsWith("AchProg_", StringComparison.Ordinal))
            {
                string aid = key["AchProg_".Length..];
                if (db.Achievements.ContainsKey(aid) && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pv))
                {
                    profile.AchievementProgress[aid] = Math.Max(0, pv);
                }
            }
        }

        private static void AddValidIds(string csv, Func<string, bool> predicate, HashSet<string> target)
        {
            foreach (string raw in csv.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string id = raw.Trim();
                if (predicate(id))
                {
                    target.Add(id);
                }
            }
        }

        public void SaveProfile(ProfileData profile)
        {
            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards Meta-Progression Profile");
            sb.AppendLine("Coins=" + profile.Coins.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Area2Unlocked=" + profile.Area2Unlocked.ToString());
            sb.AppendLine("UnlockedWeapons=" + string.Join(",", profile.UnlockedWeapons.OrderBy(x => x)));
            sb.AppendLine("UnlockedUpgrades=" + string.Join(",", profile.UnlockedUpgrades.OrderBy(x => x)));
            sb.AppendLine("UnlockedCharacters=" + string.Join(",", profile.UnlockedCharacters.OrderBy(x => x)));
            sb.AppendLine("CompletedAchievements=" + string.Join(",", profile.CompletedAchievements.OrderBy(x => x)));
            foreach (KeyValuePair<string, int> kv in profile.AchievementProgress.OrderBy(x => x.Key))
            {
                sb.AppendLine("AchProg_" + kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            File.WriteAllText(_profilePath, sb.ToString(), Encoding.UTF8);
        }

        public StatsData LoadStats(GameDatabase db)
        {
            StatsData stats = new();
            foreach (string eid in db.Enemies.Keys)
            {
                stats.EnemiesDefeatedByType[eid] = 0;
            }
            if (!File.Exists(_statsPath))
            {
                SaveStats(stats);
                return stats;
            }
            try
            {
                foreach ((string key, string val) in ReadKeyValueEntries(_statsPath))
                {
                    if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num) && num >= 0)
                    {
                        ApplyStatEntry(stats, key, num);
                    }
                }
            }
            catch (Exception ex)
            {
                LogSaveError("stats.txt load error: " + ex.Message);
            }
            return stats;
        }

        private static void ApplyStatEntry(StatsData stats, string key, int num)
        {
            switch (key)
            {
                case "TotalRunsPlayed":
                    stats.TotalRunsPlayed = num;
                    break;
                case "RunsWon":
                    stats.RunsWon = num;
                    break;
                case "HighestWaveReached":
                    stats.HighestWaveReached = num;
                    break;
                case "TotalTurnsTaken":
                    stats.TotalTurnsTaken = num;
                    break;
                case "TotalEnemiesDefeated":
                    stats.TotalEnemiesDefeated = num;
                    break;
                case "TotalFriendlyFireKills":
                    stats.TotalFriendlyFireKills = num;
                    break;
                case "TotalDamageDealt":
                    stats.TotalDamageDealt = num;
                    break;
                case "TotalCoinsEarned":
                    stats.TotalCoinsEarned = num;
                    break;
                case "TotalCardsPlayed":
                    stats.TotalCardsPlayed = num;
                    break;
                case "BearTrapStunsTriggered":
                    stats.BearTrapStunsTriggered = num;
                    break;
                default:
                    if (key.StartsWith("EnemyKill_", StringComparison.Ordinal))
                    {
                        stats.EnemiesDefeatedByType[key["EnemyKill_".Length..]] = num;
                    }
                    else if (key.StartsWith("WeaponUse_", StringComparison.Ordinal))
                    {
                        stats.WeaponUsageCounts[key["WeaponUse_".Length..]] = num;
                    }
                    break;
            }
        }

        public void SaveStats(StatsData stats)
        {
            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards Lifetime Statistics");
            sb.AppendLine("TotalRunsPlayed=" + stats.TotalRunsPlayed.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("RunsWon=" + stats.RunsWon.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("HighestWaveReached=" + stats.HighestWaveReached.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TotalTurnsTaken=" + stats.TotalTurnsTaken.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TotalEnemiesDefeated=" + stats.TotalEnemiesDefeated.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TotalFriendlyFireKills=" + stats.TotalFriendlyFireKills.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TotalDamageDealt=" + stats.TotalDamageDealt.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TotalCoinsEarned=" + stats.TotalCoinsEarned.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TotalCardsPlayed=" + stats.TotalCardsPlayed.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("BearTrapStunsTriggered=" + stats.BearTrapStunsTriggered.ToString(CultureInfo.InvariantCulture));
            foreach (KeyValuePair<string, int> kv in stats.EnemiesDefeatedByType.OrderBy(x => x.Key))
            {
                sb.AppendLine("EnemyKill_" + kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            foreach (KeyValuePair<string, int> kv in stats.WeaponUsageCounts.OrderBy(x => x.Key))
            {
                sb.AppendLine("WeaponUse_" + kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            File.WriteAllText(_statsPath, sb.ToString(), Encoding.UTF8);
        }

        public void SaveActiveRun(GameEngine engine)
        {
            if (engine.Phase == GamePhase.Victory || engine.Phase == GamePhase.Defeat)
            {
                DeleteActiveRunSave();
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine("# Rogue Cards Active Run Save");
            sb.AppendLine("FORMAT_VERSION=1");
            AppendMetaAndPlayer(sb, engine);
            AppendCardsSection(sb, "[HAND]", engine.Hand);
            AppendCardsSection(sb, "[EQUIPPED]", engine.EquippedQueue);
            AppendEnemiesSection(sb, engine.Enemies);
            AppendHazardsSection(sb, engine.Hazards);
            AppendRewardSection(sb, engine.Reward);
            File.WriteAllText(_activeRunPath, sb.ToString(), Encoding.UTF8);
        }

        private static void AppendMetaAndPlayer(StringBuilder sb, GameEngine engine)
        {
            sb.AppendLine("[META]");
            sb.AppendLine("Phase=" + engine.Phase.ToString());
            sb.AppendLine("CurrentWave=" + engine.CurrentWave.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TurnCounter=" + engine.TurnCounter.ToString(CultureInfo.InvariantCulture));

            PlayerState p = engine.Player;
            sb.AppendLine("[PLAYER]");
            sb.AppendLine("CharacterId=" + p.CharacterId);
            sb.AppendLine("Pos=" + p.Pos.X.ToString(CultureInfo.InvariantCulture) + "," + p.Pos.Y.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Hp=" + p.Hp.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("MaxHp=" + p.MaxHp.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("SpecialCooldown=" + p.SpecialCooldown.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("ImmuneTurns=" + p.ImmuneTurns.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("FreeRerollsLeft=" + p.FreeRerollsLeft.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("RerollsUsedThisRun=" + p.RerollsUsedThisRun.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TookDamageThisWave=" + p.TookDamageThisWave.ToString());
            sb.AppendLine("RunCoinsEarned=" + p.RunCoinsEarned.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("RunEnemiesDefeated=" + p.RunEnemiesDefeated.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendCardsSection(StringBuilder sb, string header, List<CardInstance> cards)
        {
            sb.AppendLine(header);
            foreach (CardInstance c in cards)
            {
                sb.AppendLine(SerializeCard(c));
            }
        }

        private static void AppendEnemiesSection(StringBuilder sb, List<EnemyInstance> enemies)
        {
            sb.AppendLine("[ENEMIES]");
            foreach (EnemyInstance e in enemies)
            {
                string locked = string.Join(";", e.LockedTiles.Select(t => t.X.ToString(CultureInfo.InvariantCulture) + "," + t.Y.ToString(CultureInfo.InvariantCulture)));
                sb.AppendLine(
                    e.EnemyId + "|" +
                    e.Pos.X.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.Pos.Y.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.Hp.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.MaxHp.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.State.ToString() + "|" +
                    e.ChargeTurnsRemaining.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.AimDir.X.ToString(CultureInfo.InvariantCulture) + "," + e.AimDir.Y.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.BossCycleIndex.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.PoisonTurns.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.StunTurns.ToString(CultureInfo.InvariantCulture) + "|" +
                    e.IntentLabel.Replace('|', ' ') + "|" +
                    locked
                );
            }
        }

        private static void AppendHazardsSection(StringBuilder sb, List<HazardInstance> hazards)
        {
            sb.AppendLine("[HAZARDS]");
            foreach (HazardInstance h in hazards)
            {
                sb.AppendLine(
                    h.Type.ToString() + "|" +
                    h.Pos.X.ToString(CultureInfo.InvariantCulture) + "|" +
                    h.Pos.Y.ToString(CultureInfo.InvariantCulture) + "|" +
                    h.RemainingTurns.ToString(CultureInfo.InvariantCulture) + "|" +
                    h.IsArmed.ToString() + "|" +
                    h.Damage.ToString(CultureInfo.InvariantCulture) + "|" +
                    h.AppliesPoison.ToString()
                );
            }
        }

        private static void AppendRewardSection(StringBuilder sb, RewardState reward)
        {
            sb.AppendLine("[REWARD]");
            sb.AppendLine("RerollCost=" + reward.RerollCost.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("OfferedWeapons=" + string.Join(",", reward.OfferedWeaponIds));
            sb.AppendLine("OfferedModifiers=" + string.Join(",", reward.OfferedModifiers.Select(m => m.ToString())));
        }

        private static string SerializeCard(CardInstance c)
        {
            return c.WeaponId + "|" +
                   c.CurrentCooldown.ToString(CultureInfo.InvariantCulture) + "|" +
                   c.BonusDamage.ToString(CultureInfo.InvariantCulture) + "|" +
                   c.CooldownModifier.ToString(CultureInfo.InvariantCulture) + "|" +
                   c.HasPoison.ToString();
        }

        public bool TryLoadActiveRun(GameEngine engine, GameDatabase db, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!File.Exists(_activeRunPath))
            {
                errorMessage = "No saved run file found.";
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(_activeRunPath, Encoding.UTF8);
                ActiveRunLoadContext ctx = new();
                string section = string.Empty;

                for (int i = 0; i < lines.Length; i++)
                {
                    int lineNum = i + 1;
                    string raw = lines[i].Trim();
                    if (string.IsNullOrEmpty(raw) || raw.StartsWith('#'))
                    {
                        continue;
                    }
                    if (raw.StartsWith('[') && raw.EndsWith(']'))
                    {
                        section = raw;
                        continue;
                    }
                    if (!ProcessSaveLine(section, raw, db, ctx, out string lineError))
                    {
                        return FailLoad(lineNum, lineError, out errorMessage);
                    }
                }

                if (!ValidateLoadedContext(ctx, out string valError))
                {
                    return FailLoad(0, valError, out errorMessage);
                }
                engine.ApplyLoadedState(ctx.Phase, ctx.Wave, ctx.Turn, ctx.Player, ctx.Hand, ctx.Equipped, ctx.Enemies, ctx.Hazards, ctx.Reward);
                return true;
            }
            catch (Exception ex)
            {
                return FailLoad(0, "Exception while loading save: " + ex.Message, out errorMessage);
            }
        }

        private static bool ProcessSaveLine(string section, string raw, GameDatabase db, ActiveRunLoadContext ctx, out string error)
        {
            error = string.Empty;
            if (section.Length == 0 && raw.StartsWith("FORMAT_VERSION=", StringComparison.Ordinal))
            {
                string v = raw["FORMAT_VERSION=".Length..].Trim();
                if (v != "1")
                {
                    error = "Incompatible save format version '" + v + "'.";
                    return false;
                }
                ctx.VersionChecked = true;
                return true;
            }
            return section switch
            {
                "[META]" => ParseMetaLine(raw, ctx, out error),
                "[PLAYER]" => ParsePlayerLine(raw, db, ctx.Player, out error),
                "[HAND]" => ParseCardIntoList(raw, db, ctx.Hand, out error),
                "[EQUIPPED]" => ParseCardIntoList(raw, db, ctx.Equipped, out error),
                "[ENEMIES]" => ParseEnemyLine(raw, db, ctx.Enemies, out error),
                "[HAZARDS]" => ParseHazardLine(raw, ctx.Hazards, out error),
                "[REWARD]" => ParseRewardLine(raw, db, ctx.Reward, out error),
                _ => true
            };
        }

        private static bool ValidateLoadedContext(ActiveRunLoadContext ctx, out string error)
        {
            error = string.Empty;
            if (!ctx.VersionChecked || string.IsNullOrEmpty(ctx.Player.CharacterId) || ctx.Player.Hp <= 0 || ctx.Player.MaxHp <= 0)
            {
                error = "Save file missing required header or player data.";
                return false;
            }
            if (ctx.Player.Hp > ctx.Player.MaxHp)
            {
                ctx.Player.Hp = ctx.Player.MaxHp;
            }
            return true;
        }

        private static bool ParseMetaLine(string raw, ActiveRunLoadContext ctx, out string error)
        {
            error = string.Empty;
            if (!TrySplitKv(raw, out string k, out string v))
            {
                error = "Malformed [META] entry.";
                return false;
            }
            if (k == "Phase")
            {
                if (!Enum.TryParse(v, out GamePhase phase) || phase == GamePhase.Victory || phase == GamePhase.Defeat)
                {
                    error = "Invalid Phase '" + v + "'.";
                    return false;
                }
                ctx.Phase = phase == GamePhase.AimingCard ? GamePhase.PlayerTurn : phase;
            }
            else if (k == "CurrentWave")
            {
                if (!TryParseIntRange(v, 0, 10, out int w))
                {
                    error = "Invalid CurrentWave '" + v + "'.";
                    return false;
                }
                ctx.Wave = w;
            }
            else if (k == "TurnCounter")
            {
                if (!TryParseIntRange(v, 0, 99999, out int t))
                {
                    error = "Invalid TurnCounter '" + v + "'.";
                    return false;
                }
                ctx.Turn = t;
            }
            return true;
        }

        private static bool ParsePlayerLine(string raw, GameDatabase db, PlayerState player, out string error)
        {
            error = string.Empty;
            if (!TrySplitKv(raw, out string k, out string v))
            {
                error = "Malformed [PLAYER] entry.";
                return false;
            }
            switch (k)
            {
                case "CharacterId":
                    if (!db.Characters.ContainsKey(v))
                    {
                        error = "Unknown CharacterId '" + v + "'.";
                        return false;
                    }
                    player.CharacterId = v;
                    return true;
                case "Pos":
                    if (!TryParsePos(v, out GridPos pos))
                    {
                        error = "Invalid player Pos '" + v + "'.";
                        return false;
                    }
                    player.Pos = pos;
                    return true;
                case "Hp":
                    return AssignIntRange(v, 1, 99, val => player.Hp = val, "Invalid player Hp.", out error);
                case "MaxHp":
                    return AssignIntRange(v, 1, 99, val => player.MaxHp = val, "Invalid player MaxHp.", out error);
                case "SpecialCooldown":
                    return AssignIntRange(v, 0, 20, val => player.SpecialCooldown = val, "Invalid SpecialCooldown.", out error);
                case "ImmuneTurns":
                    return AssignIntRange(v, 0, 10, val => player.ImmuneTurns = val, "Invalid ImmuneTurns.", out error);
                case "FreeRerollsLeft":
                    return AssignIntRange(v, 0, 10, val => player.FreeRerollsLeft = val, "Invalid FreeRerollsLeft.", out error);
                case "RerollsUsedThisRun":
                    return AssignIntRange(v, 0, 999, val => player.RerollsUsedThisRun = val, "Invalid RerollsUsedThisRun.", out error);
                case "RunCoinsEarned":
                    return AssignIntRange(v, 0, 99999, val => player.RunCoinsEarned = val, "Invalid RunCoinsEarned.", out error);
                case "RunEnemiesDefeated":
                    return AssignIntRange(v, 0, 99999, val => player.RunEnemiesDefeated = val, "Invalid RunEnemiesDefeated.", out error);
                case "TookDamageThisWave":
                    if (!bool.TryParse(v, out bool td))
                    {
                        error = "Invalid TookDamageThisWave.";
                        return false;
                    }
                    player.TookDamageThisWave = td;
                    return true;
                default:
                    return true;
            }
        }

        private static bool ParseCardIntoList(string raw, GameDatabase db, List<CardInstance> list, out string error)
        {
            if (!TryDeserializeCard(raw, db, out CardInstance card, out error))
            {
                return false;
            }
            list.Add(card);
            return true;
        }

        private static bool ParseEnemyLine(string raw, GameDatabase db, List<EnemyInstance> enemies, out string error)
        {
            error = string.Empty;
            string[] parts = raw.Split('|');
            if (parts.Length != 13)
            {
                error = "Expected 13 columns in [ENEMIES], found " + parts.Length + ".";
                return false;
            }
            string eid = parts[0].Trim();
            if (!db.Enemies.ContainsKey(eid))
            {
                error = "Unknown enemy id '" + eid + "'.";
                return false;
            }
            if (!TryParseIntRange(parts[1], 0, 4, out int ex) || !TryParseIntRange(parts[2], 0, 4, out int ey))
            {
                error = "Enemy position out of 5x5 bounds.";
                return false;
            }
            if (!TryParseIntRange(parts[3], 1, 500, out int ehp) || !TryParseIntRange(parts[4], 1, 500, out int emhp))
            {
                error = "Invalid enemy HP values.";
                return false;
            }
            if (!Enum.TryParse(parts[5], out EnemyAIState state) ||
                !TryParseIntRange(parts[6], 0, 10, out int cRem) ||
                !GridPos.TryParse(parts[7], out GridPos aimDir, false) ||
                !TryParseIntRange(parts[8], 0, 5, out int bci) ||
                !TryParseIntRange(parts[9], 0, 20, out int pt) ||
                !TryParseIntRange(parts[10], 0, 10, out int st))
            {
                error = "Invalid enemy state or numeric field.";
                return false;
            }
            if (!TryParseLockedTiles(parts[12], out List<GridPos> locked, out error))
            {
                return false;
            }

            enemies.Add(new EnemyInstance
            {
                EnemyId = eid,
                Pos = new GridPos(ex, ey),
                Hp = ehp,
                MaxHp = emhp,
                State = state,
                ChargeTurnsRemaining = cRem,
                AimDir = aimDir,
                BossCycleIndex = bci,
                PoisonTurns = pt,
                StunTurns = st,
                IntentLabel = parts[11].Trim(),
                LockedTiles = locked
            });
            return true;
        }

        private static bool TryParseLockedTiles(string rawTiles, out List<GridPos> locked, out string error)
        {
            locked = [];
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(rawTiles))
            {
                return true;
            }
            foreach (string tk in rawTiles.Split(SemicolonSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TryParsePos(tk, out GridPos lp))
                {
                    error = "Invalid locked tile '" + tk + "'.";
                    return false;
                }
                locked.Add(lp);
            }
            return true;
        }

        private static bool ParseHazardLine(string raw, List<HazardInstance> hazards, out string error)
        {
            error = string.Empty;
            string[] parts = raw.Split('|');
            if (parts.Length != 7)
            {
                error = "Expected 7 columns in [HAZARDS], found " + parts.Length + ".";
                return false;
            }
            if (!Enum.TryParse(parts[0], out HazardType ht) ||
                !TryParseIntRange(parts[1], 0, 4, out int hx) ||
                !TryParseIntRange(parts[2], 0, 4, out int hy) ||
                !TryParseIntRange(parts[3], 0, 999, out int rem) ||
                !bool.TryParse(parts[4], out bool armed) ||
                !TryParseIntRange(parts[5], 0, 50, out int hdmg) ||
                !bool.TryParse(parts[6], out bool hpois))
            {
                error = "Invalid field in [HAZARDS].";
                return false;
            }
            hazards.Add(new HazardInstance
            {
                Type = ht,
                Pos = new GridPos(hx, hy),
                RemainingTurns = rem,
                IsArmed = armed,
                Damage = hdmg,
                AppliesPoison = hpois
            });
            return true;
        }

        private static bool ParseRewardLine(string raw, GameDatabase db, RewardState reward, out string error)
        {
            error = string.Empty;
            if (!TrySplitKv(raw, out string k, out string v))
            {
                error = "Malformed [REWARD] entry.";
                return false;
            }
            if (k == "RerollCost")
            {
                return AssignIntRange(v, 1, 10000, val => reward.RerollCost = val, "Invalid RerollCost.", out error);
            }
            if (k == "OfferedWeapons" && !string.IsNullOrEmpty(v))
            {
                foreach (string wid in v.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    string id = wid.Trim();
                    if (!db.Weapons.ContainsKey(id))
                    {
                        error = "Unknown offered weapon '" + id + "'.";
                        return false;
                    }
                    reward.OfferedWeaponIds.Add(id);
                }
            }
            else if (k == "OfferedModifiers" && !string.IsNullOrEmpty(v))
            {
                foreach (string mstr in v.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Enum.TryParse(mstr.Trim(), out ModifierType mt))
                    {
                        error = "Unknown offered modifier '" + mstr + "'.";
                        return false;
                    }
                    reward.OfferedModifiers.Add(mt);
                }
            }
            return true;
        }

        private static bool TryDeserializeCard(string raw, GameDatabase db, out CardInstance card, out string err)
        {
            card = new CardInstance();
            err = string.Empty;
            string[] parts = raw.Split('|');
            if (parts.Length != 5)
            {
                err = "Expected 5 columns for card, found " + parts.Length + ".";
                return false;
            }
            string wid = parts[0].Trim();
            if (!db.Weapons.ContainsKey(wid))
            {
                err = "Unknown weapon id '" + wid + "'.";
                return false;
            }
            if (!TryParseIntRange(parts[1], 0, 30, out int curCd) ||
                !TryParseIntRange(parts[2], 0, 30, out int bDmg) ||
                !TryParseIntRange(parts[3], -15, 15, out int cdMod) ||
                !bool.TryParse(parts[4], out bool hasPoison))
            {
                err = "Invalid card attribute range or boolean.";
                return false;
            }
            card = new CardInstance
            {
                WeaponId = wid,
                CurrentCooldown = curCd,
                BonusDamage = bDmg,
                CooldownModifier = cdMod,
                HasPoison = hasPoison
            };
            return true;
        }

        private static IEnumerable<(string Key, string Value)> ReadKeyValueEntries(string path)
        {
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                string raw = line.Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith('#'))
                {
                    continue;
                }
                if (TrySplitKv(raw, out string k, out string v))
                {
                    yield return (k, v);
                }
            }
        }

        private static bool TrySplitKv(string raw, out string key, out string val)
        {
            key = string.Empty;
            val = string.Empty;
            int eq = raw.IndexOf('=');
            if (eq <= 0)
            {
                return false;
            }
            key = raw[..eq].Trim();
            val = raw[(eq + 1)..].Trim();
            return true;
        }

        private static bool TryParsePos(string raw, out GridPos pos)
        {
            return GridPos.TryParse(raw, out pos, true);
        }

        private static bool TryParseIntRange(string raw, int min, int max, out int value)
        {
            return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
        }

        private static bool AssignIntRange(string raw, int min, int max, Action<int> assign, string errMsg, out string error)
        {
            if (TryParseIntRange(raw, min, max, out int value))
            {
                assign(value);
                error = string.Empty;
                return true;
            }
            error = errMsg;
            return false;
        }

        private bool FailLoad(int lineNum, string msg, out string errorMessage)
        {
            string full = lineNum > 0
                ? "Corrupted save file (line " + lineNum.ToString(CultureInfo.InvariantCulture) + "): " + msg
                : "Corrupted save file: " + msg;
            LogSaveError(full);
            errorMessage = full;
            return false;
        }

        private void LogSaveError(string msg)
        {
            try
            {
                string logPath = Path.Combine(_logsDir, "save_errors.log");
                File.AppendAllText(logPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    public class GameEngine
    {
        private static readonly GridPos[] OrthogonalDirs =
        [
            new GridPos(0, -1),
            new GridPos(0, 1),
            new GridPos(-1, 0),
            new GridPos(1, 0)
        ];

        private static readonly GridPos[] DiagonalOffsets =
        [
            new GridPos(-1, -1),
            new GridPos(1, -1),
            new GridPos(-1, 1),
            new GridPos(1, 1)
        ];

        private static readonly string[] BossMinionIds =
        [
            "pawn",
            "archer",
            "spearman",
            "shaman"
        ];

        private static readonly ModifierType[] AllModifiers =
        [
            ModifierType.DamagePlus1,
            ModifierType.DamagePlus2,
            ModifierType.CooldownMinus1,
            ModifierType.CooldownMinus2,
            ModifierType.Poison
        ];

        public GameDatabase Db { get; }
        public ProfileData Profile { get; }
        public StatsData Stats { get; }
        public SaveManager Saves { get; }
        public CombatLogger Logger { get; }

        public GamePhase Phase { get; private set; }
        public int CurrentWave { get; private set; }
        public int TurnCounter { get; private set; }
        public PlayerState Player { get; private set; } = new();
        public List<CardInstance> Hand { get; private set; } = [];
        public List<CardInstance> EquippedQueue { get; private set; } = [];
        public List<EnemyInstance> Enemies { get; private set; } = [];
        public List<HazardInstance> Hazards { get; private set; } = [];
        public RewardState Reward { get; private set; } = new();

        public int ExecutingCardIndex { get; private set; } = -1;
        public int CardsPlayedThisTurn { get; private set; }
        public GridPos CurrentAimDir { get; private set; } = new GridPos(0, -1);
        public List<GridPos> CurrentPreviewTiles { get; private set; } = [];

        public List<FloatingText> FloatingTexts { get; } = [];
        public Queue<ToastMessage> ToastQueue { get; } = new();
        public ToastMessage ActiveToast { get; private set; }
        public string MenuNotification { get; set; } = string.Empty;

        public GameEngine(GameDatabase db, ProfileData profile, StatsData stats, SaveManager saves, CombatLogger logger)
        {
            Db = db;
            Profile = profile;
            Stats = stats;
            Saves = saves;
            Logger = logger;

            if (Db.LoadWarnings.Count > 0)
            {
                MenuNotification = "Data validation logged " + Db.LoadWarnings.Count.ToString(CultureInfo.InvariantCulture) + " warning(s) to Logs/data_validation.log.";
            }
        }

        public void StartNewRun(string characterId)
        {
            string resolvedId = Db.Characters.ContainsKey(characterId) ? characterId : "viking";
            CharacterDef cdef = Db.GetCharacterOrFallback(resolvedId);
            int maxHp = cdef.BaseHp + (Profile.UnlockedUpgrades.Contains("durability_1") ? 1 : 0);

            Player = new PlayerState
            {
                CharacterId = cdef.Id,
                Pos = new GridPos(2, 2),
                Hp = maxHp,
                MaxHp = maxHp,
                FreeRerollsLeft = Profile.UnlockedUpgrades.Contains("charisma") ? 1 : 0
            };

            Hand.Clear();
            EquippedQueue.Clear();
            Enemies.Clear();
            Hazards.Clear();
            FloatingTexts.Clear();
            CurrentWave = 0;
            TurnCounter = 1;
            ExecutingCardIndex = -1;
            CardsPlayedThisTurn = 0;

            Stats.TotalRunsPlayed += 1;
            Saves.SaveStats(Stats);
            Logger.StartNewRunLog(cdef.Name);

            PrepareStartingDraft();
            Phase = GamePhase.StartingDraft;
            Saves.SaveActiveRun(this);
        }

        public void ApplyLoadedState(
            GamePhase phase,
            int wave,
            int turn,
            PlayerState player,
            List<CardInstance> hand,
            List<CardInstance> equipped,
            List<EnemyInstance> enemies,
            List<HazardInstance> hazards,
            RewardState reward)
        {
            Phase = phase;
            CurrentWave = wave;
            TurnCounter = turn;
            Player = player;
            Hand = hand;
            EquippedQueue = equipped;
            Enemies = enemies;
            Hazards = hazards;
            Reward = reward;
            ExecutingCardIndex = -1;
            CardsPlayedThisTurn = 0;
            FloatingTexts.Clear();
            CurrentPreviewTiles.Clear();

            CharacterDef cdef = Db.GetCharacterOrFallback(player.CharacterId);
            Logger.ResumeRunLog(cdef.Name, CurrentWave, TurnCounter);

            if (Phase == GamePhase.StartingDraft && Reward.OfferedWeaponIds.Count < 4)
            {
                PrepareStartingDraft();
            }
        }

        private void PrepareStartingDraft()
        {
            Reward = new RewardState();
            List<string> unlockedPool = Profile.UnlockedWeapons.Where(id => Db.Weapons.ContainsKey(id)).ToList();
            SecureRandom.Shuffle(unlockedPool);
            int takeCount = Math.Min(4, unlockedPool.Count);
            for (int i = 0; i < takeCount; i++)
            {
                Reward.OfferedWeaponIds.Add(unlockedPool[i]);
            }
        }

        public void ToggleStartingDraftCard(int index)
        {
            if (Phase != GamePhase.StartingDraft || index < 0 || index >= Reward.OfferedWeaponIds.Count)
            {
                return;
            }
            if (!Reward.StartingDraftSelectedIndices.Remove(index) && Reward.StartingDraftSelectedIndices.Count < 2)
            {
                Reward.StartingDraftSelectedIndices.Add(index);
            }
        }

        public void ConfirmStartingDraft()
        {
            if (Phase != GamePhase.StartingDraft || Reward.StartingDraftSelectedIndices.Count != 2)
            {
                return;
            }
            Hand.Clear();
            foreach (int idx in Reward.StartingDraftSelectedIndices)
            {
                string wid = Reward.OfferedWeaponIds[idx];
                Hand.Add(new CardInstance { WeaponId = wid });
                Logger.Log("Drafted starting card: " + Db.GetWeaponOrFallback(wid).Name + ".", Color.FromArgb(140, 230, 170));
            }
            Reward = new RewardState();
            SpawnNextWave();
        }

        public void SpawnNextWave()
        {
            CurrentWave += 1;
            Player.TookDamageThisWave = false;
            Hazards.RemoveAll(h => h.Type == HazardType.Poison);

            if (CurrentWave > Stats.HighestWaveReached)
            {
                Stats.HighestWaveReached = CurrentWave;
                Saves.SaveStats(Stats);
            }
            if (CurrentWave >= 5)
            {
                UpdateAchievementMax("wave_survivor", CurrentWave);
            }

            WaveDef waveDef = Db.Waves.FirstOrDefault(w => w.WaveNumber == CurrentWave);
            if (waveDef == null)
            {
                HandleVictory();
                return;
            }

            Enemies.Clear();
            foreach (string eid in waveDef.EnemyIds)
            {
                SpawnEnemy(eid, false);
            }

            Phase = GamePhase.PlayerTurn;
            Logger.Log("--- Wave " + CurrentWave.ToString(CultureInfo.InvariantCulture) + " / 10 begins! (" + Enemies.Count.ToString(CultureInfo.InvariantCulture) + " enemies) ---", Color.FromArgb(255, 215, 95));
            Saves.SaveActiveRun(this);
        }

        private void SpawnEnemy(string enemyId, bool midTurn)
        {
            if (!Db.Enemies.TryGetValue(enemyId, out EnemyDef def))
            {
                return;
            }
            GridPos? freePos = FindFreeSpawnTile(def.SpawnNearEdge);
            if (!freePos.HasValue)
            {
                return;
            }
            EnemyInstance enemy = new()
            {
                EnemyId = enemyId,
                Pos = freePos.Value,
                Hp = def.MaxHp,
                MaxHp = def.MaxHp,
                State = EnemyAIState.Unarmed,
                IntentLabel = "Equipping " + def.WeaponName,
                SpawnedMidTurn = midTurn
            };
            Enemies.Add(enemy);
            Logger.Log(def.Name + " spawned at " + enemy.Pos.ToString() + ".", Color.FromArgb(255, 175, 110));
        }

        private GridPos? FindFreeSpawnTile(bool preferEdge)
        {
            List<GridPos> edgeFree = [];
            List<GridPos> allFree = [];

            for (int y = 0; y < GridPos.GridSize; y++)
            {
                for (int x = 0; x < GridPos.GridSize; x++)
                {
                    GridPos p = new(x, y);
                    if (IsTileOccupied(p))
                    {
                        continue;
                    }
                    allFree.Add(p);
                    if (x == 0 || x == 4 || y == 0 || y == 4)
                    {
                        edgeFree.Add(p);
                    }
                }
            }

            if (preferEdge && edgeFree.Count > 0)
            {
                return edgeFree[SecureRandom.NextInt(edgeFree.Count)];
            }
            return allFree.Count > 0 ? allFree[SecureRandom.NextInt(allFree.Count)] : null;
        }

        public EnemyInstance GetEnemyAt(GridPos pos)
        {
            return Enemies.FirstOrDefault(e => e.Pos == pos && e.Hp > 0);
        }

        public HazardInstance GetHazardAt(GridPos pos, HazardType type)
        {
            return Hazards.FirstOrDefault(h => h.Pos == pos && h.Type == type);
        }

        public bool IsTileOccupied(GridPos pos)
        {
            return Player.Pos == pos || GetEnemyAt(pos) != null;
        }

        public bool TryMovePlayer(int dx, int dy)
        {
            if (Phase != GamePhase.PlayerTurn || Math.Abs(dx) + Math.Abs(dy) != 1)
            {
                return false;
            }
            GridPos target = Player.Pos.Offset(dx, dy);
            if (!target.IsInBounds() || GetEnemyAt(target) != null)
            {
                return false;
            }

            GridPos oldPos = Player.Pos;
            Player.Pos = target;
            ArmBearTrapIfLeft(oldPos);
            Logger.Log("Moved to " + target.ToString() + ".", Color.FromArgb(145, 220, 255));
            EndPlayerTurn();
            return true;
        }

        private void ArmBearTrapIfLeft(GridPos leftPos)
        {
            foreach (HazardInstance h in Hazards)
            {
                if (h.Type == HazardType.BearTrap && h.Pos == leftPos && !h.IsArmed)
                {
                    h.IsArmed = true;
                    Logger.Log("Bear Trap at " + leftPos.ToString() + " is now ARMED!", Color.FromArgb(220, 170, 255));
                }
            }
        }

        public bool TryEquipHandCard(int handIndex)
        {
            if (Phase != GamePhase.PlayerTurn || handIndex < 0 || handIndex >= Hand.Count)
            {
                return false;
            }
            CardInstance card = Hand[handIndex];
            if (card.CurrentCooldown > 0)
            {
                AddFloatingText(Player.Pos, "ON CD (" + card.CurrentCooldown.ToString(CultureInfo.InvariantCulture) + ")", Color.Orange);
                return false;
            }
            Hand.RemoveAt(handIndex);
            EquippedQueue.Add(card);
            Logger.Log("Equipped " + Db.GetWeaponOrFallback(card.WeaponId).Name + " (Slot " + EquippedQueue.Count.ToString(CultureInfo.InvariantCulture) + ").", Color.FromArgb(180, 225, 255));
            EndPlayerTurn();
            return true;
        }

        public void ReorderEquippedCards(int fromIndex, int toIndex)
        {
            if (Phase != GamePhase.PlayerTurn || fromIndex == toIndex)
            {
                return;
            }
            if (fromIndex < 0 || fromIndex >= EquippedQueue.Count || toIndex < 0 || toIndex >= EquippedQueue.Count)
            {
                return;
            }
            CardInstance moved = EquippedQueue[fromIndex];
            EquippedQueue.RemoveAt(fromIndex);
            EquippedQueue.Insert(toIndex, moved);
            Logger.Log("Reordered equipped queue (0 turns).", Color.FromArgb(180, 195, 215));
            Saves.SaveActiveRun(this);
        }

        public void WaitTurn()
        {
            if (Phase != GamePhase.PlayerTurn)
            {
                return;
            }
            if (Profile.UnlockedUpgrades.Contains("tactical_master"))
            {
                ReduceAllActiveCardCooldowns(1);
                Logger.Log("Waited turn (Tactical Master: -1 extra cooldown on active cards).", Color.FromArgb(165, 215, 255));
                AddFloatingText(Player.Pos, "WAIT (-1 CD)", Color.Cyan);
            }
            else
            {
                Logger.Log("Waited turn.", Color.FromArgb(180, 195, 215));
                AddFloatingText(Player.Pos, "WAIT", Color.LightGray);
            }
            EndPlayerTurn();
        }

        public bool TryUseSpecial()
        {
            if (Phase != GamePhase.PlayerTurn)
            {
                return false;
            }
            if (Player.SpecialCooldown > 0)
            {
                AddFloatingText(Player.Pos, "SPECIAL CD (" + Player.SpecialCooldown.ToString(CultureInfo.InvariantCulture) + ")", Color.Violet);
                return false;
            }

            CharacterDef cdef = Db.GetCharacterOrFallback(Player.CharacterId);
            bool hasSpecMaster = Profile.UnlockedUpgrades.Contains("speciality_master");
            Player.SpecialCooldown = cdef.SpecialCooldown;
            Player.SpecialUsedThisTurn = true;

            if (Player.CharacterId == "viking")
            {
                ExecuteVikingScream(hasSpecMaster);
            }
            else
            {
                ExecuteIronBulwark(hasSpecMaster);
            }

            if (!CheckWaveCleared())
            {
                EndPlayerTurn();
            }
            return true;
        }

        private void ExecuteVikingScream(bool hasSpecMaster)
        {
            Logger.Log("Used Viking Scream!", Color.FromArgb(120, 215, 255));
            AddFloatingText(Player.Pos, "VIKING SCREAM!", Color.DeepSkyBlue);
            List<EnemyInstance> adjacent = Enemies
                .Where(e => e.Hp > 0 && Math.Abs(e.Pos.X - Player.Pos.X) <= 1 && Math.Abs(e.Pos.Y - Player.Pos.Y) <= 1)
                .ToList();

            foreach (EnemyInstance e in adjacent)
            {
                int kdx = Math.Sign(e.Pos.X - Player.Pos.X);
                int kdy = Math.Sign(e.Pos.Y - Player.Pos.Y);
                if (hasSpecMaster)
                {
                    DamageEnemy(e, 1, "Viking Scream", false, false);
                }
                if (e.Hp > 0)
                {
                    TryPushEnemy(e, kdx, kdy);
                }
            }
        }

        private void ExecuteIronBulwark(bool hasSpecMaster)
        {
            Player.ImmuneTurns = 1;
            ReduceAllActiveCardCooldowns(1);
            Logger.Log("Used Iron Bulwark! Immune for 1 turn and reduced card cooldowns by 1.", Color.FromArgb(130, 240, 255));
            AddFloatingText(Player.Pos, "IRON BULWARK!", Color.Cyan);
            if (!hasSpecMaster)
            {
                return;
            }
            foreach (GridPos d in OrthogonalDirs)
            {
                EnemyInstance e = GetEnemyAt(Player.Pos.Offset(d.X, d.Y));
                if (e != null)
                {
                    DamageEnemy(e, 1, "Iron Bulwark", false, false);
                }
            }
        }

        private void ReduceAllActiveCardCooldowns(int amount)
        {
            foreach (CardInstance c in Hand.Concat(EquippedQueue))
            {
                if (c.CurrentCooldown > 0)
                {
                    c.CurrentCooldown = Math.Max(0, c.CurrentCooldown - amount);
                }
            }
        }

        public void BeginPlayEquippedQueue()
        {
            if (Phase != GamePhase.PlayerTurn || EquippedQueue.Count == 0)
            {
                return;
            }
            ExecutingCardIndex = 0;
            CardsPlayedThisTurn = 0;
            AdvanceQueueExecution();
        }

        private void AdvanceQueueExecution()
        {
            while (ExecutingCardIndex >= 0 && ExecutingCardIndex < EquippedQueue.Count)
            {
                if (Enemies.All(e => e.Hp <= 0))
                {
                    FinishQueueEarlyOnWaveClear();
                    return;
                }

                CardInstance current = EquippedQueue[ExecutingCardIndex];
                WeaponDef wdef = Db.GetWeaponOrFallback(current.WeaponId);
                if (wdef.AimMode == AimMode.SelfTile)
                {
                    ExecuteCardWithDirection(current, new GridPos(0, 0));
                    ExecutingCardIndex += 1;
                    continue;
                }

                Phase = GamePhase.AimingCard;
                GridPos initialDir = wdef.AimMode == AimMode.Diagonal ? new GridPos(1, -1) : new GridPos(0, -1);
                UpdateAimPreview(initialDir);
                return;
            }

            CompleteEquippedQueueTurn();
        }

        private void FinishQueueEarlyOnWaveClear()
        {
            Hand.AddRange(EquippedQueue);
            EquippedQueue.Clear();
            ExecutingCardIndex = -1;
            CurrentPreviewTiles.Clear();
            CheckComboAchievement();
            CheckWaveCleared();
        }

        private void CompleteEquippedQueueTurn()
        {
            Hand.AddRange(EquippedQueue);
            EquippedQueue.Clear();
            ExecutingCardIndex = -1;
            CurrentPreviewTiles.Clear();
            CheckComboAchievement();

            if (!CheckWaveCleared())
            {
                Phase = GamePhase.PlayerTurn;
                EndPlayerTurn();
            }
        }

        private void CheckComboAchievement()
        {
            if (CardsPlayedThisTurn >= 3)
            {
                UpdateAchievementMax("combo_striker", CardsPlayedThisTurn);
            }
        }

        public CardInstance GetCurrentlyAimingCard()
        {
            if (Phase != GamePhase.AimingCard || ExecutingCardIndex < 0 || ExecutingCardIndex >= EquippedQueue.Count)
            {
                return null;
            }
            return EquippedQueue[ExecutingCardIndex];
        }

        public void UpdateAimPreviewFromHoverTile(GridPos hoveredTile)
        {
            CardInstance card = GetCurrentlyAimingCard();
            if (card == null)
            {
                return;
            }
            int dx = hoveredTile.X - Player.Pos.X;
            int dy = hoveredTile.Y - Player.Pos.Y;
            if (dx == 0 && dy == 0)
            {
                return;
            }

            WeaponDef wdef = Db.GetWeaponOrFallback(card.WeaponId);
            GridPos nextDir = wdef.AimMode == AimMode.Diagonal
                ? new GridPos(dx >= 0 ? 1 : -1, dy >= 0 ? 1 : -1)
                : GetPrimaryOrthogonalDir(Player.Pos, hoveredTile);
            UpdateAimPreview(nextDir);
        }

        public void UpdateAimPreview(GridPos dir)
        {
            CurrentAimDir = dir;
            CardInstance card = GetCurrentlyAimingCard();
            CurrentPreviewTiles = card == null ? [] : ComputeAffectedTilesForCard(card.WeaponId, Player.Pos, dir);
        }

        public List<GridPos> ComputeAffectedTilesForCard(string weaponId, GridPos origin, GridPos dir)
        {
            return weaponId switch
            {
                "viking_axe" or "wooden_club" => ComputeSingleTile(origin.Offset(dir.X, dir.Y)),
                "two_handed_hammer" => ComputeArcTiles(origin, dir),
                "throwing_axe" or "hunting_bow" => ComputePierceTiles(origin, dir, 1, 5),
                "heavy_spear" => ComputePierceTiles(origin, dir, 2, 5),
                "boat_hook" => ComputePierceTiles(origin, dir, 1, 3),
                "diagonal_dash" => ComputeDashTile(origin.Offset(dir.X, dir.Y)),
                "slide" => ComputeSlideTiles(origin, dir),
                _ => []
            };
        }

        private static List<GridPos> ComputeSingleTile(GridPos pos)
        {
            return pos.IsInBounds() ? [pos] : [];
        }

        private List<GridPos> ComputeDashTile(GridPos pos)
        {
            return pos.IsInBounds() && GetEnemyAt(pos) == null ? [pos] : [];
        }

        private static List<GridPos> ComputeArcTiles(GridPos origin, GridPos dir)
        {
            GridPos[] arc =
            [
                origin.Offset(dir.X, dir.Y),
                origin.Offset(dir.X - dir.Y, dir.Y + dir.X),
                origin.Offset(dir.X + dir.Y, dir.Y - dir.X)
            ];
            return arc.Where(p => p.IsInBounds()).ToList();
        }

        private List<GridPos> ComputePierceTiles(GridPos origin, GridPos dir, int maxHits, int maxRange)
        {
            List<GridPos> tiles = [];
            int hits = 0;
            for (int step = 1; step <= maxRange; step++)
            {
                GridPos cur = origin.Offset(dir.X * step, dir.Y * step);
                if (!cur.IsInBounds())
                {
                    break;
                }
                tiles.Add(cur);
                if (GetEnemyAt(cur) != null)
                {
                    hits += 1;
                    if (hits >= maxHits)
                    {
                        break;
                    }
                }
            }
            return tiles;
        }

        private List<GridPos> ComputeSlideTiles(GridPos origin, GridPos dir)
        {
            List<GridPos> tiles = [];
            for (int step = 1; step <= 2; step++)
            {
                GridPos cur = origin.Offset(dir.X * step, dir.Y * step);
                if (!cur.IsInBounds() || GetEnemyAt(cur) != null)
                {
                    break;
                }
                tiles.Add(cur);
            }
            return tiles;
        }

        public void ConfirmAimedCard()
        {
            CardInstance card = GetCurrentlyAimingCard();
            if (card == null)
            {
                return;
            }
            ExecuteCardWithDirection(card, CurrentAimDir);
            ExecutingCardIndex += 1;
            AdvanceQueueExecution();
        }

        public void SkipCurrentAimedCard()
        {
            CardInstance card = GetCurrentlyAimingCard();
            if (card == null)
            {
                return;
            }
            Logger.Log("Skipped " + Db.GetWeaponOrFallback(card.WeaponId).Name + ".", Color.Gray);
            ExecutingCardIndex += 1;
            AdvanceQueueExecution();
        }

        private void ExecuteCardWithDirection(CardInstance card, GridPos dir)
        {
            WeaponDef wdef = Db.GetWeaponOrFallback(card.WeaponId);
            int dmg = card.GetEffectiveDamage(Db, Profile);
            card.CurrentCooldown = card.GetEffectiveMaxCooldown(Db, Profile);
            card.UsedThisTurn = true;
            CardsPlayedThisTurn += 1;
            RecordCardUsage(card.WeaponId);

            if (card.WeaponId == "bear_trap")
            {
                ExecuteBearTrapCard(card, dmg);
            }
            else if (card.WeaponId == "diagonal_dash")
            {
                ExecuteDiagonalDashCard(dir);
            }
            else if (card.WeaponId == "slide")
            {
                ExecuteSlideCard(dir);
            }
            else
            {
                ExecuteAttackCard(card, wdef, dmg, dir);
            }
        }

        private void RecordCardUsage(string weaponId)
        {
            Stats.TotalCardsPlayed += 1;
            Stats.WeaponUsageCounts[weaponId] = Stats.WeaponUsageCounts.GetValueOrDefault(weaponId) + 1;
            Saves.SaveStats(Stats);
        }

        private void ExecuteBearTrapCard(CardInstance card, int dmg)
        {
            Hazards.RemoveAll(h => h.Type == HazardType.BearTrap && h.Pos == Player.Pos);
            Hazards.Add(new HazardInstance
            {
                Type = HazardType.BearTrap,
                Pos = Player.Pos,
                RemainingTurns = 999,
                IsArmed = false,
                Damage = dmg,
                AppliesPoison = card.HasPoison
            });
            Logger.Log("Placed Bear Trap at " + Player.Pos.ToString() + " (arms when you leave).", Color.FromArgb(200, 150, 255));
            AddFloatingText(Player.Pos, "TRAP SET", Color.Violet);
        }

        private void ExecuteDiagonalDashCard(GridPos dir)
        {
            GridPos dest = Player.Pos.Offset(dir.X, dir.Y);
            if (!dest.IsInBounds() || GetEnemyAt(dest) != null)
            {
                Logger.Log("Diagonal Dash blocked!", Color.Gray);
                AddFloatingText(Player.Pos, "BLOCKED", Color.Gray);
                return;
            }
            GridPos old = Player.Pos;
            Player.Pos = dest;
            ArmBearTrapIfLeft(old);
            Logger.Log("Diagonal Dash to " + dest.ToString() + ".", Color.FromArgb(115, 220, 255));
            AddFloatingText(dest, "DASH!", Color.Cyan);
        }

        private void ExecuteSlideCard(GridPos dir)
        {
            List<GridPos> tiles = ComputeSlideTiles(Player.Pos, dir);
            if (tiles.Count == 0)
            {
                Logger.Log("Slide blocked!", Color.Gray);
                AddFloatingText(Player.Pos, "BLOCKED", Color.Gray);
                return;
            }
            GridPos old = Player.Pos;
            GridPos dest = tiles[^1];
            Player.Pos = dest;
            ArmBearTrapIfLeft(old);
            Logger.Log("Slid to " + dest.ToString() + ".", Color.FromArgb(115, 220, 255));
            AddFloatingText(dest, "SLIDE!", Color.Cyan);
        }

        private void ExecuteAttackCard(CardInstance card, WeaponDef wdef, int dmg, GridPos dir)
        {
            List<GridPos> affected = ComputeAffectedTilesForCard(card.WeaponId, Player.Pos, dir);
            int maxHits = card.WeaponId switch
            {
                "throwing_axe" or "hunting_bow" or "boat_hook" => 1,
                "heavy_spear" => 2,
                _ => 99
            };

            int hits = 0;
            foreach (GridPos tile in affected)
            {
                EnemyInstance e = GetEnemyAt(tile);
                if (e == null)
                {
                    continue;
                }
                hits += 1;
                DamageEnemy(e, dmg, wdef.Name, card.HasPoison, false);
                ApplySecondaryWeaponEffect(card.WeaponId, e, dir);
                if (hits >= maxHits)
                {
                    break;
                }
            }

            if (hits == 0)
            {
                Logger.Log("Played " + wdef.Name + " (missed).", Color.FromArgb(180, 190, 205));
            }
        }

        private void ApplySecondaryWeaponEffect(string weaponId, EnemyInstance target, GridPos dir)
        {
            if (target.Hp <= 0)
            {
                return;
            }
            if (weaponId == "wooden_club")
            {
                TryPushEnemy(target, dir.X, dir.Y);
            }
            else if (weaponId == "boat_hook")
            {
                PullEnemyToward(target, Player.Pos, dir);
            }
        }

        private void PullEnemyToward(EnemyInstance e, GridPos pullerPos, GridPos dirFromPuller)
        {
            GridPos desired = pullerPos.Offset(dirFromPuller.X, dirFromPuller.Y);
            if (desired == e.Pos)
            {
                return;
            }
            GridPos best = e.Pos;
            GridPos cur = e.Pos;
            for (int i = 0; i < 4; i++)
            {
                GridPos next = cur.Offset(-dirFromPuller.X, -dirFromPuller.Y);
                if (next == pullerPos || !next.IsInBounds() || GetEnemyAt(next) != null)
                {
                    break;
                }
                cur = next;
                best = cur;
                if (cur == desired)
                {
                    break;
                }
            }
            if (best == e.Pos)
            {
                return;
            }
            int moveDx = best.X - e.Pos.X;
            int moveDy = best.Y - e.Pos.Y;
            e.Pos = best;
            ShiftLockedTiles(e, moveDx, moveDy);
            Logger.Log("Pulled " + Db.GetEnemyOrFallback(e.EnemyId).Name + " to " + best.ToString() + ".", Color.FromArgb(200, 160, 255));
            CheckEnemySteppedOnTrap(e);
        }

        private bool TryPushEnemy(EnemyInstance e, int dx, int dy)
        {
            GridPos dest = e.Pos.Offset(dx, dy);
            if (!dest.IsInBounds() || IsTileOccupied(dest))
            {
                return false;
            }
            e.Pos = dest;
            ShiftLockedTiles(e, dx, dy);
            Logger.Log("Knocked " + Db.GetEnemyOrFallback(e.EnemyId).Name + " back to " + dest.ToString() + ".", Color.FromArgb(255, 205, 130));
            AddFloatingText(dest, "PUSHED!", Color.Gold);
            CheckEnemySteppedOnTrap(e);
            return true;
        }

        private static void ShiftLockedTiles(EnemyInstance e, int dx, int dy)
        {
            if (e.LockedTiles.Count == 0)
            {
                return;
            }
            e.LockedTiles = e.LockedTiles
                .Select(t => t.Offset(dx, dy))
                .Where(np => np.IsInBounds())
                .ToList();
        }

        private void CheckEnemySteppedOnTrap(EnemyInstance e)
        {
            if (e.Hp <= 0)
            {
                return;
            }
            HazardInstance trap = GetHazardAt(e.Pos, HazardType.BearTrap);
            if (trap == null || !trap.IsArmed)
            {
                return;
            }
            Hazards.Remove(trap);
            e.StunTurns = Math.Max(e.StunTurns, 1);
            e.State = EnemyAIState.Armed;
            e.LockedTiles.Clear();
            e.ChargeTurnsRemaining = 0;
            e.IntentLabel = "STUNNED!";
            Stats.BearTrapStunsTriggered += 1;
            Saves.SaveStats(Stats);
            UpdateAchievementMax("trap_master", Stats.BearTrapStunsTriggered);

            Logger.Log(Db.GetEnemyOrFallback(e.EnemyId).Name + " triggered Bear Trap at " + e.Pos.ToString() + "! Took " + trap.Damage.ToString(CultureInfo.InvariantCulture) + " dmg & STUNNED!", Color.FromArgb(230, 140, 255));
            AddFloatingText(e.Pos, "TRAP STUN!", Color.Magenta);
            DamageEnemy(e, trap.Damage, "Bear Trap", trap.AppliesPoison, false);
        }

        private void DamageEnemy(EnemyInstance target, int amount, string sourceName, bool applyPoison, bool isFriendlyFire)
        {
            if (target == null || target.Hp <= 0)
            {
                return;
            }
            target.Hp -= amount;
            if (!isFriendlyFire)
            {
                Stats.TotalDamageDealt += amount;
            }
            if (applyPoison && target.Hp > 0)
            {
                target.PoisonTurns = 3;
                AddFloatingText(target.Pos, "POISONED", Color.LimeGreen);
            }

            string ename = Db.GetEnemyOrFallback(target.EnemyId).Name;
            if (isFriendlyFire)
            {
                Logger.Log("FRIENDLY FIRE! " + sourceName + " hit " + ename + " for " + amount.ToString(CultureInfo.InvariantCulture) + " dmg!", Color.FromArgb(255, 195, 70));
                AddFloatingText(target.Pos, "-" + amount.ToString(CultureInfo.InvariantCulture) + " FF!", Color.Gold);
            }
            else
            {
                Logger.Log(sourceName + " dealt " + amount.ToString(CultureInfo.InvariantCulture) + " dmg to " + ename + ".", Color.FromArgb(255, 130, 110));
                AddFloatingText(target.Pos, "-" + amount.ToString(CultureInfo.InvariantCulture), Color.OrangeRed);
            }

            if (target.Hp <= 0)
            {
                HandleEnemyDeath(target, isFriendlyFire);
            }
            else
            {
                Saves.SaveStats(Stats);
            }
        }

        private void HandleEnemyDeath(EnemyInstance enemy, bool killedByFriendlyFire)
        {
            EnemyDef def = Db.GetEnemyOrFallback(enemy.EnemyId);
            Enemies.Remove(enemy);
            Profile.Coins += def.CoinDrop;
            Player.RunCoinsEarned += def.CoinDrop;
            Player.RunEnemiesDefeated += 1;

            Stats.TotalEnemiesDefeated += 1;
            Stats.TotalCoinsEarned += def.CoinDrop;
            Stats.EnemiesDefeatedByType[enemy.EnemyId] = Stats.EnemiesDefeatedByType.GetValueOrDefault(enemy.EnemyId) + 1;

            if (killedByFriendlyFire)
            {
                Stats.TotalFriendlyFireKills += 1;
                UpdateAchievementMax("friendly_betrayal", Stats.TotalFriendlyFireKills);
                Logger.Log("FRIENDLY FIRE KILL! " + def.Name + " was slain by an ally! (+" + def.CoinDrop.ToString(CultureInfo.InvariantCulture) + " coins)", Color.Gold);
            }
            else
            {
                Logger.Log(def.Name + " defeated! (+" + def.CoinDrop.ToString(CultureInfo.InvariantCulture) + " coins)", Color.FromArgb(255, 225, 100));
            }

            AddFloatingText(enemy.Pos, "+" + def.CoinDrop.ToString(CultureInfo.InvariantCulture) + "c", Color.Gold);
            UpdateAchievementMax("first_blood", Stats.TotalEnemiesDefeated);
            UpdateAchievementMax("veteran_slayer", Stats.TotalEnemiesDefeated);

            if (def.IsBoss)
            {
                Profile.Area2Unlocked = true;
                UpdateAchievementMax("kingslayer", 1);
            }

            Saves.SaveProfile(Profile);
            Saves.SaveStats(Stats);
        }

        private void DamagePlayer(int amount, string sourceName)
        {
            if (Player.Hp <= 0)
            {
                return;
            }
            if (Player.ImmuneTurns > 0)
            {
                Logger.Log("Iron Bulwark blocked " + amount.ToString(CultureInfo.InvariantCulture) + " dmg from " + sourceName + "!", Color.Cyan);
                AddFloatingText(Player.Pos, "BLOCKED!", Color.Cyan);
                return;
            }
            Player.Hp -= amount;
            Player.TookDamageThisWave = true;
            Logger.Log(sourceName + " hit you for " + amount.ToString(CultureInfo.InvariantCulture) + " dmg! (" + Math.Max(0, Player.Hp).ToString(CultureInfo.InvariantCulture) + "/" + Player.MaxHp.ToString(CultureInfo.InvariantCulture) + " HP)", Color.FromArgb(255, 85, 85));
            AddFloatingText(Player.Pos, "-" + amount.ToString(CultureInfo.InvariantCulture) + " HP", Color.Crimson);

            if (Player.Hp <= 0)
            {
                Player.Hp = 0;
                Phase = GamePhase.Defeat;
                Logger.Log("You have fallen in battle on Wave " + CurrentWave.ToString(CultureInfo.InvariantCulture) + ".", Color.Crimson);
                Saves.DeleteActiveRunSave();
                Saves.SaveStats(Stats);
            }
        }

        private void EndPlayerTurn()
        {
            Stats.TotalTurnsTaken += 1;
            Saves.SaveStats(Stats);

            if (CheckWaveCleared())
            {
                return;
            }
            ExecuteEnemiesTurn();
            if (Phase == GamePhase.Defeat)
            {
                return;
            }
            ProcessPoisonAndHazards();
            if (Phase == GamePhase.Defeat || CheckWaveCleared())
            {
                return;
            }

            TickCooldownsForNextTurn();
            Phase = GamePhase.PlayerTurn;
            Saves.SaveActiveRun(this);
        }

        private void TickCooldownsForNextTurn()
        {
            TurnCounter += 1;
            if (Player.ImmuneTurns > 0)
            {
                Player.ImmuneTurns -= 1;
            }
            if (Player.SpecialCooldown > 0 && !Player.SpecialUsedThisTurn)
            {
                Player.SpecialCooldown -= 1;
            }
            Player.SpecialUsedThisTurn = false;

            foreach (CardInstance c in Hand.Concat(EquippedQueue))
            {
                if (c.CurrentCooldown > 0 && !c.UsedThisTurn)
                {
                    c.CurrentCooldown -= 1;
                }
                c.UsedThisTurn = false;
            }
        }

        private void ExecuteEnemiesTurn()
        {
            foreach (EnemyInstance e in Enemies.ToList())
            {
                if (Phase == GamePhase.Defeat)
                {
                    return;
                }
                if (e.Hp <= 0 || !Enemies.Contains(e))
                {
                    continue;
                }
                ExecuteSingleEnemyTurn(e);
            }
        }

        private void ExecuteSingleEnemyTurn(EnemyInstance e)
        {
            if (e.SpawnedMidTurn)
            {
                e.SpawnedMidTurn = false;
                return;
            }
            if (e.StunTurns > 0)
            {
                e.StunTurns -= 1;
                e.IntentLabel = "Recovering";
                Logger.Log(Db.GetEnemyOrFallback(e.EnemyId).Name + " is STUNNED and loses its turn.", Color.FromArgb(220, 150, 255));
                return;
            }
            if (e.State == EnemyAIState.Unarmed)
            {
                EnemyDef def = Db.GetEnemyOrFallback(e.EnemyId);
                e.State = EnemyAIState.Armed;
                e.IntentLabel = "Armed (" + def.WeaponName + ")";
                Logger.Log(def.Name + " equipped " + def.WeaponName + ".", Color.FromArgb(240, 185, 120));
                return;
            }

            switch (e.EnemyId)
            {
                case "viking_chief":
                    ExecuteBossAI(e);
                    break;
                case "pawn":
                    ExecutePawnAI(e);
                    break;
                case "archer":
                    ExecuteArcherAI(e);
                    break;
                case "spearman":
                    ExecuteSpearmanAI(e);
                    break;
                case "shaman":
                    ExecuteShamanAI(e);
                    break;
            }
        }

        private void ExecutePawnAI(EnemyInstance e)
        {
            EnemyDef def = Db.GetEnemyOrFallback(e.EnemyId);
            if (e.State == EnemyAIState.Charging)
            {
                StrikeLockedTiles(e, def.Damage, def.Name + "'s Axe", false);
                ResetEnemyAfterAttack(e, "Armed (Axe)");
                return;
            }
            if (e.Pos.ManhattanDistance(Player.Pos) == 1)
            {
                GridPos dir = new(Player.Pos.X - e.Pos.X, Player.Pos.Y - e.Pos.Y);
                BeginEnemyCharge(e, 1, dir, [Player.Pos], "CHARGING AXE!");
                Logger.Log("Pawn at " + e.Pos.ToString() + " prepares Axe strike on " + Player.Pos.ToString() + "!", Color.FromArgb(255, 145, 95));
            }
            else
            {
                MoveEnemyStepToward(e, Player.Pos);
                e.IntentLabel = "Advancing";
            }
        }

        private void ExecuteSpearmanAI(EnemyInstance e)
        {
            EnemyDef def = Db.GetEnemyOrFallback(e.EnemyId);
            if (e.State == EnemyAIState.Charging)
            {
                StrikeLockedTiles(e, def.Damage, def.Name + "'s Spear", false);
                ResetEnemyAfterAttack(e, "Armed (Spear)");
                return;
            }
            bool aligned = e.Pos.X == Player.Pos.X || e.Pos.Y == Player.Pos.Y;
            if (aligned && e.Pos.ManhattanDistance(Player.Pos) <= 2)
            {
                GridPos dir = new(Math.Sign(Player.Pos.X - e.Pos.X), Math.Sign(Player.Pos.Y - e.Pos.Y));
                BeginEnemyCharge(e, 1, dir, BuildLineTiles(e.Pos, dir, 2), "CHARGING SPEAR!");
                Logger.Log("Spearman at " + e.Pos.ToString() + " prepares 2-tile Spear thrust!", Color.FromArgb(255, 145, 95));
            }
            else
            {
                MoveEnemyStepToward(e, Player.Pos);
                e.IntentLabel = "Advancing";
            }
        }

        private void ExecuteArcherAI(EnemyInstance e)
        {
            EnemyDef def = Db.GetEnemyOrFallback(e.EnemyId);
            if (e.State == EnemyAIState.Charging)
            {
                StrikeLockedTiles(e, def.Damage, def.Name + "'s Arrow", true);
                ResetEnemyAfterAttack(e, "Armed (Bow)");
                return;
            }
            int dist = e.Pos.ManhattanDistance(Player.Pos);
            if (dist <= 2)
            {
                if (TryMoveEnemyAway(e, Player.Pos))
                {
                    e.IntentLabel = "Retreating";
                }
                else
                {
                    ChargeArcherLine(e);
                }
                return;
            }
            if (e.Pos.X == Player.Pos.X || e.Pos.Y == Player.Pos.Y)
            {
                ChargeArcherLine(e);
            }
            else
            {
                MoveEnemyToAlign(e, Player.Pos);
                e.IntentLabel = "Aligning Shot";
            }
        }

        private void ChargeArcherLine(EnemyInstance e)
        {
            GridPos dir = GetPrimaryOrthogonalDir(e.Pos, Player.Pos);
            if (e.Pos.X == Player.Pos.X)
            {
                dir = new GridPos(0, Math.Sign(Player.Pos.Y - e.Pos.Y));
            }
            else if (e.Pos.Y == Player.Pos.Y)
            {
                dir = new GridPos(Math.Sign(Player.Pos.X - e.Pos.X), 0);
            }
            BeginEnemyCharge(e, 1, dir, BuildLineTiles(e.Pos, dir, 5), "AIMING BOW!");
            Logger.Log("Archer at " + e.Pos.ToString() + " locks arrow line!", Color.FromArgb(255, 145, 95));
        }

        private void ExecuteShamanAI(EnemyInstance e)
        {
            EnemyDef def = Db.GetEnemyOrFallback(e.EnemyId);
            if (e.State == EnemyAIState.Charging)
            {
                ExecuteShamanThrow(e, def);
                ResetEnemyAfterAttack(e, "Armed (Potion)");
                return;
            }
            int dist = e.Pos.ManhattanDistance(Player.Pos);
            if (dist > 3)
            {
                MoveEnemyStepToward(e, Player.Pos);
                e.IntentLabel = "Advancing";
            }
            else if (dist == 3 || !TryMoveEnemyAway(e, Player.Pos))
            {
                ChargeShamanPotion(e);
            }
            else
            {
                e.IntentLabel = "Retreating";
            }
        }

        private void ExecuteShamanThrow(EnemyInstance e, EnemyDef def)
        {
            if (e.LockedTiles.Count == 0)
            {
                return;
            }
            GridPos center = e.LockedTiles[0];
            ApplyDamageToTileEntity(e, center, def.Damage, def.Name + "'s Poison Vial");
            foreach (GridPos tile in e.LockedTiles)
            {
                PlacePoisonHazard(tile, 3);
            }
            Logger.Log("Shaman threw Poison Potion at " + center.ToString() + " (X-pattern toxic zone)!", Color.LimeGreen);
        }

        private void ChargeShamanPotion(EnemyInstance e)
        {
            GridPos center = Player.Pos;
            List<GridPos> tiles = [center];
            foreach (GridPos off in DiagonalOffsets)
            {
                GridPos d = center.Offset(off.X, off.Y);
                if (d.IsInBounds())
                {
                    tiles.Add(d);
                }
            }
            GridPos dir = new(Math.Sign(center.X - e.Pos.X), Math.Sign(center.Y - e.Pos.Y));
            BeginEnemyCharge(e, 1, dir, tiles, "CHARGING POISON!");
            Logger.Log("Shaman at " + e.Pos.ToString() + " prepares Poison Vial at " + center.ToString() + "!", Color.LimeGreen);
        }

        private void ExecuteBossAI(EnemyInstance boss)
        {
            if (boss.State == EnemyAIState.Charging)
            {
                ExecuteBossChargingTurn(boss);
                return;
            }
            if (boss.BossCycleIndex == 0)
            {
                StartBossCycle0(boss);
            }
            else if (boss.BossCycleIndex == 1)
            {
                StartBossCycle1(boss);
            }
            else
            {
                StartBossCycle2(boss);
            }
        }

        private void ExecuteBossChargingTurn(EnemyInstance boss)
        {
            boss.ChargeTurnsRemaining -= 1;
            if (boss.ChargeTurnsRemaining > 0)
            {
                boss.IntentLabel = "HOOK+CLUB (1 TURN!)";
                Logger.Log("Viking Chief continues charging Boat Hook & Wooden Club (strikes next turn)!", Color.FromArgb(255, 110, 75));
                return;
            }

            if (boss.BossCycleIndex == 0)
            {
                StrikeLockedTiles(boss, 3, "Viking Chief's Throwing Axe", true);
                boss.BossCycleIndex = 1;
            }
            else if (boss.BossCycleIndex == 1)
            {
                ExecuteBossHookPhase(boss);
                if (Phase != GamePhase.Defeat)
                {
                    ExecuteBossClubPhase(boss);
                }
                boss.BossCycleIndex = 2;
            }
            else
            {
                StrikeLockedTiles(boss, 3, "Evolved Viking Axe", false);
                boss.BossCycleIndex = 0;
            }
            ResetEnemyAfterAttack(boss, "Boss Cycle " + (boss.BossCycleIndex + 1).ToString(CultureInfo.InvariantCulture));
        }

        private void StartBossCycle0(EnemyInstance boss)
        {
            if (boss.Pos.X != Player.Pos.X && boss.Pos.Y != Player.Pos.Y)
            {
                MoveEnemyToAlign(boss, Player.Pos);
                if (boss.Pos.X != Player.Pos.X && boss.Pos.Y != Player.Pos.Y)
                {
                    boss.IntentLabel = "Aligning Axe";
                    return;
                }
            }
            GridPos dir = GetPrimaryOrthogonalDir(boss.Pos, Player.Pos);
            BeginEnemyCharge(boss, 1, dir, BuildLineTiles(boss.Pos, dir, 5), "BOSS: THROWING AXE!");
            Logger.Log("Viking Chief prepares Throwing Axe!", Color.FromArgb(255, 110, 75));
        }

        private void StartBossCycle1(EnemyInstance boss)
        {
            GridPos dir = GetPrimaryOrthogonalDir(boss.Pos, Player.Pos);
            BeginEnemyCharge(boss, 2, dir, BuildLineTiles(boss.Pos, dir, 3), "HOOK+CLUB (2 TURNS)");
            Logger.Log("Viking Chief equips Boat Hook & Wooden Club (2-turn charge) and summons backup!", Color.FromArgb(255, 120, 75));
            SpawnEnemy(BossMinionIds[SecureRandom.NextInt(BossMinionIds.Length)], true);
        }

        private void StartBossCycle2(EnemyInstance boss)
        {
            GridPos dir = GetPrimaryOrthogonalDir(boss.Pos, Player.Pos);
            List<GridPos> pattern = BuildEvolvedAxeTiles(boss.Pos, dir);
            if (!pattern.Contains(Player.Pos) && boss.Pos.ManhattanDistance(Player.Pos) > 2)
            {
                MoveEnemyStepToward(boss, Player.Pos);
                boss.IntentLabel = "Closing In";
                return;
            }
            BeginEnemyCharge(boss, 1, dir, pattern, "EVOLVED VIKING AXE!");
            Logger.Log("Viking Chief raises Evolved Viking Axe!", Color.FromArgb(255, 95, 95));
        }

        private void ExecuteBossHookPhase(EnemyInstance boss)
        {
            GridPos adj = boss.Pos.Offset(boss.AimDir.X, boss.AimDir.Y);
            foreach (GridPos t in boss.LockedTiles)
            {
                if (Player.Pos == t)
                {
                    DamagePlayer(1, "Viking Chief's Boat Hook");
                    if (Player.Hp > 0 && adj.IsInBounds() && adj != Player.Pos && GetEnemyAt(adj) == null)
                    {
                        GridPos old = Player.Pos;
                        Player.Pos = adj;
                        ArmBearTrapIfLeft(old);
                        Logger.Log("Viking Chief hooked you to " + adj.ToString() + "!", Color.Orange);
                    }
                    return;
                }
                EnemyInstance ally = GetEnemyAt(t);
                if (ally != null && ally != boss)
                {
                    DamageEnemy(ally, 1, "Viking Chief's Boat Hook", false, true);
                    if (ally.Hp > 0)
                    {
                        PullEnemyToward(ally, boss.Pos, boss.AimDir);
                    }
                    return;
                }
            }
        }

        private void ExecuteBossClubPhase(EnemyInstance boss)
        {
            GridPos adj = boss.Pos.Offset(boss.AimDir.X, boss.AimDir.Y);
            if (!adj.IsInBounds())
            {
                return;
            }
            if (Player.Pos == adj)
            {
                DamagePlayer(2, "Viking Chief's Wooden Club");
                GridPos kb = Player.Pos.Offset(boss.AimDir.X, boss.AimDir.Y);
                if (Player.Hp > 0 && kb.IsInBounds() && GetEnemyAt(kb) == null)
                {
                    GridPos old = Player.Pos;
                    Player.Pos = kb;
                    ArmBearTrapIfLeft(old);
                    Logger.Log("Wooden Club knocked you back to " + kb.ToString() + "!", Color.Orange);
                }
                return;
            }
            EnemyInstance ally = GetEnemyAt(adj);
            if (ally != null && ally != boss)
            {
                DamageEnemy(ally, 2, "Viking Chief's Wooden Club", false, true);
                if (ally.Hp > 0)
                {
                    TryPushEnemy(ally, boss.AimDir.X, boss.AimDir.Y);
                }
            }
        }

        private static void BeginEnemyCharge(EnemyInstance e, int turns, GridPos dir, List<GridPos> locked, string intent)
        {
            e.State = EnemyAIState.Charging;
            e.ChargeTurnsRemaining = turns;
            e.AimDir = dir;
            e.LockedTiles = locked;
            e.IntentLabel = intent;
        }

        private static void ResetEnemyAfterAttack(EnemyInstance e, string intent)
        {
            e.State = EnemyAIState.Armed;
            e.LockedTiles.Clear();
            e.IntentLabel = intent;
        }

        private static List<GridPos> BuildEvolvedAxeTiles(GridPos origin, GridPos dir)
        {
            GridPos[] raw =
            [
                origin.Offset(dir.X, dir.Y),
                origin.Offset(dir.X * 2, dir.Y * 2),
                origin.Offset(dir.X - dir.Y, dir.Y + dir.X),
                origin.Offset(dir.X + dir.Y, dir.Y - dir.X),
                origin.Offset(-dir.Y, dir.X),
                origin.Offset(dir.Y, -dir.X)
            ];
            return raw.Where(p => p.IsInBounds()).Distinct().ToList();
        }

        private static GridPos GetPrimaryOrthogonalDir(GridPos from, GridPos to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return new GridPos(dx >= 0 ? 1 : -1, 0);
            }
            return new GridPos(0, dy >= 0 ? 1 : -1);
        }

        private static List<GridPos> BuildLineTiles(GridPos origin, GridPos dir, int maxLen)
        {
            List<GridPos> tiles = [];
            for (int s = 1; s <= maxLen; s++)
            {
                GridPos p = origin.Offset(dir.X * s, dir.Y * s);
                if (!p.IsInBounds())
                {
                    break;
                }
                tiles.Add(p);
            }
            return tiles;
        }

        private void StrikeLockedTiles(EnemyInstance attacker, int dmg, string attackName, bool firstOnly)
        {
            foreach (GridPos t in attacker.LockedTiles)
            {
                if (ApplyDamageToTileEntity(attacker, t, dmg, attackName) && firstOnly)
                {
                    return;
                }
            }
        }

        private bool ApplyDamageToTileEntity(EnemyInstance attacker, GridPos tile, int dmg, string attackName)
        {
            if (Player.Pos == tile)
            {
                DamagePlayer(dmg, attackName);
                return true;
            }
            EnemyInstance other = GetEnemyAt(tile);
            if (other != null && other != attacker)
            {
                DamageEnemy(other, dmg, attackName, false, true);
                return true;
            }
            return false;
        }

        private void MoveEnemyStepToward(EnemyInstance e, GridPos target)
        {
            GridPos best = e.Pos;
            int bestDist = e.Pos.ManhattanDistance(target);
            foreach (GridPos d in OrthogonalDirs)
            {
                GridPos cand = e.Pos.Offset(d.X, d.Y);
                if (cand.IsInBounds() && !IsTileOccupied(cand) && cand.ManhattanDistance(target) < bestDist)
                {
                    bestDist = cand.ManhattanDistance(target);
                    best = cand;
                }
            }
            if (best != e.Pos)
            {
                e.Pos = best;
                CheckEnemySteppedOnTrap(e);
            }
        }

        private void MoveEnemyToAlign(EnemyInstance e, GridPos target)
        {
            int dx = target.X - e.Pos.X;
            int dy = target.Y - e.Pos.Y;
            GridPos first = Math.Abs(dx) <= Math.Abs(dy) ? e.Pos.Offset(Math.Sign(dx), 0) : e.Pos.Offset(0, Math.Sign(dy));
            GridPos second = Math.Abs(dx) <= Math.Abs(dy) ? e.Pos.Offset(0, Math.Sign(dy)) : e.Pos.Offset(Math.Sign(dx), 0);

            if (first.IsInBounds() && !IsTileOccupied(first))
            {
                e.Pos = first;
                CheckEnemySteppedOnTrap(e);
                return;
            }
            if (second.IsInBounds() && !IsTileOccupied(second))
            {
                e.Pos = second;
                CheckEnemySteppedOnTrap(e);
                return;
            }
            MoveEnemyStepToward(e, target);
        }

        private bool TryMoveEnemyAway(EnemyInstance e, GridPos from)
        {
            GridPos best = e.Pos;
            int bestDist = e.Pos.ManhattanDistance(from);
            foreach (GridPos d in OrthogonalDirs)
            {
                GridPos cand = e.Pos.Offset(d.X, d.Y);
                if (cand.IsInBounds() && !IsTileOccupied(cand) && cand.ManhattanDistance(from) > bestDist)
                {
                    bestDist = cand.ManhattanDistance(from);
                    best = cand;
                }
            }
            if (best == e.Pos)
            {
                return false;
            }
            e.Pos = best;
            CheckEnemySteppedOnTrap(e);
            return true;
        }

        private void PlacePoisonHazard(GridPos pos, int turns)
        {
            HazardInstance existing = GetHazardAt(pos, HazardType.Poison);
            if (existing != null)
            {
                existing.RemainingTurns = Math.Max(existing.RemainingTurns, turns);
                return;
            }
            Hazards.Add(new HazardInstance
            {
                Type = HazardType.Poison,
                Pos = pos,
                RemainingTurns = turns,
                IsArmed = true,
                Damage = 1
            });
        }

        private void ProcessPoisonAndHazards()
        {
            ProcessEnemyPoisonTicks();
            if (GetHazardAt(Player.Pos, HazardType.Poison) != null)
            {
                DamagePlayer(1, "Poison Cloud");
            }
            if (Phase == GamePhase.Defeat)
            {
                return;
            }
            ProcessGroundPoisonOnEnemies();
            DecayPoisonHazards();
        }

        private void ProcessEnemyPoisonTicks()
        {
            foreach (EnemyInstance e in Enemies.ToList())
            {
                if (e.Hp > 0 && Enemies.Contains(e) && e.PoisonTurns > 0)
                {
                    e.PoisonTurns -= 1;
                    DamageEnemy(e, 1, "Poison", false, false);
                }
            }
        }

        private void ProcessGroundPoisonOnEnemies()
        {
            foreach (EnemyInstance e in Enemies.ToList())
            {
                if (e.Hp > 0 && Enemies.Contains(e) && GetHazardAt(e.Pos, HazardType.Poison) != null)
                {
                    DamageEnemy(e, 1, "Poison Cloud", false, true);
                }
            }
        }

        private void DecayPoisonHazards()
        {
            for (int i = Hazards.Count - 1; i >= 0; i--)
            {
                if (Hazards[i].Type == HazardType.Poison)
                {
                    Hazards[i].RemainingTurns -= 1;
                    if (Hazards[i].RemainingTurns <= 0)
                    {
                        Hazards.RemoveAt(i);
                    }
                }
            }
        }

        private bool CheckWaveCleared()
        {
            if (Enemies.Count > 0)
            {
                return false;
            }
            if (Phase is GamePhase.Victory or GamePhase.Defeat or GamePhase.RewardPickCard or GamePhase.RewardUpgrade)
            {
                return true;
            }

            Logger.Log("Wave " + CurrentWave.ToString(CultureInfo.InvariantCulture) + " cleared!", Color.FromArgb(130, 245, 165));
            if (!Player.TookDamageThisWave)
            {
                UpdateAchievementMax("untouchable", 1);
            }

            WaveDef wdef = Db.Waves.FirstOrDefault(w => w.WaveNumber == CurrentWave);
            if (wdef == null || wdef.RewardAfter == "Victory" || CurrentWave >= 10)
            {
                HandleVictory();
                return true;
            }
            if (wdef.RewardAfter == "PickCard")
            {
                ResetAllCooldowns();
                GeneratePickCardReward(false);
                Phase = GamePhase.RewardPickCard;
                Saves.SaveActiveRun(this);
                return true;
            }
            if (wdef.RewardAfter == "Upgrade")
            {
                ResetAllCooldowns();
                GenerateUpgradeReward(false);
                Phase = GamePhase.RewardUpgrade;
                Saves.SaveActiveRun(this);
                return true;
            }

            SpawnNextWave();
            return true;
        }

        private void ResetAllCooldowns()
        {
            Player.SpecialCooldown = 0;
            Player.SpecialUsedThisTurn = false;
            foreach (CardInstance c in Hand.Concat(EquippedQueue))
            {
                c.CurrentCooldown = 0;
                c.UsedThisTurn = false;
            }
            Logger.Log("Reward triggered! All card & Special cooldowns reset to 0.", Color.FromArgb(120, 240, 255));
        }

        private void GeneratePickCardReward(bool isReroll)
        {
            int nextCost = isReroll ? Reward.RerollCost : 5;
            Reward = new RewardState { RerollCost = nextCost };

            HashSet<string> owned = [.. Hand.Select(c => c.WeaponId), .. EquippedQueue.Select(c => c.WeaponId)];
            WeaponDef pick1 = PickRandomWeaponFromCategories(owned, CardCategory.Melee, CardCategory.Ranged);
            WeaponDef pick2 = PickRandomWeaponFromCategories(owned, CardCategory.Movement, CardCategory.Special);
            if (pick1 != null)
            {
                Reward.OfferedWeaponIds.Add(pick1.Id);
            }
            if (pick2 != null)
            {
                Reward.OfferedWeaponIds.Add(pick2.Id);
            }
        }

        private WeaponDef PickRandomWeaponFromCategories(HashSet<string> owned, CardCategory catA, CardCategory catB)
        {
            List<WeaponDef> pool = Profile.UnlockedWeapons
                .Select(id => Db.Weapons.TryGetValue(id, out WeaponDef w) ? w : null)
                .Where(w => w != null && (w.Category == catA || w.Category == catB))
                .ToList();
            if (pool.Count == 0)
            {
                return null;
            }
            List<WeaponDef> fresh = pool.Where(w => !owned.Contains(w.Id)).ToList();
            List<WeaponDef> source = fresh.Count > 0 ? fresh : pool;
            return source[SecureRandom.NextInt(source.Count)];
        }

        private void GenerateUpgradeReward(bool isReroll)
        {
            int nextCost = isReroll ? Reward.RerollCost : 5;
            Reward = new RewardState { RerollCost = nextCost };
            List<ModifierType> mods = [.. AllModifiers];
            SecureRandom.Shuffle(mods);
            Reward.OfferedModifiers.Add(mods[0]);
            Reward.OfferedModifiers.Add(mods[1]);
        }

        public void PickRewardCard(int offerIndex)
        {
            if (Phase != GamePhase.RewardPickCard || offerIndex < 0 || offerIndex >= Reward.OfferedWeaponIds.Count)
            {
                return;
            }
            string wid = Reward.OfferedWeaponIds[offerIndex];
            Hand.Add(new CardInstance { WeaponId = wid });
            Logger.Log("Added " + Db.GetWeaponOrFallback(wid).Name + " to hand!", Color.FromArgb(140, 240, 175));
            SpawnNextWave();
        }

        public void SelectUpgradeModifier(int modIndex)
        {
            if (Phase != GamePhase.RewardUpgrade || modIndex < 0 || modIndex >= Reward.OfferedModifiers.Count)
            {
                return;
            }
            Reward.SelectedModifier = Reward.OfferedModifiers[modIndex];
        }

        public static bool IsModifierCompatibleWithCard(ModifierType mod, CardCategory category)
        {
            if (category != CardCategory.Movement)
            {
                return true;
            }
            return mod is ModifierType.CooldownMinus1 or ModifierType.CooldownMinus2;
        }

        public bool TryAttachSelectedModifierToCard(CardInstance card)
        {
            if (Phase != GamePhase.RewardUpgrade || !Reward.SelectedModifier.HasValue || card == null)
            {
                return false;
            }
            ModifierType mod = Reward.SelectedModifier.Value;
            WeaponDef wdef = Db.GetWeaponOrFallback(card.WeaponId);
            if (!IsModifierCompatibleWithCard(mod, wdef.Category))
            {
                return false;
            }

            switch (mod)
            {
                case ModifierType.DamagePlus1:
                    card.BonusDamage += 1;
                    break;
                case ModifierType.DamagePlus2:
                    card.BonusDamage += 2;
                    break;
                case ModifierType.CooldownMinus1:
                    card.CooldownModifier -= 1;
                    break;
                case ModifierType.CooldownMinus2:
                    card.CooldownModifier -= 2;
                    break;
                case ModifierType.Poison:
                    card.HasPoison = true;
                    break;
            }

            Logger.Log("Upgraded " + wdef.Name + " with " + GetModifierLabel(mod) + "!", Color.FromArgb(255, 220, 100));
            SpawnNextWave();
            return true;
        }

        public void SkipReward()
        {
            if (Phase is not (GamePhase.RewardPickCard or GamePhase.RewardUpgrade))
            {
                return;
            }
            Logger.Log("Skipped reward.", Color.LightGray);
            SpawnNextWave();
        }

        public bool TryRerollReward()
        {
            if (Phase is not (GamePhase.RewardPickCard or GamePhase.RewardUpgrade))
            {
                return false;
            }
            if (Player.FreeRerollsLeft > 0)
            {
                Player.FreeRerollsLeft -= 1;
                Player.RerollsUsedThisRun += 1;
                Logger.Log("Used free Charisma reroll!", Color.FromArgb(145, 230, 255));
            }
            else
            {
                if (Profile.Coins < Reward.RerollCost)
                {
                    return false;
                }
                Profile.Coins -= Reward.RerollCost;
                Player.RerollsUsedThisRun += 1;
                Logger.Log("Rerolled reward for " + Reward.RerollCost.ToString(CultureInfo.InvariantCulture) + " coins.", Color.Gold);
                Reward.RerollCost *= 2;
                Saves.SaveProfile(Profile);
            }

            if (Phase == GamePhase.RewardPickCard)
            {
                GeneratePickCardReward(true);
            }
            else
            {
                GenerateUpgradeReward(true);
            }
            Saves.SaveActiveRun(this);
            return true;
        }

        private void HandleVictory()
        {
            Phase = GamePhase.Victory;
            Stats.RunsWon += 1;
            if (Player.RerollsUsedThisRun == 0)
            {
                UpdateAchievementMax("no_second_chances", 1);
            }
            Logger.Log("VICTORY! Level 1 conquered in " + TurnCounter.ToString(CultureInfo.InvariantCulture) + " turns!", Color.Gold);
            Saves.DeleteActiveRunSave();
            Saves.SaveStats(Stats);
            Saves.SaveProfile(Profile);
        }

        public bool TryBuyWeaponUnlock(string weaponId)
        {
            if (!Db.Weapons.TryGetValue(weaponId, out WeaponDef def) || Profile.UnlockedWeapons.Contains(weaponId) || Profile.Coins < def.UnlockCost)
            {
                return false;
            }
            Profile.Coins -= def.UnlockCost;
            Profile.UnlockedWeapons.Add(weaponId);
            CheckWellEquippedAchievement();
            Saves.SaveProfile(Profile);
            EnqueueToast("WEAPON UNLOCKED!", def.Name + " added to card pool.", Color.FromArgb(240, 185, 75));
            return true;
        }

        public bool TryBuyUpgradeUnlock(string upgradeId)
        {
            if (!Db.Upgrades.TryGetValue(upgradeId, out UpgradeDef def) || Profile.UnlockedUpgrades.Contains(upgradeId) || Profile.Coins < def.Cost)
            {
                return false;
            }
            Profile.Coins -= def.Cost;
            Profile.UnlockedUpgrades.Add(upgradeId);
            CheckWellEquippedAchievement();
            Saves.SaveProfile(Profile);
            EnqueueToast("UPGRADE UNLOCKED!", def.Name + " active permanently.", Color.FromArgb(110, 210, 255));
            return true;
        }

        private void CheckWellEquippedAchievement()
        {
            int boughtWeapons = Profile.UnlockedWeapons.Count(id => Db.Weapons.TryGetValue(id, out WeaponDef w) && !w.DefaultUnlocked);
            UpdateAchievementMax("well_equipped", boughtWeapons + Profile.UnlockedUpgrades.Count);
        }

        public void UpdateAchievementMax(string achievementId, int newValue)
        {
            if (!Db.Achievements.TryGetValue(achievementId, out AchievementDef def))
            {
                return;
            }
            int prev = Profile.AchievementProgress.GetValueOrDefault(achievementId);
            int clamped = Math.Min(def.TargetValue, Math.Max(prev, newValue));
            Profile.AchievementProgress[achievementId] = clamped;

            if (clamped < def.TargetValue || !Profile.CompletedAchievements.Add(achievementId))
            {
                return;
            }
            Profile.Coins += def.CoinReward;
            Stats.TotalCoinsEarned += def.CoinReward;

            string extra = string.Empty;
            if (def.SpecialUnlock.Equals("Shieldmaiden", StringComparison.OrdinalIgnoreCase))
            {
                Profile.UnlockedCharacters.Add("shieldmaiden");
                extra = " + Shieldmaiden Unlocked!";
            }

            EnqueueToast("ACHIEVEMENT: " + def.Title, "+" + def.CoinReward.ToString(CultureInfo.InvariantCulture) + " Coins" + extra, Color.Gold);
            Logger.Log("Achievement Unlocked: " + def.Title + " (+" + def.CoinReward.ToString(CultureInfo.InvariantCulture) + " coins" + extra + ")", Color.Gold);
            Saves.SaveProfile(Profile);
            Saves.SaveStats(Stats);
        }

        public void EnqueueToast(string title, string subtitle, Color accent)
        {
            ToastQueue.Enqueue(new ToastMessage
            {
                Title = title,
                Subtitle = subtitle,
                AccentColor = accent,
                RemainingSeconds = 3.8f
            });
        }

        public void AddFloatingText(GridPos pos, string text, Color color)
        {
            FloatingTexts.Add(new FloatingText
            {
                Pos = pos,
                Text = text,
                Color = color,
                OffsetX = SecureRandom.NextFloat(-8f, 8f)
            });
        }

        public void UpdateAnimations(float dt)
        {
            for (int i = FloatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingTexts[i].Progress += dt * 1.15f;
                if (FloatingTexts[i].Progress >= 1f)
                {
                    FloatingTexts.RemoveAt(i);
                }
            }
            if (ActiveToast != null)
            {
                ActiveToast.RemainingSeconds -= dt;
                if (ActiveToast.RemainingSeconds <= 0f)
                {
                    ActiveToast = null;
                }
            }
            if (ActiveToast == null && ToastQueue.Count > 0)
            {
                ActiveToast = ToastQueue.Dequeue();
            }
        }

        public List<WeaponDef> GetNextUnlockableWeapons(int count)
        {
            return Db.WeaponList
                .Where(w => !w.DefaultUnlocked && !Profile.UnlockedWeapons.Contains(w.Id))
                .OrderBy(w => w.UnlockOrder)
                .Take(count)
                .ToList();
        }

        public List<UpgradeDef> GetNextUnlockableUpgrades(int count)
        {
            return Db.UpgradeList
                .Where(u => !Profile.UnlockedUpgrades.Contains(u.Id))
                .OrderBy(u => u.UnlockOrder)
                .Take(count)
                .ToList();
        }

        public static string GetModifierLabel(ModifierType m)
        {
            return m switch
            {
                ModifierType.DamagePlus1 => "+1 DMG",
                ModifierType.DamagePlus2 => "+2 DMG",
                ModifierType.CooldownMinus1 => "-1 COOLDOWN",
                ModifierType.CooldownMinus2 => "-2 COOLDOWN",
                ModifierType.Poison => "POISON (+1 CD)",
                _ => m.ToString()
            };
        }

        public static string GetModifierDescription(ModifierType m)
        {
            return m switch
            {
                ModifierType.DamagePlus1 => "Permanently increases this card's damage by +1 for the run.",
                ModifierType.DamagePlus2 => "Permanently increases this card's damage by +2 for the run.",
                ModifierType.CooldownMinus1 => "Reduces this card's maximum cooldown by 1 turn (min 1).",
                ModifierType.CooldownMinus2 => "Reduces this card's maximum cooldown by 2 turns (min 1).",
                ModifierType.Poison => "Adds +1 max cooldown. Hit targets suffer 1 poison dmg/turn for 3 turns.",
                _ => string.Empty
            };
        }
    }

    public static class VectorArtRenderer
    {
        public static GraphicsPath CreateRoundedRect(RectangleF r, float radius)
        {
            GraphicsPath path = new();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d <= 0.1f)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRoundedRect(Graphics g, Brush brush, RectangleF r, float radius)
        {
            using GraphicsPath p = CreateRoundedRect(r, radius);
            g.FillPath(brush, p);
        }

        public static void DrawRoundedRect(Graphics g, Pen pen, RectangleF r, float radius)
        {
            using GraphicsPath p = CreateRoundedRect(r, radius);
            g.DrawPath(pen, p);
        }

        public static void DrawPanelBox(Graphics g, RectangleF r, Color bgColor, Color borderColor, float radius, float borderWidth)
        {
            using SolidBrush bg = new(bgColor);
            using Pen bp = new(borderColor, borderWidth);
            FillRoundedRect(g, bg, r, radius);
            DrawRoundedRect(g, bp, r, radius);
        }

        public static void DrawCenteredText(Graphics g, string text, Font font, Brush brush, RectangleF rect)
        {
            using StringFormat sf = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            g.DrawString(text, font, brush, rect, sf);
        }

        public static Color GetCategoryHeaderColor(CardCategory cat)
        {
            return cat switch
            {
                CardCategory.Melee => Color.FromArgb(185, 52, 52),
                CardCategory.Ranged => Color.FromArgb(42, 145, 78),
                CardCategory.Movement => Color.FromArgb(42, 118, 198),
                CardCategory.Special => Color.FromArgb(132, 62, 186),
                _ => Color.SlateGray
            };
        }

        public static Color GetCategoryLightColor(CardCategory cat)
        {
            return cat switch
            {
                CardCategory.Melee => Color.FromArgb(255, 135, 120),
                CardCategory.Ranged => Color.FromArgb(120, 235, 155),
                CardCategory.Movement => Color.FromArgb(120, 205, 255),
                CardCategory.Special => Color.FromArgb(215, 150, 255),
                _ => Color.LightGray
            };
        }

        private static void DrawGroundShadow(Graphics g, float cx, float cy, float s)
        {
            using SolidBrush sh = new(Color.FromArgb(120, 0, 0, 0));
            g.FillEllipse(sh, cx - s * 0.85f, cy + s * 0.54f, s * 1.7f, s * 0.40f);
        }

        private static void DrawHumanoidCape(Graphics g, float cx, float cy, float s, float widthFactor, Color topColor, Color botColor, Color? trimColor)
        {
            PointF[] cloak =
            [
                new PointF(cx - s * widthFactor, cy + s * 0.72f),
                new PointF(cx - s * (widthFactor * 0.78f), cy + s * 0.02f),
                new PointF(cx + s * (widthFactor * 0.78f), cy + s * 0.02f),
                new PointF(cx + s * widthFactor, cy + s * 0.72f)
            ];
            using LinearGradientBrush cb = new(new PointF(cx, cy), new PointF(cx, cy + s * 0.8f), topColor, botColor);
            g.FillPolygon(cb, cloak);
            if (trimColor.HasValue)
            {
                using Pen trim = new(trimColor.Value, Math.Max(1.4f, s * 0.07f));
                g.DrawPolygon(trim, cloak);
            }
        }

        private static void DrawHornPair(Graphics g, float cx, float cy, float s, float scale, Color hornColor, Pen outlinePen)
        {
            float[] signs = [-1f, 1f];
            using SolidBrush brush = new(hornColor);
            foreach (float dir in signs)
            {
                PointF[] horn =
                [
                    new PointF(cx + dir * s * 0.42f * scale, cy - s * 0.38f * scale),
                    new PointF(cx + dir * s * 0.88f * scale, cy - s * 0.52f * scale),
                    new PointF(cx + dir * s * 0.96f * scale, cy - s * 0.94f * scale),
                    new PointF(cx + dir * s * 0.68f * scale, cy - s * 0.66f * scale),
                    new PointF(cx + dir * s * 0.36f * scale, cy - s * 0.58f * scale)
                ];
                g.FillPolygon(brush, horn);
                g.DrawPolygon(outlinePen, horn);
            }
        }

        public static void DrawCharacterPortrait(Graphics g, string characterId, RectangleF bounds, bool isImmune)
        {
            float cx = bounds.X + bounds.Width * 0.5f;
            float cy = bounds.Y + bounds.Height * 0.52f;
            float s = Math.Min(bounds.Width, bounds.Height) * 0.44f;

            DrawGroundShadow(g, cx, cy, s);
            if (isImmune)
            {
                using Pen glow = new(Color.FromArgb(210, 90, 240, 255), 3f);
                using SolidBrush aura = new(Color.FromArgb(50, 70, 230, 255));
                g.FillEllipse(aura, cx - s * 1.08f, cy - s * 1.05f, s * 2.16f, s * 2.0f);
                g.DrawEllipse(glow, cx - s * 1.08f, cy - s * 1.05f, s * 2.16f, s * 2.0f);
            }

            if (characterId == "shieldmaiden")
            {
                DrawShieldmaidenVector(g, cx, cy, s);
            }
            else
            {
                DrawVikingVector(g, cx, cy, s);
            }
        }

        private static void DrawVikingVector(Graphics g, float cx, float cy, float s)
        {
            DrawHumanoidCape(g, cx, cy, s, 0.78f, Color.FromArgb(46, 108, 195), Color.FromArgb(22, 54, 115), Color.FromArgb(232, 188, 72));
            DrawVikingSideAxe(g, cx, cy, s);
            DrawBeardedHead(g, cx, cy, s, Color.FromArgb(234, 188, 152), Color.FromArgb(215, 148, 58), Color.FromArgb(148, 86, 28), Color.FromArgb(28, 35, 48));
            DrawHornPair(g, cx, cy, s, 1.0f, Color.FromArgb(245, 238, 215), Pens.DimGray);

            RectangleF dome = new(cx - s * 0.46f, cy - s * 0.78f, s * 0.92f, s * 0.84f);
            using LinearGradientBrush steel = new(dome, Color.FromArgb(205, 218, 232), Color.FromArgb(88, 102, 122), LinearGradientMode.Vertical);
            g.FillPie(steel, dome.X, dome.Y, dome.Width, dome.Height, 180, 180);
            using SolidBrush gold = new(Color.FromArgb(238, 194, 68));
            g.FillRectangle(gold, cx - s * 0.48f, cy - s * 0.38f, s * 0.96f, s * 0.13f);
            g.FillRectangle(gold, cx - s * 0.06f, cy - s * 0.38f, s * 0.12f, s * 0.32f);
        }

        private static void DrawVikingSideAxe(Graphics g, float cx, float cy, float s)
        {
            using Pen shaft = new(Color.FromArgb(135, 86, 44), Math.Max(2f, s * 0.11f));
            g.DrawLine(shaft, cx + s * 0.62f, cy + s * 0.62f, cx + s * 0.86f, cy - s * 0.56f);
            PointF[] axeHead =
            [
                new PointF(cx + s * 0.76f, cy - s * 0.45f),
                new PointF(cx + s * 1.08f, cy - s * 0.65f),
                new PointF(cx + s * 1.12f, cy - s * 0.20f),
                new PointF(cx + s * 0.74f, cy - s * 0.28f)
            ];
            using LinearGradientBrush blade = new(new PointF(cx + s * 0.7f, cy - s * 0.65f), new PointF(cx + s * 1.12f, cy - s * 0.2f), Color.FromArgb(228, 236, 245), Color.FromArgb(130, 145, 165));
            using Pen outline = new(Color.FromArgb(40, 48, 60), 1.2f);
            g.FillPolygon(blade, axeHead);
            g.DrawPolygon(outline, axeHead);
        }

        private static void DrawBeardedHead(Graphics g, float cx, float cy, float s, Color skinColor, Color beardTop, Color beardBot, Color eyeColor)
        {
            using SolidBrush skin = new(skinColor);
            g.FillEllipse(skin, cx - s * 0.36f, cy - s * 0.36f, s * 0.72f, s * 0.68f);
            PointF[] beard =
            [
                new PointF(cx - s * 0.38f, cy - s * 0.02f),
                new PointF(cx - s * 0.28f, cy + s * 0.56f),
                new PointF(cx, cy + s * 0.68f),
                new PointF(cx + s * 0.28f, cy + s * 0.56f),
                new PointF(cx + s * 0.38f, cy - s * 0.02f)
            ];
            using LinearGradientBrush bb = new(new PointF(cx, cy), new PointF(cx, cy + s * 0.68f), beardTop, beardBot);
            g.FillPolygon(bb, beard);
            using SolidBrush eye = new(eyeColor);
            g.FillEllipse(eye, cx - s * 0.18f, cy - s * 0.14f, s * 0.10f, s * 0.09f);
            g.FillEllipse(eye, cx + s * 0.08f, cy - s * 0.14f, s * 0.10f, s * 0.09f);
        }

        private static void DrawShieldmaidenVector(Graphics g, float cx, float cy, float s)
        {
            DrawHumanoidCape(g, cx, cy, s, 0.75f, Color.FromArgb(52, 188, 218), Color.FromArgb(24, 96, 128), Color.FromArgb(225, 238, 248));

            using SolidBrush hair = new(Color.FromArgb(238, 216, 138));
            using SolidBrush skin = new(Color.FromArgb(242, 200, 168));
            using SolidBrush eye = new(Color.FromArgb(25, 95, 135));
            g.FillEllipse(hair, cx - s * 0.44f, cy - s * 0.34f, s * 0.88f, s * 0.78f);
            g.FillEllipse(skin, cx - s * 0.30f, cy - s * 0.32f, s * 0.60f, s * 0.58f);
            g.FillEllipse(eye, cx - s * 0.15f, cy - s * 0.10f, s * 0.08f, s * 0.08f);
            g.FillEllipse(eye, cx + s * 0.07f, cy - s * 0.10f, s * 0.08f, s * 0.08f);

            DrawWingedHelm(g, cx, cy, s);
            DrawRunicShield(g, cx - s * 0.48f, cy + s * 0.30f, s * 0.48f);
        }

        private static void DrawWingedHelm(Graphics g, float cx, float cy, float s)
        {
            float[] signs = [-1f, 1f];
            using SolidBrush wingBrush = new(Color.FromArgb(236, 246, 255));
            using Pen wingEdge = new(Color.FromArgb(95, 185, 220), 1.2f);
            foreach (float dir in signs)
            {
                PointF[] wing =
                [
                    new PointF(cx + dir * s * 0.40f, cy - s * 0.36f),
                    new PointF(cx + dir * s * 0.98f, cy - s * 0.76f),
                    new PointF(cx + dir * s * 0.78f, cy - s * 0.36f),
                    new PointF(cx + dir * s * 0.90f, cy - s * 0.22f),
                    new PointF(cx + dir * s * 0.38f, cy - s * 0.18f)
                ];
                g.FillPolygon(wingBrush, wing);
                g.DrawPolygon(wingEdge, wing);
            }
            RectangleF dome = new(cx - s * 0.42f, cy - s * 0.76f, s * 0.84f, s * 0.80f);
            using LinearGradientBrush helm = new(dome, Color.FromArgb(230, 242, 252), Color.FromArgb(118, 148, 172), LinearGradientMode.Vertical);
            using SolidBrush cyanGem = new(Color.FromArgb(80, 240, 255));
            g.FillPie(helm, dome.X, dome.Y, dome.Width, dome.Height, 180, 180);
            g.FillPolygon(cyanGem,
            [
                new PointF(cx, cy - s * 0.56f),
                new PointF(cx + s * 0.10f, cy - s * 0.38f),
                new PointF(cx, cy - s * 0.22f),
                new PointF(cx - s * 0.10f, cy - s * 0.38f)
            ]);
        }

        private static void DrawRunicShield(Graphics g, float shX, float shY, float shR)
        {
            RectangleF shBounds = new(shX - shR, shY - shR, shR * 2, shR * 2);
            using LinearGradientBrush shBg = new(shBounds, Color.FromArgb(46, 148, 185), Color.FromArgb(18, 68, 96), 45f);
            using Pen rim = new(Color.FromArgb(220, 234, 245), Math.Max(1.8f, shR * 0.18f));
            using Pen rune = new(Color.FromArgb(130, 248, 255), Math.Max(1.4f, shR * 0.12f));
            g.FillEllipse(shBg, shBounds);
            g.DrawEllipse(rim, shBounds);
            g.DrawLine(rune, shX, shY - shR * 0.6f, shX, shY + shR * 0.6f);
            g.DrawLine(rune, shX - shR * 0.42f, shY - shR * 0.25f, shX + shR * 0.42f, shY + shR * 0.25f);
            g.DrawLine(rune, shX + shR * 0.42f, shY - shR * 0.25f, shX - shR * 0.42f, shY + shR * 0.25f);
        }

        public static void DrawEnemySprite(Graphics g, EnemyInstance enemy, RectangleF bounds, float animPhase)
        {
            float cx = bounds.X + bounds.Width * 0.5f;
            float cy = bounds.Y + bounds.Height * 0.50f;
            float s = Math.Min(bounds.Width, bounds.Height) * (enemy.EnemyId == "viking_chief" ? 0.47f : 0.40f);
            DrawGroundShadow(g, cx, cy, s);

            switch (enemy.EnemyId)
            {
                case "pawn":
                    DrawPawnVector(g, cx, cy, s);
                    break;
                case "archer":
                    DrawArcherVector(g, cx, cy, s);
                    break;
                case "spearman":
                    DrawSpearmanVector(g, cx, cy, s);
                    break;
                case "shaman":
                    DrawShamanVector(g, cx, cy, s, animPhase);
                    break;
                case "viking_chief":
                    DrawBossVector(g, cx, cy, s, animPhase);
                    break;
            }
        }

        private static void DrawPawnVector(Graphics g, float cx, float cy, float s)
        {
            DrawHumanoidCape(g, cx, cy, s, 0.62f, Color.FromArgb(138, 78, 58), Color.FromArgb(98, 52, 38), null);
            using SolidBrush skin = new(Color.FromArgb(224, 176, 144));
            using LinearGradientBrush helm = new(new RectangleF(cx - s * 0.38f, cy - s * 0.68f, s * 0.76f, s * 0.70f), Color.FromArgb(182, 190, 200), Color.FromArgb(78, 86, 98), 90f);
            using SolidBrush eye = new(Color.FromArgb(45, 25, 20));
            g.FillEllipse(skin, cx - s * 0.30f, cy - s * 0.34f, s * 0.60f, s * 0.56f);
            g.FillPie(helm, cx - s * 0.38f, cy - s * 0.68f, s * 0.76f, s * 0.70f, 180, 180);
            g.FillEllipse(eye, cx - s * 0.14f, cy - s * 0.12f, s * 0.08f, s * 0.08f);
            g.FillEllipse(eye, cx + s * 0.06f, cy - s * 0.12f, s * 0.08f, s * 0.08f);

            float sr = s * 0.42f;
            using SolidBrush wood = new(Color.FromArgb(148, 96, 52));
            using Pen ironRim = new(Color.FromArgb(165, 175, 185), 2f);
            g.FillEllipse(wood, cx - s * 0.74f, cy - s * 0.02f, sr * 2, sr * 2);
            g.DrawEllipse(ironRim, cx - s * 0.74f, cy - s * 0.02f, sr * 2, sr * 2);
            g.FillEllipse(Brushes.Silver, cx - s * 0.44f, cy + s * 0.28f, s * 0.24f, s * 0.24f);

            using Pen handle = new(Color.FromArgb(120, 74, 38), 2.4f);
            using SolidBrush steel = new(Color.FromArgb(205, 214, 224));
            g.DrawLine(handle, cx + s * 0.48f, cy + s * 0.50f, cx + s * 0.74f, cy - s * 0.36f);
            PointF[] blade =
            [
                new PointF(cx + s * 0.66f, cy - s * 0.32f),
                new PointF(cx + s * 0.96f, cy - s * 0.42f),
                new PointF(cx + s * 0.94f, cy - s * 0.08f),
                new PointF(cx + s * 0.64f, cy - s * 0.14f)
            ];
            g.FillPolygon(steel, blade);
            g.DrawPolygon(Pens.DimGray, blade);
        }

        private static void DrawArcherVector(Graphics g, float cx, float cy, float s)
        {
            DrawHumanoidCape(g, cx, cy, s, 0.68f, Color.FromArgb(52, 134, 72), Color.FromArgb(22, 68, 38), null);
            PointF[] hood =
            [
                new PointF(cx, cy - s * 0.88f),
                new PointF(cx + s * 0.46f, cy - s * 0.12f),
                new PointF(cx, cy + s * 0.12f),
                new PointF(cx - s * 0.46f, cy - s * 0.12f)
            ];
            using SolidBrush hb = new(Color.FromArgb(38, 108, 56));
            using SolidBrush shadow = new(Color.FromArgb(22, 28, 25));
            using SolidBrush glow = new(Color.FromArgb(255, 224, 92));
            g.FillPolygon(hb, hood);
            g.DrawPolygon(Pens.Black, hood);
            g.FillEllipse(shadow, cx - s * 0.26f, cy - s * 0.36f, s * 0.52f, s * 0.46f);
            g.FillEllipse(glow, cx - s * 0.14f, cy - s * 0.16f, s * 0.09f, s * 0.09f);
            g.FillEllipse(glow, cx + s * 0.06f, cy - s * 0.16f, s * 0.09f, s * 0.09f);

            using Pen bowWood = new(Color.FromArgb(178, 118, 56), 2.8f);
            using Pen bowString = new(Color.FromArgb(225, 225, 210), 1.2f);
            using Pen arrow = new(Color.FromArgb(245, 235, 195), 1.8f);
            g.DrawArc(bowWood, cx + s * 0.12f, cy - s * 0.58f, s * 0.78f, s * 1.16f, -80, 160);
            g.DrawLine(bowString, cx + s * 0.56f, cy - s * 0.56f, cx + s * 0.32f, cy);
            g.DrawLine(bowString, cx + s * 0.32f, cy, cx + s * 0.56f, cy + s * 0.56f);
            g.DrawLine(arrow, cx + s * 0.32f, cy, cx + s * 1.05f, cy);
            g.FillPolygon(Brushes.Silver,
            [
                new PointF(cx + s * 1.12f, cy),
                new PointF(cx + s * 0.94f, cy - s * 0.11f),
                new PointF(cx + s * 0.94f, cy + s * 0.11f)
            ]);
        }

        private static void DrawSpearmanVector(Graphics g, float cx, float cy, float s)
        {
            DrawHumanoidCape(g, cx, cy, s, 0.62f, Color.FromArgb(164, 44, 52), Color.FromArgb(118, 28, 34), null);
            using SolidBrush steel = new(Color.FromArgb(168, 180, 194));
            g.FillEllipse(steel, cx - s * 0.72f, cy - s * 0.08f, s * 0.44f, s * 0.36f);
            g.FillEllipse(steel, cx + s * 0.28f, cy - s * 0.08f, s * 0.44f, s * 0.36f);

            RectangleF helmRect = new(cx - s * 0.38f, cy - s * 0.66f, s * 0.76f, s * 0.76f);
            using LinearGradientBrush hb = new(helmRect, Color.FromArgb(215, 224, 235), Color.FromArgb(90, 102, 118), 0f);
            using SolidBrush visor = new(Color.FromArgb(20, 22, 28));
            FillRoundedRect(g, hb, helmRect, s * 0.20f);
            g.FillRectangle(visor, cx - s * 0.28f, cy - s * 0.34f, s * 0.56f, s * 0.11f);
            g.FillRectangle(visor, cx - s * 0.05f, cy - s * 0.34f, s * 0.10f, s * 0.30f);

            using Pen shaft = new(Color.FromArgb(132, 86, 46), 2.8f);
            using SolidBrush blade = new(Color.FromArgb(230, 240, 250));
            g.DrawLine(shaft, cx - s * 0.75f, cy + s * 0.75f, cx + s * 0.85f, cy - s * 0.72f);
            PointF[] spearTip =
            [
                new PointF(cx + s * 1.05f, cy - s * 0.92f),
                new PointF(cx + s * 0.68f, cy - s * 0.78f),
                new PointF(cx + s * 0.86f, cy - s * 0.56f)
            ];
            g.FillPolygon(blade, spearTip);
            g.DrawPolygon(Pens.SlateGray, spearTip);
        }

        private static void DrawShamanVector(Graphics g, float cx, float cy, float s, float animPhase)
        {
            DrawHumanoidCape(g, cx, cy, s, 0.72f, Color.FromArgb(72, 46, 98), Color.FromArgb(36, 22, 54), null);
            using Pen antler = new(Color.FromArgb(182, 152, 114), 2.2f);
            g.DrawLine(antler, cx - s * 0.28f, cy - s * 0.56f, cx - s * 0.64f, cy - s * 0.92f);
            g.DrawLine(antler, cx + s * 0.28f, cy - s * 0.56f, cx + s * 0.64f, cy - s * 0.92f);

            PointF[] mask =
            [
                new PointF(cx - s * 0.36f, cy - s * 0.62f),
                new PointF(cx + s * 0.36f, cy - s * 0.62f),
                new PointF(cx + s * 0.28f, cy + s * 0.14f),
                new PointF(cx, cy + s * 0.24f),
                new PointF(cx - s * 0.28f, cy + s * 0.14f)
            ];
            using SolidBrush mb = new(Color.FromArgb(138, 86, 52));
            using SolidBrush eyes = new(Color.FromArgb(110, 255, 110));
            g.FillPolygon(mb, mask);
            g.DrawPolygon(Pens.Black, mask);
            g.FillEllipse(eyes, cx - s * 0.20f, cy - s * 0.34f, s * 0.14f, s * 0.10f);
            g.FillEllipse(eyes, cx + s * 0.06f, cy - s * 0.34f, s * 0.14f, s * 0.10f);

            float pulse = (float)(Math.Sin(animPhase * 4.0) * 0.12 + 1.0);
            float vx = cx + s * 0.66f;
            float vy = cy + s * 0.12f;
            using SolidBrush aura = new(Color.FromArgb(90, 80, 255, 95));
            using SolidBrush liquid = new(Color.FromArgb(80, 245, 95));
            using Pen glass = new(Color.FromArgb(200, 240, 220), 1.4f);
            g.FillEllipse(aura, vx - s * 0.28f * pulse, vy - s * 0.28f * pulse, s * 0.56f * pulse, s * 0.56f * pulse);
            g.FillEllipse(liquid, vx - s * 0.18f, vy - s * 0.16f, s * 0.36f, s * 0.36f);
            g.DrawEllipse(glass, vx - s * 0.18f, vy - s * 0.16f, s * 0.36f, s * 0.36f);
            g.FillRectangle(Brushes.SaddleBrown, vx - s * 0.07f, vy - s * 0.26f, s * 0.14f, s * 0.10f);
        }

        private static void DrawBossVector(Graphics g, float cx, float cy, float s, float animPhase)
        {
            float pulse = (float)(Math.Sin(animPhase * 3.5) * 0.08 + 1.0);
            using SolidBrush aura = new(Color.FromArgb(55, 255, 75, 45));
            g.FillEllipse(aura, cx - s * 0.98f * pulse, cy - s * 0.88f * pulse, s * 1.96f * pulse, s * 1.80f * pulse);

            DrawHumanoidCape(g, cx, cy, s, 0.86f, Color.FromArgb(175, 34, 40), Color.FromArgb(72, 14, 18), Color.FromArgb(245, 198, 66));
            DrawBossDualWeapons(g, cx, cy, s);
            DrawBeardedHead(g, cx, cy, s, Color.FromArgb(222, 170, 138), Color.FromArgb(110, 48, 28), Color.FromArgb(62, 24, 14), Color.FromArgb(255, 95, 65));
            DrawHornPair(g, cx, cy, s, 1.08f, Color.FromArgb(245, 232, 198), Pens.SaddleBrown);

            RectangleF helm = new(cx - s * 0.48f, cy - s * 0.80f, s * 0.96f, s * 0.84f);
            using LinearGradientBrush hb = new(helm, Color.FromArgb(98, 108, 124), Color.FromArgb(36, 42, 52), 90f);
            using SolidBrush gold = new(Color.FromArgb(248, 202, 58));
            g.FillPie(hb, helm.X, helm.Y, helm.Width, helm.Height, 180, 180);
            PointF[] crown =
            [
                new PointF(cx - s * 0.46f, cy - s * 0.38f),
                new PointF(cx - s * 0.42f, cy - s * 0.74f),
                new PointF(cx - s * 0.22f, cy - s * 0.52f),
                new PointF(cx, cy - s * 0.88f),
                new PointF(cx + s * 0.22f, cy - s * 0.52f),
                new PointF(cx + s * 0.42f, cy - s * 0.74f),
                new PointF(cx + s * 0.46f, cy - s * 0.38f)
            ];
            g.FillPolygon(gold, crown);
            g.DrawPolygon(Pens.DarkGoldenrod, crown);
        }

        private static void DrawBossDualWeapons(Graphics g, float cx, float cy, float s)
        {
            using Pen clubShaft = new(Color.FromArgb(130, 82, 38), 3.6f);
            using SolidBrush clubHead = new(Color.FromArgb(112, 68, 34));
            g.DrawLine(clubShaft, cx - s * 0.56f, cy + s * 0.54f, cx - s * 0.96f, cy - s * 0.42f);
            g.FillEllipse(clubHead, cx - s * 1.08f, cy - s * 0.58f, s * 0.30f, s * 0.38f);
            g.DrawEllipse(Pens.Silver, cx - s * 1.08f, cy - s * 0.58f, s * 0.30f, s * 0.38f);

            using Pen axeShaft = new(Color.FromArgb(115, 68, 32), 3.2f);
            g.DrawLine(axeShaft, cx + s * 0.56f, cy + s * 0.60f, cx + s * 0.92f, cy - s * 0.68f);
            PointF[] dualBlade =
            [
                new PointF(cx + s * 0.72f, cy - s * 0.54f),
                new PointF(cx + s * 1.18f, cy - s * 0.76f),
                new PointF(cx + s * 1.20f, cy - s * 0.16f),
                new PointF(cx + s * 0.72f, cy - s * 0.28f)
            ];
            using LinearGradientBrush steel = new(new PointF(cx + s * 0.7f, cy - s * 0.76f), new PointF(cx + s * 1.2f, cy - s * 0.16f), Color.FromArgb(245, 230, 215), Color.FromArgb(148, 52, 46));
            g.FillPolygon(steel, dualBlade);
            g.DrawPolygon(Pens.Gold, dualBlade);
        }

        public static void DrawWeaponIcon(Graphics g, string weaponId, RectangleF r)
        {
            float cx = r.X + r.Width * 0.5f;
            float cy = r.Y + r.Height * 0.5f;
            float s = Math.Min(r.Width, r.Height) * 0.42f;

            switch (weaponId)
            {
                case "viking_axe":
                    DrawVikingAxeIcon(g, r, cx, cy, s);
                    break;
                case "wooden_club":
                    DrawClubIcon(g, r, cx, cy, s);
                    break;
                case "two_handed_hammer":
                    DrawHammerIcon(g, r, cx, cy, s);
                    break;
                case "throwing_axe":
                    DrawThrowingAxeIcon(g, cx, cy, s);
                    break;
                case "hunting_bow":
                    DrawBowIcon(g, cx, cy, s);
                    break;
                case "heavy_spear":
                    DrawSpearIcon(g, r, cx, cy, s);
                    break;
                case "bear_trap":
                    DrawBearTrapIcon(g, cx, cy, s);
                    break;
                case "boat_hook":
                    DrawBoatHookIcon(g, cx, cy, s);
                    break;
                case "diagonal_dash":
                    DrawDashIcon(g, cx, cy, s);
                    break;
                case "slide":
                    DrawSlideIcon(g, cx, cy, s);
                    break;
            }
        }

        private static void DrawVikingAxeIcon(Graphics g, RectangleF r, float cx, float cy, float s)
        {
            using Pen p = new(Color.FromArgb(160, 102, 52), Math.Max(2.2f, s * 0.16f));
            g.DrawLine(p, cx - s * 0.72f, cy + s * 0.76f, cx + s * 0.56f, cy - s * 0.68f);
            PointF[] blade =
            [
                new PointF(cx + s * 0.12f, cy - s * 0.56f),
                new PointF(cx + s * 0.84f, cy - s * 0.86f),
                new PointF(cx + s * 0.96f, cy - s * 0.04f),
                new PointF(cx + s * 0.34f, cy - s * 0.16f)
            ];
            using LinearGradientBrush gb = new(r, Color.FromArgb(238, 245, 252), Color.FromArgb(132, 148, 168), 45f);
            g.FillPolygon(gb, blade);
            g.DrawPolygon(Pens.DarkSlateGray, blade);
        }

        private static void DrawClubIcon(Graphics g, RectangleF r, float cx, float cy, float s)
        {
            PointF[] club =
            [
                new PointF(cx - s * 0.78f, cy + s * 0.76f),
                new PointF(cx - s * 0.60f, cy + s * 0.88f),
                new PointF(cx + s * 0.84f, cy - s * 0.38f),
                new PointF(cx + s * 0.42f, cy - s * 0.84f)
            ];
            using LinearGradientBrush wb = new(r, Color.FromArgb(182, 122, 62), Color.FromArgb(98, 56, 26), 45f);
            using SolidBrush stud = new(Color.LightSteelBlue);
            g.FillPolygon(wb, club);
            g.DrawPolygon(Pens.Black, club);
            g.FillEllipse(stud, cx + s * 0.25f, cy - s * 0.48f, s * 0.18f, s * 0.18f);
            g.FillEllipse(stud, cx + s * 0.48f, cy - s * 0.32f, s * 0.18f, s * 0.18f);
            g.FillEllipse(stud, cx + s * 0.08f, cy - s * 0.24f, s * 0.18f, s * 0.18f);
        }

        private static void DrawHammerIcon(Graphics g, RectangleF r, float cx, float cy, float s)
        {
            using Pen p = new(Color.FromArgb(148, 96, 48), Math.Max(2.5f, s * 0.18f));
            g.DrawLine(p, cx - s * 0.78f, cy + s * 0.78f, cx + s * 0.46f, cy - s * 0.46f);
            PointF[] head =
            [
                new PointF(cx - s * 0.08f, cy - s * 0.88f),
                new PointF(cx + s * 0.30f, cy - s * 0.96f),
                new PointF(cx + s * 0.96f, cy - s * 0.30f),
                new PointF(cx + s * 0.88f, cy + s * 0.08f),
                new PointF(cx + s * 0.46f, cy + s * 0.12f),
                new PointF(cx - s * 0.12f, cy - s * 0.46f)
            ];
            using LinearGradientBrush hb = new(r, Color.FromArgb(225, 232, 240), Color.FromArgb(95, 108, 125), 45f);
            using Pen trim = new(Color.Gold, 1.5f);
            g.FillPolygon(hb, head);
            g.DrawPolygon(trim, head);
        }

        private static void DrawThrowingAxeIcon(Graphics g, float cx, float cy, float s)
        {
            using Pen motion = new(Color.FromArgb(160, 120, 245, 165), 1.6f) { DashStyle = DashStyle.Dash };
            using Pen shaft = new(Color.FromArgb(162, 108, 56), Math.Max(2f, s * 0.15f));
            using SolidBrush steel = new(Color.FromArgb(225, 238, 248));
            g.DrawArc(motion, cx - s * 0.92f, cy - s * 0.92f, s * 1.84f, s * 1.84f, 140, 110);
            g.DrawLine(shaft, cx - s * 0.45f, cy + s * 0.65f, cx + s * 0.38f, cy - s * 0.56f);
            PointF[] blade =
            [
                new PointF(cx + s * 0.02f, cy - s * 0.42f),
                new PointF(cx + s * 0.78f, cy - s * 0.52f),
                new PointF(cx + s * 0.72f, cy + s * 0.18f),
                new PointF(cx + s * 0.14f, cy - s * 0.05f)
            ];
            g.FillPolygon(steel, blade);
            g.DrawPolygon(Pens.DarkSlateGray, blade);
        }

        private static void DrawBowIcon(Graphics g, float cx, float cy, float s)
        {
            using Pen bow = new(Color.FromArgb(196, 134, 66), Math.Max(2.4f, s * 0.16f));
            using Pen str = new(Color.FromArgb(230, 232, 220), 1.3f);
            using Pen arrow = new(Color.FromArgb(160, 245, 175), 2f);
            g.DrawArc(bow, cx - s * 0.72f, cy - s * 0.88f, s * 1.36f, s * 1.76f, -72, 144);
            g.DrawLine(str, cx + s * 0.15f, cy - s * 0.82f, cx - s * 0.36f, cy);
            g.DrawLine(str, cx - s * 0.36f, cy, cx + s * 0.15f, cy + s * 0.82f);
            g.DrawLine(arrow, cx - s * 0.42f, cy, cx + s * 0.88f, cy - s * 0.38f);
            g.FillPolygon(Brushes.WhiteSmoke,
            [
                new PointF(cx + s * 0.96f, cy - s * 0.42f),
                new PointF(cx + s * 0.68f, cy - s * 0.46f),
                new PointF(cx + s * 0.78f, cy - s * 0.22f)
            ]);
        }

        private static void DrawSpearIcon(Graphics g, RectangleF r, float cx, float cy, float s)
        {
            using Pen shaft = new(Color.FromArgb(156, 102, 52), Math.Max(2.2f, s * 0.15f));
            using LinearGradientBrush gb = new(r, Color.FromArgb(245, 250, 255), Color.FromArgb(110, 195, 145), 45f);
            g.DrawLine(shaft, cx - s * 0.86f, cy + s * 0.86f, cx + s * 0.46f, cy - s * 0.46f);
            PointF[] tip =
            [
                new PointF(cx + s * 0.94f, cy - s * 0.94f),
                new PointF(cx + s * 0.22f, cy - s * 0.54f),
                new PointF(cx + s * 0.54f, cy - s * 0.22f)
            ];
            g.FillPolygon(gb, tip);
            g.DrawPolygon(Pens.DarkSlateGray, tip);
        }

        private static void DrawBearTrapIcon(Graphics g, float cx, float cy, float s)
        {
            using Pen jaw = new(Color.FromArgb(196, 206, 220), 2.5f);
            using SolidBrush plate = new(Color.FromArgb(205, 78, 92));
            g.DrawEllipse(jaw, cx - s * 0.84f, cy - s * 0.64f, s * 1.68f, s * 1.28f);
            g.FillEllipse(plate, cx - s * 0.28f, cy - s * 0.22f, s * 0.56f, s * 0.44f);
            DrawTrapSpikes(g, cx, cy, s * 0.28f, s * 0.46f, s * 0.18f, s * 0.08f, Color.WhiteSmoke);
        }

        private static void DrawTrapSpikes(Graphics g, float cx, float cy, float stepX, float outerY, float innerY, float halfW, Color spikeColor)
        {
            using SolidBrush tooth = new(spikeColor);
            for (int i = -2; i <= 2; i++)
            {
                float tx = cx + i * stepX;
                g.FillPolygon(tooth, [new PointF(tx - halfW, cy - outerY), new PointF(tx + halfW, cy - outerY), new PointF(tx, cy - innerY)]);
                g.FillPolygon(tooth, [new PointF(tx - halfW, cy + outerY), new PointF(tx + halfW, cy + outerY), new PointF(tx, cy + innerY)]);
            }
        }

        private static void DrawBoatHookIcon(Graphics g, float cx, float cy, float s)
        {
            using Pen rope = new(Color.FromArgb(205, 172, 118), 2f) { DashStyle = DashStyle.Dot };
            using Pen hook = new(Color.FromArgb(215, 228, 245), Math.Max(2.6f, s * 0.18f));
            g.DrawLine(rope, cx - s * 0.84f, cy + s * 0.80f, cx - s * 0.18f, cy + s * 0.18f);
            g.DrawLine(hook, cx - s * 0.24f, cy + s * 0.24f, cx + s * 0.52f, cy - s * 0.52f);
            g.DrawArc(hook, cx + s * 0.12f, cy - s * 0.92f, s * 0.78f, s * 0.78f, -150, 210);
            g.FillPolygon(Brushes.Violet,
            [
                new PointF(cx + s * 0.88f, cy - s * 0.62f),
                new PointF(cx + s * 0.56f, cy - s * 0.74f),
                new PointF(cx + s * 0.68f, cy - s * 0.42f)
            ]);
        }

        private static void DrawDashIcon(Graphics g, float cx, float cy, float s)
        {
            using Pen trail = new(Color.FromArgb(110, 195, 255), 2.4f);
            using SolidBrush cyan = new(Color.FromArgb(120, 232, 255));
            g.DrawLine(trail, cx - s * 0.78f, cy + s * 0.72f, cx + s * 0.46f, cy - s * 0.52f);
            g.DrawLine(trail, cx - s * 0.86f, cy + s * 0.36f, cx + s * 0.08f, cy - s * 0.58f);
            g.DrawLine(trail, cx - s * 0.40f, cy + s * 0.84f, cx + s * 0.54f, cy - s * 0.10f);
            g.FillPolygon(cyan, [new PointF(cx + s * 0.82f, cy - s * 0.82f), new PointF(cx + s * 0.18f, cy - s * 0.74f), new PointF(cx + s * 0.74f, cy - s * 0.18f)]);
        }

        private static void DrawSlideIcon(Graphics g, float cx, float cy, float s)
        {
            using Pen streak = new(Color.FromArgb(100, 195, 255), 2.4f);
            using SolidBrush cyan = new(Color.FromArgb(135, 236, 255));
            g.DrawLine(streak, cx - s * 0.88f, cy - s * 0.24f, cx + s * 0.34f, cy - s * 0.24f);
            g.DrawLine(streak, cx - s * 0.72f, cy, cx + s * 0.58f, cy);
            g.DrawLine(streak, cx - s * 0.88f, cy + s * 0.24f, cx + s * 0.34f, cy + s * 0.24f);
            g.FillPolygon(cyan, [new PointF(cx + s * 0.88f, cy), new PointF(cx + s * 0.34f, cy - s * 0.46f), new PointF(cx + s * 0.50f, cy), new PointF(cx + s * 0.34f, cy + s * 0.46f)]);
        }

        public static void DrawUpgradeIcon(Graphics g, string upgradeId, RectangleF r)
        {
            float cx = r.X + r.Width * 0.5f;
            float cy = r.Y + r.Height * 0.5f;
            float s = Math.Min(r.Width, r.Height) * 0.42f;

            using SolidBrush bg = new(Color.FromArgb(46, 62, 86));
            using Pen rim = new(Color.FromArgb(125, 195, 245), 1.8f);
            g.FillEllipse(bg, cx - s, cy - s, s * 2, s * 2);
            g.DrawEllipse(rim, cx - s, cy - s, s * 2, s * 2);

            if (upgradeId == "durability_1")
            {
                using SolidBrush red = new(Color.FromArgb(235, 72, 86));
                g.FillPolygon(red, [new PointF(cx, cy + s * 0.56f), new PointF(cx - s * 0.54f, cy - s * 0.04f), new PointF(cx - s * 0.30f, cy - s * 0.48f), new PointF(cx, cy - s * 0.22f), new PointF(cx + s * 0.30f, cy - s * 0.48f), new PointF(cx + s * 0.54f, cy - s * 0.04f)]);
            }
            else if (upgradeId == "swift_boots")
            {
                using SolidBrush cb = new(Color.FromArgb(95, 205, 255));
                g.FillPolygon(cb, [new PointF(cx - s * 0.40f, cy - s * 0.48f), new PointF(cx + s * 0.06f, cy - s * 0.48f), new PointF(cx + s * 0.06f, cy + s * 0.14f), new PointF(cx + s * 0.56f, cy + s * 0.24f), new PointF(cx + s * 0.56f, cy + s * 0.52f), new PointF(cx - s * 0.44f, cy + s * 0.52f)]);
            }
            else if (upgradeId == "charisma")
            {
                g.FillPolygon(Brushes.Gold, CreateStar(cx, cy, s * 0.62f, s * 0.28f, 5));
            }
            else if (upgradeId == "melee_1")
            {
                DrawWeaponIcon(g, "viking_axe", RectangleF.Inflate(r, -4, -4));
            }
            else if (upgradeId == "ranged_1")
            {
                DrawWeaponIcon(g, "hunting_bow", RectangleF.Inflate(r, -4, -4));
            }
            else if (upgradeId == "tactical_master")
            {
                using Pen clock = new(Color.FromArgb(145, 235, 255), 2.2f);
                g.DrawEllipse(clock, cx - s * 0.54f, cy - s * 0.54f, s * 1.08f, s * 1.08f);
                g.DrawLine(clock, cx, cy, cx, cy - s * 0.36f);
                g.DrawLine(clock, cx, cy, cx + s * 0.28f, cy + s * 0.12f);
            }
            else
            {
                using SolidBrush bolt = new(Color.FromArgb(220, 150, 255));
                g.FillPolygon(bolt, [new PointF(cx + s * 0.12f, cy - s * 0.62f), new PointF(cx - s * 0.34f, cy + s * 0.04f), new PointF(cx + s * 0.02f, cy + s * 0.04f), new PointF(cx - s * 0.14f, cy + s * 0.64f), new PointF(cx + s * 0.38f, cy - s * 0.08f), new PointF(cx + s * 0.02f, cy - s * 0.08f)]);
            }
        }

        private static PointF[] CreateStar(float cx, float cy, float outerR, float innerR, int points)
        {
            PointF[] res = new PointF[points * 2];
            double angle = -Math.PI / 2.0;
            double step = Math.PI / points;
            for (int i = 0; i < points * 2; i++)
            {
                float rad = i % 2 == 0 ? outerR : innerR;
                res[i] = new PointF(cx + (float)(Math.Cos(angle) * rad), cy + (float)(Math.Sin(angle) * rad));
                angle += step;
            }
            return res;
        }

        public static void DrawTrophyIcon(Graphics g, RectangleF r, bool unlocked)
        {
            float cx = r.X + r.Width * 0.5f;
            float cy = r.Y + r.Height * 0.5f;
            float s = Math.Min(r.Width, r.Height) * 0.44f;
            Color c1 = unlocked ? Color.FromArgb(255, 224, 92) : Color.FromArgb(105, 114, 128);
            Color c2 = unlocked ? Color.FromArgb(206, 146, 36) : Color.FromArgb(62, 68, 78);

            using Pen handles = new(c1, 2.2f);
            using LinearGradientBrush gb = new(r, c1, c2, 90f);
            g.DrawArc(handles, cx - s * 0.88f, cy - s * 0.62f, s * 0.54f, s * 0.62f, 90, 180);
            g.DrawArc(handles, cx + s * 0.34f, cy - s * 0.62f, s * 0.54f, s * 0.62f, 270, 180);
            g.FillPolygon(gb, [new PointF(cx - s * 0.58f, cy - s * 0.72f), new PointF(cx + s * 0.58f, cy - s * 0.72f), new PointF(cx + s * 0.40f, cy + s * 0.08f), new PointF(cx, cy + s * 0.28f), new PointF(cx - s * 0.40f, cy + s * 0.08f)]);
            g.FillRectangle(gb, cx - s * 0.14f, cy + s * 0.22f, s * 0.28f, s * 0.34f);
            g.FillRectangle(gb, cx - s * 0.48f, cy + s * 0.52f, s * 0.96f, s * 0.22f);
        }

        public static void DrawHazardTile(Graphics g, HazardInstance h, RectangleF tileRect, float animPhase)
        {
            if (h.Type == HazardType.Poison)
            {
                DrawPoisonTile(g, h, tileRect, animPhase);
            }
            else if (h.Type == HazardType.BearTrap)
            {
                DrawBearTrapTile(g, h, tileRect);
            }
        }

        private static void DrawPoisonTile(Graphics g, HazardInstance h, RectangleF tileRect, float animPhase)
        {
            float pulse = (float)(Math.Sin(animPhase * 4.2 + h.Pos.X + h.Pos.Y) * 0.14 + 0.86);
            DrawPanelBox(g, RectangleF.Inflate(tileRect, -3, -3), Color.FromArgb((int)(88 * pulse), 48, 212, 72), Color.FromArgb(170, 92, 248, 108), 6f, 1.6f);
            for (int i = 0; i < 4; i++)
            {
                float phase = (animPhase * 1.4f + i * 0.27f + h.Pos.X * 0.2f) % 1.0f;
                float bx = tileRect.X + 16 + ((i * 19 + h.Pos.Y * 7) % 48);
                float by = tileRect.Bottom - 12 - phase * 42f;
                float br = 3.2f + (1f - phase) * 3.5f;
                using SolidBrush bub = new(Color.FromArgb((int)((1f - phase) * 190), 130, 255, 130));
                g.FillEllipse(bub, bx - br, by - br, br * 2, br * 2);
            }
            using Font f = new("Segoe UI", 7f, FontStyle.Bold);
            using SolidBrush txt = new(Color.FromArgb(190, 255, 190));
            g.DrawString("POISON " + h.RemainingTurns.ToString(CultureInfo.InvariantCulture) + "T", f, txt, tileRect.X + 5, tileRect.Bottom - 14);
        }

        private static void DrawBearTrapTile(Graphics g, HazardInstance h, RectangleF tileRect)
        {
            float cx = tileRect.X + tileRect.Width * 0.5f;
            float cy = tileRect.Y + tileRect.Height * 0.56f;
            Color jawColor = h.IsArmed ? Color.FromArgb(228, 232, 240) : Color.FromArgb(128, 136, 148);
            Color padColor = h.IsArmed ? Color.FromArgb(225, 62, 78) : Color.FromArgb(115, 118, 128);

            using SolidBrush plate = new(Color.FromArgb(90, 20, 24, 30));
            using Pen jawPen = new(jawColor, 2.4f);
            using SolidBrush trig = new(padColor);
            g.FillEllipse(plate, cx - 22, cy - 15, 44, 30);
            g.DrawEllipse(jawPen, cx - 21, cy - 14, 42, 28);
            g.FillEllipse(trig, cx - 8, cy - 6, 16, 12);
            DrawTrapSpikes(g, cx, cy, 8f, 13f, 5f, 2.5f, jawColor);

            using Font f = new("Segoe UI", 6.8f, FontStyle.Bold);
            using SolidBrush txt = new(h.IsArmed ? Color.FromArgb(255, 155, 165) : Color.LightGray);
            g.DrawString(h.IsArmed ? "ARMED TRAP" : "UNARMED", f, txt, tileRect.X + 5, tileRect.Bottom - 14);
        }

        public static void DrawEnemyIntentBadge(Graphics g, EnemyInstance e, RectangleF tileRect, float animPhase)
        {
            RectangleF badge = new(tileRect.X + 4, tileRect.Y + 3, tileRect.Width - 8, 15);
            (Color bg, string label, Color fg) = GetEnemyBadgeStyle(e, animPhase);
            using SolidBrush b = new(bg);
            using Font f = new("Segoe UI", 6.8f, FontStyle.Bold);
            using SolidBrush tf = new(fg);
            FillRoundedRect(g, b, badge, 4f);
            DrawCenteredText(g, label, f, tf, badge);
        }

        private static (Color Bg, string Label, Color Fg) GetEnemyBadgeStyle(EnemyInstance e, float animPhase)
        {
            if (e.StunTurns > 0)
            {
                return (Color.FromArgb(148, 56, 195), "STUNNED", Color.White);
            }
            if (e.State == EnemyAIState.Unarmed)
            {
                return (Color.FromArgb(72, 80, 94), "EQUIPPING", Color.LightGray);
            }
            if (e.State == EnemyAIState.Charging)
            {
                int flash = Math.Clamp((int)(175 + Math.Sin(animPhase * 9.0) * 65), 110, 245);
                string text = e.ChargeTurnsRemaining > 1 ? "CHARGE (" + e.ChargeTurnsRemaining.ToString(CultureInfo.InvariantCulture) + ")" : "! ATTACK !";
                return (Color.FromArgb(flash, 34, 34), text, Color.FromArgb(255, 245, 180));
            }
            string weaponTag = e.EnemyId switch
            {
                "pawn" => "AXE",
                "archer" => "BOW",
                "spearman" => "SPEAR",
                "shaman" => "POTION",
                _ => "BOSS C" + (e.BossCycleIndex + 1).ToString(CultureInfo.InvariantCulture)
            };
            return (Color.FromArgb(52, 68, 92), weaponTag, Color.White);
        }

        public static void DrawPhysicalCard(
            Graphics g,
            WeaponDef def,
            CardInstance inst,
            GameDatabase db,
            ProfileData profile,
            RectangleF rect,
            bool isHovered,
            bool isSelected,
            bool isExecuting,
            int queueNumber,
            bool compact)
        {
            RectangleF r = (isHovered && !isExecuting) ? new RectangleF(rect.X, rect.Y - 6, rect.Width, rect.Height) : rect;
            float headerH = compact ? 24f : 26f;

            DrawCardShellAndHeader(g, def, r, headerH, isHovered, isSelected, isExecuting, queueNumber, compact);
            RectangleF artBox = DrawCardArtBox(g, def, r, headerH, compact);
            float descTop = DrawCardStatsAndMods(g, def, inst, db, profile, r, artBox.Bottom + 4, compact);

            RectangleF descRect = new(r.X + 7, descTop, r.Width - 14, Math.Max(12f, r.Bottom - descTop - 5));
            using Font df = new("Segoe UI", compact ? 6.6f : 7.3f, FontStyle.Regular);
            using SolidBrush dtxt = new(Color.FromArgb(215, 224, 236));
            g.DrawString(def.Description, df, dtxt, descRect);

            int curCd = inst != null ? inst.CurrentCooldown : 0;
            if (curCd > 0)
            {
                DrawCardCooldownVeil(g, r, curCd);
            }
        }

        private static void DrawCardShellAndHeader(Graphics g, WeaponDef def, RectangleF r, float headerH, bool isHovered, bool isSelected, bool isExecuting, int queueNumber, bool compact)
        {
            using SolidBrush sh = new(Color.FromArgb(135, 0, 0, 0));
            FillRoundedRect(g, sh, new RectangleF(r.X + 4, r.Y + 5, r.Width, r.Height), 9f);

            Color bodyTop = isExecuting ? Color.FromArgb(58, 54, 38) : Color.FromArgb(34, 41, 54);
            Color bodyBot = isExecuting ? Color.FromArgb(40, 36, 26) : Color.FromArgb(22, 27, 36);
            using LinearGradientBrush bodyBrush = new(r, bodyTop, bodyBot, 90f);
            FillRoundedRect(g, bodyBrush, r, 9f);

            Color headerColor = GetCategoryHeaderColor(def.Category);
            RectangleF headerRect = new(r.X + 1, r.Y + 1, r.Width - 2, headerH);
            using LinearGradientBrush hb = new(headerRect, headerColor, ControlPaint.Dark(headerColor, 0.25f), 90f);
            FillRoundedRect(g, hb, headerRect, 8f);

            (Color borderColor, float borderW) = isExecuting
                ? (Color.FromArgb(255, 215, 70), 3.0f)
                : isSelected
                    ? (Color.FromArgb(95, 230, 175), 2.8f)
                    : isHovered
                        ? (GetCategoryLightColor(def.Category), 2.4f)
                        : (Color.FromArgb(74, 86, 108), 1.6f);

            using Pen bp = new(borderColor, borderW);
            DrawRoundedRect(g, bp, r, 9f);

            using Font nameFont = new("Segoe UI", compact ? 7.6f : 8.4f, FontStyle.Bold);
            DrawCenteredText(g, def.Name, nameFont, Brushes.White, headerRect);

            if (queueNumber > 0)
            {
                RectangleF qBadge = new(r.X - 6, r.Y - 6, 20, 20);
                using SolidBrush qb = new(isExecuting ? Color.Gold : Color.FromArgb(52, 148, 224));
                using Font qf = new("Segoe UI", 8.5f, FontStyle.Bold);
                g.FillEllipse(qb, qBadge);
                g.DrawEllipse(Pens.White, qBadge);
                DrawCenteredText(g, queueNumber.ToString(CultureInfo.InvariantCulture), qf, isExecuting ? Brushes.Black : Brushes.White, qBadge);
            }
        }

        private static RectangleF DrawCardArtBox(Graphics g, WeaponDef def, RectangleF r, float headerH, bool compact)
        {
            RectangleF artBox = new(r.X + 7, r.Y + headerH + 4, r.Width - 14, compact ? 42f : 50f);
            DrawPanelBox(g, artBox, Color.FromArgb(18, 22, 30), Color.FromArgb(56, 66, 84), 5f, 1f);
            DrawWeaponIcon(g, def.Id, RectangleF.Inflate(artBox, -6, -3));

            using Font catFont = new("Segoe UI", 6.2f, FontStyle.Bold);
            using SolidBrush catTxt = new(GetCategoryLightColor(def.Category));
            g.DrawString(def.Category.ToString().ToUpperInvariant(), catFont, catTxt, artBox.X + 4, artBox.Y + 2);
            return artBox;
        }

        private static float DrawCardStatsAndMods(Graphics g, WeaponDef def, CardInstance inst, GameDatabase db, ProfileData profile, RectangleF r, float statsY, bool compact)
        {
            int dmg = inst != null ? inst.GetEffectiveDamage(db, profile) : def.BaseDamage;
            int maxCd = inst != null ? inst.GetEffectiveMaxCooldown(db, profile) : def.BaseCooldown;
            int curCd = inst != null ? inst.CurrentCooldown : 0;

            RectangleF dmgPill = new(r.X + 7, statsY, (r.Width - 18) * 0.48f, 18);
            RectangleF cdPill = new(dmgPill.Right + 4, statsY, (r.Width - 18) * 0.52f, 18);

            using SolidBrush dBg = new(Color.FromArgb(62, 34, 38));
            using SolidBrush cBg = new(curCd > 0 ? Color.FromArgb(88, 56, 26) : Color.FromArgb(30, 52, 74));
            using Font stFont = new("Segoe UI", compact ? 6.8f : 7.4f, FontStyle.Bold);
            FillRoundedRect(g, dBg, dmgPill, 4f);
            FillRoundedRect(g, cBg, cdPill, 4f);

            string dmgStr = def.Category == CardCategory.Movement ? "MOVE" : ("DMG " + dmg.ToString(CultureInfo.InvariantCulture));
            string cdStr = curCd > 0
                ? ("CD " + curCd.ToString(CultureInfo.InvariantCulture) + "/" + maxCd.ToString(CultureInfo.InvariantCulture))
                : ("CD " + maxCd.ToString(CultureInfo.InvariantCulture));
            DrawCenteredText(g, dmgStr, stFont, Brushes.LightSalmon, dmgPill);
            DrawCenteredText(g, cdStr, stFont, curCd > 0 ? Brushes.Orange : Brushes.LightSkyBlue, cdPill);

            float nextY = dmgPill.Bottom + 3;
            string modStr = inst != null ? inst.GetModifiersSummary() : string.Empty;
            if (!string.IsNullOrEmpty(modStr))
            {
                RectangleF modRect = new(r.X + 6, nextY, r.Width - 12, 14);
                using SolidBrush mb = new(Color.FromArgb(52, 66, 44));
                using Font mf = new("Segoe UI", 6.4f, FontStyle.Bold);
                FillRoundedRect(g, mb, modRect, 3f);
                DrawCenteredText(g, modStr, mf, Brushes.LightGreen, modRect);
                nextY = modRect.Bottom + 3;
            }
            return nextY;
        }

        private static void DrawCardCooldownVeil(Graphics g, RectangleF r, int curCd)
        {
            using SolidBrush veil = new(Color.FromArgb(128, 12, 16, 22));
            FillRoundedRect(g, veil, r, 9f);
            RectangleF cdCircle = new(r.X + r.Width * 0.5f - 20, r.Y + r.Height * 0.44f - 20, 40, 40);
            using SolidBrush cb = new(Color.FromArgb(215, 36, 44, 58));
            using Pen cp = new(Color.Orange, 2.5f);
            using Font cf = new("Segoe UI", 13f, FontStyle.Bold);
            g.FillEllipse(cb, cdCircle);
            g.DrawEllipse(cp, cdCircle);
            DrawCenteredText(g, curCd.ToString(CultureInfo.InvariantCulture), cf, Brushes.Orange, cdCircle);
        }
    }

    public class ClickRegion
    {
        public RectangleF Bounds { get; init; }
        public string ActionId { get; init; } = string.Empty;
        public int Index { get; init; }
        public string Tag { get; init; } = string.Empty;
    }

    public class GameCanvas : Control
    {
        private const float TileSize = 82f;
        private static readonly RectangleF GridRect = new(404, 58, 410, 410);

        private readonly GameEngine _engine;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly List<ClickRegion> _clickRegions = [];

        private ScreenMode _screen = ScreenMode.MainMenu;
        private string _selectedCharacterId = "viking";
        private float _animTime;
        private Point _mousePos;
        private GridPos? _hoveredGridTile;
        private CardInstance _hoveredCard;

        private int _dragFromIndex = -1;
        private int _dragHoverIndex = -1;
        private bool _isDraggingQueueCard;
        private Point _dragStartPoint;

        public GameCanvas(GameEngine engine)
        {
            _engine = engine;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(16, 20, 28);
            TabStop = true;

            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _animTime += 0.033f;
            _engine.UpdateAnimations(0.033f);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            _clickRegions.Clear();

            using LinearGradientBrush bg = new(new Rectangle(0, 0, Width, Height), Color.FromArgb(18, 23, 33), Color.FromArgb(11, 14, 22), 90f);
            g.FillRectangle(bg, 0, 0, Width, Height);

            if (_screen == ScreenMode.MainMenu)
            {
                RenderMainMenu(g);
            }
            else if (_screen == ScreenMode.Gameplay)
            {
                RenderGameplay(g);
            }
            else
            {
                RenderAchievementsAndStats(g);
            }

            RenderToastBanner(g);
        }

        private void RegisterClick(RectangleF bounds, string actionId, int index = 0, string tag = "")
        {
            _clickRegions.Add(new ClickRegion
            {
                Bounds = bounds,
                ActionId = actionId,
                Index = index,
                Tag = tag ?? string.Empty
            });
        }

        private bool IsMouseOver(RectangleF r)
        {
            return r.Contains(_mousePos);
        }

        private void DrawButton(Graphics g, RectangleF r, string text, Color baseColor, bool enabled, string actionId, int index = 0, string tag = "", string subText = "")
        {
            bool hov = enabled && IsMouseOver(r);
            Color top = !enabled ? Color.FromArgb(44, 50, 60) : (hov ? ControlPaint.Light(baseColor, 0.18f) : baseColor);
            Color bot = !enabled ? Color.FromArgb(32, 36, 44) : ControlPaint.Dark(baseColor, 0.28f);
            Color border = !enabled ? Color.FromArgb(62, 68, 78) : (hov ? Color.White : ControlPaint.Light(baseColor, 0.35f));

            using LinearGradientBrush gb = new(r, top, bot, 90f);
            using Pen bp = new(border, hov ? 2.2f : 1.4f);
            VectorArtRenderer.FillRoundedRect(g, gb, r, 7f);
            VectorArtRenderer.DrawRoundedRect(g, bp, r, 7f);

            using SolidBrush tb1 = new(enabled ? Color.White : Color.Gray);
            if (string.IsNullOrEmpty(subText))
            {
                using Font f = new("Segoe UI", 10.5f, FontStyle.Bold);
                VectorArtRenderer.DrawCenteredText(g, text, f, tb1, r);
            }
            else
            {
                using Font f1 = new("Segoe UI", 10f, FontStyle.Bold);
                using Font f2 = new("Segoe UI", 7.8f, FontStyle.Regular);
                using SolidBrush tb2 = new(enabled ? Color.FromArgb(225, 236, 248) : Color.DimGray);
                VectorArtRenderer.DrawCenteredText(g, text, f1, tb1, new RectangleF(r.X, r.Y + 4, r.Width, r.Height * 0.56f));
                VectorArtRenderer.DrawCenteredText(g, subText, f2, tb2, new RectangleF(r.X, r.Y + r.Height * 0.54f, r.Width, r.Height * 0.40f));
            }

            if (enabled)
            {
                RegisterClick(r, actionId, index, tag);
            }
        }

        private void RenderMainMenu(Graphics g)
        {
            RenderMenuHeader(g);
            float contentTop = RenderMenuNotification(g);
            RenderHeroSelectionPanel(g, new RectangleF(24, contentTop, 516, 704 - contentTop));
            RenderArmoryAndTrainingPanel(g, new RectangleF(558, contentTop, 698, 704 - contentTop));
        }

        private void RenderMenuHeader(Graphics g)
        {
            using LinearGradientBrush hb = new(new Rectangle(0, 0, Width, 74), Color.FromArgb(26, 34, 48), Color.FromArgb(18, 24, 34), 90f);
            using Pen div = new(Color.FromArgb(58, 72, 96), 2f);
            g.FillRectangle(hb, 0, 0, Width, 74);
            g.DrawLine(div, 0, 74, Width, 74);

            using Font titleFont = new("Segoe UI", 24f, FontStyle.Bold);
            using Font subFont = new("Segoe UI", 9.5f, FontStyle.Regular);
            using SolidBrush sb = new(Color.FromArgb(150, 180, 215));
            g.DrawString("ROGUE CARDS", titleFont, Brushes.White, 26, 12);
            g.DrawString("Tactical 5x5 Turn-Based Deckbuilder", subFont, sb, 28, 50);

            RectangleF areaBox = new(760, 16, 300, 42);
            VectorArtRenderer.DrawPanelBox(g, areaBox, Color.FromArgb(30, 40, 56), _engine.Profile.Area2Unlocked ? Color.Gold : Color.FromArgb(72, 92, 120), 8f, 1.5f);
            using Font af1 = new("Segoe UI", 8.5f, FontStyle.Bold);
            using Font af2 = new("Segoe UI", 7.6f, FontStyle.Regular);
            g.DrawString("LEVEL 1: FROSTFJORD (10 WAVES)", af1, Brushes.LightSkyBlue, areaBox.X + 12, areaBox.Y + 5);
            string a2Txt = _engine.Profile.Area2Unlocked ? "Area 2: Ashlands UNLOCKED (Expansion Hook)" : "Area 2: Locked (Defeat Wave 10 Boss)";
            g.DrawString(a2Txt, af2, _engine.Profile.Area2Unlocked ? Brushes.Gold : Brushes.Silver, areaBox.X + 12, areaBox.Y + 23);

            RectangleF coinBox = new(1078, 16, 178, 42);
            VectorArtRenderer.DrawPanelBox(g, coinBox, Color.FromArgb(46, 40, 24), Color.Gold, 8f, 1.8f);
            using Font cf = new("Segoe UI", 13f, FontStyle.Bold);
            VectorArtRenderer.DrawCenteredText(g, _engine.Profile.Coins.ToString(CultureInfo.InvariantCulture) + " COINS", cf, Brushes.Gold, coinBox);
        }

        private float RenderMenuNotification(Graphics g)
        {
            if (string.IsNullOrEmpty(_engine.MenuNotification))
            {
                return 90f;
            }
            RectangleF notif = new(24, 80, 1232, 26);
            VectorArtRenderer.DrawPanelBox(g, notif, Color.FromArgb(128, 42, 42), Color.OrangeRed, 5f, 1.4f);
            using Font nf = new("Segoe UI", 8.5f, FontStyle.Bold);
            g.DrawString("NOTICE: " + _engine.MenuNotification, nf, Brushes.White, notif.X + 12, notif.Y + 5);
            DrawButton(g, new RectangleF(notif.Right - 72, notif.Y + 3, 64, 20), "DISMISS", Color.FromArgb(88, 36, 36), true, "DismissNotification");
            return 112f;
        }

        private void RenderHeroSelectionPanel(Graphics g, RectangleF panel)
        {
            VectorArtRenderer.DrawPanelBox(g, panel, Color.FromArgb(24, 30, 42), Color.FromArgb(56, 68, 88), 10f, 1.5f);
            using Font secFont = new("Segoe UI", 11f, FontStyle.Bold);
            g.DrawString("SELECT HERO", secFont, Brushes.LightSteelBlue, panel.X + 16, panel.Y + 12);

            float cardY = panel.Y + 40;
            const float cardH = 296f;
            for (int i = 0; i < Math.Min(2, _engine.Db.CharacterList.Count); i++)
            {
                RectangleF cRect = new(panel.X + 16 + i * 246, cardY, 236, cardH);
                RenderHeroCard(g, _engine.Db.CharacterList[i], i, cRect);
            }

            float btnY = cardY + cardH + 16;
            bool hasSave = _engine.Saves.HasActiveRunSave();
            DrawButton(g, new RectangleF(panel.X + 16, btnY, 484, 54), "START NEW RUN", Color.FromArgb(44, 148, 86), true, "StartNewRun", 0, _selectedCharacterId, "Draft 2 of 4 unlocked cards & enter Level 1");
            DrawButton(g, new RectangleF(panel.X + 16, btnY + 64, 484, 52), "CONTINUE SAVED RUN", Color.FromArgb(48, 112, 182), hasSave, "ContinueRun", 0, string.Empty, hasSave ? "Resume from Saves/active_run.txt" : "No active run save found");
            DrawButton(g, new RectangleF(panel.X + 16, btnY + 126, 484, 50), "ACHIEVEMENTS & STATISTICS", Color.FromArgb(118, 66, 168), true, "OpenAchievements", 0, string.Empty, "Unlocked: " + _engine.Profile.CompletedAchievements.Count.ToString(CultureInfo.InvariantCulture) + " / 10   |   Lifetime Stats");
        }

        private void RenderHeroCard(Graphics g, CharacterDef cdef, int index, RectangleF cRect)
        {
            bool unlocked = _engine.Profile.UnlockedCharacters.Contains(cdef.Id);
            bool selected = _selectedCharacterId == cdef.Id;
            Color cBg = selected ? Color.FromArgb(34, 52, 74) : Color.FromArgb(20, 26, 36);
            Color cBorder = selected ? Color.FromArgb(95, 220, 255) : (unlocked ? Color.FromArgb(68, 82, 106) : Color.FromArgb(52, 56, 64));
            VectorArtRenderer.DrawPanelBox(g, cRect, cBg, cBorder, 8f, selected ? 2.6f : 1.4f);

            VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, new RectangleF(cRect.X + 54, cRect.Y + 14, 128, 118), false);
            if (!unlocked)
            {
                using SolidBrush lockVeil = new(Color.FromArgb(145, 14, 16, 22));
                VectorArtRenderer.FillRoundedRect(g, lockVeil, cRect, 8f);
            }

            int maxHp = cdef.BaseHp + (_engine.Profile.UnlockedUpgrades.Contains("durability_1") ? 1 : 0);
            using Font nf = new("Segoe UI", 12f, FontStyle.Bold);
            using Font sf = new("Segoe UI", 8.2f, FontStyle.Bold);
            using Font df = new("Segoe UI", 7.8f, FontStyle.Regular);
            VectorArtRenderer.DrawCenteredText(g, cdef.Name.ToUpperInvariant(), nf, unlocked ? Brushes.White : Brushes.LightGray, new RectangleF(cRect.X, cRect.Y + 134, cRect.Width, 24));
            VectorArtRenderer.DrawCenteredText(g, "HP: " + maxHp.ToString(CultureInfo.InvariantCulture) + "   |   SPECIAL CD: " + cdef.SpecialCooldown.ToString(CultureInfo.InvariantCulture), sf, Brushes.LightSkyBlue, new RectangleF(cRect.X, cRect.Y + 158, cRect.Width, 18));
            g.DrawString("Special: " + cdef.SpecialName, sf, Brushes.Gold, new RectangleF(cRect.X + 10, cRect.Y + 180, cRect.Width - 20, 18));
            g.DrawString(cdef.SpecialDescription, df, Brushes.Gainsboro, new RectangleF(cRect.X + 10, cRect.Y + 199, cRect.Width - 20, 68));

            if (!unlocked)
            {
                RectangleF lockTag = new(cRect.X + 14, cRect.Bottom - 28, cRect.Width - 28, 22);
                using SolidBrush lb = new(Color.FromArgb(140, 42, 42));
                using Font lf = new("Segoe UI", 7.2f, FontStyle.Bold);
                VectorArtRenderer.FillRoundedRect(g, lb, lockTag, 5f);
                VectorArtRenderer.DrawCenteredText(g, "LOCKED: " + cdef.UnlockConditionText, lf, Brushes.White, lockTag);
            }
            else
            {
                if (selected)
                {
                    RectangleF selTag = new(cRect.X + 58, cRect.Bottom - 26, cRect.Width - 116, 20);
                    using SolidBrush sb = new(Color.FromArgb(44, 146, 205));
                    VectorArtRenderer.FillRoundedRect(g, sb, selTag, 5f);
                    VectorArtRenderer.DrawCenteredText(g, "SELECTED", sf, Brushes.White, selTag);
                }
                RegisterClick(cRect, "SelectHero", index, cdef.Id);
            }
        }

        private void RenderArmoryAndTrainingPanel(Graphics g, RectangleF panel)
        {
            VectorArtRenderer.DrawPanelBox(g, panel, Color.FromArgb(24, 30, 42), Color.FromArgb(56, 68, 88), 10f, 1.5f);
            using Font secFont = new("Segoe UI", 10.5f, FontStyle.Bold);

            g.DrawString("NEXT UNLOCKABLE WEAPONS (ARMORY)", secFont, Brushes.Orange, panel.X + 16, panel.Y + 12);
            float wepY = panel.Y + 38;
            List<WeaponDef> nextWeapons = _engine.GetNextUnlockableWeapons(2);
            if (nextWeapons.Count == 0)
            {
                RenderEmptyShopBanner(g, new RectangleF(panel.X + 16, wepY, panel.Width - 32, 232), Color.FromArgb(28, 42, 34), "ALL WEAPONS UNLOCKED!", Brushes.LightGreen);
            }
            else
            {
                for (int i = 0; i < nextWeapons.Count; i++)
                {
                    RenderWeaponUnlockCard(g, nextWeapons[i], i, new RectangleF(panel.X + 16 + i * 338, wepY, 326, 238));
                }
            }

            float upgTitleY = wepY + 252;
            g.DrawString("NEXT PERMANENT UPGRADES (TRAINING)", secFont, Brushes.LightSkyBlue, panel.X + 16, upgTitleY);
            float upgY = upgTitleY + 26;
            float upgH = panel.Bottom - upgY - 14;
            List<UpgradeDef> nextUpgrades = _engine.GetNextUnlockableUpgrades(2);
            if (nextUpgrades.Count == 0)
            {
                RenderEmptyShopBanner(g, new RectangleF(panel.X + 16, upgY, panel.Width - 32, upgH), Color.FromArgb(26, 40, 56), "ALL PERMANENT UPGRADES MASTERED!", Brushes.LightSkyBlue);
            }
            else
            {
                for (int i = 0; i < nextUpgrades.Count; i++)
                {
                    RenderUpgradeUnlockCard(g, nextUpgrades[i], i, new RectangleF(panel.X + 16 + i * 338, upgY, 326, upgH));
                }
            }
        }

        private static void RenderEmptyShopBanner(Graphics g, RectangleF box, Color bgColor, string text, Brush textBrush)
        {
            using SolidBrush eb = new(bgColor);
            using Font ef = new("Segoe UI", 13f, FontStyle.Bold);
            VectorArtRenderer.FillRoundedRect(g, eb, box, 8f);
            VectorArtRenderer.DrawCenteredText(g, text, ef, textBrush, box);
        }

        private void RenderWeaponUnlockCard(Graphics g, WeaponDef w, int index, RectangleF slot)
        {
            VectorArtRenderer.DrawPanelBox(g, slot, Color.FromArgb(20, 25, 36), Color.FromArgb(62, 74, 96), 8f, 1.4f);
            VectorArtRenderer.DrawPhysicalCard(g, w, null, _engine.Db, _engine.Profile, new RectangleF(slot.X + 12, slot.Y + 12, 134, 174), false, false, false, 0, true);

            using Font wf1 = new("Segoe UI", 10.5f, FontStyle.Bold);
            using Font wf2 = new("Segoe UI", 8.2f, FontStyle.Regular);
            using SolidBrush catBrush = new(VectorArtRenderer.GetCategoryLightColor(w.Category));
            g.DrawString(w.Name, wf1, Brushes.White, new RectangleF(slot.X + 156, slot.Y + 14, 160, 42));
            g.DrawString("Category: " + w.Category.ToString(), wf2, catBrush, slot.X + 156, slot.Y + 58);
            g.DrawString("Damage: " + w.BaseDamage.ToString(CultureInfo.InvariantCulture) + "   CD: " + w.BaseCooldown.ToString(CultureInfo.InvariantCulture), wf2, Brushes.LightSteelBlue, slot.X + 156, slot.Y + 76);
            g.DrawString(w.Description, wf2, Brushes.Gainsboro, new RectangleF(slot.X + 156, slot.Y + 98, 160, 88));

            DrawButton(g, new RectangleF(slot.X + 12, slot.Bottom - 40, slot.Width - 24, 30), "UNLOCK — " + w.UnlockCost.ToString(CultureInfo.InvariantCulture) + " COINS", Color.FromArgb(182, 128, 36), _engine.Profile.Coins >= w.UnlockCost, "BuyWeapon", index, w.Id);
        }

        private void RenderUpgradeUnlockCard(Graphics g, UpgradeDef u, int index, RectangleF slot)
        {
            VectorArtRenderer.DrawPanelBox(g, slot, Color.FromArgb(20, 26, 38), Color.FromArgb(62, 80, 108), 8f, 1.4f);
            VectorArtRenderer.DrawUpgradeIcon(g, u.Id, new RectangleF(slot.X + 16, slot.Y + 18, 68, 68));

            using Font uf1 = new("Segoe UI", 10.5f, FontStyle.Bold);
            using Font uf2 = new("Segoe UI", 8.4f, FontStyle.Regular);
            using Font tierF = new("Segoe UI", 7.8f, FontStyle.Bold);
            g.DrawString(u.Name, uf1, Brushes.White, new RectangleF(slot.X + 94, slot.Y + 18, 220, 26));
            g.DrawString("Tier #" + u.UnlockOrder.ToString(CultureInfo.InvariantCulture), tierF, Brushes.LightSkyBlue, slot.X + 94, slot.Y + 42);
            g.DrawString(u.Description, uf2, Brushes.Gainsboro, new RectangleF(slot.X + 16, slot.Y + 96, slot.Width - 32, slot.Height - 144));

            DrawButton(g, new RectangleF(slot.X + 12, slot.Bottom - 40, slot.Width - 24, 30), "UNLOCK — " + u.Cost.ToString(CultureInfo.InvariantCulture) + " COINS", Color.FromArgb(46, 128, 192), _engine.Profile.Coins >= u.Cost, "BuyUpgrade", index, u.Id);
        }
        private void RenderGameplay(Graphics g)
        {
            RenderTopStatusBar(g);
            RenderInspectorPanel(g);
            RenderLegendPanel(g);
            RenderTacticalGrid(g);
            RenderWaveProgressPanel(g);
            RenderCombatLogPanel(g);
            RenderBottomCardDock(g);
            RenderActiveModalOverlay(g);
        }

        private void RenderActiveModalOverlay(Graphics g)
        {
            switch (_engine.Phase)
            {
                case GamePhase.StartingDraft:
                    RenderStartingDraftOverlay(g);
                    break;
                case GamePhase.RewardPickCard:
                    RenderPickCardOverlay(g);
                    break;
                case GamePhase.RewardUpgrade:
                    RenderUpgradeOverlay(g);
                    break;
                case GamePhase.Victory:
                case GamePhase.Defeat:
                    RenderEndRunOverlay(g);
                    break;
            }
        }

        private void RenderTopStatusBar(Graphics g)
        {
            using SolidBrush tb = new(Color.FromArgb(24, 30, 42));
            using Pen div = new(Color.FromArgb(56, 70, 94), 1.5f);
            g.FillRectangle(tb, 0, 0, Width, 48);
            g.DrawLine(div, 0, 48, Width, 48);

            CharacterDef cdef = _engine.Db.GetCharacterOrFallback(_engine.Player.CharacterId);
            VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, new RectangleF(10, 4, 40, 40), _engine.Player.ImmuneTurns > 0);

            using Font fBold = new("Segoe UI", 10.5f, FontStyle.Bold);
            using Font fSmall = new("Segoe UI", 8.5f, FontStyle.Bold);
            g.DrawString(cdef.Name.ToUpperInvariant(), fBold, Brushes.White, 56, 14);
            RenderTopHpBar(g, new RectangleF(192, 12, 168, 24), fSmall);

            g.DrawString("COINS: " + _engine.Profile.Coins.ToString(CultureInfo.InvariantCulture), fBold, Brushes.Gold, 382, 14);
            string waveTxt = _engine.CurrentWave > 0 ? ("WAVE " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + " / 10") : "STARTING DRAFT";
            g.DrawString(waveTxt, fBold, Brushes.LightSkyBlue, 514, 14);
            g.DrawString("TURN " + _engine.TurnCounter.ToString(CultureInfo.InvariantCulture), fBold, Brushes.LightSteelBlue, 668, 14);

            RenderTopStatusBanner(g, fSmall);
            DrawButton(g, new RectangleF(1092, 7, 174, 34), "SAVE & EXIT", Color.FromArgb(148, 56, 62), true, "SaveAndExit");
        }

        private void RenderTopHpBar(Graphics g, RectangleF hpBg, Font labelFont)
        {
            using SolidBrush bg = new(Color.FromArgb(48, 24, 28));
            VectorArtRenderer.FillRoundedRect(g, bg, hpBg, 6f);
            float ratio = _engine.Player.MaxHp > 0 ? Math.Clamp((float)_engine.Player.Hp / _engine.Player.MaxHp, 0f, 1f) : 0f;
            if (ratio > 0f)
            {
                RectangleF hpFill = new(hpBg.X, hpBg.Y, hpBg.Width * ratio, hpBg.Height);
                using LinearGradientBrush hb = new(hpBg, Color.FromArgb(228, 62, 74), Color.FromArgb(168, 32, 42), 90f);
                VectorArtRenderer.FillRoundedRect(g, hb, hpFill, 6f);
            }
            VectorArtRenderer.DrawRoundedRect(g, Pens.IndianRed, hpBg, 6f);
            VectorArtRenderer.DrawCenteredText(g, "HP  " + _engine.Player.Hp.ToString(CultureInfo.InvariantCulture) + " / " + _engine.Player.MaxHp.ToString(CultureInfo.InvariantCulture), labelFont, Brushes.White, hpBg);
        }

        private void RenderTopStatusBanner(Graphics g, Font fSmall)
        {
            if (_engine.Player.ImmuneTurns > 0)
            {
                RectangleF immR = new(785, 11, 130, 26);
                using SolidBrush ib = new(Color.FromArgb(32, 118, 148));
                VectorArtRenderer.FillRoundedRect(g, ib, immR, 6f);
                VectorArtRenderer.DrawCenteredText(g, "IMMUNE (1T)", fSmall, Brushes.Cyan, immR);
            }
            else if (_engine.Phase == GamePhase.AimingCard)
            {
                RectangleF aimR = new(785, 11, 286, 26);
                using SolidBrush ab = new(Color.FromArgb(148, 86, 24));
                VectorArtRenderer.FillRoundedRect(g, ab, aimR, 6f);
                CardInstance aimCard = _engine.GetCurrentlyAimingCard();
                string aTxt = aimCard != null ? ("AIMING: " + _engine.Db.GetWeaponOrFallback(aimCard.WeaponId).Name + " (Click Grid)") : "AIMING...";
                VectorArtRenderer.DrawCenteredText(g, aTxt, fSmall, Brushes.White, aimR);
            }
        }

        private void RenderInspectorPanel(Graphics g)
        {
            RectangleF inspBox = new(14, 58, 374, 198);
            VectorArtRenderer.DrawPanelBox(g, inspBox, Color.FromArgb(24, 30, 42), Color.FromArgb(56, 68, 90), 8f, 1.4f);

            using Font hFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font sFont = new("Segoe UI", 8.2f, FontStyle.Bold);
            using Font bFont = new("Segoe UI", 8.2f, FontStyle.Regular);
            g.DrawString("TACTICAL INSPECTOR", hFont, Brushes.LightSkyBlue, inspBox.X + 12, inspBox.Y + 8);

            if (_hoveredCard != null)
            {
                RenderCardInspector(g, inspBox, _hoveredCard, hFont, sFont, bFont);
                return;
            }
            if (_hoveredGridTile.HasValue)
            {
                GridPos gp = _hoveredGridTile.Value;
                EnemyInstance enemy = _engine.GetEnemyAt(gp);
                if (enemy != null)
                {
                    RenderEnemyInspector(g, inspBox, enemy, gp, hFont, sFont, bFont);
                    return;
                }
                if (_engine.Player.Pos == gp)
                {
                    RenderHeroInspector(g, inspBox, hFont, sFont, bFont);
                    return;
                }
                RenderOpenTileInspector(g, inspBox, gp, sFont, bFont);
                return;
            }
            RenderHeroInspector(g, inspBox, hFont, sFont, bFont);
        }

        private void RenderCardInspector(Graphics g, RectangleF box, CardInstance card, Font hFont, Font sFont, Font bFont)
        {
            WeaponDef wdef = _engine.Db.GetWeaponOrFallback(card.WeaponId);
            VectorArtRenderer.DrawWeaponIcon(g, wdef.Id, new RectangleF(box.X + 14, box.Y + 34, 68, 68));
            g.DrawString(wdef.Name + " (" + wdef.Category + ")", hFont, Brushes.White, box.X + 90, box.Y + 32);
            int dmg = card.GetEffectiveDamage(_engine.Db, _engine.Profile);
            int maxCd = card.GetEffectiveMaxCooldown(_engine.Db, _engine.Profile);
            g.DrawString("DMG: " + dmg.ToString(CultureInfo.InvariantCulture) + "   CD: " + card.CurrentCooldown.ToString(CultureInfo.InvariantCulture) + "/" + maxCd.ToString(CultureInfo.InvariantCulture) + "   Aim: " + wdef.AimMode, sFont, Brushes.LightSalmon, box.X + 90, box.Y + 52);
            string mods = card.GetModifiersSummary();
            g.DrawString(string.IsNullOrEmpty(mods) ? "Modifiers: None" : "Modifiers: " + mods, sFont, Brushes.LightGreen, box.X + 90, box.Y + 72);
            g.DrawString(wdef.Description, bFont, Brushes.Gainsboro, new RectangleF(box.X + 12, box.Y + 114, box.Width - 24, 76));
        }

        private void RenderEnemyInspector(Graphics g, RectangleF box, EnemyInstance enemy, GridPos gp, Font hFont, Font sFont, Font bFont)
        {
            EnemyDef edef = _engine.Db.GetEnemyOrFallback(enemy.EnemyId);
            VectorArtRenderer.DrawEnemySprite(g, enemy, new RectangleF(box.X + 12, box.Y + 34, 72, 72), _animTime);
            g.DrawString(edef.Name + (edef.IsBoss ? " [BOSS]" : string.Empty) + "  (" + gp + ")", hFont, edef.IsBoss ? Brushes.Gold : Brushes.White, box.X + 92, box.Y + 32);
            g.DrawString("HP: " + enemy.Hp.ToString(CultureInfo.InvariantCulture) + "/" + enemy.MaxHp.ToString(CultureInfo.InvariantCulture) + "   DMG: " + edef.Damage.ToString(CultureInfo.InvariantCulture) + "   DROP: " + edef.CoinDrop.ToString(CultureInfo.InvariantCulture) + "c", sFont, Brushes.LightSalmon, box.X + 92, box.Y + 52);
            g.DrawString("Intent: " + enemy.IntentLabel, sFont, enemy.State == EnemyAIState.Charging ? Brushes.OrangeRed : Brushes.LightSteelBlue, box.X + 92, box.Y + 70);

            string status = (enemy.PoisonTurns > 0 ? "POISON (" + enemy.PoisonTurns.ToString(CultureInfo.InvariantCulture) + "T) " : string.Empty) + (enemy.StunTurns > 0 ? "STUNNED" : string.Empty);
            if (!string.IsNullOrEmpty(status))
            {
                g.DrawString(status, sFont, Brushes.LimeGreen, box.X + 92, box.Y + 88);
            }
            g.DrawString(edef.Description, bFont, Brushes.Gainsboro, new RectangleF(box.X + 12, box.Y + 114, box.Width - 24, 76));
        }

        private void RenderHeroInspector(Graphics g, RectangleF box, Font hFont, Font sFont, Font bFont)
        {
            CharacterDef cdef = _engine.Db.GetCharacterOrFallback(_engine.Player.CharacterId);
            VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, new RectangleF(box.X + 12, box.Y + 34, 72, 72), _engine.Player.ImmuneTurns > 0);
            g.DrawString(cdef.Name + " (" + _engine.Player.Pos + ")", hFont, Brushes.Cyan, box.X + 92, box.Y + 32);
            g.DrawString("Special: " + cdef.SpecialName, sFont, Brushes.Gold, box.X + 92, box.Y + 54);
            string cdTxt = _engine.Player.SpecialCooldown == 0 ? "READY" : ("CD " + _engine.Player.SpecialCooldown.ToString(CultureInfo.InvariantCulture) + " turns");
            g.DrawString("Status: " + cdTxt, sFont, _engine.Player.SpecialCooldown == 0 ? Brushes.LightGreen : Brushes.Orange, box.X + 92, box.Y + 74);
            g.DrawString(cdef.SpecialDescription, bFont, Brushes.Gainsboro, new RectangleF(box.X + 12, box.Y + 114, box.Width - 24, 76));
        }

        private void RenderOpenTileInspector(Graphics g, RectangleF box, GridPos gp, Font sFont, Font bFont)
        {
            g.DrawString("Tile " + gp + " — Open Ground", sFont, Brushes.White, box.X + 12, box.Y + 36);
            HazardInstance pHaz = _engine.GetHazardAt(gp, HazardType.Poison);
            HazardInstance tHaz = _engine.GetHazardAt(gp, HazardType.BearTrap);
            float ty = box.Y + 60;
            if (pHaz != null)
            {
                g.DrawString("Hazard: Poison Cloud (" + pHaz.RemainingTurns.ToString(CultureInfo.InvariantCulture) + " turns left). Deals 1 dmg/turn to any entity standing here.", bFont, Brushes.LightGreen, new RectangleF(box.X + 12, ty, box.Width - 24, 42));
                ty += 46;
            }
            if (tHaz != null)
            {
                string tDesc = tHaz.IsArmed
                    ? "Hazard: ARMED Bear Trap (" + tHaz.Damage.ToString(CultureInfo.InvariantCulture) + " dmg + Stun)."
                    : "Hazard: Unarmed Bear Trap (arms once you move off this tile).";
                g.DrawString(tDesc, bFont, Brushes.Violet, new RectangleF(box.X + 12, ty, box.Width - 24, 42));
            }
            if (pHaz == null && tHaz == null)
            {
                g.DrawString("Hover over any Hero, Enemy, or Card to inspect full stats, weapon intent, and tactical effects.\n\nClick an adjacent orthogonal tile (or press WASD/Arrows) to move.", bFont, Brushes.Silver, new RectangleF(box.X + 12, ty, box.Width - 24, 110));
            }
        }

        private static void RenderLegendPanel(Graphics g)
        {
            RectangleF legBox = new(14, 266, 374, 202);
            VectorArtRenderer.DrawPanelBox(g, legBox, Color.FromArgb(24, 30, 42), Color.FromArgb(56, 68, 90), 8f, 1.4f);

            using Font hFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font bFont = new("Segoe UI", 8.0f, FontStyle.Regular);
            g.DrawString("INTENT LEGEND & CONTROLS", hFont, Brushes.LightSkyBlue, legBox.X + 12, legBox.Y + 8);

            DrawLegendItem(g, legBox.X + 12, legBox.Y + 34, Color.FromArgb(220, 52, 52), "Pulsing Red Tile: Enemy charged attack (Friendly Fire active!)");
            DrawLegendItem(g, legBox.X + 12, legBox.Y + 56, Color.FromArgb(255, 155, 45), "Orange Highlight: Your weapon aiming preview (hits/pierces/arc)");
            DrawLegendItem(g, legBox.X + 12, legBox.Y + 78, Color.FromArgb(70, 215, 255), "Cyan Highlight: Valid movement / dash / slide destination");
            DrawLegendItem(g, legBox.X + 12, legBox.Y + 100, Color.FromArgb(72, 224, 92), "Green Glow / Spikes: Ground Poison (1 dmg/T) or Bear Trap (Stun)");

            using Pen div = new(Color.FromArgb(54, 64, 82), 1f);
            g.DrawLine(div, legBox.X + 12, legBox.Y + 126, legBox.Right - 12, legBox.Y + 126);

            const string controls =
                "MOVE: WASD / Arrows or click adjacent tile (1 Turn)\n" +
                "EQUIP: Click hand card or press 1-4 (1 Turn)  |  REORDER: Drag & Drop\n" +
                "PLAY QUEUE: Enter or button (1 Turn; aim each card sequentially)\n" +
                "WAIT: Space (1 Turn)   |   SPECIAL: E (1 Turn)   |   DIAG AIM: Q/E/Z/C";
            g.DrawString(controls, bFont, Brushes.LightSteelBlue, new RectangleF(legBox.X + 12, legBox.Y + 132, legBox.Width - 24, 66));
        }

        private static void DrawLegendItem(Graphics g, float x, float y, Color c, string text)
        {
            using SolidBrush b = new(c);
            using Font f = new("Segoe UI", 7.8f, FontStyle.Regular);
            VectorArtRenderer.FillRoundedRect(g, b, new RectangleF(x, y + 2, 14, 14), 3f);
            g.DrawString(text, f, Brushes.Gainsboro, x + 20, y + 1);
        }

        private void RenderTacticalGrid(Graphics g)
        {
            RectangleF frame = RectangleF.Inflate(GridRect, 5, 5);
            VectorArtRenderer.DrawPanelBox(g, frame, Color.FromArgb(14, 18, 26), _engine.Phase == GamePhase.AimingCard ? Color.Orange : Color.FromArgb(72, 88, 114), 10f, 2.2f);

            HashSet<GridPos> telegraphedTiles = CollectTelegraphedTiles();
            CardInstance aimingCard = _engine.GetCurrentlyAimingCard();
            bool aimingMove = aimingCard != null && _engine.Db.GetWeaponOrFallback(aimingCard.WeaponId).Category == CardCategory.Movement;

            for (int y = 0; y < GridPos.GridSize; y++)
            {
                for (int x = 0; x < GridPos.GridSize; x++)
                {
                    RenderSingleGridTile(g, new GridPos(x, y), telegraphedTiles, aimingMove);
                }
            }

            RenderTelegraphArrows(g);
            RenderGridEntities(g);
            RenderFloatingPopups(g);
        }

        private HashSet<GridPos> CollectTelegraphedTiles()
        {
            HashSet<GridPos> set = [];
            foreach (EnemyInstance e in _engine.Enemies.Where(e => e.State == EnemyAIState.Charging))
            {
                foreach (GridPos lp in e.LockedTiles)
                {
                    set.Add(lp);
                }
            }
            return set;
        }

        private void RenderSingleGridTile(Graphics g, GridPos gp, HashSet<GridPos> telegraphedTiles, bool aimingMove)
        {
            RectangleF tr = GetTileRect(gp);
            Color baseTile = (gp.X + gp.Y) % 2 == 0 ? Color.FromArgb(30, 37, 50) : Color.FromArgb(25, 31, 43);
            using (SolidBrush tb = new(baseTile))
            {
                VectorArtRenderer.FillRoundedRect(g, tb, RectangleF.Inflate(tr, -1.5f, -1.5f), 5f);
            }

            using (Font cf = new("Segoe UI", 6.5f, FontStyle.Regular))
            using (SolidBrush cbr = new(Color.FromArgb(75, 90, 115)))
            {
                g.DrawString(gp.ToString(), cf, cbr, tr.X + 4, tr.Y + 3);
            }

            if (_engine.Phase == GamePhase.PlayerTurn && gp.ManhattanDistance(_engine.Player.Pos) == 1 && _engine.GetEnemyAt(gp) == null)
            {
                bool isHov = _hoveredGridTile.HasValue && _hoveredGridTile.Value == gp;
                VectorArtRenderer.DrawPanelBox(g, RectangleF.Inflate(tr, -3, -3), Color.FromArgb(isHov ? 88 : 34, 70, 210, 255), Color.FromArgb(isHov ? 220 : 95, 90, 225, 255), 5f, isHov ? 2.0f : 1.0f);
            }

            HazardInstance poison = _engine.GetHazardAt(gp, HazardType.Poison);
            if (poison != null)
            {
                VectorArtRenderer.DrawHazardTile(g, poison, tr, _animTime);
            }
            HazardInstance trap = _engine.GetHazardAt(gp, HazardType.BearTrap);
            if (trap != null)
            {
                VectorArtRenderer.DrawHazardTile(g, trap, tr, _animTime);
            }

            if (telegraphedTiles.Contains(gp))
            {
                int alpha = Math.Clamp((int)(70 + Math.Sin(_animTime * 8.0) * 42), 30, 140);
                using SolidBrush warn = new(Color.FromArgb(alpha, 235, 42, 42));
                using Pen warnPen = new(Color.FromArgb(225, 255, 92, 72), 2.2f) { DashStyle = DashStyle.Dash };
                VectorArtRenderer.FillRoundedRect(g, warn, RectangleF.Inflate(tr, -3, -3), 5f);
                VectorArtRenderer.DrawRoundedRect(g, warnPen, RectangleF.Inflate(tr, -3, -3), 5f);
            }

            if (_engine.Phase == GamePhase.AimingCard && _engine.CurrentPreviewTiles.Contains(gp))
            {
                RenderAimPreviewOnTile(g, tr, aimingMove);
            }

            if (_hoveredGridTile.HasValue && _hoveredGridTile.Value == gp)
            {
                using Pen hp = new(Color.FromArgb(160, 255, 255, 255), 1.4f);
                VectorArtRenderer.DrawRoundedRect(g, hp, RectangleF.Inflate(tr, -2, -2), 5f);
            }
        }

        private static void RenderAimPreviewOnTile(Graphics g, RectangleF tr, bool aimingMove)
        {
            Color fillC = aimingMove ? Color.FromArgb(105, 65, 225, 255) : Color.FromArgb(120, 255, 138, 36);
            Color edgeC = aimingMove ? Color.FromArgb(240, 120, 245, 255) : Color.FromArgb(255, 255, 195, 70);
            VectorArtRenderer.DrawPanelBox(g, RectangleF.Inflate(tr, -3, -3), fillC, edgeC, 5f, 2.6f);
            if (aimingMove)
            {
                return;
            }
            float tcx = tr.X + TileSize * 0.5f;
            float tcy = tr.Y + TileSize * 0.5f;
            using Pen ch = new(Color.FromArgb(210, 255, 240, 180), 1.6f);
            g.DrawEllipse(ch, tcx - 12, tcy - 12, 24, 24);
            g.DrawLine(ch, tcx - 17, tcy, tcx + 17, tcy);
            g.DrawLine(ch, tcx, tcy - 17, tcx, tcy + 17);
        }

        private void RenderTelegraphArrows(Graphics g)
        {
            using Pen arrowPen = new(Color.FromArgb(215, 255, 80, 65), 2.8f)
            {
                CustomEndCap = new AdjustableArrowCap(5f, 6f)
            };
            foreach (EnemyInstance e in _engine.Enemies.Where(e => e.State == EnemyAIState.Charging && e.LockedTiles.Count > 0))
            {
                RectangleF fromR = GetTileRect(e.Pos);
                RectangleF toR = GetTileRect(e.LockedTiles[0]);
                g.DrawLine(arrowPen, fromR.X + fromR.Width * 0.5f, fromR.Y + fromR.Height * 0.5f, toR.X + toR.Width * 0.5f, toR.Y + toR.Height * 0.5f);
            }
        }

        private void RenderGridEntities(Graphics g)
        {
            RectangleF pRect = GetTileRect(_engine.Player.Pos);
            CharacterDef hero = _engine.Db.GetCharacterOrFallback(_engine.Player.CharacterId);
            VectorArtRenderer.DrawCharacterPortrait(g, hero.Id, new RectangleF(pRect.X + 8, pRect.Y + 12, pRect.Width - 16, pRect.Height - 28), _engine.Player.ImmuneTurns > 0);
            DrawEntityNameAndHpBar(g, pRect, hero.Name, _engine.Player.Hp, _engine.Player.MaxHp, true, false);

            foreach (EnemyInstance enemy in _engine.Enemies.Where(e => e.Hp > 0))
            {
                RectangleF eRect = GetTileRect(enemy.Pos);
                VectorArtRenderer.DrawEnemyIntentBadge(g, enemy, eRect, _animTime);
                VectorArtRenderer.DrawEnemySprite(g, enemy, new RectangleF(eRect.X + 6, eRect.Y + 16, eRect.Width - 12, eRect.Height - 32), _animTime);
                DrawEntityNameAndHpBar(g, eRect, _engine.Db.GetEnemyOrFallback(enemy.EnemyId).Name, enemy.Hp, enemy.MaxHp, false, enemy.PoisonTurns > 0);
            }
        }

        private void RenderFloatingPopups(Graphics g)
        {
            using Font ff = new("Segoe UI", 9.5f, FontStyle.Bold);
            using StringFormat sf = new() { Alignment = StringAlignment.Center };
            foreach (FloatingText ft in _engine.FloatingTexts)
            {
                RectangleF tr = GetTileRect(ft.Pos);
                float fx = tr.X + tr.Width * 0.5f + ft.OffsetX;
                float fy = tr.Y + 26 - ft.Progress * 34f;
                int alpha = Math.Clamp((int)((1f - ft.Progress) * 255), 0, 255);
                using SolidBrush sh = new(Color.FromArgb(alpha, 0, 0, 0));
                using SolidBrush fg = new(Color.FromArgb(alpha, ft.Color));
                g.DrawString(ft.Text, ff, sh, fx + 1, fy + 1, sf);
                g.DrawString(ft.Text, ff, fg, fx, fy, sf);
            }
        }

        private static void DrawEntityNameAndHpBar(Graphics g, RectangleF tileRect, string name, int hp, int maxHp, bool isPlayer, bool isPoisoned)
        {
            RectangleF nameR = new(tileRect.X + 2, tileRect.Bottom - 23, tileRect.Width - 4, 12);
            using (SolidBrush nb = new(Color.FromArgb(165, 10, 14, 20)))
            using (Font nf = new("Segoe UI", 6.8f, FontStyle.Bold))
            {
                g.FillRectangle(nb, nameR);
                VectorArtRenderer.DrawCenteredText(g, name, nf, isPlayer ? Brushes.Cyan : Brushes.White, nameR);
            }

            RectangleF barBg = new(tileRect.X + 6, tileRect.Bottom - 10, tileRect.Width - 12, 7);
            using (SolidBrush bg = new(Color.FromArgb(30, 16, 18)))
            {
                g.FillRectangle(bg, barBg);
            }
            float ratio = maxHp > 0 ? Math.Clamp((float)hp / maxHp, 0f, 1f) : 0f;
            if (ratio > 0f)
            {
                Color fill = isPlayer ? Color.FromArgb(62, 212, 138) : (isPoisoned ? Color.LimeGreen : Color.FromArgb(232, 68, 68));
                using SolidBrush fb = new(fill);
                g.FillRectangle(fb, barBg.X, barBg.Y, barBg.Width * ratio, barBg.Height);
            }
            g.DrawRectangle(Pens.Black, barBg.X, barBg.Y, barBg.Width, barBg.Height);

            using Font hf = new("Segoe UI", 6.2f, FontStyle.Bold);
            g.DrawString(hp.ToString(CultureInfo.InvariantCulture) + "/" + maxHp.ToString(CultureInfo.InvariantCulture), hf, Brushes.White, tileRect.Right - 26, tileRect.Y + 19);
        }

        private static RectangleF GetTileRect(GridPos gp)
        {
            return new RectangleF(GridRect.X + gp.X * TileSize, GridRect.Y + gp.Y * TileSize, TileSize, TileSize);
        }

        private void RenderWaveProgressPanel(Graphics g)
        {
            RectangleF waveBox = new(832, 58, 434, 92);
            VectorArtRenderer.DrawPanelBox(g, waveBox, Color.FromArgb(24, 30, 42), Color.FromArgb(56, 68, 90), 8f, 1.4f);

            using Font hf = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font wf = new("Segoe UI", 7.2f, FontStyle.Bold);
            using Font tagF = new("Segoe UI", 5.8f, FontStyle.Bold);
            g.DrawString("LEVEL 1 PROGRESS", hf, Brushes.LightSkyBlue, waveBox.X + 12, waveBox.Y + 8);
            g.DrawString("Enemies Left: " + _engine.Enemies.Count.ToString(CultureInfo.InvariantCulture), hf, Brushes.Orange, waveBox.Right - 122, waveBox.Y + 8);

            for (int w = 1; w <= 10; w++)
            {
                RectangleF wr = new(waveBox.X + 12 + (w - 1) * 41.2f, waveBox.Y + 34, 36, 26);
                Color wc = w < _engine.CurrentWave
                    ? Color.FromArgb(46, 132, 78)
                    : w == _engine.CurrentWave
                        ? (w == 10 ? Color.FromArgb(196, 54, 48) : Color.FromArgb(54, 134, 212))
                        : Color.FromArgb(36, 44, 58);

                using SolidBrush wb = new(wc);
                VectorArtRenderer.FillRoundedRect(g, wb, wr, 5f);
                VectorArtRenderer.DrawCenteredText(g, w == 10 ? "BOSS" : w.ToString(CultureInfo.InvariantCulture), wf, Brushes.White, wr);

                if (w is 2 or 6)
                {
                    g.DrawString("CARD", tagF, Brushes.LightGreen, wr.X + 4, wr.Bottom + 2);
                }
                else if (w is 4 or 8)
                {
                    g.DrawString("UPG", tagF, Brushes.Gold, wr.X + 7, wr.Bottom + 2);
                }
            }
        }

        private void RenderCombatLogPanel(Graphics g)
        {
            RectangleF logBox = new(832, 160, 434, 308);
            VectorArtRenderer.DrawPanelBox(g, logBox, Color.FromArgb(20, 25, 36), Color.FromArgb(56, 68, 90), 8f, 1.4f);

            using Font hf = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font lf = new("Segoe UI", 8.0f, FontStyle.Regular);
            using Pen div = new(Color.FromArgb(46, 56, 74), 1f);
            g.DrawString("LIVE COMBAT LOG (Latest 8 Entries — Full log saved to Logs/)", hf, Brushes.LightSkyBlue, logBox.X + 12, logBox.Y + 8);
            g.DrawLine(div, logBox.X + 10, logBox.Y + 30, logBox.Right - 10, logBox.Y + 30);

            float ly = logBox.Y + 36;
            for (int i = 0; i < _engine.Logger.RecentEntries.Count; i++)
            {
                CombatLogEntry entry = _engine.Logger.RecentEntries[i];
                RectangleF row = new(logBox.X + 10, ly, logBox.Width - 20, 32);
                if (i % 2 == 1)
                {
                    using SolidBrush stripe = new(Color.FromArgb(26, 32, 44));
                    g.FillRectangle(stripe, row);
                }
                using SolidBrush tb = new(entry.Color);
                g.DrawString("[" + entry.Timestamp + "] " + entry.Text, lf, tb, row);
                ly += 33.5f;
            }
        }

        private void RenderBottomCardDock(Graphics g)
        {
            RenderHandDock(g);
            RenderEquippedQueueDock(g);
            RenderActionButtonsColumn(g);
            RenderDragGhostCard(g);
        }

        private void RenderHandDock(Graphics g)
        {
            RectangleF handBox = new(14, 478, 608, 232);
            VectorArtRenderer.DrawPanelBox(g, handBox, Color.FromArgb(22, 28, 40), Color.FromArgb(54, 66, 88), 8f, 1.4f);
            using Font hf = new("Segoe UI", 9.0f, FontStyle.Bold);
            g.DrawString("HAND  (Click available card to Equip — 1 Turn)", hf, Brushes.LightSteelBlue, handBox.X + 12, handBox.Y + 7);

            for (int i = 0; i < _engine.Hand.Count; i++)
            {
                CardInstance c = _engine.Hand[i];
                RectangleF cr = GetHandSlotRect(i);
                bool canEquip = _engine.Phase == GamePhase.PlayerTurn && c.CurrentCooldown == 0;
                VectorArtRenderer.DrawPhysicalCard(g, _engine.Db.GetWeaponOrFallback(c.WeaponId), c, _engine.Db, _engine.Profile, cr, IsMouseOver(cr) && canEquip, false, false, 0, false);
                if (canEquip)
                {
                    RegisterClick(cr, "EquipCard", i);
                }
            }
        }

        private void RenderEquippedQueueDock(Graphics g)
        {
            RectangleF eqBox = new(632, 478, 484, 232);
            bool aiming = _engine.Phase == GamePhase.AimingCard;
            VectorArtRenderer.DrawPanelBox(g, eqBox, Color.FromArgb(24, 30, 44), aiming ? Color.Gold : Color.FromArgb(62, 78, 104), 8f, aiming ? 2.2f : 1.4f);

            using Font hf = new("Segoe UI", 9.0f, FontStyle.Bold);
            string eqTitle = aiming ? "EXECUTING QUEUE — Aim highlighted card on grid!" : "EQUIPPED QUEUE  (Drag & Drop to Reorder — 0 Turns)";
            g.DrawString(eqTitle, hf, aiming ? Brushes.Gold : Brushes.LightSkyBlue, eqBox.X + 12, eqBox.Y + 7);

            if (_engine.EquippedQueue.Count == 0)
            {
                using Font ef = new("Segoe UI", 9.5f, FontStyle.Italic);
                VectorArtRenderer.DrawCenteredText(g, "Equip cards from your Hand to build a combo queue.", ef, Brushes.SlateGray, eqBox);
                return;
            }

            for (int i = 0; i < _engine.EquippedQueue.Count; i++)
            {
                CardInstance c = _engine.EquippedQueue[i];
                RectangleF cr = GetEquippedSlotRect(i);
                if (_isDraggingQueueCard && _dragFromIndex == i)
                {
                    using Pen dash = new(Color.LightSkyBlue, 2f) { DashStyle = DashStyle.Dash };
                    VectorArtRenderer.DrawRoundedRect(g, dash, cr, 8f);
                    continue;
                }
                bool isExec = aiming && _engine.ExecutingCardIndex == i;
                bool isDropTarget = _isDraggingQueueCard && _dragHoverIndex == i && _dragFromIndex != i;
                VectorArtRenderer.DrawPhysicalCard(g, _engine.Db.GetWeaponOrFallback(c.WeaponId), c, _engine.Db, _engine.Profile, cr, IsMouseOver(cr) && _engine.Phase == GamePhase.PlayerTurn, isDropTarget, isExec, i + 1, true);
            }
        }

        private void RenderActionButtonsColumn(Graphics g)
        {
            const float bx = 1126f;
            const float bw = 140f;
            if (_engine.Phase == GamePhase.AimingCard)
            {
                DrawButton(g, new RectangleF(bx, 480, bw, 68), "SKIP CARD", Color.FromArgb(165, 92, 34), true, "SkipAimCard", 0, string.Empty, "Skip current aim");
                DrawButton(g, new RectangleF(bx, 558, bw, 68), "CONFIRM AIM", Color.FromArgb(46, 152, 86), true, "ConfirmAimCard", 0, string.Empty, "Or click grid");
                DrawButton(g, new RectangleF(bx, 636, bw, 72), "WAIT", Color.FromArgb(62, 74, 92), false, "WaitTurn", 0, string.Empty, "Finish aim first");
                return;
            }

            bool playerTurn = _engine.Phase == GamePhase.PlayerTurn;
            DrawButton(g, new RectangleF(bx, 480, bw, 68), "PLAY QUEUE", Color.FromArgb(44, 154, 86), playerTurn && _engine.EquippedQueue.Count > 0, "PlayQueue", 0, string.Empty, _engine.EquippedQueue.Count.ToString(CultureInfo.InvariantCulture) + " card(s) • 1 Turn");
            string waitSub = _engine.Profile.UnlockedUpgrades.Contains("tactical_master") ? "1 Turn (-1 CD)" : "Skip Turn";
            DrawButton(g, new RectangleF(bx, 558, bw, 68), "WAIT", Color.FromArgb(58, 92, 138), playerTurn, "WaitTurn", 0, string.Empty, waitSub);

            CharacterDef cdef = _engine.Db.GetCharacterOrFallback(_engine.Player.CharacterId);
            string specSub = _engine.Player.SpecialCooldown == 0 ? cdef.SpecialName : ("CD: " + _engine.Player.SpecialCooldown.ToString(CultureInfo.InvariantCulture) + " Turns");
            DrawButton(g, new RectangleF(bx, 636, bw, 72), "SPECIAL", Color.FromArgb(132, 64, 186), playerTurn && _engine.Player.SpecialCooldown == 0, "UseSpecial", 0, string.Empty, specSub);
        }

        private void RenderDragGhostCard(Graphics g)
        {
            if (!_isDraggingQueueCard || _dragFromIndex < 0 || _dragFromIndex >= _engine.EquippedQueue.Count)
            {
                return;
            }
            CardInstance dc = _engine.EquippedQueue[_dragFromIndex];
            RectangleF ghost = new(_mousePos.X - 57, _mousePos.Y - 96, 114, 192);
            VectorArtRenderer.DrawPhysicalCard(g, _engine.Db.GetWeaponOrFallback(dc.WeaponId), dc, _engine.Db, _engine.Profile, ghost, true, true, false, _dragFromIndex + 1, true);
        }

        private static RectangleF GetHandSlotRect(int index)
        {
            return new RectangleF(24 + index * 148, 510, 142, 192);
        }

        private static RectangleF GetEquippedSlotRect(int index)
        {
            return new RectangleF(642 + index * 118, 510, 114, 192);
        }

        private void RenderStartingDraftOverlay(Graphics g)
        {
            DrawModalBackdrop(g, Width, Height);
            RectangleF modal = new(200, 110, 880, 495);
            DrawModalFrame(g, modal, "STARTING HAND DRAFT — CHOOSE 2 OF 4 CARDS", "Select 2 cards from your unlocked pool to begin Wave 1.");

            for (int i = 0; i < _engine.Reward.OfferedWeaponIds.Count; i++)
            {
                WeaponDef w = _engine.Db.GetWeaponOrFallback(_engine.Reward.OfferedWeaponIds[i]);
                RectangleF cr = new(modal.X + 46 + i * 204, modal.Y + 96, 176, 260);
                int selIdx = _engine.Reward.StartingDraftSelectedIndices.IndexOf(i);
                VectorArtRenderer.DrawPhysicalCard(g, w, null, _engine.Db, _engine.Profile, cr, IsMouseOver(cr), selIdx >= 0, false, selIdx >= 0 ? selIdx + 1 : 0, false);
                RegisterClick(cr, "ToggleDraftCard", i);
            }

            int selCount = _engine.Reward.StartingDraftSelectedIndices.Count;
            DrawButton(g, new RectangleF(modal.X + 300, modal.Bottom - 86, 280, 54), "CONFIRM STARTING HAND (" + selCount.ToString(CultureInfo.InvariantCulture) + "/2)", Color.FromArgb(44, 152, 86), selCount == 2, "ConfirmDraft");
        }

        private void RenderPickCardOverlay(Graphics g)
        {
            DrawModalBackdrop(g, Width, Height);
            RectangleF modal = new(260, 105, 760, 510);
            DrawModalFrame(g, modal, "REWARD — PICK A CARD (WAVE " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + " CLEARED!)", "All card & Special cooldowns have been reset to 0!");

            for (int i = 0; i < _engine.Reward.OfferedWeaponIds.Count; i++)
            {
                WeaponDef w = _engine.Db.GetWeaponOrFallback(_engine.Reward.OfferedWeaponIds[i]);
                RectangleF cr = new(modal.X + 148 + i * 268, modal.Y + 92, 196, 272);
                VectorArtRenderer.DrawPhysicalCard(g, w, null, _engine.Db, _engine.Profile, cr, IsMouseOver(cr), false, false, 0, false);
                DrawButton(g, new RectangleF(cr.X + 12, cr.Bottom + 12, cr.Width - 24, 34), "PICK CARD", Color.FromArgb(46, 152, 86), true, "PickRewardCard", i);
            }

            RenderRewardFooterButtons(g, modal.X + 170, modal.Bottom - 62);
        }

        private void RenderUpgradeOverlay(Graphics g)
        {
            DrawModalBackdrop(g, Width, Height);
            RectangleF modal = new(180, 68, 920, 588);
            DrawModalFrame(g, modal, "REWARD — UPGRADE A CARD (WAVE " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + " CLEARED!)", "All cooldowns reset! Step 1: Select a modifier. Step 2: Click one of your cards below to attach it.");

            for (int i = 0; i < _engine.Reward.OfferedModifiers.Count; i++)
            {
                ModifierType mt = _engine.Reward.OfferedModifiers[i];
                bool sel = _engine.Reward.SelectedModifier.HasValue && _engine.Reward.SelectedModifier.Value == mt;
                RectangleF mr = new(modal.X + 150 + i * 320, modal.Y + 86, 300, 92);
                VectorArtRenderer.DrawPanelBox(g, mr, sel ? Color.FromArgb(46, 78, 56) : Color.FromArgb(26, 34, 48), sel ? Color.LightGreen : Color.FromArgb(72, 88, 114), 8f, sel ? 2.6f : 1.5f);

                using Font mf1 = new("Segoe UI", 12f, FontStyle.Bold);
                using Font mf2 = new("Segoe UI", 8.2f, FontStyle.Regular);
                g.DrawString(GameEngine.GetModifierLabel(mt), mf1, mt == ModifierType.Poison ? Brushes.LimeGreen : Brushes.Gold, mr.X + 14, mr.Y + 10);
                g.DrawString(GameEngine.GetModifierDescription(mt), mf2, Brushes.Gainsboro, new RectangleF(mr.X + 14, mr.Y + 38, mr.Width - 28, 48));
                RegisterClick(mr, "SelectModifier", i);
            }

            RenderUpgradeTargetCards(g, modal);
            RenderRewardFooterButtons(g, modal.X + 250, modal.Bottom - 62);
        }

        private void RenderUpgradeTargetCards(Graphics g, RectangleF modal)
        {
            using Font sf = new("Segoe UI", 9.5f, FontStyle.Bold);
            string step2 = _engine.Reward.SelectedModifier.HasValue
                ? "STEP 2: CLICK A COMPATIBLE CARD BELOW TO ATTACH [" + GameEngine.GetModifierLabel(_engine.Reward.SelectedModifier.Value) + "]"
                : "STEP 2: SELECT A MODIFIER ABOVE FIRST";
            g.DrawString(step2, sf, _engine.Reward.SelectedModifier.HasValue ? Brushes.LightGreen : Brushes.Silver, modal.X + 36, modal.Y + 194);

            List<CardInstance> allCards = _engine.Hand.Concat(_engine.EquippedQueue).ToList();
            float startX = modal.X + (modal.Width - (allCards.Count * 170 - 14)) * 0.5f;
            for (int i = 0; i < allCards.Count; i++)
            {
                CardInstance c = allCards[i];
                WeaponDef w = _engine.Db.GetWeaponOrFallback(c.WeaponId);
                RectangleF cr = new(startX + i * 170, modal.Y + 224, 156, 224);
                bool compatible = !_engine.Reward.SelectedModifier.HasValue || GameEngine.IsModifierCompatibleWithCard(_engine.Reward.SelectedModifier.Value, w.Category);
                bool clickable = _engine.Reward.SelectedModifier.HasValue && compatible;

                VectorArtRenderer.DrawPhysicalCard(g, w, c, _engine.Db, _engine.Profile, cr, clickable && IsMouseOver(cr), false, false, 0, false);
                if (!compatible)
                {
                    using SolidBrush dis = new(Color.FromArgb(140, 16, 18, 24));
                    using Font wf = new("Segoe UI", 8f, FontStyle.Bold);
                    VectorArtRenderer.FillRoundedRect(g, dis, cr, 9f);
                    VectorArtRenderer.DrawCenteredText(g, "NOT VALID FOR\nMOVEMENT CARD", wf, Brushes.IndianRed, cr);
                }
                else if (clickable)
                {
                    RegisterClick(cr, "AttachUpgradeCard", i, c.InstanceUid);
                }
            }
        }

        private void RenderRewardFooterButtons(Graphics g, float leftX, float topY)
        {
            bool isFree = _engine.Player.FreeRerollsLeft > 0;
            bool canReroll = isFree || _engine.Profile.Coins >= _engine.Reward.RerollCost;
            string rerollLbl = isFree ? "REROLL (FREE!)" : ("REROLL (" + _engine.Reward.RerollCost.ToString(CultureInfo.InvariantCulture) + " COINS)");
            DrawButton(g, new RectangleF(leftX, topY, 200, 42), rerollLbl, Color.FromArgb(182, 126, 38), canReroll, "RerollReward");
            DrawButton(g, new RectangleF(leftX + 220, topY, 200, 42), "SKIP REWARD", Color.FromArgb(78, 88, 106), true, "SkipReward");
        }

        private void RenderEndRunOverlay(Graphics g)
        {
            DrawModalBackdrop(g, Width, Height);
            bool won = _engine.Phase == GamePhase.Victory;
            RectangleF modal = new(330, 140, 620, 430);
            DrawModalFrame(g, modal, won ? "VICTORY — LEVEL 1 CONQUERED!" : "DEFEAT — FALLEN IN BATTLE", won ? "You defeated the Viking Chief! Area 2 expansion hook unlocked." : "Your run has ended on Wave " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + ".");

            CharacterDef cdef = _engine.Db.GetCharacterOrFallback(_engine.Player.CharacterId);
            VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, new RectangleF(modal.X + 245, modal.Y + 92, 130, 120), false);

            RectangleF statBox = new(modal.X + 60, modal.Y + 220, modal.Width - 120, 118);
            using (SolidBrush sb = new(Color.FromArgb(20, 26, 38)))
            {
                VectorArtRenderer.FillRoundedRect(g, sb, statBox, 8f);
            }
            using (Font sf = new("Segoe UI", 10.5f, FontStyle.Bold))
            {
                g.DrawString("Hero: " + cdef.Name, sf, Brushes.Cyan, statBox.X + 24, statBox.Y + 14);
                g.DrawString("Wave Reached: " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + " / 10", sf, Brushes.White, statBox.X + 24, statBox.Y + 46);
                g.DrawString("Turns Taken: " + _engine.TurnCounter.ToString(CultureInfo.InvariantCulture), sf, Brushes.LightSteelBlue, statBox.X + 24, statBox.Y + 78);
                g.DrawString("Enemies Slain: " + _engine.Player.RunEnemiesDefeated.ToString(CultureInfo.InvariantCulture), sf, Brushes.Orange, statBox.X + 275, statBox.Y + 14);
                g.DrawString("Coins Earned: +" + _engine.Player.RunCoinsEarned.ToString(CultureInfo.InvariantCulture), sf, Brushes.Gold, statBox.X + 275, statBox.Y + 46);
                g.DrawString("Total Coins: " + _engine.Profile.Coins.ToString(CultureInfo.InvariantCulture), sf, Brushes.LightGreen, statBox.X + 275, statBox.Y + 78);
            }

            DrawButton(g, new RectangleF(modal.X + 70, modal.Bottom - 68, 225, 46), "RETURN TO MENU", Color.FromArgb(58, 108, 176), true, "ReturnToMenu");
            DrawButton(g, new RectangleF(modal.X + 325, modal.Bottom - 68, 225, 46), "START NEW RUN", Color.FromArgb(46, 152, 86), true, "StartNewRun", 0, _engine.Player.CharacterId);
        }

        private static void DrawModalBackdrop(Graphics g, int width, int height)
        {
            using SolidBrush veil = new(Color.FromArgb(182, 8, 10, 16));
            g.FillRectangle(veil, 0, 0, width, height);
        }

        private static void DrawModalFrame(Graphics g, RectangleF r, string title, string subtitle)
        {
            using LinearGradientBrush bg = new(r, Color.FromArgb(30, 38, 54), Color.FromArgb(18, 24, 34), 90f);
            using Pen bp = new(Color.FromArgb(95, 138, 188), 2.2f);
            VectorArtRenderer.FillRoundedRect(g, bg, r, 12f);
            VectorArtRenderer.DrawRoundedRect(g, bp, r, 12f);

            using Font tf = new("Segoe UI", 15f, FontStyle.Bold);
            using Font sf = new("Segoe UI", 9.5f, FontStyle.Regular);
            VectorArtRenderer.DrawCenteredText(g, title, tf, Brushes.White, new RectangleF(r.X + 16, r.Y + 16, r.Width - 32, 30));
            VectorArtRenderer.DrawCenteredText(g, subtitle, sf, Brushes.LightSkyBlue, new RectangleF(r.X + 16, r.Y + 48, r.Width - 32, 24));
        }

        private void RenderAchievementsAndStats(Graphics g)
        {
            RenderStatsHeader(g);
            RenderLifetimeStatsPanel(g);
            RenderAchievementsGrid(g);
        }

        private void RenderStatsHeader(Graphics g)
        {
            using SolidBrush hb = new(Color.FromArgb(24, 30, 42));
            using Pen div = new(Color.FromArgb(56, 70, 94), 1.5f);
            g.FillRectangle(hb, 0, 0, Width, 52);
            g.DrawLine(div, 0, 52, Width, 52);

            using Font tf = new("Segoe UI", 16f, FontStyle.Bold);
            using Font cf = new("Segoe UI", 12f, FontStyle.Bold);
            g.DrawString("ACHIEVEMENTS & LIFETIME STATISTICS", tf, Brushes.White, 22, 11);
            g.DrawString("COINS: " + _engine.Profile.Coins.ToString(CultureInfo.InvariantCulture) + "   |   UNLOCKED: " + _engine.Profile.CompletedAchievements.Count.ToString(CultureInfo.InvariantCulture) + " / 10", cf, Brushes.Gold, 740, 15);
            DrawButton(g, new RectangleF(1088, 9, 170, 34), "BACK TO MENU", Color.FromArgb(56, 108, 178), true, "ReturnToMenu");
        }

        private void RenderLifetimeStatsPanel(Graphics g)
        {
            VectorArtRenderer.DrawPanelBox(g, new RectangleF(20, 62, 1240, 166), Color.FromArgb(22, 28, 40), Color.FromArgb(56, 70, 94), 9f, 1.5f);
            StatsData st = _engine.Stats;

            using Font hFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font lFont = new("Segoe UI", 8.8f, FontStyle.Regular);
            using Font vFont = new("Segoe UI", 9.0f, FontStyle.Bold);

            g.DrawString("RUNS & PROGRESSION", hFont, Brushes.LightSkyBlue, 38, 72);
            DrawStatRow(g, 38, 98, "Total Runs Played:", st.TotalRunsPlayed.ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 38, 124, "Runs Won:", st.RunsWon.ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 38, 150, "Highest Wave Reached:", st.HighestWaveReached.ToString(CultureInfo.InvariantCulture) + " / 10", lFont, vFont);
            DrawStatRow(g, 38, 176, "Total Turns Taken:", st.TotalTurnsTaken.ToString(CultureInfo.InvariantCulture), lFont, vFont);

            g.DrawString("COMBAT & TACTICS", hFont, Brushes.LightSalmon, 355, 72);
            DrawStatRow(g, 355, 98, "Total Enemies Defeated:", st.TotalEnemiesDefeated.ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 355, 124, "Total Damage Dealt:", st.TotalDamageDealt.ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 355, 150, "Friendly-Fire Kills:", st.TotalFriendlyFireKills.ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 355, 176, "Bear Trap Stuns:", st.BearTrapStunsTriggered.ToString(CultureInfo.InvariantCulture), lFont, vFont);

            g.DrawString("DECK & ECONOMY", hFont, Brushes.Gold, 670, 72);
            DrawStatRow(g, 670, 98, "Total Coins Earned:", st.TotalCoinsEarned.ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 670, 124, "Total Cards Played:", st.TotalCardsPlayed.ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 670, 150, "Most Used Weapon:", st.GetMostUsedWeaponName(_engine.Db), lFont, vFont);
            DrawStatRow(g, 670, 176, "Unlocked Weapons/Upg:", _engine.Profile.UnlockedWeapons.Count.ToString(CultureInfo.InvariantCulture) + " / " + _engine.Profile.UnlockedUpgrades.Count.ToString(CultureInfo.InvariantCulture), lFont, vFont);

            g.DrawString("ENEMIES SLAIN BY TYPE", hFont, Brushes.LightGreen, 985, 72);
            DrawStatRow(g, 985, 96, "Pawns:", st.EnemiesDefeatedByType.GetValueOrDefault("pawn").ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 985, 118, "Archers:", st.EnemiesDefeatedByType.GetValueOrDefault("archer").ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 985, 140, "Spearmen:", st.EnemiesDefeatedByType.GetValueOrDefault("spearman").ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 985, 162, "Shamans:", st.EnemiesDefeatedByType.GetValueOrDefault("shaman").ToString(CultureInfo.InvariantCulture), lFont, vFont);
            DrawStatRow(g, 985, 184, "Viking Chiefs (Boss):", st.EnemiesDefeatedByType.GetValueOrDefault("viking_chief").ToString(CultureInfo.InvariantCulture), lFont, vFont);
        }

        private void RenderAchievementsGrid(Graphics g)
        {
            for (int i = 0; i < Math.Min(10, _engine.Db.AchievementList.Count); i++)
            {
                int col = i % 5;
                int row = i / 5;
                RectangleF cr = new(20f + col * 250.5f, 240f + row * 238f, 238f, 224f);
                RenderAchievementCard(g, _engine.Db.AchievementList[i], cr);
            }
        }

        private void RenderAchievementCard(Graphics g, AchievementDef ach, RectangleF cr)
        {
            bool done = _engine.Profile.CompletedAchievements.Contains(ach.Id);
            int prog = done ? ach.TargetValue : _engine.Profile.AchievementProgress.GetValueOrDefault(ach.Id);

            Color bg1 = done ? Color.FromArgb(42, 40, 26) : Color.FromArgb(24, 29, 40);
            Color bg2 = done ? Color.FromArgb(26, 24, 18) : Color.FromArgb(18, 22, 30);
            using LinearGradientBrush gb = new(cr, bg1, bg2, 90f);
            using Pen bp = new(done ? Color.Gold : Color.FromArgb(58, 68, 86), done ? 2.0f : 1.3f);
            VectorArtRenderer.FillRoundedRect(g, gb, cr, 9f);
            VectorArtRenderer.DrawRoundedRect(g, bp, cr, 9f);

            VectorArtRenderer.DrawTrophyIcon(g, new RectangleF(cr.X + 12, cr.Y + 12, 44, 44), done);

            RectangleF stTag = new(cr.Right - 88, cr.Y + 14, 76, 20);
            using (SolidBrush stb = new(done ? Color.FromArgb(54, 148, 76) : Color.FromArgb(56, 62, 74)))
            using (Font tagF = new("Segoe UI", 7.2f, FontStyle.Bold))
            {
                VectorArtRenderer.FillRoundedRect(g, stb, stTag, 4f);
                VectorArtRenderer.DrawCenteredText(g, done ? "UNLOCKED" : "LOCKED", tagF, done ? Brushes.White : Brushes.LightGray, stTag);
            }

            using Font tf = new("Segoe UI", 10f, FontStyle.Bold);
            using Font df = new("Segoe UI", 8.0f, FontStyle.Regular);
            using Font rf = new("Segoe UI", 7.8f, FontStyle.Bold);
            g.DrawString(ach.Title, tf, done ? Brushes.Gold : Brushes.White, new RectangleF(cr.X + 12, cr.Y + 62, cr.Width - 24, 22));
            g.DrawString(ach.Description, df, Brushes.Gainsboro, new RectangleF(cr.X + 12, cr.Y + 86, cr.Width - 24, 56));

            RectangleF pBar = new(cr.X + 12, cr.Y + 148, cr.Width - 24, 18);
            using (SolidBrush pbg = new(Color.FromArgb(14, 18, 26)))
            {
                VectorArtRenderer.FillRoundedRect(g, pbg, pBar, 4f);
            }
            float ratio = ach.TargetValue > 0 ? Math.Min(1f, (float)prog / ach.TargetValue) : 0f;
            if (ratio > 0f)
            {
                using SolidBrush pf = new(done ? Color.Gold : Color.FromArgb(62, 152, 224));
                VectorArtRenderer.FillRoundedRect(g, pf, new RectangleF(pBar.X, pBar.Y, pBar.Width * ratio, pBar.Height), 4f);
            }
            VectorArtRenderer.DrawCenteredText(g, prog.ToString(CultureInfo.InvariantCulture) + " / " + ach.TargetValue.ToString(CultureInfo.InvariantCulture), rf, done ? Brushes.Black : Brushes.White, pBar);

            string rew = "Reward: " + ach.CoinReward.ToString(CultureInfo.InvariantCulture) + " Coins";
            if (!string.IsNullOrEmpty(ach.SpecialUnlock) && ach.SpecialUnlock != "None")
            {
                rew += " + " + ach.SpecialUnlock;
            }
            g.DrawString(rew, rf, Brushes.Gold, new RectangleF(cr.X + 12, cr.Bottom - 36, cr.Width - 24, 28));
        }

        private static void DrawStatRow(Graphics g, float x, float y, string label, string value, Font lFont, Font vFont)
        {
            g.DrawString(label, lFont, Brushes.LightSteelBlue, x, y);
            g.DrawString(value, vFont, Brushes.White, x + 178, y);
        }

        private void RenderToastBanner(Graphics g)
        {
            ToastMessage t = _engine.ActiveToast;
            if (t == null)
            {
                return;
            }
            RectangleF tr = new(Width * 0.5f - 220, 12, 440, 56);
            VectorArtRenderer.DrawPanelBox(g, tr, Color.FromArgb(238, 22, 28, 40), t.AccentColor, 10f, 2.4f);
            VectorArtRenderer.DrawTrophyIcon(g, new RectangleF(tr.X + 12, tr.Y + 8, 40, 40), true);
            using Font f1 = new("Segoe UI", 10.5f, FontStyle.Bold);
            using Font f2 = new("Segoe UI", 8.8f, FontStyle.Regular);
            using SolidBrush b1 = new(t.AccentColor);
            g.DrawString(t.Title, f1, b1, tr.X + 60, tr.Y + 8);
            g.DrawString(t.Subtitle, f2, Brushes.White, tr.X + 60, tr.Y + 30);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.Location;
            if (_screen == ScreenMode.Gameplay)
            {
                UpdateHoveredGridTile();
                UpdateHoveredCard();
                UpdateAimAndDragOnMove(e.Location);
            }
            Invalidate();
        }

        private void UpdateAimAndDragOnMove(Point pt)
        {
            if (_engine.Phase == GamePhase.AimingCard && _hoveredGridTile.HasValue)
            {
                _engine.UpdateAimPreviewFromHoverTile(_hoveredGridTile.Value);
            }
            if (_dragFromIndex < 0)
            {
                return;
            }
            if (!_isDraggingQueueCard && (Math.Abs(pt.X - _dragStartPoint.X) > 6 || Math.Abs(pt.Y - _dragStartPoint.Y) > 6))
            {
                _isDraggingQueueCard = true;
            }
            if (_isDraggingQueueCard)
            {
                _dragHoverIndex = GetEquippedIndexAtPoint(pt);
            }
        }

        private void UpdateHoveredCard()
        {
            _hoveredCard = null;
            if (_engine.Phase is not (GamePhase.PlayerTurn or GamePhase.AimingCard))
            {
                return;
            }
            for (int i = 0; i < _engine.Hand.Count; i++)
            {
                if (GetHandSlotRect(i).Contains(_mousePos))
                {
                    _hoveredCard = _engine.Hand[i];
                    return;
                }
            }
            for (int i = 0; i < _engine.EquippedQueue.Count; i++)
            {
                if (GetEquippedSlotRect(i).Contains(_mousePos))
                {
                    _hoveredCard = _engine.EquippedQueue[i];
                    return;
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _mousePos = e.Location;
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            if (TryStartQueueDrag(e.Location) || TryDispatchClickRegion(e.Location))
            {
                Invalidate();
                return;
            }
            HandleGridClick();
            Invalidate();
        }

        private bool TryStartQueueDrag(Point pt)
        {
            if (_screen != ScreenMode.Gameplay || _engine.Phase != GamePhase.PlayerTurn)
            {
                return false;
            }
            int eqIdx = GetEquippedIndexAtPoint(pt);
            if (eqIdx < 0)
            {
                return false;
            }
            _dragFromIndex = eqIdx;
            _dragHoverIndex = eqIdx;
            _dragStartPoint = pt;
            _isDraggingQueueCard = false;
            return true;
        }

        private bool TryDispatchClickRegion(Point pt)
        {
            for (int i = _clickRegions.Count - 1; i >= 0; i--)
            {
                if (_clickRegions[i].Bounds.Contains(pt))
                {
                    HandleClickAction(_clickRegions[i]);
                    return true;
                }
            }
            return false;
        }

        private void HandleGridClick()
        {
            if (_screen != ScreenMode.Gameplay || !_hoveredGridTile.HasValue)
            {
                return;
            }
            GridPos clicked = _hoveredGridTile.Value;
            if (_engine.Phase == GamePhase.AimingCard)
            {
                _engine.UpdateAimPreviewFromHoverTile(clicked);
                _engine.ConfirmAimedCard();
            }
            else if (_engine.Phase == GamePhase.PlayerTurn)
            {
                int dx = clicked.X - _engine.Player.Pos.X;
                int dy = clicked.Y - _engine.Player.Pos.Y;
                if (Math.Abs(dx) + Math.Abs(dy) == 1)
                {
                    _engine.TryMovePlayer(dx, dy);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            CompleteQueueDragIfActive(e.Location);
            ResetDragState();
            Invalidate();
        }

        private void CompleteQueueDragIfActive(Point pt)
        {
            if (!_isDraggingQueueCard || _dragFromIndex < 0)
            {
                return;
            }
            int targetIdx = GetEquippedIndexAtPoint(pt);
            if (targetIdx >= 0 && targetIdx != _dragFromIndex)
            {
                _engine.ReorderEquippedCards(_dragFromIndex, targetIdx);
            }
        }

        private void ResetDragState()
        {
            _dragFromIndex = -1;
            _dragHoverIndex = -1;
            _isDraggingQueueCard = false;
        }

        private int GetEquippedIndexAtPoint(Point pt)
        {
            for (int i = 0; i < _engine.EquippedQueue.Count; i++)
            {
                if (GetEquippedSlotRect(i).Contains(pt))
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpdateHoveredGridTile()
        {
            if (!GridRect.Contains(_mousePos))
            {
                _hoveredGridTile = null;
                return;
            }
            GridPos gp = new((int)((_mousePos.X - GridRect.X) / TileSize), (int)((_mousePos.Y - GridRect.Y) / TileSize));
            _hoveredGridTile = gp.IsInBounds() ? gp : null;
        }

        private void HandleClickAction(ClickRegion reg)
        {
            if (HandleMenuAction(reg) || HandleGameplayAction(reg))
            {
                return;
            }
            HandleRewardAction(reg);
        }

        private bool HandleMenuAction(ClickRegion reg)
        {
            switch (reg.ActionId)
            {
                case "DismissNotification":
                    _engine.MenuNotification = string.Empty;
                    return true;
                case "SelectHero":
                    _selectedCharacterId = reg.Tag;
                    return true;
                case "StartNewRun":
                    _engine.StartNewRun(string.IsNullOrEmpty(reg.Tag) ? _selectedCharacterId : reg.Tag);
                    _screen = ScreenMode.Gameplay;
                    return true;
                case "ContinueRun":
                    if (_engine.Saves.TryLoadActiveRun(_engine, _engine.Db, out string err))
                    {
                        _engine.MenuNotification = string.Empty;
                        _screen = ScreenMode.Gameplay;
                    }
                    else
                    {
                        _engine.MenuNotification = err;
                    }
                    return true;
                case "OpenAchievements":
                    _screen = ScreenMode.AchievementsAndStats;
                    return true;
                case "ReturnToMenu":
                    _screen = ScreenMode.MainMenu;
                    return true;
                case "BuyWeapon":
                    _engine.TryBuyWeaponUnlock(reg.Tag);
                    return true;
                case "BuyUpgrade":
                    _engine.TryBuyUpgradeUnlock(reg.Tag);
                    return true;
                default:
                    return false;
            }
        }

        private bool HandleGameplayAction(ClickRegion reg)
        {
            switch (reg.ActionId)
            {
                case "SaveAndExit":
                    _engine.Saves.SaveActiveRun(_engine);
                    _screen = ScreenMode.MainMenu;
                    return true;
                case "EquipCard":
                    _engine.TryEquipHandCard(reg.Index);
                    return true;
                case "PlayQueue":
                    _engine.BeginPlayEquippedQueue();
                    return true;
                case "WaitTurn":
                    _engine.WaitTurn();
                    return true;
                case "UseSpecial":
                    _engine.TryUseSpecial();
                    return true;
                case "SkipAimCard":
                    _engine.SkipCurrentAimedCard();
                    return true;
                case "ConfirmAimCard":
                    _engine.ConfirmAimedCard();
                    return true;
                default:
                    return false;
            }
        }

        private void HandleRewardAction(ClickRegion reg)
        {
            switch (reg.ActionId)
            {
                case "ToggleDraftCard":
                    _engine.ToggleStartingDraftCard(reg.Index);
                    break;
                case "ConfirmDraft":
                    _engine.ConfirmStartingDraft();
                    break;
                case "PickRewardCard":
                    _engine.PickRewardCard(reg.Index);
                    break;
                case "SelectModifier":
                    _engine.SelectUpgradeModifier(reg.Index);
                    break;
                case "AttachUpgradeCard":
                    CardInstance target = _engine.Hand.Concat(_engine.EquippedQueue).FirstOrDefault(c => c.InstanceUid == reg.Tag);
                    _engine.TryAttachSelectedModifierToCard(target);
                    break;
                case "RerollReward":
                    _engine.TryRerollReward();
                    break;
                case "SkipReward":
                    _engine.SkipReward();
                    break;
            }
        }

        public void HandleKeyInput(Keys keyCode)
        {
            if (_screen == ScreenMode.AchievementsAndStats && keyCode == Keys.Escape)
            {
                _screen = ScreenMode.MainMenu;
                Invalidate();
                return;
            }
            if (_screen != ScreenMode.Gameplay)
            {
                return;
            }
            if (_engine.Phase == GamePhase.PlayerTurn)
            {
                HandlePlayerTurnKey(keyCode);
            }
            else if (_engine.Phase == GamePhase.AimingCard)
            {
                HandleAimingKey(keyCode);
            }
            Invalidate();
        }

        private void HandlePlayerTurnKey(Keys keyCode)
        {
            switch (keyCode)
            {
                case Keys.W:
                case Keys.Up:
                    _engine.TryMovePlayer(0, -1);
                    break;
                case Keys.S:
                case Keys.Down:
                    _engine.TryMovePlayer(0, 1);
                    break;
                case Keys.A:
                case Keys.Left:
                    _engine.TryMovePlayer(-1, 0);
                    break;
                case Keys.D:
                case Keys.Right:
                    _engine.TryMovePlayer(1, 0);
                    break;
                case Keys.Space:
                    _engine.WaitTurn();
                    break;
                case Keys.E:
                    _engine.TryUseSpecial();
                    break;
                case Keys.Enter:
                    _engine.BeginPlayEquippedQueue();
                    break;
                case Keys.D1:
                case Keys.D2:
                case Keys.D3:
                case Keys.D4:
                    _engine.TryEquipHandCard((int)(keyCode - Keys.D1));
                    break;
            }
        }

        private void HandleAimingKey(Keys keyCode)
        {
            CardInstance aimCard = _engine.GetCurrentlyAimingCard();
            if (aimCard == null)
            {
                return;
            }
            if (keyCode is Keys.Enter or Keys.Space)
            {
                _engine.ConfirmAimedCard();
                return;
            }

            AimMode mode = _engine.Db.GetWeaponOrFallback(aimCard.WeaponId).AimMode;
            GridPos? nextDir = ResolveAimDirectionFromKey(keyCode, mode);
            if (!nextDir.HasValue)
            {
                return;
            }
            if (_engine.CurrentAimDir == nextDir.Value)
            {
                _engine.ConfirmAimedCard();
            }
            else
            {
                _engine.UpdateAimPreview(nextDir.Value);
            }
        }

        private static GridPos? ResolveAimDirectionFromKey(Keys keyCode, AimMode mode)
        {
            if (mode == AimMode.Orthogonal)
            {
                return keyCode switch
                {
                    Keys.W or Keys.Up => new GridPos(0, -1),
                    Keys.S or Keys.Down => new GridPos(0, 1),
                    Keys.A or Keys.Left => new GridPos(-1, 0),
                    Keys.D or Keys.Right => new GridPos(1, 0),
                    _ => null
                };
            }
            if (mode == AimMode.Diagonal)
            {
                return keyCode switch
                {
                    Keys.Q => new GridPos(-1, -1),
                    Keys.E => new GridPos(1, -1),
                    Keys.Z => new GridPos(-1, 1),
                    Keys.C => new GridPos(1, 1),
                    _ => null
                };
            }
            return null;
        }
    }

    public class MainForm : Form
    {
        private static readonly HashSet<Keys> HandledKeys =
        [
            Keys.Up,
        Keys.Down,
        Keys.Left,
        Keys.Right,
        Keys.W,
        Keys.A,
        Keys.S,
        Keys.D,
        Keys.Q,
        Keys.E,
        Keys.Z,
        Keys.C,
        Keys.Space,
        Keys.Enter,
        Keys.Escape,
        Keys.D1,
        Keys.D2,
        Keys.D3,
        Keys.D4
        ];

        private readonly GameCanvas _canvas;

        public MainForm(GameEngine engine)
        {
            Text = "Rogue Cards — Tactical 5x5 Deckbuilder";
            ClientSize = new Size(1280, 720);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScroll = false;
            KeyPreview = true;
            BackColor = Color.FromArgb(16, 20, 28);

            _canvas = new GameCanvas(engine) { Dock = DockStyle.Fill };
            Controls.Add(_canvas);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            if (HandledKeys.Contains(keyCode))
            {
                _canvas.HandleKeyInput(keyCode);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data");
            string savesDir = Path.Combine(baseDir, "Saves");
            string logsDir = Path.Combine(baseDir, "Logs");

            GameDatabase db = DataParser.LoadAll(dataDir, logsDir);
            SaveManager saves = new(savesDir, logsDir);
            ProfileData profile = saves.LoadProfile(db);
            StatsData stats = saves.LoadStats(db);
            CombatLogger logger = new(logsDir);

            GameEngine engine = new(db, profile, stats, saves, logger);
            using MainForm form = new(engine);
            Application.Run(form);
        }
    }
}