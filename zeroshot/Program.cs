#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
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

    public struct GridPos : IEquatable<GridPos>
    {
        public int X;
        public int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsInBounds()
        {
            return X >= 0 && X < 5 && Y >= 0 && Y < 5;
        }

        public int ManhattanDistance(GridPos other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public bool Equals(GridPos other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            if (obj is GridPos other)
            {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
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
            char col = (char)('A' + Math.Max(0, Math.Min(4, X)));
            return col.ToString() + (Y + 1).ToString(CultureInfo.InvariantCulture);
        }
    }

    public class WeaponDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public CardCategory Category { get; set; }
        public AimMode AimMode { get; set; }
        public int BaseDamage { get; set; }
        public int BaseCooldown { get; set; }
        public bool DefaultUnlocked { get; set; }
        public int UnlockOrder { get; set; }
        public int UnlockCost { get; set; }
        public string Description { get; set; }
    }

    public class EnemyDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int MaxHp { get; set; }
        public int Damage { get; set; }
        public int CoinDrop { get; set; }
        public bool IsBoss { get; set; }
        public bool SpawnNearEdge { get; set; }
        public string WeaponName { get; set; }
        public string Description { get; set; }
    }

    public class WaveDef
    {
        public int WaveNumber { get; set; }
        public List<string> EnemyIds { get; set; }
        public string RewardAfter { get; set; }

        public WaveDef()
        {
            EnemyIds = new List<string>();
        }
    }

    public class UpgradeDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int UnlockOrder { get; set; }
        public int Cost { get; set; }
        public string Description { get; set; }
    }

    public class CharacterDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int BaseHp { get; set; }
        public string SpecialName { get; set; }
        public int SpecialCooldown { get; set; }
        public bool DefaultUnlocked { get; set; }
        public string UnlockConditionText { get; set; }
        public string SpecialDescription { get; set; }
    }

    public class AchievementDef
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int TargetValue { get; set; }
        public int CoinReward { get; set; }
        public string SpecialUnlock { get; set; }
    }

    public class GameDatabase
    {
        public Dictionary<string, WeaponDef> Weapons { get; set; }
        public List<WeaponDef> WeaponList { get; set; }
        public Dictionary<string, EnemyDef> Enemies { get; set; }
        public List<WaveDef> Waves { get; set; }
        public Dictionary<string, UpgradeDef> Upgrades { get; set; }
        public List<UpgradeDef> UpgradeList { get; set; }
        public Dictionary<string, CharacterDef> Characters { get; set; }
        public List<CharacterDef> CharacterList { get; set; }
        public Dictionary<string, AchievementDef> Achievements { get; set; }
        public List<AchievementDef> AchievementList { get; set; }
        public List<string> LoadWarnings { get; set; }

        public GameDatabase()
        {
            Weapons = new Dictionary<string, WeaponDef>(StringComparer.OrdinalIgnoreCase);
            WeaponList = new List<WeaponDef>();
            Enemies = new Dictionary<string, EnemyDef>(StringComparer.OrdinalIgnoreCase);
            Waves = new List<WaveDef>();
            Upgrades = new Dictionary<string, UpgradeDef>(StringComparer.OrdinalIgnoreCase);
            UpgradeList = new List<UpgradeDef>();
            Characters = new Dictionary<string, CharacterDef>(StringComparer.OrdinalIgnoreCase);
            CharacterList = new List<CharacterDef>();
            Achievements = new Dictionary<string, AchievementDef>(StringComparer.OrdinalIgnoreCase);
            AchievementList = new List<AchievementDef>();
            LoadWarnings = new List<string>();
        }
    }

    public class CardInstance
    {
        public string InstanceUid { get; set; }
        public string WeaponId { get; set; }
        public int CurrentCooldown { get; set; }
        public int BonusDamage { get; set; }
        public int CooldownModifier { get; set; }
        public bool HasPoison { get; set; }
        public bool UsedThisTurn { get; set; }

        public CardInstance()
        {
            InstanceUid = Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public int GetEffectiveDamage(GameDatabase db, ProfileData profile)
        {
            WeaponDef def = db.Weapons[WeaponId];
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
            WeaponDef def = db.Weapons[WeaponId];
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
            List<string> parts = new List<string>();
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
        public string Uid { get; set; }
        public string EnemyId { get; set; }
        public GridPos Pos { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public EnemyAIState State { get; set; }
        public int ChargeTurnsRemaining { get; set; }
        public List<GridPos> LockedTiles { get; set; }
        public GridPos AimDir { get; set; }
        public int BossCycleIndex { get; set; }
        public int PoisonTurns { get; set; }
        public int StunTurns { get; set; }
        public string IntentLabel { get; set; }
        public bool SpawnedMidTurn { get; set; }

        public EnemyInstance()
        {
            Uid = Guid.NewGuid().ToString("N").Substring(0, 8);
            LockedTiles = new List<GridPos>();
            IntentLabel = "Equipping";
        }
    }

    public class HazardInstance
    {
        public HazardType Type { get; set; }
        public GridPos Pos { get; set; }
        public int RemainingTurns { get; set; }
        public bool IsArmed { get; set; }
        public int Damage { get; set; }
        public bool AppliesPoison { get; set; }
    }

    public class PlayerState
    {
        public string CharacterId { get; set; }
        public GridPos Pos { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
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
        public List<string> OfferedWeaponIds { get; set; }
        public List<ModifierType> OfferedModifiers { get; set; }
        public ModifierType? SelectedModifier { get; set; }
        public int RerollCost { get; set; }
        public List<int> StartingDraftSelectedIndices { get; set; }

        public RewardState()
        {
            OfferedWeaponIds = new List<string>();
            OfferedModifiers = new List<ModifierType>();
            StartingDraftSelectedIndices = new List<int>();
            RerollCost = 5;
        }
    }

    public class FloatingText
    {
        public GridPos Pos { get; set; }
        public string Text { get; set; }
        public Color Color { get; set; }
        public float Progress { get; set; }
        public float OffsetX { get; set; }
    }

    public class ToastMessage
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public Color AccentColor { get; set; }
        public float RemainingSeconds { get; set; }
    }

    public class ProfileData
    {
        public int Coins { get; set; }
        public HashSet<string> UnlockedWeapons { get; set; }
        public HashSet<string> UnlockedUpgrades { get; set; }
        public HashSet<string> UnlockedCharacters { get; set; }
        public HashSet<string> CompletedAchievements { get; set; }
        public Dictionary<string, int> AchievementProgress { get; set; }
        public bool Area2Unlocked { get; set; }

        public ProfileData()
        {
            UnlockedWeapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UnlockedUpgrades = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UnlockedCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CompletedAchievements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AchievementProgress = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public class StatsData
    {
        public int TotalRunsPlayed { get; set; }
        public int RunsWon { get; set; }
        public int HighestWaveReached { get; set; }
        public int TotalTurnsTaken { get; set; }
        public int TotalEnemiesDefeated { get; set; }
        public Dictionary<string, int> EnemiesDefeatedByType { get; set; }
        public int TotalFriendlyFireKills { get; set; }
        public int TotalDamageDealt { get; set; }
        public int TotalCoinsEarned { get; set; }
        public int TotalCardsPlayed { get; set; }
        public Dictionary<string, int> WeaponUsageCounts { get; set; }
        public int BearTrapStunsTriggered { get; set; }

        public StatsData()
        {
            EnemiesDefeatedByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            WeaponUsageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public string GetMostUsedWeaponName(GameDatabase db)
        {
            if (WeaponUsageCounts.Count == 0)
            {
                return "None";
            }
            string bestId = null;
            int bestCount = -1;
            foreach (KeyValuePair<string, int> kv in WeaponUsageCounts)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    bestId = kv.Key;
                }
            }
            if (bestId == null || bestCount <= 0)
            {
                return "None";
            }
            if (db.Weapons.ContainsKey(bestId))
            {
                return db.Weapons[bestId].Name + " (" + bestCount.ToString(CultureInfo.InvariantCulture) + ")";
            }
            return bestId + " (" + bestCount.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }

    public class CombatLogEntry
    {
        public string Timestamp { get; set; }
        public string Text { get; set; }
        public Color Color { get; set; }
    }

    public static class DefaultDataInitializer
    {
        public static void EnsureDefaultFiles(string dataDir)
        {
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

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
            StringBuilder sb = new StringBuilder();
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
            StringBuilder sb = new StringBuilder();
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
            StringBuilder sb = new StringBuilder();
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
            StringBuilder sb = new StringBuilder();
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
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Rogue Cards - Playable Characters");
            sb.AppendLine("# Format: id|name|baseHp|specialName|specialCooldown|defaultUnlocked|unlockCondition|specialDescription");
            sb.AppendLine("viking|Viking|5|Viking Scream|6|true|Unlocked by default|Knocks back all adjacent enemies (8 directions) by 1 tile. Deals 0 dmg (1 with Speciality Master).");
            sb.AppendLine("shieldmaiden|Shieldmaiden|5|Iron Bulwark|6|false|Complete Kingslayer Achievement|Grants 1 turn of full damage immunity and reduces card cooldowns by 1. (+1 dmg to orthogonal foes with Speciality Master).");
            return sb.ToString();
        }

        private static string GetDefaultAchievements()
        {
            StringBuilder sb = new StringBuilder();
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
        public static GameDatabase LoadAll(string dataDir, string logsDir)
        {
            DefaultDataInitializer.EnsureDefaultFiles(dataDir);
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            GameDatabase db = new GameDatabase();
            List<string> errors = new List<string>();

            ParseWeapons(Path.Combine(dataDir, "weapons.txt"), db, errors);
            ParseEnemies(Path.Combine(dataDir, "enemies.txt"), db, errors);
            ParseWaves(Path.Combine(dataDir, "waves.txt"), db, errors);
            ParseUpgrades(Path.Combine(dataDir, "upgrades.txt"), db, errors);
            ParseCharacters(Path.Combine(dataDir, "characters.txt"), db, errors);
            ParseAchievements(Path.Combine(dataDir, "achievements.txt"), db, errors);

            db.WeaponList = db.WeaponList.OrderBy(w => w.UnlockOrder).ThenBy(w => w.Name).ToList();
            db.UpgradeList = db.UpgradeList.OrderBy(u => u.UnlockOrder).ToList();
            db.Waves = db.Waves.OrderBy(w => w.WaveNumber).ToList();

            if (errors.Count > 0)
            {
                string logPath = Path.Combine(logsDir, "data_validation.log");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== Data Validation Run: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " ===");
                foreach (string err in errors)
                {
                    sb.AppendLine(err);
                    db.LoadWarnings.Add(err);
                }
                File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
            }

            return db;
        }

        private static void ParseWeapons(string path, GameDatabase db, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add("weapons.txt: File missing.");
                return;
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                string raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }
                string[] parts = raw.Split('|');
                if (parts.Length != 10)
                {
                    errors.Add("weapons.txt line " + lineNum + ": Expected 10 columns, found " + parts.Length + ".");
                    continue;
                }
                string id = parts[0].Trim();
                string name = parts[1].Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    errors.Add("weapons.txt line " + lineNum + ": Empty id or name.");
                    continue;
                }
                if (db.Weapons.ContainsKey(id))
                {
                    errors.Add("weapons.txt line " + lineNum + ": Duplicate weapon id '" + id + "'.");
                    continue;
                }
                if (!Enum.TryParse(parts[2].Trim(), true, out CardCategory cat))
                {
                    errors.Add("weapons.txt line " + lineNum + ": Invalid category '" + parts[2] + "'.");
                    continue;
                }
                if (!Enum.TryParse(parts[3].Trim(), true, out AimMode aim))
                {
                    errors.Add("weapons.txt line " + lineNum + ": Invalid aimMode '" + parts[3] + "'.");
                    continue;
                }
                if (!int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dmg) || dmg < 0 || dmg > 50)
                {
                    errors.Add("weapons.txt line " + lineNum + ": Invalid damage '" + parts[4] + "' (range 0..50).");
                    continue;
                }
                if (!int.TryParse(parts[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cd) || cd < 1 || cd > 20)
                {
                    errors.Add("weapons.txt line " + lineNum + ": Invalid cooldown '" + parts[5] + "' (range 1..20).");
                    continue;
                }
                if (!bool.TryParse(parts[6].Trim(), out bool defUnlocked))
                {
                    errors.Add("weapons.txt line " + lineNum + ": Invalid defaultUnlocked boolean '" + parts[6] + "'.");
                    continue;
                }
                if (!int.TryParse(parts[7].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int order) || order < 0 || order > 100)
                {
                    errors.Add("weapons.txt line " + lineNum + ": Invalid unlockOrder '" + parts[7] + "'.");
                    continue;
                }
                if (!int.TryParse(parts[8].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cost) || cost < 0 || cost > 9999)
                {
                    errors.Add("weapons.txt line " + lineNum + ": Invalid unlockCost '" + parts[8] + "'.");
                    continue;
                }
                string desc = parts[9].Trim();
                if (string.IsNullOrEmpty(desc))
                {
                    errors.Add("weapons.txt line " + lineNum + ": Empty description.");
                    continue;
                }

                WeaponDef w = new WeaponDef
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
        }

        private static void ParseEnemies(string path, GameDatabase db, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add("enemies.txt: File missing.");
                return;
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                string raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }
                string[] parts = raw.Split('|');
                if (parts.Length != 9)
                {
                    errors.Add("enemies.txt line " + lineNum + ": Expected 9 columns, found " + parts.Length + ".");
                    continue;
                }
                string id = parts[0].Trim();
                string name = parts[1].Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    errors.Add("enemies.txt line " + lineNum + ": Empty id or name.");
                    continue;
                }
                if (db.Enemies.ContainsKey(id))
                {
                    errors.Add("enemies.txt line " + lineNum + ": Duplicate enemy id '" + id + "'.");
                    continue;
                }
                if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hp) || hp < 1 || hp > 500)
                {
                    errors.Add("enemies.txt line " + lineNum + ": Invalid maxHp '" + parts[2] + "' (range 1..500).");
                    continue;
                }
                if (!int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dmg) || dmg < 0 || dmg > 50)
                {
                    errors.Add("enemies.txt line " + lineNum + ": Invalid damage '" + parts[3] + "' (range 0..50).");
                    continue;
                }
                if (!int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int coins) || coins < 0 || coins > 500)
                {
                    errors.Add("enemies.txt line " + lineNum + ": Invalid coinDrop '" + parts[4] + "'.");
                    continue;
                }
                if (!bool.TryParse(parts[5].Trim(), out bool isBoss))
                {
                    errors.Add("enemies.txt line " + lineNum + ": Invalid isBoss boolean '" + parts[5] + "'.");
                    continue;
                }
                if (!bool.TryParse(parts[6].Trim(), out bool edge))
                {
                    errors.Add("enemies.txt line " + lineNum + ": Invalid spawnNearEdge boolean '" + parts[6] + "'.");
                    continue;
                }
                EnemyDef e = new EnemyDef
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
                db.Enemies[id] = e;
            }
        }

        private static void ParseWaves(string path, GameDatabase db, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add("waves.txt: File missing.");
                return;
            }
            HashSet<int> seenWaves = new HashSet<int>();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                string raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }
                string[] parts = raw.Split('|');
                if (parts.Length != 3)
                {
                    errors.Add("waves.txt line " + lineNum + ": Expected 3 columns, found " + parts.Length + ".");
                    continue;
                }
                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int waveNum) || waveNum < 1 || waveNum > 50)
                {
                    errors.Add("waves.txt line " + lineNum + ": Invalid waveNumber '" + parts[0] + "'.");
                    continue;
                }
                if (seenWaves.Contains(waveNum))
                {
                    errors.Add("waves.txt line " + lineNum + ": Duplicate waveNumber " + waveNum + ".");
                    continue;
                }
                string[] enemyTokens = parts[1].Split(',');
                List<string> validEnemyIds = new List<string>();
                bool hasBadEnemy = false;
                foreach (string tok in enemyTokens)
                {
                    string eid = tok.Trim();
                    if (string.IsNullOrEmpty(eid))
                    {
                        continue;
                    }
                    if (!db.Enemies.ContainsKey(eid))
                    {
                        errors.Add("waves.txt line " + lineNum + ": References unknown enemy id '" + eid + "'.");
                        hasBadEnemy = true;
                    }
                    else
                    {
                        validEnemyIds.Add(eid);
                    }
                }
                if (validEnemyIds.Count == 0 || hasBadEnemy)
                {
                    if (validEnemyIds.Count == 0)
                    {
                        errors.Add("waves.txt line " + lineNum + ": Wave has no valid enemies.");
                    }
                    if (validEnemyIds.Count == 0)
                    {
                        continue;
                    }
                }
                string reward = parts[2].Trim();
                if (reward != "None" && reward != "PickCard" && reward != "Upgrade" && reward != "Victory")
                {
                    errors.Add("waves.txt line " + lineNum + ": Invalid rewardAfter '" + reward + "'.");
                    continue;
                }
                seenWaves.Add(waveNum);
                WaveDef wd = new WaveDef
                {
                    WaveNumber = waveNum,
                    EnemyIds = validEnemyIds,
                    RewardAfter = reward
                };
                db.Waves.Add(wd);
            }
        }

        private static void ParseUpgrades(string path, GameDatabase db, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add("upgrades.txt: File missing.");
                return;
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                string raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }
                string[] parts = raw.Split('|');
                if (parts.Length != 5)
                {
                    errors.Add("upgrades.txt line " + lineNum + ": Expected 5 columns, found " + parts.Length + ".");
                    continue;
                }
                string id = parts[0].Trim();
                string name = parts[1].Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    errors.Add("upgrades.txt line " + lineNum + ": Empty id or name.");
                    continue;
                }
                if (db.Upgrades.ContainsKey(id))
                {
                    errors.Add("upgrades.txt line " + lineNum + ": Duplicate upgrade id '" + id + "'.");
                    continue;
                }
                if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int order) || order < 1 || order > 100)
                {
                    errors.Add("upgrades.txt line " + lineNum + ": Invalid unlockOrder '" + parts[2] + "'.");
                    continue;
                }
                if (!int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cost) || cost < 0 || cost > 9999)
                {
                    errors.Add("upgrades.txt line " + lineNum + ": Invalid cost '" + parts[3] + "'.");
                    continue;
                }
                UpgradeDef u = new UpgradeDef
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
        }

        private static void ParseCharacters(string path, GameDatabase db, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add("characters.txt: File missing.");
                return;
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                string raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }
                string[] parts = raw.Split('|');
                if (parts.Length != 8)
                {
                    errors.Add("characters.txt line " + lineNum + ": Expected 8 columns, found " + parts.Length + ".");
                    continue;
                }
                string id = parts[0].Trim();
                string name = parts[1].Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    errors.Add("characters.txt line " + lineNum + ": Empty id or name.");
                    continue;
                }
                if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hp) || hp < 1 || hp > 50)
                {
                    errors.Add("characters.txt line " + lineNum + ": Invalid baseHp '" + parts[2] + "'.");
                    continue;
                }
                if (!int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cd) || cd < 1 || cd > 20)
                {
                    errors.Add("characters.txt line " + lineNum + ": Invalid specialCooldown '" + parts[4] + "'.");
                    continue;
                }
                if (!bool.TryParse(parts[5].Trim(), out bool defUnlocked))
                {
                    errors.Add("characters.txt line " + lineNum + ": Invalid defaultUnlocked '" + parts[5] + "'.");
                    continue;
                }
                CharacterDef c = new CharacterDef
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
        }

        private static void ParseAchievements(string path, GameDatabase db, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add("achievements.txt: File missing.");
                return;
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                string raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                {
                    continue;
                }
                string[] parts = raw.Split('|');
                if (parts.Length != 6)
                {
                    errors.Add("achievements.txt line " + lineNum + ": Expected 6 columns, found " + parts.Length + ".");
                    continue;
                }
                string id = parts[0].Trim();
                string title = parts[1].Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
                {
                    errors.Add("achievements.txt line " + lineNum + ": Empty id or title.");
                    continue;
                }
                if (!int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int target) || target < 1 || target > 10000)
                {
                    errors.Add("achievements.txt line " + lineNum + ": Invalid targetValue '" + parts[3] + "'.");
                    continue;
                }
                if (!int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int reward) || reward < 0 || reward > 5000)
                {
                    errors.Add("achievements.txt line " + lineNum + ": Invalid coinReward '" + parts[4] + "'.");
                    continue;
                }
                AchievementDef a = new AchievementDef
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
        }
    }

    public class CombatLogger
    {
        private readonly string _logsDir;
        private string _currentLogFile;
        public List<CombatLogEntry> RecentEntries { get; private set; }

        public CombatLogger(string logsDir)
        {
            _logsDir = logsDir;
            RecentEntries = new List<CombatLogEntry>();
            if (!Directory.Exists(_logsDir))
            {
                Directory.CreateDirectory(_logsDir);
            }
        }

        public void StartNewRunLog(string characterName)
        {
            RecentEntries.Clear();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _currentLogFile = Path.Combine(_logsDir, "combat_log_" + stamp + ".txt");
            Log("New run started as " + characterName + ".", Color.FromArgb(140, 210, 255));
        }

        public void ResumeRunLog(string characterName, int wave, int turn)
        {
            RecentEntries.Clear();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _currentLogFile = Path.Combine(_logsDir, "combat_log_" + stamp + ".txt");
            Log("Resumed saved run as " + characterName + " (Wave " + wave + ", Turn " + turn + ").", Color.FromArgb(140, 210, 255));
        }

        public void Log(string message, Color color)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            CombatLogEntry entry = new CombatLogEntry
            {
                Timestamp = ts,
                Text = message,
                Color = color
            };
            RecentEntries.Add(entry);
            while (RecentEntries.Count > 8)
            {
                RecentEntries.RemoveAt(0);
            }

            if (!string.IsNullOrEmpty(_currentLogFile))
            {
                try
                {
                    File.AppendAllText(_currentLogFile, "[" + ts + "] " + message + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Ignore non-fatal file lock during logging
                }
            }
        }
    }

    public class SaveManager
    {
        private readonly string _savesDir;
        private readonly string _logsDir;
        private readonly string _profilePath;
        private readonly string _statsPath;
        private readonly string _activeRunPath;

        public SaveManager(string savesDir, string logsDir)
        {
            _savesDir = savesDir;
            _logsDir = logsDir;
            if (!Directory.Exists(_savesDir))
            {
                Directory.CreateDirectory(_savesDir);
            }
            if (!Directory.Exists(_logsDir))
            {
                Directory.CreateDirectory(_logsDir);
            }
            _profilePath = Path.Combine(_savesDir, "profile.txt");
            _statsPath = Path.Combine(_savesDir, "stats.txt");
            _activeRunPath = Path.Combine(_savesDir, "active_run.txt");
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
                // Ignore delete error
            }
        }

        public ProfileData LoadProfile(GameDatabase db)
        {
            ProfileData profile = new ProfileData();
            foreach (WeaponDef w in db.WeaponList)
            {
                if (w.DefaultUnlocked)
                {
                    profile.UnlockedWeapons.Add(w.Id);
                }
            }
            foreach (CharacterDef c in db.CharacterList)
            {
                if (c.DefaultUnlocked)
                {
                    profile.UnlockedCharacters.Add(c.Id);
                }
            }
            foreach (AchievementDef a in db.AchievementList)
            {
                profile.AchievementProgress[a.Id] = 0;
            }

            if (!File.Exists(_profilePath))
            {
                SaveProfile(profile);
                return profile;
            }

            try
            {
                string[] lines = File.ReadAllLines(_profilePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string raw = lines[i].Trim();
                    if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                    {
                        continue;
                    }
                    int eq = raw.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = raw.Substring(0, eq).Trim();
                    string val = raw.Substring(eq + 1).Trim();
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
                        foreach (string s in val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string id = s.Trim();
                            if (db.Weapons.ContainsKey(id))
                            {
                                profile.UnlockedWeapons.Add(id);
                            }
                        }
                    }
                    else if (key == "UnlockedUpgrades")
                    {
                        foreach (string s in val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string id = s.Trim();
                            if (db.Upgrades.ContainsKey(id))
                            {
                                profile.UnlockedUpgrades.Add(id);
                            }
                        }
                    }
                    else if (key == "UnlockedCharacters")
                    {
                        foreach (string s in val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string id = s.Trim();
                            if (db.Characters.ContainsKey(id))
                            {
                                profile.UnlockedCharacters.Add(id);
                            }
                        }
                    }
                    else if (key == "CompletedAchievements")
                    {
                        foreach (string s in val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string id = s.Trim();
                            if (db.Achievements.ContainsKey(id))
                            {
                                profile.CompletedAchievements.Add(id);
                            }
                        }
                    }
                    else if (key.StartsWith("AchProg_"))
                    {
                        string aid = key.Substring("AchProg_".Length);
                        if (db.Achievements.ContainsKey(aid) && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pv))
                        {
                            profile.AchievementProgress[aid] = Math.Max(0, pv);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSaveError("profile.txt load error: " + ex.Message);
            }

            return profile;
        }

        public void SaveProfile(ProfileData profile)
        {
            StringBuilder sb = new StringBuilder();
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
            StatsData stats = new StatsData();
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
                string[] lines = File.ReadAllLines(_statsPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string raw = lines[i].Trim();
                    if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                    {
                        continue;
                    }
                    int eq = raw.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = raw.Substring(0, eq).Trim();
                    string val = raw.Substring(eq + 1).Trim();
                    if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num) || num < 0)
                    {
                        continue;
                    }
                    if (key == "TotalRunsPlayed") stats.TotalRunsPlayed = num;
                    else if (key == "RunsWon") stats.RunsWon = num;
                    else if (key == "HighestWaveReached") stats.HighestWaveReached = num;
                    else if (key == "TotalTurnsTaken") stats.TotalTurnsTaken = num;
                    else if (key == "TotalEnemiesDefeated") stats.TotalEnemiesDefeated = num;
                    else if (key == "TotalFriendlyFireKills") stats.TotalFriendlyFireKills = num;
                    else if (key == "TotalDamageDealt") stats.TotalDamageDealt = num;
                    else if (key == "TotalCoinsEarned") stats.TotalCoinsEarned = num;
                    else if (key == "TotalCardsPlayed") stats.TotalCardsPlayed = num;
                    else if (key == "BearTrapStunsTriggered") stats.BearTrapStunsTriggered = num;
                    else if (key.StartsWith("EnemyKill_"))
                    {
                        string eid = key.Substring("EnemyKill_".Length);
                        stats.EnemiesDefeatedByType[eid] = num;
                    }
                    else if (key.StartsWith("WeaponUse_"))
                    {
                        string wid = key.Substring("WeaponUse_".Length);
                        stats.WeaponUsageCounts[wid] = num;
                    }
                }
            }
            catch (Exception ex)
            {
                LogSaveError("stats.txt load error: " + ex.Message);
            }

            return stats;
        }

        public void SaveStats(StatsData stats)
        {
            StringBuilder sb = new StringBuilder();
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

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Rogue Cards Active Run Save");
            sb.AppendLine("FORMAT_VERSION=1");
            sb.AppendLine("[META]");
            sb.AppendLine("Phase=" + engine.Phase.ToString());
            sb.AppendLine("CurrentWave=" + engine.CurrentWave.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("TurnCounter=" + engine.TurnCounter.ToString(CultureInfo.InvariantCulture));

            sb.AppendLine("[PLAYER]");
            PlayerState p = engine.Player;
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

            sb.AppendLine("[HAND]");
            foreach (CardInstance c in engine.Hand)
            {
                sb.AppendLine(SerializeCard(c));
            }

            sb.AppendLine("[EQUIPPED]");
            foreach (CardInstance c in engine.EquippedQueue)
            {
                sb.AppendLine(SerializeCard(c));
            }

            sb.AppendLine("[ENEMIES]");
            foreach (EnemyInstance e in engine.Enemies)
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
                    e.IntentLabel.Replace("|", " ") + "|" +
                    locked
                );
            }

            sb.AppendLine("[HAZARDS]");
            foreach (HazardInstance h in engine.Hazards)
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

            sb.AppendLine("[REWARD]");
            sb.AppendLine("RerollCost=" + engine.Reward.RerollCost.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("OfferedWeapons=" + string.Join(",", engine.Reward.OfferedWeaponIds));
            sb.AppendLine("OfferedModifiers=" + string.Join(",", engine.Reward.OfferedModifiers.Select(m => m.ToString())));

            File.WriteAllText(_activeRunPath, sb.ToString(), Encoding.UTF8);
        }

        private string SerializeCard(CardInstance c)
        {
            return c.WeaponId + "|" +
                   c.CurrentCooldown.ToString(CultureInfo.InvariantCulture) + "|" +
                   c.BonusDamage.ToString(CultureInfo.InvariantCulture) + "|" +
                   c.CooldownModifier.ToString(CultureInfo.InvariantCulture) + "|" +
                   c.HasPoison.ToString();
        }

        public bool TryLoadActiveRun(GameEngine engine, GameDatabase db, out string errorMessage)
        {
            errorMessage = null;
            if (!File.Exists(_activeRunPath))
            {
                errorMessage = "No saved run file found.";
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(_activeRunPath, Encoding.UTF8);
                string section = "";
                bool versionChecked = false;

                GamePhase phase = GamePhase.PlayerTurn;
                int wave = 1;
                int turn = 1;
                PlayerState player = new PlayerState();
                List<CardInstance> hand = new List<CardInstance>();
                List<CardInstance> equipped = new List<CardInstance>();
                List<EnemyInstance> enemies = new List<EnemyInstance>();
                List<HazardInstance> hazards = new List<HazardInstance>();
                RewardState reward = new RewardState();

                for (int i = 0; i < lines.Length; i++)
                {
                    int lineNum = i + 1;
                    string raw = lines[i].Trim();
                    if (string.IsNullOrEmpty(raw) || raw.StartsWith("#"))
                    {
                        continue;
                    }
                    if (raw.StartsWith("[") && raw.EndsWith("]"))
                    {
                        section = raw;
                        continue;
                    }
                    if (section == "" && raw.StartsWith("FORMAT_VERSION="))
                    {
                        string v = raw.Substring("FORMAT_VERSION=".Length).Trim();
                        if (v != "1")
                        {
                            return FailLoad(lineNum, "Incompatible save format version '" + v + "'.", out errorMessage);
                        }
                        versionChecked = true;
                        continue;
                    }

                    if (section == "[META]")
                    {
                        if (!TrySplitKv(raw, out string k, out string v))
                        {
                            return FailLoad(lineNum, "Malformed [META] entry.", out errorMessage);
                        }
                        if (k == "Phase")
                        {
                            if (!Enum.TryParse(v, out phase) || phase == GamePhase.Victory || phase == GamePhase.Defeat)
                            {
                                return FailLoad(lineNum, "Invalid Phase '" + v + "'.", out errorMessage);
                            }
                            if (phase == GamePhase.AimingCard)
                            {
                                phase = GamePhase.PlayerTurn;
                            }
                        }
                        else if (k == "CurrentWave")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out wave) || wave < 0 || wave > 10)
                            {
                                return FailLoad(lineNum, "Invalid CurrentWave '" + v + "'.", out errorMessage);
                            }
                        }
                        else if (k == "TurnCounter")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out turn) || turn < 0 || turn > 99999)
                            {
                                return FailLoad(lineNum, "Invalid TurnCounter '" + v + "'.", out errorMessage);
                            }
                        }
                    }
                    else if (section == "[PLAYER]")
                    {
                        if (!TrySplitKv(raw, out string k, out string v))
                        {
                            return FailLoad(lineNum, "Malformed [PLAYER] entry.", out errorMessage);
                        }
                        if (k == "CharacterId")
                        {
                            if (!db.Characters.ContainsKey(v))
                            {
                                return FailLoad(lineNum, "Unknown CharacterId '" + v + "'.", out errorMessage);
                            }
                            player.CharacterId = v;
                        }
                        else if (k == "Pos")
                        {
                            if (!TryParsePos(v, out GridPos pos))
                            {
                                return FailLoad(lineNum, "Invalid player Pos '" + v + "'.", out errorMessage);
                            }
                            player.Pos = pos;
                        }
                        else if (k == "Hp")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hp) || hp < 1 || hp > 99)
                            {
                                return FailLoad(lineNum, "Invalid player Hp '" + v + "'.", out errorMessage);
                            }
                            player.Hp = hp;
                        }
                        else if (k == "MaxHp")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mhp) || mhp < 1 || mhp > 99)
                            {
                                return FailLoad(lineNum, "Invalid player MaxHp '" + v + "'.", out errorMessage);
                            }
                            player.MaxHp = mhp;
                        }
                        else if (k == "SpecialCooldown")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scd) || scd < 0 || scd > 20)
                            {
                                return FailLoad(lineNum, "Invalid SpecialCooldown '" + v + "'.", out errorMessage);
                            }
                            player.SpecialCooldown = scd;
                        }
                        else if (k == "ImmuneTurns")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int imm) || imm < 0 || imm > 10)
                            {
                                return FailLoad(lineNum, "Invalid ImmuneTurns '" + v + "'.", out errorMessage);
                            }
                            player.ImmuneTurns = imm;
                        }
                        else if (k == "FreeRerollsLeft")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fr) || fr < 0 || fr > 10)
                            {
                                return FailLoad(lineNum, "Invalid FreeRerollsLeft '" + v + "'.", out errorMessage);
                            }
                            player.FreeRerollsLeft = fr;
                        }
                        else if (k == "RerollsUsedThisRun")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ru) || ru < 0 || ru > 999)
                            {
                                return FailLoad(lineNum, "Invalid RerollsUsedThisRun '" + v + "'.", out errorMessage);
                            }
                            player.RerollsUsedThisRun = ru;
                        }
                        else if (k == "TookDamageThisWave")
                        {
                            if (!bool.TryParse(v, out bool td))
                            {
                                return FailLoad(lineNum, "Invalid TookDamageThisWave '" + v + "'.", out errorMessage);
                            }
                            player.TookDamageThisWave = td;
                        }
                        else if (k == "RunCoinsEarned")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rc) || rc < 0)
                            {
                                return FailLoad(lineNum, "Invalid RunCoinsEarned '" + v + "'.", out errorMessage);
                            }
                            player.RunCoinsEarned = rc;
                        }
                        else if (k == "RunEnemiesDefeated")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int re) || re < 0)
                            {
                                return FailLoad(lineNum, "Invalid RunEnemiesDefeated '" + v + "'.", out errorMessage);
                            }
                            player.RunEnemiesDefeated = re;
                        }
                    }
                    else if (section == "[HAND]" || section == "[EQUIPPED]")
                    {
                        if (!TryDeserializeCard(raw, db, out CardInstance card, out string cErr))
                        {
                            return FailLoad(lineNum, cErr, out errorMessage);
                        }
                        if (section == "[HAND]")
                        {
                            hand.Add(card);
                        }
                        else
                        {
                            equipped.Add(card);
                        }
                    }
                    else if (section == "[ENEMIES]")
                    {
                        string[] parts = raw.Split('|');
                        if (parts.Length != 13)
                        {
                            return FailLoad(lineNum, "Expected 13 columns in [ENEMIES], found " + parts.Length + ".", out errorMessage);
                        }
                        string eid = parts[0].Trim();
                        if (!db.Enemies.ContainsKey(eid))
                        {
                            return FailLoad(lineNum, "Unknown enemy id '" + eid + "'.", out errorMessage);
                        }
                        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ex) ||
                            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ey) ||
                            !new GridPos(ex, ey).IsInBounds())
                        {
                            return FailLoad(lineNum, "Enemy position out of 5x5 bounds.", out errorMessage);
                        }
                        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ehp) || ehp < 1 || ehp > 500 ||
                            !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int emhp) || emhp < 1 || emhp > 500)
                        {
                            return FailLoad(lineNum, "Invalid enemy HP values.", out errorMessage);
                        }
                        if (!Enum.TryParse(parts[5], out EnemyAIState state))
                        {
                            return FailLoad(lineNum, "Invalid enemy AI state '" + parts[5] + "'.", out errorMessage);
                        }
                        if (!int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cRem) || cRem < 0 || cRem > 10)
                        {
                            return FailLoad(lineNum, "Invalid ChargeTurnsRemaining.", out errorMessage);
                        }
                        string[] dirParts = parts[7].Split(',');
                        if (dirParts.Length != 2 ||
                            !int.TryParse(dirParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dx) ||
                            !int.TryParse(dirParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dy))
                        {
                            return FailLoad(lineNum, "Invalid enemy AimDir.", out errorMessage);
                        }
                        if (!int.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bci) || bci < 0 || bci > 5 ||
                            !int.TryParse(parts[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pt) || pt < 0 || pt > 20 ||
                            !int.TryParse(parts[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out int st) || st < 0 || st > 10)
                        {
                            return FailLoad(lineNum, "Invalid enemy status counters.", out errorMessage);
                        }
                        List<GridPos> locked = new List<GridPos>();
                        if (!string.IsNullOrEmpty(parts[12].Trim()))
                        {
                            string[] tks = parts[12].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string tk in tks)
                            {
                                if (!TryParsePos(tk, out GridPos lp))
                                {
                                    return FailLoad(lineNum, "Invalid locked tile '" + tk + "'.", out errorMessage);
                                }
                                locked.Add(lp);
                            }
                        }
                        EnemyInstance enemy = new EnemyInstance
                        {
                            EnemyId = eid,
                            Pos = new GridPos(ex, ey),
                            Hp = ehp,
                            MaxHp = emhp,
                            State = state,
                            ChargeTurnsRemaining = cRem,
                            AimDir = new GridPos(dx, dy),
                            BossCycleIndex = bci,
                            PoisonTurns = pt,
                            StunTurns = st,
                            IntentLabel = parts[11].Trim(),
                            LockedTiles = locked
                        };
                        enemies.Add(enemy);
                    }
                    else if (section == "[HAZARDS]")
                    {
                        string[] parts = raw.Split('|');
                        if (parts.Length != 7)
                        {
                            return FailLoad(lineNum, "Expected 7 columns in [HAZARDS], found " + parts.Length + ".", out errorMessage);
                        }
                        if (!Enum.TryParse(parts[0], out HazardType ht))
                        {
                            return FailLoad(lineNum, "Invalid HazardType '" + parts[0] + "'.", out errorMessage);
                        }
                        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hx) ||
                            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hy) ||
                            !new GridPos(hx, hy).IsInBounds())
                        {
                            return FailLoad(lineNum, "Hazard position out of 5x5 bounds.", out errorMessage);
                        }
                        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rem) || rem < 0 || rem > 999)
                        {
                            return FailLoad(lineNum, "Invalid hazard RemainingTurns.", out errorMessage);
                        }
                        if (!bool.TryParse(parts[4], out bool armed))
                        {
                            return FailLoad(lineNum, "Invalid hazard IsArmed.", out errorMessage);
                        }
                        if (!int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hdmg) || hdmg < 0 || hdmg > 50)
                        {
                            return FailLoad(lineNum, "Invalid hazard Damage.", out errorMessage);
                        }
                        if (!bool.TryParse(parts[6], out bool hpois))
                        {
                            return FailLoad(lineNum, "Invalid hazard AppliesPoison.", out errorMessage);
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
                    }
                    else if (section == "[REWARD]")
                    {
                        if (!TrySplitKv(raw, out string k, out string v))
                        {
                            return FailLoad(lineNum, "Malformed [REWARD] entry.", out errorMessage);
                        }
                        if (k == "RerollCost")
                        {
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rc) || rc < 1 || rc > 10000)
                            {
                                return FailLoad(lineNum, "Invalid RerollCost.", out errorMessage);
                            }
                            reward.RerollCost = rc;
                        }
                        else if (k == "OfferedWeapons" && !string.IsNullOrEmpty(v))
                        {
                            foreach (string wid in v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string id = wid.Trim();
                                if (!db.Weapons.ContainsKey(id))
                                {
                                    return FailLoad(lineNum, "Unknown offered weapon '" + id + "'.", out errorMessage);
                                }
                                reward.OfferedWeaponIds.Add(id);
                            }
                        }
                        else if (k == "OfferedModifiers" && !string.IsNullOrEmpty(v))
                        {
                            foreach (string mstr in v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (!Enum.TryParse(mstr.Trim(), out ModifierType mt))
                                {
                                    return FailLoad(lineNum, "Unknown offered modifier '" + mstr + "'.", out errorMessage);
                                }
                                reward.OfferedModifiers.Add(mt);
                            }
                        }
                    }
                }

                if (!versionChecked || string.IsNullOrEmpty(player.CharacterId) || player.Hp <= 0 || player.MaxHp <= 0)
                {
                    return FailLoad(0, "Save file missing required header or player data.", out errorMessage);
                }
                if (player.Hp > player.MaxHp)
                {
                    player.Hp = player.MaxHp;
                }

                engine.ApplyLoadedState(phase, wave, turn, player, hand, equipped, enemies, hazards, reward);
                return true;
            }
            catch (Exception ex)
            {
                return FailLoad(0, "Exception while loading save: " + ex.Message, out errorMessage);
            }
        }

        private bool TryDeserializeCard(string raw, GameDatabase db, out CardInstance card, out string err)
        {
            card = null;
            err = null;
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
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int curCd) || curCd < 0 || curCd > 30)
            {
                err = "Invalid card CurrentCooldown.";
                return false;
            }
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bDmg) || bDmg < 0 || bDmg > 30)
            {
                err = "Invalid card BonusDamage.";
                return false;
            }
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cdMod) || cdMod < -15 || cdMod > 15)
            {
                err = "Invalid card CooldownModifier.";
                return false;
            }
            if (!bool.TryParse(parts[4], out bool hasPoison))
            {
                err = "Invalid card HasPoison boolean.";
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

        private bool TrySplitKv(string raw, out string key, out string val)
        {
            key = null;
            val = null;
            int eq = raw.IndexOf('=');
            if (eq <= 0)
            {
                return false;
            }
            key = raw.Substring(0, eq).Trim();
            val = raw.Substring(eq + 1).Trim();
            return true;
        }

        private bool TryParsePos(string raw, out GridPos pos)
        {
            pos = new GridPos(0, 0);
            string[] parts = raw.Split(',');
            if (parts.Length != 2)
            {
                return false;
            }
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                return false;
            }
            pos = new GridPos(x, y);
            return pos.IsInBounds();
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
        public GameDatabase Db { get; private set; }
        public ProfileData Profile { get; private set; }
        public StatsData Stats { get; private set; }
        public SaveManager Saves { get; private set; }
        public CombatLogger Logger { get; private set; }

        public GamePhase Phase { get; private set; }
        public int CurrentWave { get; private set; }
        public int TurnCounter { get; private set; }
        public PlayerState Player { get; private set; }
        public List<CardInstance> Hand { get; private set; }
        public List<CardInstance> EquippedQueue { get; private set; }
        public List<EnemyInstance> Enemies { get; private set; }
        public List<HazardInstance> Hazards { get; private set; }
        public RewardState Reward { get; private set; }

        public int ExecutingCardIndex { get; private set; }
        public int CardsPlayedThisTurn { get; private set; }
        public GridPos CurrentAimDir { get; private set; }
        public List<GridPos> CurrentPreviewTiles { get; private set; }

        public List<FloatingText> FloatingTexts { get; private set; }
        public Queue<ToastMessage> ToastQueue { get; private set; }
        public ToastMessage ActiveToast { get; private set; }
        public string MenuNotification { get; set; }

        private readonly Random _rng;

        public GameEngine(GameDatabase db, ProfileData profile, StatsData stats, SaveManager saves, CombatLogger logger)
        {
            Db = db;
            Profile = profile;
            Stats = stats;
            Saves = saves;
            Logger = logger;
            Hand = new List<CardInstance>();
            EquippedQueue = new List<CardInstance>();
            Enemies = new List<EnemyInstance>();
            Hazards = new List<HazardInstance>();
            Reward = new RewardState();
            CurrentPreviewTiles = new List<GridPos>();
            FloatingTexts = new List<FloatingText>();
            ToastQueue = new Queue<ToastMessage>();
            _rng = new Random();
            Player = new PlayerState();
            CurrentAimDir = new GridPos(0, -1);

            if (Db.LoadWarnings.Count > 0)
            {
                MenuNotification = "Data validation logged " + Db.LoadWarnings.Count.ToString(CultureInfo.InvariantCulture) + " warning(s) to Logs/data_validation.log.";
            }
        }

        public void StartNewRun(string characterId)
        {
            if (!Db.Characters.ContainsKey(characterId))
            {
                characterId = "viking";
            }
            CharacterDef cdef = Db.Characters[characterId];
            int maxHp = cdef.BaseHp + (Profile.UnlockedUpgrades.Contains("durability_1") ? 1 : 0);

            Player = new PlayerState
            {
                CharacterId = characterId,
                Pos = new GridPos(2, 2),
                Hp = maxHp,
                MaxHp = maxHp,
                SpecialCooldown = 0,
                SpecialUsedThisTurn = false,
                ImmuneTurns = 0,
                FreeRerollsLeft = Profile.UnlockedUpgrades.Contains("charisma") ? 1 : 0,
                RerollsUsedThisRun = 0,
                TookDamageThisWave = false,
                RunCoinsEarned = 0,
                RunEnemiesDefeated = 0
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

            string cName = Db.Characters.ContainsKey(player.CharacterId) ? Db.Characters[player.CharacterId].Name : player.CharacterId;
            Logger.ResumeRunLog(cName, CurrentWave, TurnCounter);

            if (Phase == GamePhase.StartingDraft && Reward.OfferedWeaponIds.Count < 4)
            {
                PrepareStartingDraft();
            }
        }

        private void PrepareStartingDraft()
        {
            Reward = new RewardState();
            List<string> unlockedPool = Profile.UnlockedWeapons.Where(id => Db.Weapons.ContainsKey(id)).ToList();
            Shuffle(unlockedPool);
            int takeCount = Math.Min(4, unlockedPool.Count);
            for (int i = 0; i < takeCount; i++)
            {
                Reward.OfferedWeaponIds.Add(unlockedPool[i]);
            }
        }

        public void ToggleStartingDraftCard(int index)
        {
            if (Phase != GamePhase.StartingDraft)
            {
                return;
            }
            if (index < 0 || index >= Reward.OfferedWeaponIds.Count)
            {
                return;
            }
            if (Reward.StartingDraftSelectedIndices.Contains(index))
            {
                Reward.StartingDraftSelectedIndices.Remove(index);
            }
            else if (Reward.StartingDraftSelectedIndices.Count < 2)
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
                Logger.Log("Drafted starting card: " + Db.Weapons[wid].Name + ".", Color.FromArgb(140, 230, 170));
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
            else
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
            if (!Db.Enemies.ContainsKey(enemyId))
            {
                return;
            }
            EnemyDef def = Db.Enemies[enemyId];
            GridPos? freePos = FindFreeSpawnTile(def.SpawnNearEdge);
            if (!freePos.HasValue)
            {
                return;
            }

            EnemyInstance enemy = new EnemyInstance
            {
                EnemyId = enemyId,
                Pos = freePos.Value,
                Hp = def.MaxHp,
                MaxHp = def.MaxHp,
                State = EnemyAIState.Unarmed,
                ChargeTurnsRemaining = 0,
                BossCycleIndex = 0,
                IntentLabel = "Equipping " + def.WeaponName,
                SpawnedMidTurn = midTurn
            };
            Enemies.Add(enemy);
            Logger.Log(def.Name + " spawned at " + enemy.Pos.ToString() + ".", Color.FromArgb(255, 175, 110));
        }

        private GridPos? FindFreeSpawnTile(bool preferEdge)
        {
            List<GridPos> edgeFree = new List<GridPos>();
            List<GridPos> allFree = new List<GridPos>();

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    GridPos p = new GridPos(x, y);
                    if (p == Player.Pos)
                    {
                        continue;
                    }
                    if (GetEnemyAt(p) != null)
                    {
                        continue;
                    }
                    allFree.Add(p);
                    bool isEdge = (x == 0 || x == 4 || y == 0 || y == 4);
                    if (isEdge)
                    {
                        edgeFree.Add(p);
                    }
                }
            }

            if (preferEdge && edgeFree.Count > 0)
            {
                return edgeFree[_rng.Next(edgeFree.Count)];
            }
            if (allFree.Count > 0)
            {
                return allFree[_rng.Next(allFree.Count)];
            }
            return null;
        }

        public EnemyInstance GetEnemyAt(GridPos pos)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                if (Enemies[i].Pos == pos && Enemies[i].Hp > 0)
                {
                    return Enemies[i];
                }
            }
            return null;
        }

        public HazardInstance GetHazardAt(GridPos pos, HazardType type)
        {
            for (int i = 0; i < Hazards.Count; i++)
            {
                if (Hazards[i].Pos == pos && Hazards[i].Type == type)
                {
                    return Hazards[i];
                }
            }
            return null;
        }

        public bool IsTileOccupied(GridPos pos)
        {
            if (Player.Pos == pos)
            {
                return true;
            }
            return GetEnemyAt(pos) != null;
        }

        public bool TryMovePlayer(int dx, int dy)
        {
            if (Phase != GamePhase.PlayerTurn)
            {
                return false;
            }
            if (Math.Abs(dx) + Math.Abs(dy) != 1)
            {
                return false;
            }
            GridPos target = new GridPos(Player.Pos.X + dx, Player.Pos.Y + dy);
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
            for (int i = 0; i < Hazards.Count; i++)
            {
                if (Hazards[i].Type == HazardType.BearTrap && Hazards[i].Pos == leftPos && !Hazards[i].IsArmed)
                {
                    Hazards[i].IsArmed = true;
                    Logger.Log("Bear Trap at " + leftPos.ToString() + " is now ARMED!", Color.FromArgb(220, 170, 255));
                }
            }
        }

        public bool TryEquipHandCard(int handIndex)
        {
            if (Phase != GamePhase.PlayerTurn)
            {
                return false;
            }
            if (handIndex < 0 || handIndex >= Hand.Count)
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
            Logger.Log("Equipped " + Db.Weapons[card.WeaponId].Name + " (Slot " + EquippedQueue.Count.ToString(CultureInfo.InvariantCulture) + ").", Color.FromArgb(180, 225, 255));
            EndPlayerTurn();
            return true;
        }

        public void ReorderEquippedCards(int fromIndex, int toIndex)
        {
            if (Phase != GamePhase.PlayerTurn)
            {
                return;
            }
            if (fromIndex < 0 || fromIndex >= EquippedQueue.Count || toIndex < 0 || toIndex >= EquippedQueue.Count || fromIndex == toIndex)
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

            CharacterDef cdef = Db.Characters[Player.CharacterId];
            bool hasSpecMaster = Profile.UnlockedUpgrades.Contains("speciality_master");
            Player.SpecialCooldown = cdef.SpecialCooldown;
            Player.SpecialUsedThisTurn = true;

            if (Player.CharacterId == "viking")
            {
                Logger.Log("Used Viking Scream!", Color.FromArgb(120, 215, 255));
                AddFloatingText(Player.Pos, "VIKING SCREAM!", Color.DeepSkyBlue);
                List<EnemyInstance> adjacent = new List<EnemyInstance>();
                for (int i = 0; i < Enemies.Count; i++)
                {
                    EnemyInstance e = Enemies[i];
                    if (e.Hp <= 0)
                    {
                        continue;
                    }
                    int adx = Math.Abs(e.Pos.X - Player.Pos.X);
                    int ady = Math.Abs(e.Pos.Y - Player.Pos.Y);
                    if (adx <= 1 && ady <= 1 && (adx + ady > 0))
                    {
                        adjacent.Add(e);
                    }
                }
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
            else
            {
                Player.ImmuneTurns = 1;
                ReduceAllActiveCardCooldowns(1);
                Logger.Log("Used Iron Bulwark! Immune for 1 turn and reduced card cooldowns by 1.", Color.FromArgb(130, 240, 255));
                AddFloatingText(Player.Pos, "IRON BULWARK!", Color.Cyan);
                if (hasSpecMaster)
                {
                    GridPos[] orth = new[] { new GridPos(0, -1), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(1, 0) };
                    foreach (GridPos d in orth)
                    {
                        EnemyInstance e = GetEnemyAt(new GridPos(Player.Pos.X + d.X, Player.Pos.Y + d.Y));
                        if (e != null)
                        {
                            DamageEnemy(e, 1, "Iron Bulwark", false, false);
                        }
                    }
                }
            }

            if (CheckWaveCleared())
            {
                return true;
            }
            EndPlayerTurn();
            return true;
        }

        private void ReduceAllActiveCardCooldowns(int amount)
        {
            foreach (CardInstance c in Hand)
            {
                if (c.CurrentCooldown > 0)
                {
                    c.CurrentCooldown = Math.Max(0, c.CurrentCooldown - amount);
                }
            }
            foreach (CardInstance c in EquippedQueue)
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
                WeaponDef wdef = Db.Weapons[current.WeaponId];

                if (wdef.AimMode == AimMode.SelfTile)
                {
                    ExecuteCardWithDirection(current, new GridPos(0, 0));
                    ExecutingCardIndex += 1;
                    if (Enemies.All(e => e.Hp <= 0))
                    {
                        FinishQueueEarlyOnWaveClear();
                        return;
                    }
                }
                else
                {
                    Phase = GamePhase.AimingCard;
                    CurrentAimDir = wdef.AimMode == AimMode.Diagonal ? new GridPos(1, -1) : new GridPos(0, -1);
                    UpdateAimPreview(CurrentAimDir);
                    return;
                }
            }

            CompleteEquippedQueueTurn();
        }

        private void FinishQueueEarlyOnWaveClear()
        {
            for (int i = EquippedQueue.Count - 1; i >= 0; i--)
            {
                CardInstance c = EquippedQueue[i];
                Hand.Add(c);
            }
            EquippedQueue.Clear();
            ExecutingCardIndex = -1;
            CurrentPreviewTiles.Clear();
            CheckComboAchievement();
            CheckWaveCleared();
        }

        private void CompleteEquippedQueueTurn()
        {
            for (int i = 0; i < EquippedQueue.Count; i++)
            {
                Hand.Add(EquippedQueue[i]);
            }
            EquippedQueue.Clear();
            ExecutingCardIndex = -1;
            CurrentPreviewTiles.Clear();
            CheckComboAchievement();

            if (CheckWaveCleared())
            {
                return;
            }
            Phase = GamePhase.PlayerTurn;
            EndPlayerTurn();
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
            if (Phase != GamePhase.AimingCard)
            {
                return null;
            }
            if (ExecutingCardIndex < 0 || ExecutingCardIndex >= EquippedQueue.Count)
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
            WeaponDef wdef = Db.Weapons[card.WeaponId];
            int dx = hoveredTile.X - Player.Pos.X;
            int dy = hoveredTile.Y - Player.Pos.Y;
            if (dx == 0 && dy == 0)
            {
                return;
            }

            if (wdef.AimMode == AimMode.Orthogonal)
            {
                if (Math.Abs(dx) >= Math.Abs(dy))
                {
                    CurrentAimDir = new GridPos(Math.Sign(dx), 0);
                }
                else
                {
                    CurrentAimDir = new GridPos(0, Math.Sign(dy));
                }
            }
            else if (wdef.AimMode == AimMode.Diagonal)
            {
                int sx = dx >= 0 ? 1 : -1;
                int sy = dy >= 0 ? 1 : -1;
                CurrentAimDir = new GridPos(sx, sy);
            }
            UpdateAimPreview(CurrentAimDir);
        }

        public void UpdateAimPreview(GridPos dir)
        {
            CurrentAimDir = dir;
            CurrentPreviewTiles.Clear();
            CardInstance card = GetCurrentlyAimingCard();
            if (card == null)
            {
                return;
            }
            CurrentPreviewTiles = ComputeAffectedTilesForCard(card.WeaponId, Player.Pos, dir);
        }

        public List<GridPos> ComputeAffectedTilesForCard(string weaponId, GridPos origin, GridPos dir)
        {
            List<GridPos> tiles = new List<GridPos>();
            if (weaponId == "viking_axe" || weaponId == "wooden_club")
            {
                GridPos p = new GridPos(origin.X + dir.X, origin.Y + dir.Y);
                if (p.IsInBounds())
                {
                    tiles.Add(p);
                }
            }
            else if (weaponId == "two_handed_hammer")
            {
                GridPos[] arc = new[]
                {
                    new GridPos(origin.X + dir.X, origin.Y + dir.Y),
                    new GridPos(origin.X + dir.X - dir.Y, origin.Y + dir.Y + dir.X),
                    new GridPos(origin.X + dir.X + dir.Y, origin.Y + dir.Y - dir.X)
                };
                foreach (GridPos p in arc)
                {
                    if (p.IsInBounds())
                    {
                        tiles.Add(p);
                    }
                }
            }
            else if (weaponId == "throwing_axe" || weaponId == "hunting_bow")
            {
                GridPos cur = new GridPos(origin.X + dir.X, origin.Y + dir.Y);
                while (cur.IsInBounds())
                {
                    tiles.Add(cur);
                    if (GetEnemyAt(cur) != null)
                    {
                        break;
                    }
                    cur = new GridPos(cur.X + dir.X, cur.Y + dir.Y);
                }
            }
            else if (weaponId == "heavy_spear")
            {
                int hits = 0;
                GridPos cur = new GridPos(origin.X + dir.X, origin.Y + dir.Y);
                while (cur.IsInBounds())
                {
                    tiles.Add(cur);
                    if (GetEnemyAt(cur) != null)
                    {
                        hits += 1;
                        if (hits >= 2)
                        {
                            break;
                        }
                    }
                    cur = new GridPos(cur.X + dir.X, cur.Y + dir.Y);
                }
            }
            else if (weaponId == "boat_hook")
            {
                for (int step = 1; step <= 3; step++)
                {
                    GridPos cur = new GridPos(origin.X + dir.X * step, origin.Y + dir.Y * step);
                    if (!cur.IsInBounds())
                    {
                        break;
                    }
                    tiles.Add(cur);
                    if (GetEnemyAt(cur) != null)
                    {
                        break;
                    }
                }
            }
            else if (weaponId == "diagonal_dash")
            {
                GridPos p = new GridPos(origin.X + dir.X, origin.Y + dir.Y);
                if (p.IsInBounds() && GetEnemyAt(p) == null)
                {
                    tiles.Add(p);
                }
            }
            else if (weaponId == "slide")
            {
                GridPos best = origin;
                for (int step = 1; step <= 2; step++)
                {
                    GridPos cur = new GridPos(origin.X + dir.X * step, origin.Y + dir.Y * step);
                    if (!cur.IsInBounds() || GetEnemyAt(cur) != null)
                    {
                        break;
                    }
                    best = cur;
                    tiles.Add(cur);
                }
                if (best == origin)
                {
                    tiles.Clear();
                }
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
            Logger.Log("Skipped " + Db.Weapons[card.WeaponId].Name + ".", Color.Gray);
            ExecutingCardIndex += 1;
            AdvanceQueueExecution();
        }

        private void ExecuteCardWithDirection(CardInstance card, GridPos dir)
        {
            WeaponDef wdef = Db.Weapons[card.WeaponId];
            int dmg = card.GetEffectiveDamage(Db, Profile);
            int maxCd = card.GetEffectiveMaxCooldown(Db, Profile);

            card.CurrentCooldown = maxCd;
            card.UsedThisTurn = true;
            CardsPlayedThisTurn += 1;
            Stats.TotalCardsPlayed += 1;
            if (!Stats.WeaponUsageCounts.ContainsKey(card.WeaponId))
            {
                Stats.WeaponUsageCounts[card.WeaponId] = 0;
            }
            Stats.WeaponUsageCounts[card.WeaponId] += 1;
            Saves.SaveStats(Stats);

            if (card.WeaponId == "bear_trap")
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
                return;
            }

            if (card.WeaponId == "diagonal_dash")
            {
                GridPos dest = new GridPos(Player.Pos.X + dir.X, Player.Pos.Y + dir.Y);
                if (dest.IsInBounds() && GetEnemyAt(dest) == null)
                {
                    GridPos old = Player.Pos;
                    Player.Pos = dest;
                    ArmBearTrapIfLeft(old);
                    Logger.Log("Diagonal Dash to " + dest.ToString() + ".", Color.FromArgb(115, 220, 255));
                    AddFloatingText(dest, "DASH!", Color.Cyan);
                }
                else
                {
                    Logger.Log("Diagonal Dash blocked!", Color.Gray);
                    AddFloatingText(Player.Pos, "BLOCKED", Color.Gray);
                }
                return;
            }

            if (card.WeaponId == "slide")
            {
                GridPos old = Player.Pos;
                GridPos dest = old;
                for (int step = 1; step <= 2; step++)
                {
                    GridPos next = new GridPos(old.X + dir.X * step, old.Y + dir.Y * step);
                    if (!next.IsInBounds() || GetEnemyAt(next) != null)
                    {
                        break;
                    }
                    dest = next;
                }
                if (dest != old)
                {
                    Player.Pos = dest;
                    ArmBearTrapIfLeft(old);
                    Logger.Log("Slid to " + dest.ToString() + ".", Color.FromArgb(115, 220, 255));
                    AddFloatingText(dest, "SLIDE!", Color.Cyan);
                }
                else
                {
                    Logger.Log("Slide blocked!", Color.Gray);
                    AddFloatingText(Player.Pos, "BLOCKED", Color.Gray);
                }
                return;
            }

            List<GridPos> affected = ComputeAffectedTilesForCard(card.WeaponId, Player.Pos, dir);
            bool hitAny = false;

            if (card.WeaponId == "viking_axe")
            {
                foreach (GridPos t in affected)
                {
                    EnemyInstance e = GetEnemyAt(t);
                    if (e != null)
                    {
                        hitAny = true;
                        DamageEnemy(e, dmg, wdef.Name, card.HasPoison, false);
                    }
                }
            }
            else if (card.WeaponId == "wooden_club")
            {
                foreach (GridPos t in affected)
                {
                    EnemyInstance e = GetEnemyAt(t);
                    if (e != null)
                    {
                        hitAny = true;
                        DamageEnemy(e, dmg, wdef.Name, card.HasPoison, false);
                        if (e.Hp > 0)
                        {
                            TryPushEnemy(e, dir.X, dir.Y);
                        }
                    }
                }
            }
            else if (card.WeaponId == "two_handed_hammer")
            {
                foreach (GridPos t in affected)
                {
                    EnemyInstance e = GetEnemyAt(t);
                    if (e != null)
                    {
                        hitAny = true;
                        DamageEnemy(e, dmg, wdef.Name, card.HasPoison, false);
                    }
                }
            }
            else if (card.WeaponId == "throwing_axe" || card.WeaponId == "hunting_bow")
            {
                foreach (GridPos t in affected)
                {
                    EnemyInstance e = GetEnemyAt(t);
                    if (e != null)
                    {
                        hitAny = true;
                        DamageEnemy(e, dmg, wdef.Name, card.HasPoison, false);
                        break;
                    }
                }
            }
            else if (card.WeaponId == "heavy_spear")
            {
                int hits = 0;
                foreach (GridPos t in affected)
                {
                    EnemyInstance e = GetEnemyAt(t);
                    if (e != null)
                    {
                        hitAny = true;
                        DamageEnemy(e, dmg, wdef.Name, card.HasPoison, false);
                        hits += 1;
                        if (hits >= 2)
                        {
                            break;
                        }
                    }
                }
            }
            else if (card.WeaponId == "boat_hook")
            {
                foreach (GridPos t in affected)
                {
                    EnemyInstance e = GetEnemyAt(t);
                    if (e != null)
                    {
                        hitAny = true;
                        DamageEnemy(e, dmg, wdef.Name, card.HasPoison, false);
                        if (e.Hp > 0)
                        {
                            PullEnemyToward(e, Player.Pos, dir);
                        }
                        break;
                    }
                }
            }

            if (!hitAny)
            {
                Logger.Log("Played " + wdef.Name + " (missed).", Color.FromArgb(180, 190, 205));
            }
        }

        private void PullEnemyToward(EnemyInstance e, GridPos pullerPos, GridPos dirFromPuller)
        {
            GridPos desired = new GridPos(pullerPos.X + dirFromPuller.X, pullerPos.Y + dirFromPuller.Y);
            if (desired == e.Pos)
            {
                return;
            }
            GridPos best = e.Pos;
            int stepX = -dirFromPuller.X;
            int stepY = -dirFromPuller.Y;
            GridPos cur = e.Pos;
            for (int i = 0; i < 4; i++)
            {
                GridPos next = new GridPos(cur.X + stepX, cur.Y + stepY);
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
            if (best != e.Pos)
            {
                int moveDx = best.X - e.Pos.X;
                int moveDy = best.Y - e.Pos.Y;
                e.Pos = best;
                ShiftLockedTiles(e, moveDx, moveDy);
                Logger.Log("Pulled " + Db.Enemies[e.EnemyId].Name + " to " + best.ToString() + ".", Color.FromArgb(200, 160, 255));
                CheckEnemySteppedOnTrap(e);
            }
        }

        private bool TryPushEnemy(EnemyInstance e, int dx, int dy)
        {
            GridPos dest = new GridPos(e.Pos.X + dx, e.Pos.Y + dy);
            if (!dest.IsInBounds() || IsTileOccupied(dest))
            {
                return false;
            }
            e.Pos = dest;
            ShiftLockedTiles(e, dx, dy);
            Logger.Log("Knocked " + Db.Enemies[e.EnemyId].Name + " back to " + dest.ToString() + ".", Color.FromArgb(255, 205, 130));
            AddFloatingText(dest, "PUSHED!", Color.Gold);
            CheckEnemySteppedOnTrap(e);
            return true;
        }

        private void ShiftLockedTiles(EnemyInstance e, int dx, int dy)
        {
            if (e.LockedTiles.Count == 0)
            {
                return;
            }
            List<GridPos> shifted = new List<GridPos>();
            foreach (GridPos t in e.LockedTiles)
            {
                GridPos np = new GridPos(t.X + dx, t.Y + dy);
                if (np.IsInBounds())
                {
                    shifted.Add(np);
                }
            }
            e.LockedTiles = shifted;
        }

        private void CheckEnemySteppedOnTrap(EnemyInstance e)
        {
            if (e.Hp <= 0)
            {
                return;
            }
            HazardInstance trap = GetHazardAt(e.Pos, HazardType.BearTrap);
            if (trap != null && trap.IsArmed)
            {
                Hazards.Remove(trap);
                e.StunTurns = Math.Max(e.StunTurns, 1);
                e.State = EnemyAIState.Armed;
                e.LockedTiles.Clear();
                e.ChargeTurnsRemaining = 0;
                e.IntentLabel = "STUNNED!";
                Stats.BearTrapStunsTriggered += 1;
                Saves.SaveStats(Stats);
                UpdateAchievementMax("trap_master", Stats.BearTrapStunsTriggered);

                Logger.Log(Db.Enemies[e.EnemyId].Name + " triggered Bear Trap at " + e.Pos.ToString() + "! Took " + trap.Damage.ToString(CultureInfo.InvariantCulture) + " dmg & STUNNED!", Color.FromArgb(230, 140, 255));
                AddFloatingText(e.Pos, "TRAP STUN!", Color.Magenta);
                DamageEnemy(e, trap.Damage, "Bear Trap", trap.AppliesPoison, false);
            }
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
            string ename = Db.Enemies[target.EnemyId].Name;
            if (applyPoison && target.Hp > 0)
            {
                target.PoisonTurns = 3;
                AddFloatingText(target.Pos, "POISONED", Color.LimeGreen);
            }

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
            EnemyDef def = Db.Enemies[enemy.EnemyId];
            Enemies.Remove(enemy);
            Profile.Coins += def.CoinDrop;
            Player.RunCoinsEarned += def.CoinDrop;
            Player.RunEnemiesDefeated += 1;

            Stats.TotalEnemiesDefeated += 1;
            Stats.TotalCoinsEarned += def.CoinDrop;
            if (!Stats.EnemiesDefeatedByType.ContainsKey(enemy.EnemyId))
            {
                Stats.EnemiesDefeatedByType[enemy.EnemyId] = 0;
            }
            Stats.EnemiesDefeatedByType[enemy.EnemyId] += 1;

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

            if (Phase == GamePhase.Defeat)
            {
                return;
            }

            if (CheckWaveCleared())
            {
                return;
            }

            // Prepare start of next player turn
            TurnCounter += 1;
            if (Player.ImmuneTurns > 0)
            {
                Player.ImmuneTurns -= 1;
            }

            // Tick cooldowns for cards & special not used on the turn that just ended
            if (Player.SpecialCooldown > 0 && !Player.SpecialUsedThisTurn)
            {
                Player.SpecialCooldown -= 1;
            }
            Player.SpecialUsedThisTurn = false;

            foreach (CardInstance c in Hand)
            {
                if (c.CurrentCooldown > 0 && !c.UsedThisTurn)
                {
                    c.CurrentCooldown -= 1;
                }
                c.UsedThisTurn = false;
            }
            foreach (CardInstance c in EquippedQueue)
            {
                if (c.CurrentCooldown > 0 && !c.UsedThisTurn)
                {
                    c.CurrentCooldown -= 1;
                }
                c.UsedThisTurn = false;
            }

            Phase = GamePhase.PlayerTurn;
            Saves.SaveActiveRun(this);
        }

        private void ExecuteEnemiesTurn()
        {
            List<EnemyInstance> snapshot = Enemies.ToList();
            foreach (EnemyInstance e in snapshot)
            {
                if (Phase == GamePhase.Defeat)
                {
                    return;
                }
                if (e.Hp <= 0 || !Enemies.Contains(e))
                {
                    continue;
                }
                if (e.SpawnedMidTurn)
                {
                    e.SpawnedMidTurn = false;
                    continue;
                }
                if (e.StunTurns > 0)
                {
                    e.StunTurns -= 1;
                    e.IntentLabel = "Recovering";
                    Logger.Log(Db.Enemies[e.EnemyId].Name + " is STUNNED and loses its turn.", Color.FromArgb(220, 150, 255));
                    continue;
                }
                if (e.State == EnemyAIState.Unarmed)
                {
                    e.State = EnemyAIState.Armed;
                    e.IntentLabel = "Armed (" + Db.Enemies[e.EnemyId].WeaponName + ")";
                    Logger.Log(Db.Enemies[e.EnemyId].Name + " equipped " + Db.Enemies[e.EnemyId].WeaponName + ".", Color.FromArgb(240, 185, 120));
                    continue;
                }

                if (e.EnemyId == "viking_chief")
                {
                    ExecuteBossAI(e);
                }
                else if (e.EnemyId == "pawn")
                {
                    ExecutePawnAI(e);
                }
                else if (e.EnemyId == "archer")
                {
                    ExecuteArcherAI(e);
                }
                else if (e.EnemyId == "spearman")
                {
                    ExecuteSpearmanAI(e);
                }
                else if (e.EnemyId == "shaman")
                {
                    ExecuteShamanAI(e);
                }
            }
        }

        private void ExecutePawnAI(EnemyInstance e)
        {
            EnemyDef def = Db.Enemies[e.EnemyId];
            if (e.State == EnemyAIState.Charging)
            {
                StrikeLockedTiles(e, def.Damage, def.Name + "'s Axe", false);
                e.State = EnemyAIState.Armed;
                e.LockedTiles.Clear();
                e.IntentLabel = "Armed (Axe)";
                return;
            }

            if (e.Pos.ManhattanDistance(Player.Pos) == 1)
            {
                e.State = EnemyAIState.Charging;
                e.ChargeTurnsRemaining = 1;
                e.AimDir = new GridPos(Player.Pos.X - e.Pos.X, Player.Pos.Y - e.Pos.Y);
                e.LockedTiles = new List<GridPos> { Player.Pos };
                e.IntentLabel = "CHARGING AXE!";
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
            EnemyDef def = Db.Enemies[e.EnemyId];
            if (e.State == EnemyAIState.Charging)
            {
                StrikeLockedTiles(e, def.Damage, def.Name + "'s Spear", false);
                e.State = EnemyAIState.Armed;
                e.LockedTiles.Clear();
                e.IntentLabel = "Armed (Spear)";
                return;
            }

            bool aligned = (e.Pos.X == Player.Pos.X || e.Pos.Y == Player.Pos.Y);
            int dist = e.Pos.ManhattanDistance(Player.Pos);
            if (aligned && dist <= 2)
            {
                GridPos dir = new GridPos(Math.Sign(Player.Pos.X - e.Pos.X), Math.Sign(Player.Pos.Y - e.Pos.Y));
                e.State = EnemyAIState.Charging;
                e.ChargeTurnsRemaining = 1;
                e.AimDir = dir;
                e.LockedTiles = new List<GridPos>();
                for (int s = 1; s <= 2; s++)
                {
                    GridPos t = new GridPos(e.Pos.X + dir.X * s, e.Pos.Y + dir.Y * s);
                    if (t.IsInBounds())
                    {
                        e.LockedTiles.Add(t);
                    }
                }
                e.IntentLabel = "CHARGING SPEAR!";
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
            EnemyDef def = Db.Enemies[e.EnemyId];
            if (e.State == EnemyAIState.Charging)
            {
                FireProjectileLockedTiles(e, def.Damage, def.Name + "'s Arrow");
                e.State = EnemyAIState.Armed;
                e.LockedTiles.Clear();
                e.IntentLabel = "Armed (Bow)";
                return;
            }

            int dist = e.Pos.ManhattanDistance(Player.Pos);
            bool aligned = (e.Pos.X == Player.Pos.X || e.Pos.Y == Player.Pos.Y);

            if (dist <= 2)
            {
                if (TryMoveEnemyAway(e, Player.Pos))
                {
                    e.IntentLabel = "Retreating";
                    return;
                }
                // Blocked: charge and fire
                ChargeArcherLine(e);
                return;
            }

            if (aligned)
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
            int dx = Player.Pos.X - e.Pos.X;
            int dy = Player.Pos.Y - e.Pos.Y;
            GridPos dir;
            if (e.Pos.X == Player.Pos.X)
            {
                dir = new GridPos(0, Math.Sign(dy));
            }
            else if (e.Pos.Y == Player.Pos.Y)
            {
                dir = new GridPos(Math.Sign(dx), 0);
            }
            else if (Math.Abs(dx) >= Math.Abs(dy))
            {
                dir = new GridPos(Math.Sign(dx), 0);
            }
            else
            {
                dir = new GridPos(0, Math.Sign(dy));
            }

            e.State = EnemyAIState.Charging;
            e.ChargeTurnsRemaining = 1;
            e.AimDir = dir;
            e.LockedTiles = BuildLineTiles(e.Pos, dir, 5);
            e.IntentLabel = "AIMING BOW!";
            Logger.Log("Archer at " + e.Pos.ToString() + " locks arrow line!", Color.FromArgb(255, 145, 95));
        }

        private void ExecuteShamanAI(EnemyInstance e)
        {
            EnemyDef def = Db.Enemies[e.EnemyId];
            if (e.State == EnemyAIState.Charging)
            {
                if (e.LockedTiles.Count > 0)
                {
                    GridPos center = e.LockedTiles[0];
                    if (Player.Pos == center)
                    {
                        DamagePlayer(def.Damage, def.Name + "'s Poison Vial");
                    }
                    else
                    {
                        EnemyInstance hitAlly = GetEnemyAt(center);
                        if (hitAlly != null && hitAlly != e)
                        {
                            DamageEnemy(hitAlly, def.Damage, def.Name + "'s Poison Vial", false, true);
                        }
                    }
                    foreach (GridPos tile in e.LockedTiles)
                    {
                        PlacePoisonHazard(tile, 3);
                    }
                    Logger.Log("Shaman threw Poison Potion at " + center.ToString() + " (X-pattern toxic zone)!", Color.LimeGreen);
                }
                e.State = EnemyAIState.Armed;
                e.LockedTiles.Clear();
                e.IntentLabel = "Armed (Potion)";
                return;
            }

            int dist = e.Pos.ManhattanDistance(Player.Pos);
            if (dist > 3)
            {
                MoveEnemyStepToward(e, Player.Pos);
                e.IntentLabel = "Advancing";
            }
            else if (dist == 3)
            {
                ChargeShamanPotion(e);
            }
            else
            {
                if (TryMoveEnemyAway(e, Player.Pos))
                {
                    e.IntentLabel = "Retreating";
                }
                else
                {
                    ChargeShamanPotion(e);
                }
            }
        }

        private void ChargeShamanPotion(EnemyInstance e)
        {
            e.State = EnemyAIState.Charging;
            e.ChargeTurnsRemaining = 1;
            e.AimDir = new GridPos(Math.Sign(Player.Pos.X - e.Pos.X), Math.Sign(Player.Pos.Y - e.Pos.Y));
            e.LockedTiles = new List<GridPos>();
            GridPos c = Player.Pos;
            e.LockedTiles.Add(c);
            GridPos[] diags = new[]
            {
                new GridPos(c.X - 1, c.Y - 1),
                new GridPos(c.X + 1, c.Y - 1),
                new GridPos(c.X - 1, c.Y + 1),
                new GridPos(c.X + 1, c.Y + 1)
            };
            foreach (GridPos d in diags)
            {
                if (d.IsInBounds())
                {
                    e.LockedTiles.Add(d);
                }
            }
            e.IntentLabel = "CHARGING POISON!";
            Logger.Log("Shaman at " + e.Pos.ToString() + " prepares Poison Vial at " + c.ToString() + "!", Color.LimeGreen);
        }

        private void ExecuteBossAI(EnemyInstance boss)
        {
            // Cycle 0: Throwing Axe (3 dmg)
            // Cycle 1: Boat Hook + Wooden Club (2-turn charge + spawns 1 random non-boss enemy)
            // Cycle 2: Evolved Viking Axe (3 dmg, 6-tile pattern)
            if (boss.State == EnemyAIState.Charging)
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
                    FireProjectileLockedTiles(boss, 3, "Viking Chief's Throwing Axe");
                    boss.BossCycleIndex = 1;
                }
                else if (boss.BossCycleIndex == 1)
                {
                    ExecuteBossHookAndClub(boss);
                    boss.BossCycleIndex = 2;
                }
                else
                {
                    StrikeLockedTiles(boss, 3, "Evolved Viking Axe", false);
                    boss.BossCycleIndex = 0;
                }

                boss.State = EnemyAIState.Armed;
                boss.LockedTiles.Clear();
                boss.IntentLabel = "Boss Cycle " + (boss.BossCycleIndex + 1).ToString(CultureInfo.InvariantCulture);
                return;
            }

            // Not charging: start current cycle step
            if (boss.BossCycleIndex == 0)
            {
                bool aligned = (boss.Pos.X == Player.Pos.X || boss.Pos.Y == Player.Pos.Y);
                if (!aligned)
                {
                    MoveEnemyToAlign(boss, Player.Pos);
                    if (boss.Pos.X != Player.Pos.X && boss.Pos.Y != Player.Pos.Y)
                    {
                        boss.IntentLabel = "Aligning Axe";
                        return;
                    }
                }
                GridPos dir = GetPrimaryOrthogonalDir(boss.Pos, Player.Pos);
                boss.State = EnemyAIState.Charging;
                boss.ChargeTurnsRemaining = 1;
                boss.AimDir = dir;
                boss.LockedTiles = BuildLineTiles(boss.Pos, dir, 5);
                boss.IntentLabel = "BOSS: THROWING AXE!";
                Logger.Log("Viking Chief prepares Throwing Axe!", Color.FromArgb(255, 110, 75));
            }
            else if (boss.BossCycleIndex == 1)
            {
                GridPos dir = GetPrimaryOrthogonalDir(boss.Pos, Player.Pos);
                boss.State = EnemyAIState.Charging;
                boss.ChargeTurnsRemaining = 2;
                boss.AimDir = dir;
                boss.LockedTiles = BuildLineTiles(boss.Pos, dir, 3);
                boss.IntentLabel = "HOOK+CLUB (2 TURNS)";
                Logger.Log("Viking Chief equips Boat Hook & Wooden Club (2-turn charge) and summons backup!", Color.FromArgb(255, 120, 75));

                string[] minions = new[] { "pawn", "archer", "spearman", "shaman" };
                string chosen = minions[_rng.Next(minions.Length)];
                SpawnEnemy(chosen, true);
            }
            else
            {
                GridPos dir = GetPrimaryOrthogonalDir(boss.Pos, Player.Pos);
                List<GridPos> pattern = BuildEvolvedAxeTiles(boss.Pos, dir);
                if (!pattern.Contains(Player.Pos) && boss.Pos.ManhattanDistance(Player.Pos) > 2)
                {
                    MoveEnemyStepToward(boss, Player.Pos);
                    boss.IntentLabel = "Closing In";
                    return;
                }
                boss.State = EnemyAIState.Charging;
                boss.ChargeTurnsRemaining = 1;
                boss.AimDir = dir;
                boss.LockedTiles = pattern;
                boss.IntentLabel = "EVOLVED VIKING AXE!";
                Logger.Log("Viking Chief raises Evolved Viking Axe!", Color.FromArgb(255, 95, 95));
            }
        }

        private void ExecuteBossHookAndClub(EnemyInstance boss)
        {
            GridPos dir = boss.AimDir;
            GridPos adj = new GridPos(boss.Pos.X + dir.X, boss.Pos.Y + dir.Y);
            // Boat Hook pull first entity on locked tiles
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
                    break;
                }
                EnemyInstance ally = GetEnemyAt(t);
                if (ally != null && ally != boss)
                {
                    DamageEnemy(ally, 1, "Viking Chief's Boat Hook", false, true);
                    if (ally.Hp > 0)
                    {
                        PullEnemyToward(ally, boss.Pos, dir);
                    }
                    break;
                }
            }

            if (Phase == GamePhase.Defeat)
            {
                return;
            }

            // Wooden Club strike adjacent tile
            if (adj.IsInBounds())
            {
                if (Player.Pos == adj)
                {
                    DamagePlayer(2, "Viking Chief's Wooden Club");
                    if (Player.Hp > 0)
                    {
                        GridPos kb = new GridPos(Player.Pos.X + dir.X, Player.Pos.Y + dir.Y);
                        if (kb.IsInBounds() && GetEnemyAt(kb) == null)
                        {
                            GridPos old = Player.Pos;
                            Player.Pos = kb;
                            ArmBearTrapIfLeft(old);
                            Logger.Log("Wooden Club knocked you back to " + kb.ToString() + "!", Color.Orange);
                        }
                    }
                }
                else
                {
                    EnemyInstance ally = GetEnemyAt(adj);
                    if (ally != null && ally != boss)
                    {
                        DamageEnemy(ally, 2, "Viking Chief's Wooden Club", false, true);
                        if (ally.Hp > 0)
                        {
                            TryPushEnemy(ally, dir.X, dir.Y);
                        }
                    }
                }
            }
        }

        private List<GridPos> BuildEvolvedAxeTiles(GridPos origin, GridPos dir)
        {
            GridPos[] raw = new[]
            {
                new GridPos(origin.X + dir.X, origin.Y + dir.Y),
                new GridPos(origin.X + dir.X * 2, origin.Y + dir.Y * 2),
                new GridPos(origin.X + dir.X - dir.Y, origin.Y + dir.Y + dir.X),
                new GridPos(origin.X + dir.X + dir.Y, origin.Y + dir.Y - dir.X),
                new GridPos(origin.X - dir.Y, origin.Y + dir.X),
                new GridPos(origin.X + dir.Y, origin.Y - dir.X)
            };
            List<GridPos> result = new List<GridPos>();
            foreach (GridPos p in raw)
            {
                if (p.IsInBounds() && !result.Contains(p))
                {
                    result.Add(p);
                }
            }
            return result;
        }

        private GridPos GetPrimaryOrthogonalDir(GridPos from, GridPos to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return new GridPos(dx >= 0 ? 1 : -1, 0);
            }
            return new GridPos(0, dy >= 0 ? 1 : -1);
        }

        private List<GridPos> BuildLineTiles(GridPos origin, GridPos dir, int maxLen)
        {
            List<GridPos> tiles = new List<GridPos>();
            for (int s = 1; s <= maxLen; s++)
            {
                GridPos p = new GridPos(origin.X + dir.X * s, origin.Y + dir.Y * s);
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
                if (Player.Pos == t)
                {
                    DamagePlayer(dmg, attackName);
                    if (firstOnly)
                    {
                        return;
                    }
                }
                else
                {
                    EnemyInstance other = GetEnemyAt(t);
                    if (other != null && other != attacker)
                    {
                        DamageEnemy(other, dmg, attackName, false, true);
                        if (firstOnly)
                        {
                            return;
                        }
                    }
                }
            }
        }

        private void FireProjectileLockedTiles(EnemyInstance attacker, int dmg, string attackName)
        {
            StrikeLockedTiles(attacker, dmg, attackName, true);
        }

        private void MoveEnemyStepToward(EnemyInstance e, GridPos target)
        {
            GridPos[] dirs = new[] { new GridPos(0, -1), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(1, 0) };
            GridPos best = e.Pos;
            int bestDist = e.Pos.ManhattanDistance(target);

            foreach (GridPos d in dirs)
            {
                GridPos cand = new GridPos(e.Pos.X + d.X, e.Pos.Y + d.Y);
                if (!cand.IsInBounds() || IsTileOccupied(cand))
                {
                    continue;
                }
                int dist = cand.ManhattanDistance(target);
                if (dist < bestDist)
                {
                    bestDist = dist;
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
            List<GridPos> candidates = new List<GridPos>();
            if (Math.Abs(dx) <= Math.Abs(dy))
            {
                candidates.Add(new GridPos(e.Pos.X + Math.Sign(dx), e.Pos.Y));
                candidates.Add(new GridPos(e.Pos.X, e.Pos.Y + Math.Sign(dy)));
            }
            else
            {
                candidates.Add(new GridPos(e.Pos.X, e.Pos.Y + Math.Sign(dy)));
                candidates.Add(new GridPos(e.Pos.X + Math.Sign(dx), e.Pos.Y));
            }
            foreach (GridPos c in candidates)
            {
                if (c.IsInBounds() && !IsTileOccupied(c))
                {
                    e.Pos = c;
                    CheckEnemySteppedOnTrap(e);
                    return;
                }
            }
            MoveEnemyStepToward(e, target);
        }

        private bool TryMoveEnemyAway(EnemyInstance e, GridPos from)
        {
            GridPos[] dirs = new[] { new GridPos(0, -1), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(1, 0) };
            GridPos best = e.Pos;
            int bestDist = e.Pos.ManhattanDistance(from);

            foreach (GridPos d in dirs)
            {
                GridPos cand = new GridPos(e.Pos.X + d.X, e.Pos.Y + d.Y);
                if (!cand.IsInBounds() || IsTileOccupied(cand))
                {
                    continue;
                }
                int dist = cand.ManhattanDistance(from);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = cand;
                }
            }

            if (best != e.Pos)
            {
                e.Pos = best;
                CheckEnemySteppedOnTrap(e);
                return true;
            }
            return false;
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
                Damage = 1,
                AppliesPoison = false
            });
        }

        private void ProcessPoisonAndHazards()
        {
            // Enemy poison status ticks (from player's Poison card modifier)
            List<EnemyInstance> snap = Enemies.ToList();
            foreach (EnemyInstance e in snap)
            {
                if (e.Hp <= 0 || !Enemies.Contains(e))
                {
                    continue;
                }
                if (e.PoisonTurns > 0)
                {
                    e.PoisonTurns -= 1;
                    DamageEnemy(e, 1, "Poison", false, false);
                }
            }

            // Ground Poison hazard damage
            HazardInstance playerPoison = GetHazardAt(Player.Pos, HazardType.Poison);
            if (playerPoison != null)
            {
                DamagePlayer(1, "Poison Cloud");
            }

            if (Phase == GamePhase.Defeat)
            {
                return;
            }

            snap = Enemies.ToList();
            foreach (EnemyInstance e in snap)
            {
                if (e.Hp <= 0 || !Enemies.Contains(e))
                {
                    continue;
                }
                HazardInstance ePoison = GetHazardAt(e.Pos, HazardType.Poison);
                if (ePoison != null)
                {
                    DamageEnemy(e, 1, "Poison Cloud", false, true);
                }
            }

            // Decrement poison hazard durations
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
            if (Phase == GamePhase.Victory || Phase == GamePhase.Defeat || Phase == GamePhase.RewardPickCard || Phase == GamePhase.RewardUpgrade)
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
            foreach (CardInstance c in Hand)
            {
                c.CurrentCooldown = 0;
                c.UsedThisTurn = false;
            }
            foreach (CardInstance c in EquippedQueue)
            {
                c.CurrentCooldown = 0;
                c.UsedThisTurn = false;
            }
            Logger.Log("Reward triggered! All card & Special cooldowns reset to 0.", Color.FromArgb(120, 240, 255));
        }

        private void GeneratePickCardReward(bool isReroll)
        {
            int nextCost = isReroll ? Reward.RerollCost : 5;
            Reward = new RewardState
            {
                RerollCost = nextCost
            };

            HashSet<string> owned = new HashSet<string>(Hand.Select(c => c.WeaponId).Concat(EquippedQueue.Select(c => c.WeaponId)));

            List<WeaponDef> offensePool = Profile.UnlockedWeapons
                .Where(id => Db.Weapons.ContainsKey(id))
                .Select(id => Db.Weapons[id])
                .Where(w => w.Category == CardCategory.Melee || w.Category == CardCategory.Ranged)
                .ToList();

            List<WeaponDef> utilityPool = Profile.UnlockedWeapons
                .Where(id => Db.Weapons.ContainsKey(id))
                .Select(id => Db.Weapons[id])
                .Where(w => w.Category == CardCategory.Movement || w.Category == CardCategory.Special)
                .ToList();

            List<WeaponDef> offFresh = offensePool.Where(w => !owned.Contains(w.Id)).ToList();
            List<WeaponDef> utilFresh = utilityPool.Where(w => !owned.Contains(w.Id)).ToList();

            WeaponDef pick1 = offFresh.Count > 0
                ? offFresh[_rng.Next(offFresh.Count)]
                : offensePool[_rng.Next(offensePool.Count)];
            WeaponDef pick2 = utilFresh.Count > 0
                ? utilFresh[_rng.Next(utilFresh.Count)]
                : utilityPool[_rng.Next(utilityPool.Count)];

            Reward.OfferedWeaponIds.Add(pick1.Id);
            Reward.OfferedWeaponIds.Add(pick2.Id);
        }

        private void GenerateUpgradeReward(bool isReroll)
        {
            int nextCost = isReroll ? Reward.RerollCost : 5;
            Reward = new RewardState
            {
                RerollCost = nextCost
            };
            List<ModifierType> allMods = new List<ModifierType>
            {
                ModifierType.DamagePlus1,
                ModifierType.DamagePlus2,
                ModifierType.CooldownMinus1,
                ModifierType.CooldownMinus2,
                ModifierType.Poison
            };
            Shuffle(allMods);
            Reward.OfferedModifiers.Add(allMods[0]);
            Reward.OfferedModifiers.Add(allMods[1]);
        }

        public void PickRewardCard(int offerIndex)
        {
            if (Phase != GamePhase.RewardPickCard)
            {
                return;
            }
            if (offerIndex < 0 || offerIndex >= Reward.OfferedWeaponIds.Count)
            {
                return;
            }
            string wid = Reward.OfferedWeaponIds[offerIndex];
            Hand.Add(new CardInstance { WeaponId = wid });
            Logger.Log("Added " + Db.Weapons[wid].Name + " to hand!", Color.FromArgb(140, 240, 175));
            SpawnNextWave();
        }

        public void SelectUpgradeModifier(int modIndex)
        {
            if (Phase != GamePhase.RewardUpgrade)
            {
                return;
            }
            if (modIndex < 0 || modIndex >= Reward.OfferedModifiers.Count)
            {
                return;
            }
            Reward.SelectedModifier = Reward.OfferedModifiers[modIndex];
        }

        public bool TryAttachSelectedModifierToCard(CardInstance card)
        {
            if (Phase != GamePhase.RewardUpgrade || !Reward.SelectedModifier.HasValue || card == null)
            {
                return false;
            }
            ModifierType mod = Reward.SelectedModifier.Value;
            WeaponDef wdef = Db.Weapons[card.WeaponId];

            if ((mod == ModifierType.DamagePlus1 || mod == ModifierType.DamagePlus2 || mod == ModifierType.Poison) && wdef.Category == CardCategory.Movement)
            {
                return false;
            }

            if (mod == ModifierType.DamagePlus1)
            {
                card.BonusDamage += 1;
            }
            else if (mod == ModifierType.DamagePlus2)
            {
                card.BonusDamage += 2;
            }
            else if (mod == ModifierType.CooldownMinus1)
            {
                card.CooldownModifier -= 1;
            }
            else if (mod == ModifierType.CooldownMinus2)
            {
                card.CooldownModifier -= 2;
            }
            else if (mod == ModifierType.Poison)
            {
                card.HasPoison = true;
            }

            Logger.Log("Upgraded " + wdef.Name + " with " + GetModifierLabel(mod) + "!", Color.FromArgb(255, 220, 100));
            SpawnNextWave();
            return true;
        }

        public void SkipReward()
        {
            if (Phase != GamePhase.RewardPickCard && Phase != GamePhase.RewardUpgrade)
            {
                return;
            }
            Logger.Log("Skipped reward.", Color.LightGray);
            SpawnNextWave();
        }

        public bool TryRerollReward()
        {
            if (Phase != GamePhase.RewardPickCard && Phase != GamePhase.RewardUpgrade)
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
            if (!Db.Weapons.ContainsKey(weaponId) || Profile.UnlockedWeapons.Contains(weaponId))
            {
                return false;
            }
            WeaponDef def = Db.Weapons[weaponId];
            if (Profile.Coins < def.UnlockCost)
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
            if (!Db.Upgrades.ContainsKey(upgradeId) || Profile.UnlockedUpgrades.Contains(upgradeId))
            {
                return false;
            }
            UpgradeDef def = Db.Upgrades[upgradeId];
            if (Profile.Coins < def.Cost)
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
            int boughtWeapons = Profile.UnlockedWeapons.Count(id => Db.Weapons.ContainsKey(id) && !Db.Weapons[id].DefaultUnlocked);
            int boughtUpgrades = Profile.UnlockedUpgrades.Count;
            UpdateAchievementMax("well_equipped", boughtWeapons + boughtUpgrades);
        }

        public void UpdateAchievementMax(string achievementId, int newValue)
        {
            if (!Db.Achievements.ContainsKey(achievementId))
            {
                return;
            }
            AchievementDef def = Db.Achievements[achievementId];
            int prev = Profile.AchievementProgress.ContainsKey(achievementId) ? Profile.AchievementProgress[achievementId] : 0;
            int clamped = Math.Min(def.TargetValue, Math.Max(prev, newValue));
            Profile.AchievementProgress[achievementId] = clamped;

            if (clamped >= def.TargetValue && !Profile.CompletedAchievements.Contains(achievementId))
            {
                Profile.CompletedAchievements.Add(achievementId);
                Profile.Coins += def.CoinReward;
                Stats.TotalCoinsEarned += def.CoinReward;

                string extra = "";
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
                Progress = 0f,
                OffsetX = (float)(_rng.NextDouble() * 16.0 - 8.0)
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
            switch (m)
            {
                case ModifierType.DamagePlus1: return "+1 DMG";
                case ModifierType.DamagePlus2: return "+2 DMG";
                case ModifierType.CooldownMinus1: return "-1 COOLDOWN";
                case ModifierType.CooldownMinus2: return "-2 COOLDOWN";
                case ModifierType.Poison: return "POISON (+1 CD)";
                default: return m.ToString();
            }
        }

        public static string GetModifierDescription(ModifierType m)
        {
            switch (m)
            {
                case ModifierType.DamagePlus1: return "Permanently increases this card's damage by +1 for the run.";
                case ModifierType.DamagePlus2: return "Permanently increases this card's damage by +2 for the run.";
                case ModifierType.CooldownMinus1: return "Reduces this card's maximum cooldown by 1 turn (min 1).";
                case ModifierType.CooldownMinus2: return "Reduces this card's maximum cooldown by 2 turns (min 1).";
                case ModifierType.Poison: return "Adds +1 max cooldown. Hit targets suffer 1 poison dmg/turn for 3 turns.";
                default: return "";
            }
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    public static class VectorArtRenderer
    {
        public static GraphicsPath CreateRoundedRect(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
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
            using (GraphicsPath p = CreateRoundedRect(r, radius))
            {
                g.FillPath(brush, p);
            }
        }

        public static void DrawRoundedRect(Graphics g, Pen pen, RectangleF r, float radius)
        {
            using (GraphicsPath p = CreateRoundedRect(r, radius))
            {
                g.DrawPath(pen, p);
            }
        }

        public static Color GetCategoryHeaderColor(CardCategory cat)
        {
            switch (cat)
            {
                case CardCategory.Melee: return Color.FromArgb(185, 52, 52);
                case CardCategory.Ranged: return Color.FromArgb(42, 145, 78);
                case CardCategory.Movement: return Color.FromArgb(42, 118, 198);
                case CardCategory.Special: return Color.FromArgb(132, 62, 186);
                default: return Color.SlateGray;
            }
        }

        public static Color GetCategoryLightColor(CardCategory cat)
        {
            switch (cat)
            {
                case CardCategory.Melee: return Color.FromArgb(255, 135, 120);
                case CardCategory.Ranged: return Color.FromArgb(120, 235, 155);
                case CardCategory.Movement: return Color.FromArgb(120, 205, 255);
                case CardCategory.Special: return Color.FromArgb(215, 150, 255);
                default: return Color.LightGray;
            }
        }

        public static void DrawCharacterPortrait(Graphics g, string characterId, RectangleF bounds, bool isImmune)
        {
            float cx = bounds.X + bounds.Width * 0.5f;
            float cy = bounds.Y + bounds.Height * 0.52f;
            float s = Math.Min(bounds.Width, bounds.Height) * 0.44f;

            // Ground shadow
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                g.FillEllipse(sh, cx - s * 0.85f, cy + s * 0.55f, s * 1.7f, s * 0.42f);
            }

            if (isImmune)
            {
                using (Pen glow = new Pen(Color.FromArgb(210, 90, 240, 255), 3f))
                using (SolidBrush aura = new SolidBrush(Color.FromArgb(50, 70, 230, 255)))
                {
                    g.FillEllipse(aura, cx - s * 1.08f, cy - s * 1.05f, s * 2.16f, s * 2.0f);
                    g.DrawEllipse(glow, cx - s * 1.08f, cy - s * 1.05f, s * 2.16f, s * 2.0f);
                }
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
            // Cloak & armored shoulders (Blue/Gold theme)
            PointF[] cloak = new[]
            {
                new PointF(cx - s * 0.78f, cy + s * 0.72f),
                new PointF(cx - s * 0.62f, cy + s * 0.05f),
                new PointF(cx + s * 0.62f, cy + s * 0.05f),
                new PointF(cx + s * 0.78f, cy + s * 0.72f)
            };
            using (LinearGradientBrush cb = new LinearGradientBrush(new PointF(cx, cy), new PointF(cx, cy + s * 0.8f), Color.FromArgb(46, 108, 195), Color.FromArgb(22, 54, 115)))
            {
                g.FillPolygon(cb, cloak);
            }
            using (Pen goldTrim = new Pen(Color.FromArgb(232, 188, 72), Math.Max(1.4f, s * 0.07f)))
            {
                g.DrawPolygon(goldTrim, cloak);
            }

            // Battle Axe held on right
            using (Pen shaft = new Pen(Color.FromArgb(135, 86, 44), Math.Max(2f, s * 0.11f)))
            {
                g.DrawLine(shaft, cx + s * 0.62f, cy + s * 0.62f, cx + s * 0.86f, cy - s * 0.56f);
            }
            PointF[] axeHead = new[]
            {
                new PointF(cx + s * 0.76f, cy - s * 0.45f),
                new PointF(cx + s * 1.08f, cy - s * 0.65f),
                new PointF(cx + s * 1.12f, cy - s * 0.20f),
                new PointF(cx + s * 0.74f, cy - s * 0.28f)
            };
            using (LinearGradientBrush blade = new LinearGradientBrush(new PointF(cx + s * 0.7f, cy - s * 0.65f), new PointF(cx + s * 1.12f, cy - s * 0.2f), Color.FromArgb(228, 236, 245), Color.FromArgb(130, 145, 165)))
            {
                g.FillPolygon(blade, axeHead);
            }
            using (Pen outline = new Pen(Color.FromArgb(40, 48, 60), 1.2f))
            {
                g.DrawPolygon(outline, axeHead);
            }

            // Face & braided golden-brown beard
            using (SolidBrush skin = new SolidBrush(Color.FromArgb(234, 188, 152)))
            {
                g.FillEllipse(skin, cx - s * 0.36f, cy - s * 0.36f, s * 0.72f, s * 0.68f);
            }
            PointF[] beard = new[]
            {
                new PointF(cx - s * 0.38f, cy - s * 0.02f),
                new PointF(cx - s * 0.28f, cy + s * 0.56f),
                new PointF(cx, cy + s * 0.68f),
                new PointF(cx + s * 0.28f, cy + s * 0.56f),
                new PointF(cx + s * 0.38f, cy - s * 0.02f)
            };
            using (LinearGradientBrush bb = new LinearGradientBrush(new PointF(cx, cy), new PointF(cx, cy + s * 0.68f), Color.FromArgb(215, 148, 58), Color.FromArgb(148, 86, 28)))
            {
                g.FillPolygon(bb, beard);
            }

            // Eyes
            using (SolidBrush eye = new SolidBrush(Color.FromArgb(28, 35, 48)))
            {
                g.FillEllipse(eye, cx - s * 0.18f, cy - s * 0.14f, s * 0.09f, s * 0.09f);
                g.FillEllipse(eye, cx + s * 0.09f, cy - s * 0.14f, s * 0.09f, s * 0.09f);
            }

            // Curved ivory Viking horns
            using (GraphicsPath leftHorn = new GraphicsPath())
            {
                leftHorn.AddPolygon(new[]
                {
                    new PointF(cx - s * 0.42f, cy - s * 0.38f),
                    new PointF(cx - s * 0.88f, cy - s * 0.52f),
                    new PointF(cx - s * 0.96f, cy - s * 0.94f),
                    new PointF(cx - s * 0.68f, cy - s * 0.66f),
                    new PointF(cx - s * 0.36f, cy - s * 0.58f)
                });
                using (SolidBrush ivory = new SolidBrush(Color.FromArgb(245, 238, 215)))
                {
                    g.FillPath(ivory, leftHorn);
                }
                g.DrawPath(Pens.DimGray, leftHorn);
            }
            using (GraphicsPath rightHorn = new GraphicsPath())
            {
                rightHorn.AddPolygon(new[]
                {
                    new PointF(cx + s * 0.42f, cy - s * 0.38f),
                    new PointF(cx + s * 0.88f, cy - s * 0.52f),
                    new PointF(cx + s * 0.96f, cy - s * 0.94f),
                    new PointF(cx + s * 0.68f, cy - s * 0.66f),
                    new PointF(cx + s * 0.36f, cy - s * 0.58f)
                });
                using (SolidBrush ivory = new SolidBrush(Color.FromArgb(245, 238, 215)))
                {
                    g.FillPath(ivory, rightHorn);
                }
                g.DrawPath(Pens.DimGray, rightHorn);
            }

            // Steel Helmet dome & gold band + nasal guard
            RectangleF dome = new RectangleF(cx - s * 0.46f, cy - s * 0.78f, s * 0.92f, s * 0.84f);
            using (LinearGradientBrush steel = new LinearGradientBrush(dome, Color.FromArgb(205, 218, 232), Color.FromArgb(88, 102, 122), LinearGradientMode.Vertical))
            {
                g.FillPie(steel, dome.X, dome.Y, dome.Width, dome.Height, 180, 180);
            }
            using (SolidBrush gold = new SolidBrush(Color.FromArgb(238, 194, 68)))
            {
                g.FillRectangle(gold, cx - s * 0.48f, cy - s * 0.38f, s * 0.96f, s * 0.13f);
                g.FillRectangle(gold, cx - s * 0.06f, cy - s * 0.38f, s * 0.12f, s * 0.32f);
            }
        }

        private static void DrawShieldmaidenVector(Graphics g, float cx, float cy, float s)
        {
            // Flowing cyan cape & silver armor
            PointF[] cape = new[]
            {
                new PointF(cx - s * 0.75f, cy + s * 0.72f),
                new PointF(cx - s * 0.55f, cy + s * 0.04f),
                new PointF(cx + s * 0.55f, cy + s * 0.04f),
                new PointF(cx + s * 0.75f, cy + s * 0.72f)
            };
            using (LinearGradientBrush cb = new LinearGradientBrush(new PointF(cx, cy), new PointF(cx, cy + s * 0.75f), Color.FromArgb(52, 188, 218), Color.FromArgb(24, 96, 128)))
            {
                g.FillPolygon(cb, cape);
            }
            using (Pen silverTrim = new Pen(Color.FromArgb(225, 238, 248), Math.Max(1.4f, s * 0.07f)))
            {
                g.DrawPolygon(silverTrim, cape);
            }

            // Face & braided silver-blonde hair
            using (SolidBrush hair = new SolidBrush(Color.FromArgb(238, 216, 138)))
            {
                g.FillEllipse(hair, cx - s * 0.44f, cy - s * 0.34f, s * 0.88f, s * 0.78f);
            }
            using (SolidBrush skin = new SolidBrush(Color.FromArgb(242, 200, 168)))
            {
                g.FillEllipse(skin, cx - s * 0.30f, cy - s * 0.32f, s * 0.60f, s * 0.58f);
            }
            using (SolidBrush eye = new SolidBrush(Color.FromArgb(25, 95, 135)))
            {
                g.FillEllipse(eye, cx - s * 0.15f, cy - s * 0.10f, s * 0.08f, s * 0.08f);
                g.FillEllipse(eye, cx + s * 0.07f, cy - s * 0.10f, s * 0.08f, s * 0.08f);
            }

            // Winged Valkyrie Helm
            PointF[] leftWing = new[]
            {
                new PointF(cx - s * 0.40f, cy - s * 0.36f),
                new PointF(cx - s * 0.98f, cy - s * 0.76f),
                new PointF(cx - s * 0.78f, cy - s * 0.36f),
                new PointF(cx - s * 0.90f, cy - s * 0.22f),
                new PointF(cx - s * 0.38f, cy - s * 0.18f)
            };
            PointF[] rightWing = new[]
            {
                new PointF(cx + s * 0.40f, cy - s * 0.36f),
                new PointF(cx + s * 0.98f, cy - s * 0.76f),
                new PointF(cx + s * 0.78f, cy - s * 0.36f),
                new PointF(cx + s * 0.90f, cy - s * 0.22f),
                new PointF(cx + s * 0.38f, cy - s * 0.18f)
            };
            using (SolidBrush wingBrush = new SolidBrush(Color.FromArgb(236, 246, 255)))
            using (Pen wingEdge = new Pen(Color.FromArgb(95, 185, 220), 1.2f))
            {
                g.FillPolygon(wingBrush, leftWing);
                g.DrawPolygon(wingEdge, leftWing);
                g.FillPolygon(wingBrush, rightWing);
                g.DrawPolygon(wingEdge, rightWing);
            }
            RectangleF dome = new RectangleF(cx - s * 0.42f, cy - s * 0.76f, s * 0.84f, s * 0.80f);
            using (LinearGradientBrush helm = new LinearGradientBrush(dome, Color.FromArgb(230, 242, 252), Color.FromArgb(118, 148, 172), LinearGradientMode.Vertical))
            {
                g.FillPie(helm, dome.X, dome.Y, dome.Width, dome.Height, 180, 180);
            }
            using (SolidBrush cyanGem = new SolidBrush(Color.FromArgb(80, 240, 255)))
            {
                g.FillPolygon(cyanGem, new[]
                {
                    new PointF(cx, cy - s * 0.56f),
                    new PointF(cx + s * 0.10f, cy - s * 0.38f),
                    new PointF(cx, cy - s * 0.22f),
                    new PointF(cx - s * 0.10f, cy - s * 0.38f)
                });
            }

            // Round Runic Shield on left front
            float shR = s * 0.48f;
            float shX = cx - s * 0.48f;
            float shY = cy + s * 0.30f;
            using (LinearGradientBrush shBg = new LinearGradientBrush(new RectangleF(shX - shR, shY - shR, shR * 2, shR * 2), Color.FromArgb(46, 148, 185), Color.FromArgb(18, 68, 96), 45f))
            {
                g.FillEllipse(shBg, shX - shR, shY - shR, shR * 2, shR * 2);
            }
            using (Pen rim = new Pen(Color.FromArgb(220, 234, 245), Math.Max(1.8f, s * 0.09f)))
            {
                g.DrawEllipse(rim, shX - shR, shY - shR, shR * 2, shR * 2);
            }
            using (Pen rune = new Pen(Color.FromArgb(130, 248, 255), Math.Max(1.4f, s * 0.06f)))
            {
                g.DrawLine(rune, shX, shY - shR * 0.6f, shX, shY + shR * 0.6f);
                g.DrawLine(rune, shX - shR * 0.42f, shY - shR * 0.25f, shX + shR * 0.42f, shY + shR * 0.25f);
                g.DrawLine(rune, shX + shR * 0.42f, shY - shR * 0.25f, shX - shR * 0.42f, shY + shR * 0.25f);
            }
        }

        public static void DrawEnemySprite(Graphics g, EnemyInstance enemy, RectangleF bounds, float animPhase)
        {
            float cx = bounds.X + bounds.Width * 0.5f;
            float cy = bounds.Y + bounds.Height * 0.50f;
            float s = Math.Min(bounds.Width, bounds.Height) * 0.40f;
            if (enemy.EnemyId == "viking_chief")
            {
                s *= 1.18f;
            }

            // Ground shadow
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                g.FillEllipse(sh, cx - s * 0.85f, cy + s * 0.54f, s * 1.7f, s * 0.40f);
            }

            if (enemy.EnemyId == "pawn")
            {
                DrawPawnVector(g, cx, cy, s);
            }
            else if (enemy.EnemyId == "archer")
            {
                DrawArcherVector(g, cx, cy, s);
            }
            else if (enemy.EnemyId == "spearman")
            {
                DrawSpearmanVector(g, cx, cy, s);
            }
            else if (enemy.EnemyId == "shaman")
            {
                DrawShamanVector(g, cx, cy, s, animPhase);
            }
            else if (enemy.EnemyId == "viking_chief")
            {
                DrawBossVector(g, cx, cy, s, animPhase);
            }
        }

        private static void DrawPawnVector(Graphics g, float cx, float cy, float s)
        {
            // Tunic & shoulders
            PointF[] body = new[]
            {
                new PointF(cx - s * 0.62f, cy + s * 0.66f),
                new PointF(cx - s * 0.48f, cy + s * 0.02f),
                new PointF(cx + s * 0.48f, cy + s * 0.02f),
                new PointF(cx + s * 0.62f, cy + s * 0.66f)
            };
            using (SolidBrush tunic = new SolidBrush(Color.FromArgb(128, 72, 54)))
            {
                g.FillPolygon(tunic, body);
            }
            // Head + iron cap helmet
            using (SolidBrush skin = new SolidBrush(Color.FromArgb(224, 176, 144)))
            {
                g.FillEllipse(skin, cx - s * 0.30f, cy - s * 0.34f, s * 0.60f, s * 0.56f);
            }
            using (LinearGradientBrush helm = new LinearGradientBrush(new RectangleF(cx - s * 0.38f, cy - s * 0.68f, s * 0.76f, s * 0.70f), Color.FromArgb(182, 190, 200), Color.FromArgb(78, 86, 98), 90f))
            {
                g.FillPie(helm, cx - s * 0.38f, cy - s * 0.68f, s * 0.76f, s * 0.70f, 180, 180);
            }
            // Angry eyes
            using (SolidBrush eye = new SolidBrush(Color.FromArgb(45, 25, 20)))
            {
                g.FillEllipse(eye, cx - s * 0.14f, cy - s * 0.12f, s * 0.08f, s * 0.08f);
                g.FillEllipse(eye, cx + s * 0.06f, cy - s * 0.12f, s * 0.08f, s * 0.08f);
            }
            // Round wooden shield on left
            float sr = s * 0.42f;
            using (SolidBrush wood = new SolidBrush(Color.FromArgb(148, 96, 52)))
            using (Pen ironRim = new Pen(Color.FromArgb(165, 175, 185), 2f))
            {
                g.FillEllipse(wood, cx - s * 0.74f, cy - s * 0.02f, sr * 2, sr * 2);
                g.DrawEllipse(ironRim, cx - s * 0.74f, cy - s * 0.02f, sr * 2, sr * 2);
                g.FillEllipse(Brushes.Silver, cx - s * 0.44f, cy + s * 0.28f, s * 0.24f, s * 0.24f);
            }
            // Hand axe on right
            using (Pen handle = new Pen(Color.FromArgb(120, 74, 38), 2.4f))
            {
                g.DrawLine(handle, cx + s * 0.48f, cy + s * 0.50f, cx + s * 0.74f, cy - s * 0.36f);
            }
            PointF[] blade = new[]
            {
                new PointF(cx + s * 0.66f, cy - s * 0.32f),
                new PointF(cx + s * 0.96f, cy - s * 0.42f),
                new PointF(cx + s * 0.94f, cy - s * 0.08f),
                new PointF(cx + s * 0.64f, cy - s * 0.14f)
            };
            using (SolidBrush steel = new SolidBrush(Color.FromArgb(205, 214, 224)))
            {
                g.FillPolygon(steel, blade);
                g.DrawPolygon(Pens.DimGray, blade);
            }
        }

        private static void DrawArcherVector(Graphics g, float cx, float cy, float s)
        {
            // Forest ranger cloak
            PointF[] cloak = new[]
            {
                new PointF(cx - s * 0.68f, cy + s * 0.68f),
                new PointF(cx - s * 0.46f, cy - s * 0.15f),
                new PointF(cx + s * 0.46f, cy - s * 0.15f),
                new PointF(cx + s * 0.68f, cy + s * 0.68f)
            };
            using (LinearGradientBrush gb = new LinearGradientBrush(new PointF(cx, cy - s * 0.2f), new PointF(cx, cy + s * 0.7f), Color.FromArgb(52, 134, 72), Color.FromArgb(22, 68, 38)))
            {
                g.FillPolygon(gb, cloak);
            }
            // Pointed Hood
            PointF[] hood = new[]
            {
                new PointF(cx, cy - s * 0.88f),
                new PointF(cx + s * 0.46f, cy - s * 0.12f),
                new PointF(cx, cy + s * 0.12f),
                new PointF(cx - s * 0.46f, cy - s * 0.12f)
            };
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(38, 108, 56)))
            {
                g.FillPolygon(hb, hood);
                g.DrawPolygon(Pens.Black, hood);
            }
            // Shadowed face with glowing eyes
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(22, 28, 25)))
            {
                g.FillEllipse(shadow, cx - s * 0.26f, cy - s * 0.36f, s * 0.52f, s * 0.46f);
            }
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(255, 224, 92)))
            {
                g.FillEllipse(glow, cx - s * 0.14f, cy - s * 0.16f, s * 0.09f, s * 0.09f);
                g.FillEllipse(glow, cx + s * 0.06f, cy - s * 0.16f, s * 0.09f, s * 0.09f);
            }
            // Drawn Wooden Bow + String + Arrow on right
            using (Pen bowWood = new Pen(Color.FromArgb(178, 118, 56), 2.8f))
            {
                g.DrawArc(bowWood, cx + s * 0.12f, cy - s * 0.58f, s * 0.78f, s * 1.16f, -80, 160);
            }
            using (Pen bowString = new Pen(Color.FromArgb(225, 225, 210), 1.2f))
            {
                g.DrawLine(bowString, cx + s * 0.56f, cy - s * 0.56f, cx + s * 0.32f, cy);
                g.DrawLine(bowString, cx + s * 0.32f, cy, cx + s * 0.56f, cy + s * 0.56f);
            }
            using (Pen arrow = new Pen(Color.FromArgb(245, 235, 195), 1.8f))
            {
                g.DrawLine(arrow, cx + s * 0.32f, cy, cx + s * 1.05f, cy);
            }
            PointF[] tip = new[]
            {
                new PointF(cx + s * 1.12f, cy),
                new PointF(cx + s * 0.94f, cy - s * 0.11f),
                new PointF(cx + s * 0.94f, cy + s * 0.11f)
            };
            g.FillPolygon(Brushes.Silver, tip);
        }

        private static void DrawSpearmanVector(Graphics g, float cx, float cy, float s)
        {
            // Armored body & crimson surcoat
            PointF[] torso = new[]
            {
                new PointF(cx - s * 0.62f, cy + s * 0.68f),
                new PointF(cx - s * 0.54f, cy - s * 0.04f),
                new PointF(cx + s * 0.54f, cy - s * 0.04f),
                new PointF(cx + s * 0.62f, cy + s * 0.68f)
            };
            using (SolidBrush redCoat = new SolidBrush(Color.FromArgb(148, 38, 46)))
            {
                g.FillPolygon(redCoat, torso);
            }
            // Steel shoulder pauldrons
            using (SolidBrush steel = new SolidBrush(Color.FromArgb(168, 180, 194)))
            {
                g.FillEllipse(steel, cx - s * 0.72f, cy - s * 0.08f, s * 0.44f, s * 0.36f);
                g.FillEllipse(steel, cx + s * 0.28f, cy - s * 0.08f, s * 0.44f, s * 0.36f);
            }
            // Full Armored Greathelm with visor slit
            RectangleF helmRect = new RectangleF(cx - s * 0.38f, cy - s * 0.66f, s * 0.76f, s * 0.76f);
            using (LinearGradientBrush hb = new LinearGradientBrush(helmRect, Color.FromArgb(215, 224, 235), Color.FromArgb(90, 102, 118), 0f))
            {
                FillRoundedRect(g, hb, helmRect, s * 0.20f);
            }
            using (SolidBrush visor = new SolidBrush(Color.FromArgb(20, 22, 28)))
            {
                g.FillRectangle(visor, cx - s * 0.28f, cy - s * 0.34f, s * 0.56f, s * 0.11f);
                g.FillRectangle(visor, cx - s * 0.05f, cy - s * 0.34f, s * 0.10f, s * 0.30f);
            }
            // Long Diagonal Spear
            using (Pen shaft = new Pen(Color.FromArgb(132, 86, 46), 2.8f))
            {
                g.DrawLine(shaft, cx - s * 0.75f, cy + s * 0.75f, cx + s * 0.85f, cy - s * 0.72f);
            }
            PointF[] spearTip = new[]
            {
                new PointF(cx + s * 1.05f, cy - s * 0.92f),
                new PointF(cx + s * 0.68f, cy - s * 0.78f),
                new PointF(cx + s * 0.86f, cy - s * 0.56f)
            };
            using (SolidBrush blade = new SolidBrush(Color.FromArgb(230, 240, 250)))
            {
                g.FillPolygon(blade, spearTip);
                g.DrawPolygon(Pens.SlateGray, spearTip);
            }
        }

        private static void DrawShamanVector(Graphics g, float cx, float cy, float s, float animPhase)
        {
            // Dark mystic robes
            PointF[] robe = new[]
            {
                new PointF(cx - s * 0.72f, cy + s * 0.70f),
                new PointF(cx - s * 0.42f, cy - s * 0.08f),
                new PointF(cx + s * 0.42f, cy - s * 0.08f),
                new PointF(cx + s * 0.72f, cy + s * 0.70f)
            };
            using (LinearGradientBrush rb = new LinearGradientBrush(new PointF(cx, cy), new PointF(cx, cy + s * 0.72f), Color.FromArgb(72, 46, 98), Color.FromArgb(36, 22, 54)))
            {
                g.FillPolygon(rb, robe);
            }
            // Tribal wooden mask with antlers
            using (Pen antler = new Pen(Color.FromArgb(182, 152, 114), 2.2f))
            {
                g.DrawLine(antler, cx - s * 0.28f, cy - s * 0.56f, cx - s * 0.64f, cy - s * 0.92f);
                g.DrawLine(antler, cx + s * 0.28f, cy - s * 0.56f, cx + s * 0.64f, cy - s * 0.92f);
            }
            PointF[] mask = new[]
            {
                new PointF(cx - s * 0.36f, cy - s * 0.62f),
                new PointF(cx + s * 0.36f, cy - s * 0.62f),
                new PointF(cx + s * 0.28f, cy + s * 0.14f),
                new PointF(cx, cy + s * 0.24f),
                new PointF(cx - s * 0.28f, cy + s * 0.14f)
            };
            using (SolidBrush mb = new SolidBrush(Color.FromArgb(138, 86, 52)))
            {
                g.FillPolygon(mb, mask);
                g.DrawPolygon(Pens.Black, mask);
            }
            // Glowing emerald eyes
            using (SolidBrush eyes = new SolidBrush(Color.FromArgb(110, 255, 110)))
            {
                g.FillEllipse(eyes, cx - s * 0.20f, cy - s * 0.34f, s * 0.14f, s * 0.10f);
                g.FillEllipse(eyes, cx + s * 0.06f, cy - s * 0.34f, s * 0.14f, s * 0.10f);
            }
            // Glowing Poison Potion Vial
            float pulse = (float)(Math.Sin(animPhase * 4.0) * 0.12 + 1.0);
            float vx = cx + s * 0.66f;
            float vy = cy + s * 0.12f;
            using (SolidBrush aura = new SolidBrush(Color.FromArgb(90, 80, 255, 95)))
            {
                g.FillEllipse(aura, vx - s * 0.28f * pulse, vy - s * 0.28f * pulse, s * 0.56f * pulse, s * 0.56f * pulse);
            }
            using (SolidBrush liquid = new SolidBrush(Color.FromArgb(80, 245, 95)))
            using (Pen glass = new Pen(Color.FromArgb(200, 240, 220), 1.4f))
            {
                g.FillEllipse(liquid, vx - s * 0.18f, vy - s * 0.16f, s * 0.36f, s * 0.36f);
                g.DrawEllipse(glass, vx - s * 0.18f, vy - s * 0.16f, s * 0.36f, s * 0.36f);
                g.FillRectangle(Brushes.SaddleBrown, vx - s * 0.07f, vy - s * 0.26f, s * 0.14f, s * 0.10f);
            }
        }

        private static void DrawBossVector(Graphics g, float cx, float cy, float s, float animPhase)
        {
            // Boss fiery warlord aura
            float pulse = (float)(Math.Sin(animPhase * 3.5) * 0.08 + 1.0);
            using (SolidBrush aura = new SolidBrush(Color.FromArgb(55, 255, 75, 45)))
            {
                g.FillEllipse(aura, cx - s * 0.98f * pulse, cy - s * 0.88f * pulse, s * 1.96f * pulse, s * 1.80f * pulse);
            }

            // Crimson Warlord Cape & Heavy Armored Shoulders
            PointF[] cape = new[]
            {
            new PointF(cx - s * 0.86f, cy + s * 0.74f),
            new PointF(cx - s * 0.68f, cy - s * 0.02f),
            new PointF(cx + s * 0.68f, cy - s * 0.02f),
            new PointF(cx + s * 0.86f, cy + s * 0.74f)
        };
            using (LinearGradientBrush cb = new LinearGradientBrush(new PointF(cx, cy), new PointF(cx, cy + s * 0.78f), Color.FromArgb(175, 34, 40), Color.FromArgb(72, 14, 18)))
            {
                g.FillPolygon(cb, cape);
            }
            using (Pen goldTrim = new Pen(Color.FromArgb(245, 198, 66), 2.2f))
            {
                g.DrawPolygon(goldTrim, cape);
            }

            // Left Weapon: Heavy Wooden Club
            using (Pen clubShaft = new Pen(Color.FromArgb(130, 82, 38), 3.6f))
            {
                g.DrawLine(clubShaft, cx - s * 0.56f, cy + s * 0.54f, cx - s * 0.96f, cy - s * 0.42f);
            }
            using (SolidBrush clubHead = new SolidBrush(Color.FromArgb(112, 68, 34)))
            {
                g.FillEllipse(clubHead, cx - s * 1.08f, cy - s * 0.58f, s * 0.30f, s * 0.38f);
                g.DrawEllipse(Pens.Silver, cx - s * 1.08f, cy - s * 0.58f, s * 0.30f, s * 0.38f);
            }

            // Right Weapon: Massive Evolved War-Axe
            using (Pen axeShaft = new Pen(Color.FromArgb(115, 68, 32), 3.2f))
            {
                g.DrawLine(axeShaft, cx + s * 0.56f, cy + s * 0.60f, cx + s * 0.92f, cy - s * 0.68f);
            }
            PointF[] dualBlade = new[]
            {
            new PointF(cx + s * 0.72f, cy - s * 0.54f),
            new PointF(cx + s * 1.18f, cy - s * 0.76f),
            new PointF(cx + s * 1.20f, cy - s * 0.16f),
            new PointF(cx + s * 0.72f, cy - s * 0.28f)
        };
            using (LinearGradientBrush steel = new LinearGradientBrush(new PointF(cx + s * 0.7f, cy - s * 0.76f), new PointF(cx + s * 1.2f, cy - s * 0.16f), Color.FromArgb(245, 230, 215), Color.FromArgb(148, 52, 46)))
            {
                g.FillPolygon(steel, dualBlade);
                g.DrawPolygon(Pens.Gold, dualBlade);
            }

            // Face & heavy dark beard
            using (SolidBrush skin = new SolidBrush(Color.FromArgb(222, 170, 138)))
            {
                g.FillEllipse(skin, cx - s * 0.36f, cy - s * 0.36f, s * 0.72f, s * 0.66f);
            }
            PointF[] beard = new[]
            {
            new PointF(cx - s * 0.40f, cy - s * 0.04f),
            new PointF(cx - s * 0.34f, cy + s * 0.58f),
            new PointF(cx, cy + s * 0.72f),
            new PointF(cx + s * 0.34f, cy + s * 0.58f),
            new PointF(cx + s * 0.40f, cy - s * 0.04f)
        };
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(92, 42, 24)))
            {
                g.FillPolygon(bb, beard);
            }

            // Glowing crimson eyes
            using (SolidBrush eyes = new SolidBrush(Color.FromArgb(255, 95, 65)))
            {
                g.FillEllipse(eyes, cx - s * 0.20f, cy - s * 0.16f, s * 0.12f, s * 0.09f);
                g.FillEllipse(eyes, cx + s * 0.08f, cy - s * 0.16f, s * 0.12f, s * 0.09f);
            }

            // Massive Horned & Crowned Warlord Helm
            PointF[] leftHorn = new[]
            {
            new PointF(cx - s * 0.42f, cy - s * 0.40f),
            new PointF(cx - s * 0.96f, cy - s * 0.54f),
            new PointF(cx - s * 1.06f, cy - s * 1.02f),
            new PointF(cx - s * 0.72f, cy - s * 0.72f),
            new PointF(cx - s * 0.38f, cy - s * 0.60f)
        };
            PointF[] rightHorn = new[]
            {
            new PointF(cx + s * 0.42f, cy - s * 0.40f),
            new PointF(cx + s * 0.96f, cy - s * 0.54f),
            new PointF(cx + s * 1.06f, cy - s * 1.02f),
            new PointF(cx + s * 0.72f, cy - s * 0.72f),
            new PointF(cx + s * 0.38f, cy - s * 0.60f)
        };
            using (SolidBrush bone = new SolidBrush(Color.FromArgb(245, 232, 198)))
            {
                g.FillPolygon(bone, leftHorn);
                g.FillPolygon(bone, rightHorn);
                g.DrawPolygon(Pens.SaddleBrown, leftHorn);
                g.DrawPolygon(Pens.SaddleBrown, rightHorn);
            }
            RectangleF helm = new RectangleF(cx - s * 0.48f, cy - s * 0.80f, s * 0.96f, s * 0.84f);
            using (LinearGradientBrush hb = new LinearGradientBrush(helm, Color.FromArgb(98, 108, 124), Color.FromArgb(36, 42, 52), 90f))
            {
                g.FillPie(hb, helm.X, helm.Y, helm.Width, helm.Height, 180, 180);
            }
            // Golden Crown Spikes
            PointF[] crown = new[]
            {
            new PointF(cx - s * 0.46f, cy - s * 0.38f),
            new PointF(cx - s * 0.42f, cy - s * 0.74f),
            new PointF(cx - s * 0.22f, cy - s * 0.52f),
            new PointF(cx, cy - s * 0.88f),
            new PointF(cx + s * 0.22f, cy - s * 0.52f),
            new PointF(cx + s * 0.42f, cy - s * 0.74f),
            new PointF(cx + s * 0.46f, cy - s * 0.38f)
        };
            using (SolidBrush gold = new SolidBrush(Color.FromArgb(248, 202, 58)))
            {
                g.FillPolygon(gold, crown);
                g.DrawPolygon(Pens.DarkGoldenrod, crown);
            }
        }

        public static void DrawWeaponIcon(Graphics g, string weaponId, RectangleF r)
        {
            float cx = r.X + r.Width * 0.5f;
            float cy = r.Y + r.Height * 0.5f;
            float s = Math.Min(r.Width, r.Height) * 0.42f;

            if (weaponId == "viking_axe")
            {
                using (Pen p = new Pen(Color.FromArgb(160, 102, 52), Math.Max(2.2f, s * 0.16f)))
                {
                    g.DrawLine(p, cx - s * 0.72f, cy + s * 0.76f, cx + s * 0.56f, cy - s * 0.68f);
                }
                PointF[] blade = new[]
                {
                new PointF(cx + s * 0.12f, cy - s * 0.56f),
                new PointF(cx + s * 0.84f, cy - s * 0.86f),
                new PointF(cx + s * 0.96f, cy - s * 0.04f),
                new PointF(cx + s * 0.34f, cy - s * 0.16f)
            };
                using (LinearGradientBrush gb = new LinearGradientBrush(r, Color.FromArgb(238, 245, 252), Color.FromArgb(132, 148, 168), 45f))
                {
                    g.FillPolygon(gb, blade);
                }
                g.DrawPolygon(Pens.DarkSlateGray, blade);
            }
            else if (weaponId == "wooden_club")
            {
                PointF[] club = new[]
                {
                new PointF(cx - s * 0.78f, cy + s * 0.76f),
                new PointF(cx - s * 0.60f, cy + s * 0.88f),
                new PointF(cx + s * 0.84f, cy - s * 0.38f),
                new PointF(cx + s * 0.42f, cy - s * 0.84f)
            };
                using (LinearGradientBrush wb = new LinearGradientBrush(r, Color.FromArgb(182, 122, 62), Color.FromArgb(98, 56, 26), 45f))
                {
                    g.FillPolygon(wb, club);
                }
                g.DrawPolygon(Pens.Black, club);
                using (SolidBrush stud = new SolidBrush(Color.LightSteelBlue))
                {
                    g.FillEllipse(stud, cx + s * 0.25f, cy - s * 0.48f, s * 0.18f, s * 0.18f);
                    g.FillEllipse(stud, cx + s * 0.48f, cy - s * 0.32f, s * 0.18f, s * 0.18f);
                    g.FillEllipse(stud, cx + s * 0.08f, cy - s * 0.24f, s * 0.18f, s * 0.18f);
                }
            }
            else if (weaponId == "two_handed_hammer")
            {
                using (Pen p = new Pen(Color.FromArgb(148, 96, 48), Math.Max(2.5f, s * 0.18f)))
                {
                    g.DrawLine(p, cx - s * 0.78f, cy + s * 0.78f, cx + s * 0.46f, cy - s * 0.46f);
                }
                PointF[] head = new[]
                {
                new PointF(cx - s * 0.08f, cy - s * 0.88f),
                new PointF(cx + s * 0.30f, cy - s * 0.96f),
                new PointF(cx + s * 0.96f, cy - s * 0.30f),
                new PointF(cx + s * 0.88f, cy + s * 0.08f),
                new PointF(cx + s * 0.46f, cy + s * 0.12f),
                new PointF(cx - s * 0.12f, cy - s * 0.46f)
            };
                using (LinearGradientBrush hb = new LinearGradientBrush(r, Color.FromArgb(225, 232, 240), Color.FromArgb(95, 108, 125), 45f))
                {
                    g.FillPolygon(hb, head);
                }
                using (Pen trim = new Pen(Color.Gold, 1.5f))
                {
                    g.DrawPolygon(trim, head);
                }
            }
            else if (weaponId == "throwing_axe")
            {
                using (Pen motion = new Pen(Color.FromArgb(160, 120, 245, 165), 1.6f))
                {
                    motion.DashStyle = DashStyle.Dash;
                    g.DrawArc(motion, cx - s * 0.92f, cy - s * 0.92f, s * 1.84f, s * 1.84f, 140, 110);
                }
                using (Pen shaft = new Pen(Color.FromArgb(162, 108, 56), Math.Max(2f, s * 0.15f)))
                {
                    g.DrawLine(shaft, cx - s * 0.45f, cy + s * 0.65f, cx + s * 0.38f, cy - s * 0.56f);
                }
                PointF[] blade = new[]
                {
                new PointF(cx + s * 0.02f, cy - s * 0.42f),
                new PointF(cx + s * 0.78f, cy - s * 0.52f),
                new PointF(cx + s * 0.72f, cy + s * 0.18f),
                new PointF(cx + s * 0.14f, cy - s * 0.05f)
            };
                using (SolidBrush steel = new SolidBrush(Color.FromArgb(225, 238, 248)))
                {
                    g.FillPolygon(steel, blade);
                    g.DrawPolygon(Pens.DarkSlateGray, blade);
                }
            }
            else if (weaponId == "hunting_bow")
            {
                using (Pen bow = new Pen(Color.FromArgb(196, 134, 66), Math.Max(2.4f, s * 0.16f)))
                {
                    g.DrawArc(bow, cx - s * 0.72f, cy - s * 0.88f, s * 1.36f, s * 1.76f, -72, 144);
                }
                using (Pen str = new Pen(Color.FromArgb(230, 232, 220), 1.3f))
                {
                    g.DrawLine(str, cx + s * 0.15f, cy - s * 0.82f, cx - s * 0.36f, cy);
                    g.DrawLine(str, cx - s * 0.36f, cy, cx + s * 0.15f, cy + s * 0.82f);
                }
                using (Pen arrow = new Pen(Color.FromArgb(160, 245, 175), 2f))
                {
                    g.DrawLine(arrow, cx - s * 0.42f, cy, cx + s * 0.88f, cy - s * 0.38f);
                }
                PointF[] tip = new[]
                {
                new PointF(cx + s * 0.96f, cy - s * 0.42f),
                new PointF(cx + s * 0.68f, cy - s * 0.46f),
                new PointF(cx + s * 0.78f, cy - s * 0.22f)
            };
                g.FillPolygon(Brushes.WhiteSmoke, tip);
            }
            else if (weaponId == "heavy_spear")
            {
                using (Pen shaft = new Pen(Color.FromArgb(156, 102, 52), Math.Max(2.2f, s * 0.15f)))
                {
                    g.DrawLine(shaft, cx - s * 0.86f, cy + s * 0.86f, cx + s * 0.46f, cy - s * 0.46f);
                }
                PointF[] tip = new[]
                {
                new PointF(cx + s * 0.94f, cy - s * 0.94f),
                new PointF(cx + s * 0.22f, cy - s * 0.54f),
                new PointF(cx + s * 0.54f, cy - s * 0.22f)
            };
                using (LinearGradientBrush gb = new LinearGradientBrush(r, Color.FromArgb(245, 250, 255), Color.FromArgb(110, 195, 145), 45f))
                {
                    g.FillPolygon(gb, tip);
                    g.DrawPolygon(Pens.DarkSlateGray, tip);
                }
            }
            else if (weaponId == "bear_trap")
            {
                using (Pen jaw = new Pen(Color.FromArgb(196, 206, 220), 2.5f))
                {
                    g.DrawArc(jaw, cx - s * 0.84f, cy - s * 0.64f, s * 1.68f, s * 1.28f, 180, 180);
                    g.DrawArc(jaw, cx - s * 0.84f, cy - s * 0.64f, s * 1.68f, s * 1.28f, 0, 180);
                }
                using (SolidBrush plate = new SolidBrush(Color.FromArgb(205, 78, 92)))
                {
                    g.FillEllipse(plate, cx - s * 0.28f, cy - s * 0.22f, s * 0.56f, s * 0.44f);
                }
                using (SolidBrush tooth = new SolidBrush(Color.WhiteSmoke))
                {
                    for (int i = -2; i <= 2; i++)
                    {
                        float tx = cx + i * s * 0.28f;
                        g.FillPolygon(tooth, new[]
                        {
                        new PointF(tx - s * 0.08f, cy - s * 0.46f),
                        new PointF(tx + s * 0.08f, cy - s * 0.46f),
                        new PointF(tx, cy - s * 0.18f)
                    });
                        g.FillPolygon(tooth, new[]
                        {
                        new PointF(tx - s * 0.08f, cy + s * 0.46f),
                        new PointF(tx + s * 0.08f, cy + s * 0.46f),
                        new PointF(tx, cy + s * 0.18f)
                    });
                    }
                }
            }
            else if (weaponId == "boat_hook")
            {
                using (Pen rope = new Pen(Color.FromArgb(205, 172, 118), 2f))
                {
                    rope.DashStyle = DashStyle.Dot;
                    g.DrawLine(rope, cx - s * 0.84f, cy + s * 0.80f, cx - s * 0.18f, cy + s * 0.18f);
                }
                using (Pen hook = new Pen(Color.FromArgb(215, 228, 245), Math.Max(2.6f, s * 0.18f)))
                {
                    g.DrawLine(hook, cx - s * 0.24f, cy + s * 0.24f, cx + s * 0.52f, cy - s * 0.52f);
                    g.DrawArc(hook, cx + s * 0.12f, cy - s * 0.92f, s * 0.78f, s * 0.78f, -150, 210);
                }
                PointF[] barb = new[]
                {
                new PointF(cx + s * 0.88f, cy - s * 0.62f),
                new PointF(cx + s * 0.56f, cy - s * 0.74f),
                new PointF(cx + s * 0.68f, cy - s * 0.42f)
            };
                g.FillPolygon(Brushes.Violet, barb);
            }
            else if (weaponId == "diagonal_dash")
            {
                using (Pen trail = new Pen(Color.FromArgb(110, 195, 255), 2.4f))
                {
                    g.DrawLine(trail, cx - s * 0.78f, cy + s * 0.72f, cx + s * 0.46f, cy - s * 0.52f);
                    g.DrawLine(trail, cx - s * 0.86f, cy + s * 0.36f, cx + s * 0.08f, cy - s * 0.58f);
                    g.DrawLine(trail, cx - s * 0.40f, cy + s * 0.84f, cx + s * 0.54f, cy - s * 0.10f);
                }
                PointF[] arrow = new[]
                {
                new PointF(cx + s * 0.82f, cy - s * 0.82f),
                new PointF(cx + s * 0.18f, cy - s * 0.74f),
                new PointF(cx + s * 0.74f, cy - s * 0.18f)
            };
                using (SolidBrush cyan = new SolidBrush(Color.FromArgb(120, 232, 255)))
                {
                    g.FillPolygon(cyan, arrow);
                }
            }
            else if (weaponId == "slide")
            {
                using (Pen streak = new Pen(Color.FromArgb(100, 195, 255), 2.4f))
                {
                    g.DrawLine(streak, cx - s * 0.88f, cy - s * 0.24f, cx + s * 0.34f, cy - s * 0.24f);
                    g.DrawLine(streak, cx - s * 0.72f, cy, cx + s * 0.58f, cy);
                    g.DrawLine(streak, cx - s * 0.88f, cy + s * 0.24f, cx + s * 0.34f, cy + s * 0.24f);
                }
                PointF[] chev = new[]
                {
                new PointF(cx + s * 0.88f, cy),
                new PointF(cx + s * 0.34f, cy - s * 0.46f),
                new PointF(cx + s * 0.50f, cy),
                new PointF(cx + s * 0.34f, cy + s * 0.46f)
            };
                using (SolidBrush cyan = new SolidBrush(Color.FromArgb(135, 236, 255)))
                {
                    g.FillPolygon(cyan, chev);
                }
            }
        }

        public static void DrawUpgradeIcon(Graphics g, string upgradeId, RectangleF r)
        {
            float cx = r.X + r.Width * 0.5f;
            float cy = r.Y + r.Height * 0.5f;
            float s = Math.Min(r.Width, r.Height) * 0.42f;

            using (SolidBrush bg = new SolidBrush(Color.FromArgb(46, 62, 86)))
            using (Pen rim = new Pen(Color.FromArgb(125, 195, 245), 1.8f))
            {
                g.FillEllipse(bg, cx - s, cy - s, s * 2, s * 2);
                g.DrawEllipse(rim, cx - s, cy - s, s * 2, s * 2);
            }

            if (upgradeId == "durability_1")
            {
                PointF[] heart = new[]
                {
                new PointF(cx, cy + s * 0.56f),
                new PointF(cx - s * 0.54f, cy - s * 0.04f),
                new PointF(cx - s * 0.30f, cy - s * 0.48f),
                new PointF(cx, cy - s * 0.22f),
                new PointF(cx + s * 0.30f, cy - s * 0.48f),
                new PointF(cx + s * 0.54f, cy - s * 0.04f)
            };
                using (SolidBrush red = new SolidBrush(Color.FromArgb(235, 72, 86)))
                {
                    g.FillPolygon(red, heart);
                }
            }
            else if (upgradeId == "swift_boots")
            {
                PointF[] boot = new[]
                {
                new PointF(cx - s * 0.40f, cy - s * 0.48f),
                new PointF(cx + s * 0.06f, cy - s * 0.48f),
                new PointF(cx + s * 0.06f, cy + s * 0.14f),
                new PointF(cx + s * 0.56f, cy + s * 0.24f),
                new PointF(cx + s * 0.56f, cy + s * 0.52f),
                new PointF(cx - s * 0.44f, cy + s * 0.52f)
            };
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(95, 205, 255)))
                {
                    g.FillPolygon(cb, boot);
                }
            }
            else if (upgradeId == "charisma")
            {
                using (SolidBrush gold = new SolidBrush(Color.Gold))
                {
                    PointF[] star = CreateStar(cx, cy, s * 0.62f, s * 0.28f, 5);
                    g.FillPolygon(gold, star);
                }
            }
            else if (upgradeId == "melee_1")
            {
                DrawWeaponIcon(g, "viking_axe", new RectangleF(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8));
            }
            else if (upgradeId == "ranged_1")
            {
                DrawWeaponIcon(g, "hunting_bow", new RectangleF(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8));
            }
            else if (upgradeId == "tactical_master")
            {
                using (Pen clock = new Pen(Color.FromArgb(145, 235, 255), 2.2f))
                {
                    g.DrawEllipse(clock, cx - s * 0.54f, cy - s * 0.54f, s * 1.08f, s * 1.08f);
                    g.DrawLine(clock, cx, cy, cx, cy - s * 0.36f);
                    g.DrawLine(clock, cx, cy, cx + s * 0.28f, cy + s * 0.12f);
                }
            }
            else
            {
                using (SolidBrush bolt = new SolidBrush(Color.FromArgb(220, 150, 255)))
                {
                    PointF[] zig = new[]
                    {
                    new PointF(cx + s * 0.12f, cy - s * 0.62f),
                    new PointF(cx - s * 0.34f, cy + s * 0.04f),
                    new PointF(cx + s * 0.02f, cy + s * 0.04f),
                    new PointF(cx - s * 0.14f, cy + s * 0.64f),
                    new PointF(cx + s * 0.38f, cy - s * 0.08f),
                    new PointF(cx + s * 0.02f, cy - s * 0.08f)
                };
                    g.FillPolygon(bolt, zig);
                }
            }
        }

        private static PointF[] CreateStar(float cx, float cy, float outerR, float innerR, int points)
        {
            PointF[] res = new PointF[points * 2];
            double angle = -Math.PI / 2.0;
            double step = Math.PI / points;
            for (int i = 0; i < points * 2; i++)
            {
                float rad = (i % 2 == 0) ? outerR : innerR;
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

            using (Pen handles = new Pen(c1, 2.2f))
            {
                g.DrawArc(handles, cx - s * 0.88f, cy - s * 0.62f, s * 0.54f, s * 0.62f, 90, 180);
                g.DrawArc(handles, cx + s * 0.34f, cy - s * 0.62f, s * 0.54f, s * 0.62f, 270, 180);
            }
            PointF[] cup = new[]
            {
            new PointF(cx - s * 0.58f, cy - s * 0.72f),
            new PointF(cx + s * 0.58f, cy - s * 0.72f),
            new PointF(cx + s * 0.40f, cy + s * 0.08f),
            new PointF(cx, cy + s * 0.28f),
            new PointF(cx - s * 0.40f, cy + s * 0.08f)
        };
            using (LinearGradientBrush gb = new LinearGradientBrush(r, c1, c2, 90f))
            {
                g.FillPolygon(gb, cup);
                g.FillRectangle(gb, cx - s * 0.14f, cy + s * 0.22f, s * 0.28f, s * 0.34f);
                g.FillRectangle(gb, cx - s * 0.48f, cy + s * 0.52f, s * 0.96f, s * 0.22f);
            }
        }

        public static void DrawHazardTile(Graphics g, HazardInstance h, RectangleF tileRect, float animPhase)
        {
            if (h.Type == HazardType.Poison)
            {
                float pulse = (float)(Math.Sin(animPhase * 4.2 + h.Pos.X + h.Pos.Y) * 0.14 + 0.86);
                using (SolidBrush toxicBg = new SolidBrush(Color.FromArgb((int)(88 * pulse), 48, 212, 72)))
                {
                    FillRoundedRect(g, toxicBg, RectangleF.Inflate(tileRect, -3, -3), 6f);
                }
                using (Pen toxicBorder = new Pen(Color.FromArgb(170, 92, 248, 108), 1.6f))
                {
                    DrawRoundedRect(g, toxicBorder, RectangleF.Inflate(tileRect, -4, -4), 6f);
                }
                for (int i = 0; i < 4; i++)
                {
                    float phase = (animPhase * 1.4f + i * 0.27f + h.Pos.X * 0.2f) % 1.0f;
                    float bx = tileRect.X + 16 + ((i * 19 + h.Pos.Y * 7) % 48);
                    float by = tileRect.Bottom - 12 - phase * 42f;
                    float br = 3.2f + (1f - phase) * 3.5f;
                    using (SolidBrush bub = new SolidBrush(Color.FromArgb((int)((1f - phase) * 190), 130, 255, 130)))
                    {
                        g.FillEllipse(bub, bx - br, by - br, br * 2, br * 2);
                    }
                }
                using (Font f = new Font("Segoe UI", 7f, FontStyle.Bold))
                using (SolidBrush txt = new SolidBrush(Color.FromArgb(190, 255, 190)))
                {
                    g.DrawString("POISON " + h.RemainingTurns.ToString(CultureInfo.InvariantCulture) + "T", f, txt, tileRect.X + 5, tileRect.Bottom - 14);
                }
            }
            else if (h.Type == HazardType.BearTrap)
            {
                float cx = tileRect.X + tileRect.Width * 0.5f;
                float cy = tileRect.Y + tileRect.Height * 0.56f;
                Color jawColor = h.IsArmed ? Color.FromArgb(228, 232, 240) : Color.FromArgb(128, 136, 148);
                Color padColor = h.IsArmed ? Color.FromArgb(225, 62, 78) : Color.FromArgb(115, 118, 128);

                using (SolidBrush plate = new SolidBrush(Color.FromArgb(90, 20, 24, 30)))
                {
                    g.FillEllipse(plate, cx - 22, cy - 15, 44, 30);
                }
                using (Pen jawPen = new Pen(jawColor, 2.4f))
                {
                    g.DrawEllipse(jawPen, cx - 21, cy - 14, 42, 28);
                }
                using (SolidBrush trig = new SolidBrush(padColor))
                {
                    g.FillEllipse(trig, cx - 8, cy - 6, 16, 12);
                }
                using (SolidBrush spike = new SolidBrush(jawColor))
                {
                    for (int i = -2; i <= 2; i++)
                    {
                        float sx = cx + i * 8;
                        g.FillPolygon(spike, new[]
                        {
                        new PointF(sx - 2.5f, cy - 13),
                        new PointF(sx + 2.5f, cy - 13),
                        new PointF(sx, cy - 5)
                    });
                        g.FillPolygon(spike, new[]
                        {
                        new PointF(sx - 2.5f, cy + 13),
                        new PointF(sx + 2.5f, cy + 13),
                        new PointF(sx, cy + 5)
                    });
                    }
                }
                using (Font f = new Font("Segoe UI", 6.8f, FontStyle.Bold))
                using (SolidBrush txt = new SolidBrush(h.IsArmed ? Color.FromArgb(255, 155, 165) : Color.LightGray))
                {
                    g.DrawString(h.IsArmed ? "ARMED TRAP" : "UNARMED", f, txt, tileRect.X + 5, tileRect.Bottom - 14);
                }
            }
        }

        public static void DrawEnemyIntentBadge(Graphics g, EnemyInstance e, RectangleF tileRect, float animPhase)
        {
            RectangleF badge = new RectangleF(tileRect.X + 4, tileRect.Y + 3, tileRect.Width - 8, 15);
            Color bg;
            string label;
            Color fg = Color.White;

            if (e.StunTurns > 0)
            {
                bg = Color.FromArgb(148, 56, 195);
                label = "STUNNED";
            }
            else if (e.State == EnemyAIState.Unarmed)
            {
                bg = Color.FromArgb(72, 80, 94);
                label = "EQUIPPING";
                fg = Color.LightGray;
            }
            else if (e.State == EnemyAIState.Charging)
            {
                int flash = (int)(175 + Math.Sin(animPhase * 9.0) * 65);
                bg = Color.FromArgb(Math.Max(110, Math.Min(245, flash)), 34, 34);
                label = e.ChargeTurnsRemaining > 1 ? "CHARGE (" + e.ChargeTurnsRemaining.ToString(CultureInfo.InvariantCulture) + ")" : "! ATTACK !";
                fg = Color.FromArgb(255, 245, 180);
            }
            else
            {
                bg = Color.FromArgb(52, 68, 92);
                if (e.EnemyId == "pawn") label = "AXE";
                else if (e.EnemyId == "archer") label = "BOW";
                else if (e.EnemyId == "spearman") label = "SPEAR";
                else if (e.EnemyId == "shaman") label = "POTION";
                else label = "BOSS C" + (e.BossCycleIndex + 1).ToString(CultureInfo.InvariantCulture);
            }

            using (SolidBrush b = new SolidBrush(bg))
            {
                FillRoundedRect(g, b, badge, 4f);
            }
            using (Font f = new Font("Segoe UI", 6.8f, FontStyle.Bold))
            using (SolidBrush tf = new SolidBrush(fg))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(label, f, tf, badge, sf);
            }
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
            RectangleF r = rect;
            if (isHovered && !isExecuting)
            {
                r = new RectangleF(rect.X, rect.Y - 6, rect.Width, rect.Height);
            }

            // Drop shadow
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(135, 0, 0, 0)))
            {
                FillRoundedRect(g, sh, new RectangleF(r.X + 4, r.Y + 5, r.Width, r.Height), 9f);
            }

            // Card body gradient
            Color bodyTop = isExecuting ? Color.FromArgb(58, 54, 38) : Color.FromArgb(34, 41, 54);
            Color bodyBot = isExecuting ? Color.FromArgb(40, 36, 26) : Color.FromArgb(22, 27, 36);
            using (LinearGradientBrush bodyBrush = new LinearGradientBrush(r, bodyTop, bodyBot, 90f))
            {
                FillRoundedRect(g, bodyBrush, r, 9f);
            }

            // Color-coded Header
            Color headerColor = GetCategoryHeaderColor(def.Category);
            float headerH = compact ? 24f : 26f;
            RectangleF headerRect = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, headerH);
            using (LinearGradientBrush hb = new LinearGradientBrush(headerRect, headerColor, ControlPaint.Dark(headerColor, 0.25f), 90f))
            {
                FillRoundedRect(g, hb, headerRect, 8f);
            }

            // Border highlight
            Color borderColor = Color.FromArgb(74, 86, 108);
            float borderW = 1.6f;
            if (isExecuting)
            {
                borderColor = Color.FromArgb(255, 215, 70);
                borderW = 3.0f;
            }
            else if (isSelected)
            {
                borderColor = Color.FromArgb(95, 230, 175);
                borderW = 2.8f;
            }
            else if (isHovered)
            {
                borderColor = GetCategoryLightColor(def.Category);
                borderW = 2.4f;
            }
            using (Pen bp = new Pen(borderColor, borderW))
            {
                DrawRoundedRect(g, bp, r, 9f);
            }

            // Card Name in header
            using (Font nameFont = new Font("Segoe UI", compact ? 7.6f : 8.4f, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(def.Name, nameFont, Brushes.White, headerRect, sf);
            }

            // Queue execution order badge (1..4)
            if (queueNumber > 0)
            {
                RectangleF qBadge = new RectangleF(r.X - 6, r.Y - 6, 20, 20);
                using (SolidBrush qb = new SolidBrush(isExecuting ? Color.Gold : Color.FromArgb(52, 148, 224)))
                {
                    g.FillEllipse(qb, qBadge);
                }
                g.DrawEllipse(Pens.White, qBadge);
                using (Font qf = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (StringFormat qsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(queueNumber.ToString(CultureInfo.InvariantCulture), qf, isExecuting ? Brushes.Black : Brushes.White, qBadge, qsf);
                }
            }

            // Category tag + Art window
            float artTop = r.Y + headerH + 4;
            float artH = compact ? 42f : 50f;
            RectangleF artBox = new RectangleF(r.X + 7, artTop, r.Width - 14, artH);
            using (SolidBrush artBg = new SolidBrush(Color.FromArgb(18, 22, 30)))
            using (Pen artRim = new Pen(Color.FromArgb(56, 66, 84), 1f))
            {
                FillRoundedRect(g, artBg, artBox, 5f);
                DrawRoundedRect(g, artRim, artBox, 5f);
            }
            DrawWeaponIcon(g, def.Id, RectangleF.Inflate(artBox, -6, -3));

            // Category small pill in top-left of art box
            using (Font catFont = new Font("Segoe UI", 6.2f, FontStyle.Bold))
            using (SolidBrush catTxt = new SolidBrush(GetCategoryLightColor(def.Category)))
            {
                g.DrawString(def.Category.ToString().ToUpperInvariant(), catFont, catTxt, artBox.X + 4, artBox.Y + 2);
            }

            // Stats Strip: DMG & CD
            int dmg = inst != null ? inst.GetEffectiveDamage(db, profile) : def.BaseDamage;
            int maxCd = inst != null ? inst.GetEffectiveMaxCooldown(db, profile) : def.BaseCooldown;
            int curCd = inst != null ? inst.CurrentCooldown : 0;

            float statsY = artBox.Bottom + 4;
            RectangleF dmgPill = new RectangleF(r.X + 7, statsY, (r.Width - 18) * 0.48f, 18);
            RectangleF cdPill = new RectangleF(dmgPill.Right + 4, statsY, (r.Width - 18) * 0.52f, 18);

            using (SolidBrush dBg = new SolidBrush(Color.FromArgb(62, 34, 38)))
            using (SolidBrush cBg = new SolidBrush(curCd > 0 ? Color.FromArgb(88, 56, 26) : Color.FromArgb(30, 52, 74)))
            {
                FillRoundedRect(g, dBg, dmgPill, 4f);
                FillRoundedRect(g, cBg, cdPill, 4f);
            }
            using (Font stFont = new Font("Segoe UI", compact ? 6.8f : 7.4f, FontStyle.Bold))
            using (StringFormat csf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string dmgStr = def.Category == CardCategory.Movement ? "MOVE" : ("DMG " + dmg.ToString(CultureInfo.InvariantCulture));
                g.DrawString(dmgStr, stFont, Brushes.LightSalmon, dmgPill, csf);
                string cdStr = curCd > 0
                    ? ("CD " + curCd.ToString(CultureInfo.InvariantCulture) + "/" + maxCd.ToString(CultureInfo.InvariantCulture))
                    : ("CD " + maxCd.ToString(CultureInfo.InvariantCulture));
                g.DrawString(cdStr, stFont, curCd > 0 ? Brushes.Orange : Brushes.LightSkyBlue, cdPill, csf);
            }

            // Active modifiers strip (if any)
            float descTop = dmgPill.Bottom + 3;
            string modStr = inst != null ? inst.GetModifiersSummary() : "";
            if (!string.IsNullOrEmpty(modStr))
            {
                RectangleF modRect = new RectangleF(r.X + 6, descTop, r.Width - 12, 14);
                using (SolidBrush mb = new SolidBrush(Color.FromArgb(52, 66, 44)))
                {
                    FillRoundedRect(g, mb, modRect, 3f);
                }
                using (Font mf = new Font("Segoe UI", 6.4f, FontStyle.Bold))
                using (StringFormat msf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(modStr, mf, Brushes.LightGreen, modRect, msf);
                }
                descTop = modRect.Bottom + 3;
            }

            // Full English Description directly on card face
            RectangleF descRect = new RectangleF(r.X + 7, descTop, r.Width - 14, r.Bottom - descTop - 5);
            using (Font df = new Font("Segoe UI", compact ? 6.6f : 7.3f, FontStyle.Regular))
            using (SolidBrush dtxt = new SolidBrush(Color.FromArgb(215, 224, 236)))
            {
                g.DrawString(def.Description, df, dtxt, descRect);
            }

            // Cooldown dark veil if on cooldown
            if (curCd > 0)
            {
                using (SolidBrush veil = new SolidBrush(Color.FromArgb(128, 12, 16, 22)))
                {
                    FillRoundedRect(g, veil, r, 9f);
                }
                RectangleF cdCircle = new RectangleF(r.X + r.Width * 0.5f - 20, r.Y + r.Height * 0.44f - 20, 40, 40);
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(215, 36, 44, 58)))
                using (Pen cp = new Pen(Color.Orange, 2.5f))
                {
                    g.FillEllipse(cb, cdCircle);
                    g.DrawEllipse(cp, cdCircle);
                }
                using (Font cf = new Font("Segoe UI", 13f, FontStyle.Bold))
                using (StringFormat csf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(curCd.ToString(CultureInfo.InvariantCulture), cf, Brushes.Orange, cdCircle, csf);
                }
            }
        }
    }

    public class ClickRegion
    {
        public RectangleF Bounds { get; set; }
        public string ActionId { get; set; }
        public int Index { get; set; }
        public string Tag { get; set; }
    }

    public class GameCanvas : Control
    {
        private readonly GameEngine _engine;
        private readonly System.Windows.Forms.Timer _timer;
        private ScreenMode _screen;
        private string _selectedCharacterId;
        private float _animTime;

        private Point _mousePos;
        private GridPos? _hoveredGridTile;
        private CardInstance _hoveredCard;
        private List<ClickRegion> _clickRegions;

        // Drag & drop state for reordering Equipped Queue
        private int _dragFromIndex;
        private int _dragHoverIndex;
        private bool _isDraggingQueueCard;
        private Point _dragStartPoint;
        private RectangleF _gridRect;

        public GameCanvas(GameEngine engine)
        {
            _engine = engine;
            _screen = ScreenMode.MainMenu;
            _selectedCharacterId = "viking";
            _clickRegions = new List<ClickRegion>();
            _dragFromIndex = -1;
            _dragHoverIndex = -1;
            _gridRect = new RectangleF(404, 58, 410, 410);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(16, 20, 28);
            TabStop = true;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 33;
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
            if (disposing && _timer != null)
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
            _hoveredCard = null;

            // Deep slate background
            using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), Color.FromArgb(18, 23, 33), Color.FromArgb(11, 14, 22), 90f))
            {
                g.FillRectangle(bg, 0, 0, Width, Height);
            }

            if (_screen == ScreenMode.MainMenu)
            {
                RenderMainMenu(g);
            }
            else if (_screen == ScreenMode.Gameplay)
            {
                RenderGameplay(g);
            }
            else if (_screen == ScreenMode.AchievementsAndStats)
            {
                RenderAchievementsAndStats(g);
            }

            // Global non-blocking achievement toast banner
            RenderToastBanner(g);
        }

        private void RegisterClick(RectangleF bounds, string actionId, int index = 0, string tag = null)
        {
            _clickRegions.Add(new ClickRegion
            {
                Bounds = bounds,
                ActionId = actionId,
                Index = index,
                Tag = tag
            });
        }

        private bool IsMouseOver(RectangleF r)
        {
            return r.Contains(_mousePos);
        }

        private void DrawButton(Graphics g, RectangleF r, string text, Color baseColor, bool enabled, string actionId, int index = 0, string tag = null, string subText = null)
        {
            bool hov = enabled && IsMouseOver(r);
            Color top = !enabled ? Color.FromArgb(44, 50, 60) : (hov ? ControlPaint.Light(baseColor, 0.18f) : baseColor);
            Color bot = !enabled ? Color.FromArgb(32, 36, 44) : ControlPaint.Dark(baseColor, 0.28f);

            using (LinearGradientBrush gb = new LinearGradientBrush(r, top, bot, 90f))
            {
                VectorArtRenderer.FillRoundedRect(g, gb, r, 7f);
            }
            Color border = !enabled ? Color.FromArgb(62, 68, 78) : (hov ? Color.White : ControlPaint.Light(baseColor, 0.35f));
            using (Pen bp = new Pen(border, hov ? 2.2f : 1.4f))
            {
                VectorArtRenderer.DrawRoundedRect(g, bp, r, 7f);
            }

            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                if (string.IsNullOrEmpty(subText))
                {
                    using (Font f = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                    using (SolidBrush tb = new SolidBrush(enabled ? Color.White : Color.Gray))
                    {
                        g.DrawString(text, f, tb, r, sf);
                    }
                }
                else
                {
                    RectangleF topR = new RectangleF(r.X, r.Y + 4, r.Width, r.Height * 0.56f);
                    RectangleF botR = new RectangleF(r.X, r.Y + r.Height * 0.54f, r.Width, r.Height * 0.40f);
                    using (Font f1 = new Font("Segoe UI", 10f, FontStyle.Bold))
                    using (Font f2 = new Font("Segoe UI", 7.8f, FontStyle.Regular))
                    using (SolidBrush tb1 = new SolidBrush(enabled ? Color.White : Color.Gray))
                    using (SolidBrush tb2 = new SolidBrush(enabled ? Color.FromArgb(225, 236, 248) : Color.DimGray))
                    {
                        g.DrawString(text, f1, tb1, topR, sf);
                        g.DrawString(subText, f2, tb2, botR, sf);
                    }
                }
            }

            if (enabled)
            {
                RegisterClick(r, actionId, index, tag);
            }
        }

        private void RenderMainMenu(Graphics g)
        {
            // Header bar
            using (LinearGradientBrush hb = new LinearGradientBrush(new Rectangle(0, 0, Width, 74), Color.FromArgb(26, 34, 48), Color.FromArgb(18, 24, 34), 90f))
            {
                g.FillRectangle(hb, 0, 0, Width, 74);
            }
            using (Pen div = new Pen(Color.FromArgb(58, 72, 96), 2f))
            {
                g.DrawLine(div, 0, 74, Width, 74);
            }

            using (Font titleFont = new Font("Segoe UI", 24f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            {
                g.DrawString("ROGUE CARDS", titleFont, Brushes.White, 26, 12);
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(150, 180, 215)))
                {
                    g.DrawString("Tactical 5x5 Turn-Based Deckbuilder", subFont, sb, 28, 50);
                }
            }

            // Area selector preview badge
            RectangleF areaBox = new RectangleF(760, 16, 300, 42);
            using (SolidBrush ab = new SolidBrush(Color.FromArgb(30, 40, 56)))
            using (Pen ap = new Pen(_engine.Profile.Area2Unlocked ? Color.Gold : Color.FromArgb(72, 92, 120), 1.5f))
            {
                VectorArtRenderer.FillRoundedRect(g, ab, areaBox, 8f);
                VectorArtRenderer.DrawRoundedRect(g, ap, areaBox, 8f);
            }
            using (Font af1 = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Font af2 = new Font("Segoe UI", 7.6f, FontStyle.Regular))
            {
                g.DrawString("LEVEL 1: FROSTFJORD (10 WAVES)", af1, Brushes.LightSkyBlue, areaBox.X + 12, areaBox.Y + 5);
                string a2Txt = _engine.Profile.Area2Unlocked ? "Area 2: Ashlands UNLOCKED (Expansion Hook)" : "Area 2: Locked (Defeat Wave 10 Boss)";
                g.DrawString(a2Txt, af2, _engine.Profile.Area2Unlocked ? Brushes.Gold : Brushes.Silver, areaBox.X + 12, areaBox.Y + 23);
            }

            // Coins badge
            RectangleF coinBox = new RectangleF(1078, 16, 178, 42);
            using (SolidBrush cb = new SolidBrush(Color.FromArgb(46, 40, 24)))
            using (Pen cp = new Pen(Color.Gold, 1.8f))
            {
                VectorArtRenderer.FillRoundedRect(g, cb, coinBox, 8f);
                VectorArtRenderer.DrawRoundedRect(g, cp, coinBox, 8f);
            }
            using (Font cf = new Font("Segoe UI", 13f, FontStyle.Bold))
            using (StringFormat csf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(_engine.Profile.Coins.ToString(CultureInfo.InvariantCulture) + " COINS", cf, Brushes.Gold, coinBox, csf);
            }

            // Optional notification banner (corrupt save / parser warnings)
            float contentTop = 90f;
            if (!string.IsNullOrEmpty(_engine.MenuNotification))
            {
                RectangleF notif = new RectangleF(24, 80, 1232, 26);
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(128, 42, 42)))
                using (Pen np = new Pen(Color.OrangeRed, 1.4f))
                {
                    VectorArtRenderer.FillRoundedRect(g, nb, notif, 5f);
                    VectorArtRenderer.DrawRoundedRect(g, np, notif, 5f);
                }
                using (Font nf = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                {
                    g.DrawString("NOTICE: " + _engine.MenuNotification, nf, Brushes.White, notif.X + 12, notif.Y + 5);
                }
                RectangleF dismissR = new RectangleF(notif.Right - 72, notif.Y + 3, 64, 20);
                DrawButton(g, dismissR, "DISMISS", Color.FromArgb(88, 36, 36), true, "DismissNotification");
                contentTop = 112f;
            }

            // Left Panel: Hero Selection & Run Controls
            RectangleF leftPanel = new RectangleF(24, contentTop, 516, 704 - contentTop);
            using (SolidBrush pb = new SolidBrush(Color.FromArgb(24, 30, 42)))
            using (Pen pp = new Pen(Color.FromArgb(56, 68, 88), 1.5f))
            {
                VectorArtRenderer.FillRoundedRect(g, pb, leftPanel, 10f);
                VectorArtRenderer.DrawRoundedRect(g, pp, leftPanel, 10f);
            }
            using (Font secFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            {
                g.DrawString("SELECT HERO", secFont, Brushes.LightSteelBlue, leftPanel.X + 16, leftPanel.Y + 12);
            }

            float cardY = leftPanel.Y + 40;
            float cardH = 296f;
            for (int i = 0; i < Math.Min(2, _engine.Db.CharacterList.Count); i++)
            {
                CharacterDef cdef = _engine.Db.CharacterList[i];
                bool unlocked = _engine.Profile.UnlockedCharacters.Contains(cdef.Id);
                bool selected = _selectedCharacterId == cdef.Id;
                RectangleF cRect = new RectangleF(leftPanel.X + 16 + i * 246, cardY, 236, cardH);

                Color cBg = selected ? Color.FromArgb(34, 52, 74) : Color.FromArgb(20, 26, 36);
                using (SolidBrush cb = new SolidBrush(cBg))
                {
                    VectorArtRenderer.FillRoundedRect(g, cb, cRect, 8f);
                }
                Color cBorder = selected ? Color.FromArgb(95, 220, 255) : (unlocked ? Color.FromArgb(68, 82, 106) : Color.FromArgb(52, 56, 64));
                using (Pen cp = new Pen(cBorder, selected ? 2.6f : 1.4f))
                {
                    VectorArtRenderer.DrawRoundedRect(g, cp, cRect, 8f);
                }

                RectangleF portraitRect = new RectangleF(cRect.X + 54, cRect.Y + 14, 128, 118);
                VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, portraitRect, false);

                if (!unlocked)
                {
                    using (SolidBrush lockVeil = new SolidBrush(Color.FromArgb(145, 14, 16, 22)))
                    {
                        VectorArtRenderer.FillRoundedRect(g, lockVeil, cRect, 8f);
                    }
                }

                int maxHp = cdef.BaseHp + (_engine.Profile.UnlockedUpgrades.Contains("durability_1") ? 1 : 0);
                using (Font nf = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (Font sf = new Font("Segoe UI", 8.2f, FontStyle.Bold))
                using (Font df = new Font("Segoe UI", 7.8f, FontStyle.Regular))
                using (StringFormat ctr = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString(cdef.Name.ToUpperInvariant(), nf, unlocked ? Brushes.White : Brushes.LightGray, new RectangleF(cRect.X, cRect.Y + 134, cRect.Width, 24), ctr);
                    g.DrawString("HP: " + maxHp.ToString(CultureInfo.InvariantCulture) + "   |   SPECIAL CD: " + cdef.SpecialCooldown.ToString(CultureInfo.InvariantCulture), sf, Brushes.LightSkyBlue, new RectangleF(cRect.X, cRect.Y + 158, cRect.Width, 18), ctr);
                    g.DrawString("Special: " + cdef.SpecialName, sf, Brushes.Gold, new RectangleF(cRect.X + 10, cRect.Y + 180, cRect.Width - 20, 18));
                    g.DrawString(cdef.SpecialDescription, df, Brushes.Gainsboro, new RectangleF(cRect.X + 10, cRect.Y + 199, cRect.Width - 20, 72));

                    if (!unlocked)
                    {
                        RectangleF lockTag = new RectangleF(cRect.X + 14, cRect.Bottom - 28, cRect.Width - 28, 22);
                        using (SolidBrush lb = new SolidBrush(Color.FromArgb(140, 42, 42)))
                        {
                            VectorArtRenderer.FillRoundedRect(g, lb, lockTag, 5f);
                        }
                        g.DrawString("LOCKED: " + cdef.UnlockConditionText, new Font("Segoe UI", 7.2f, FontStyle.Bold), Brushes.White, lockTag, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                    else if (selected)
                    {
                        RectangleF selTag = new RectangleF(cRect.X + 58, cRect.Bottom - 26, cRect.Width - 116, 20);
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(44, 146, 205)))
                        {
                            VectorArtRenderer.FillRoundedRect(g, sb, selTag, 5f);
                        }
                        g.DrawString("SELECTED", sf, Brushes.White, selTag, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                }

                if (unlocked)
                {
                    RegisterClick(cRect, "SelectHero", i, cdef.Id);
                }
            }

            // Main Action Buttons
            float btnY = cardY + cardH + 16;
            bool hasSave = _engine.Saves.HasActiveRunSave();
            DrawButton(g, new RectangleF(leftPanel.X + 16, btnY, 484, 54), "START NEW RUN", Color.FromArgb(44, 148, 86), true, "StartNewRun", 0, _selectedCharacterId, "Draft 2 of 4 unlocked cards & enter Level 1");
            DrawButton(g, new RectangleF(leftPanel.X + 16, btnY + 64, 484, 52), "CONTINUE SAVED RUN", Color.FromArgb(48, 112, 182), hasSave, "ContinueRun", 0, null, hasSave ? "Resume from Saves/active_run.txt" : "No active run save found");
            DrawButton(g, new RectangleF(leftPanel.X + 16, btnY + 126, 484, 50), "ACHIEVEMENTS & STATISTICS", Color.FromArgb(118, 66, 168), true, "OpenAchievements", 0, null, "Unlocked: " + _engine.Profile.CompletedAchievements.Count.ToString(CultureInfo.InvariantCulture) + " / 10   |   Lifetime Stats");

            // Right Panel: Armory (2 Next Weapons) & Training (2 Next Upgrades)
            RectangleF rightPanel = new RectangleF(558, contentTop, 698, 704 - contentTop);
            using (SolidBrush pb = new SolidBrush(Color.FromArgb(24, 30, 42)))
            using (Pen pp = new Pen(Color.FromArgb(56, 68, 88), 1.5f))
            {
                VectorArtRenderer.FillRoundedRect(g, pb, rightPanel, 10f);
                VectorArtRenderer.DrawRoundedRect(g, pp, rightPanel, 10f);
            }

            using (Font secFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            {
                g.DrawString("NEXT UNLOCKABLE WEAPONS (ARMORY)", secFont, Color.FromArgb(255, 196, 105) == Color.Empty ? Brushes.Gold : Brushes.Orange, rightPanel.X + 16, rightPanel.Y + 12);
            }

            List<WeaponDef> nextWeapons = _engine.GetNextUnlockableWeapons(2);
            float wepY = rightPanel.Y + 38;
            if (nextWeapons.Count == 0)
            {
                RectangleF emptyBox = new RectangleF(rightPanel.X + 16, wepY, rightPanel.Width - 32, 232);
                using (SolidBrush eb = new SolidBrush(Color.FromArgb(28, 42, 34)))
                {
                    VectorArtRenderer.FillRoundedRect(g, eb, emptyBox, 8f);
                }
                using (Font ef = new Font("Segoe UI", 13f, FontStyle.Bold))
                using (StringFormat esf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("ALL WEAPONS UNLOCKED!", ef, Brushes.LightGreen, emptyBox, esf);
                }
            }
            else
            {
                for (int i = 0; i < nextWeapons.Count; i++)
                {
                    WeaponDef w = nextWeapons[i];
                    RectangleF slot = new RectangleF(rightPanel.X + 16 + i * 338, wepY, 326, 238);
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(20, 25, 36)))
                    using (Pen sp = new Pen(Color.FromArgb(62, 74, 96), 1.4f))
                    {
                        VectorArtRenderer.FillRoundedRect(g, sb, slot, 8f);
                        VectorArtRenderer.DrawRoundedRect(g, sp, slot, 8f);
                    }

                    RectangleF miniCard = new RectangleF(slot.X + 12, slot.Y + 12, 134, 174);
                    VectorArtRenderer.DrawPhysicalCard(g, w, null, _engine.Db, _engine.Profile, miniCard, false, false, false, 0, true);

                    using (Font wf1 = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                    using (Font wf2 = new Font("Segoe UI", 8.2f, FontStyle.Regular))
                    {
                        g.DrawString(w.Name, wf1, Brushes.White, new RectangleF(slot.X + 156, slot.Y + 14, 160, 42));
                        g.DrawString("Category: " + w.Category.ToString(), wf2, VectorArtRenderer.GetCategoryLightColor(w.Category) == Color.Empty ? Brushes.White : new SolidBrush(VectorArtRenderer.GetCategoryLightColor(w.Category)), slot.X + 156, slot.Y + 58);
                        g.DrawString("Damage: " + w.BaseDamage.ToString(CultureInfo.InvariantCulture) + "   CD: " + w.BaseCooldown.ToString(CultureInfo.InvariantCulture), wf2, Brushes.LightSteelBlue, slot.X + 156, slot.Y + 76);
                        g.DrawString(w.Description, wf2, Brushes.Gainsboro, new RectangleF(slot.X + 156, slot.Y + 98, 160, 88));
                    }

                    bool canAfford = _engine.Profile.Coins >= w.UnlockCost;
                    DrawButton(g, new RectangleF(slot.X + 12, slot.Bottom - 40, slot.Width - 24, 30), "UNLOCK — " + w.UnlockCost.ToString(CultureInfo.InvariantCulture) + " COINS", Color.FromArgb(182, 128, 36), canAfford, "BuyWeapon", i, w.Id);
                }
            }

            // Permanent Upgrades Section
            float upgTitleY = wepY + 252;
            using (Font secFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            {
                g.DrawString("NEXT PERMANENT UPGRADES (TRAINING)", secFont, Brushes.LightSkyBlue, rightPanel.X + 16, upgTitleY);
            }

            List<UpgradeDef> nextUpgrades = _engine.GetNextUnlockableUpgrades(2);
            float upgY = upgTitleY + 26;
            float upgH = rightPanel.Bottom - upgY - 14;
            if (nextUpgrades.Count == 0)
            {
                RectangleF emptyBox = new RectangleF(rightPanel.X + 16, upgY, rightPanel.Width - 32, upgH);
                using (SolidBrush eb = new SolidBrush(Color.FromArgb(26, 40, 56)))
                {
                    VectorArtRenderer.FillRoundedRect(g, eb, emptyBox, 8f);
                }
                using (Font ef = new Font("Segoe UI", 13f, FontStyle.Bold))
                using (StringFormat esf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("ALL PERMANENT UPGRADES MASTERED!", ef, Brushes.LightSkyBlue, emptyBox, esf);
                }
            }
            else
            {
                for (int i = 0; i < nextUpgrades.Count; i++)
                {
                    UpgradeDef u = nextUpgrades[i];
                    RectangleF slot = new RectangleF(rightPanel.X + 16 + i * 338, upgY, 326, upgH);
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(20, 26, 38)))
                    using (Pen sp = new Pen(Color.FromArgb(62, 80, 108), 1.4f))
                    {
                        VectorArtRenderer.FillRoundedRect(g, sb, slot, 8f);
                        VectorArtRenderer.DrawRoundedRect(g, sp, slot, 8f);
                    }

                    VectorArtRenderer.DrawUpgradeIcon(g, u.Id, new RectangleF(slot.X + 16, slot.Y + 18, 68, 68));
                    using (Font uf1 = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                    using (Font uf2 = new Font("Segoe UI", 8.4f, FontStyle.Regular))
                    {
                        g.DrawString(u.Name, uf1, Brushes.White, new RectangleF(slot.X + 94, slot.Y + 18, 220, 26));
                        g.DrawString("Tier #" + u.UnlockOrder.ToString(CultureInfo.InvariantCulture), new Font("Segoe UI", 7.8f, FontStyle.Bold), Brushes.LightSkyBlue, slot.X + 94, slot.Y + 42);
                        g.DrawString(u.Description, uf2, Brushes.Gainsboro, new RectangleF(slot.X + 16, slot.Y + 96, slot.Width - 32, upgH - 144));
                    }

                    bool canAfford = _engine.Profile.Coins >= u.Cost;
                    DrawButton(g, new RectangleF(slot.X + 12, slot.Bottom - 40, slot.Width - 24, 30), "UNLOCK — " + u.Cost.ToString(CultureInfo.InvariantCulture) + " COINS", Color.FromArgb(46, 128, 192), canAfford, "BuyUpgrade", i, u.Id);
                }
            }
        }

        private void RenderGameplay(Graphics g)
        {
            RenderTopStatusBar(g);
            RenderLeftInspectorAndLegend(g);
            RenderTacticalGrid(g);
            RenderRightWaveAndCombatLog(g);
            RenderBottomCardDock(g);

            // Overlay Modals (Draft, Pick Card, Upgrade, Victory, Defeat)
            if (_engine.Phase == GamePhase.StartingDraft)
            {
                RenderStartingDraftOverlay(g);
            }
            else if (_engine.Phase == GamePhase.RewardPickCard)
            {
                RenderPickCardOverlay(g);
            }
            else if (_engine.Phase == GamePhase.RewardUpgrade)
            {
                RenderUpgradeOverlay(g);
            }
            else if (_engine.Phase == GamePhase.Victory || _engine.Phase == GamePhase.Defeat)
            {
                RenderEndRunOverlay(g);
            }
        }

        private void RenderTopStatusBar(Graphics g)
        {
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(24, 30, 42)))
            {
                g.FillRectangle(tb, 0, 0, Width, 48);
            }
            using (Pen div = new Pen(Color.FromArgb(56, 70, 94), 1.5f))
            {
                g.DrawLine(div, 0, 48, Width, 48);
            }

            CharacterDef cdef = _engine.Db.Characters[_engine.Player.CharacterId];
            VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, new RectangleF(10, 4, 40, 40), _engine.Player.ImmuneTurns > 0);
            using (Font fBold = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Font fSmall = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                g.DrawString(cdef.Name.ToUpperInvariant(), fBold, Brushes.White, 56, 14);

                // HP Bar
                RectangleF hpBg = new RectangleF(192, 12, 168, 24);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(48, 24, 28)))
                {
                    VectorArtRenderer.FillRoundedRect(g, bg, hpBg, 6f);
                }
                float ratio = _engine.Player.MaxHp > 0 ? Math.Max(0f, Math.Min(1f, (float)_engine.Player.Hp / _engine.Player.MaxHp)) : 0f;
                if (ratio > 0f)
                {
                    RectangleF hpFill = new RectangleF(hpBg.X, hpBg.Y, hpBg.Width * ratio, hpBg.Height);
                    using (LinearGradientBrush hb = new LinearGradientBrush(hpBg, Color.FromArgb(228, 62, 74), Color.FromArgb(168, 32, 42), 90f))
                    {
                        VectorArtRenderer.FillRoundedRect(g, hb, hpFill, 6f);
                    }
                }
                VectorArtRenderer.DrawRoundedRect(g, Pens.IndianRed, hpBg, 6f);
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("HP  " + _engine.Player.Hp.ToString(CultureInfo.InvariantCulture) + " / " + _engine.Player.MaxHp.ToString(CultureInfo.InvariantCulture), fSmall, Brushes.White, hpBg, sf);
                }

                // Coins
                g.DrawString("COINS: " + _engine.Profile.Coins.ToString(CultureInfo.InvariantCulture), fBold, Brushes.Gold, 382, 14);

                // Wave
                string waveTxt = _engine.CurrentWave > 0 ? ("WAVE " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + " / 10") : "STARTING DRAFT";
                g.DrawString(waveTxt, fBold, Brushes.LightSkyBlue, 514, 14);

                // Turn
                g.DrawString("TURN " + _engine.TurnCounter.ToString(CultureInfo.InvariantCulture), fBold, Brushes.LightSteelBlue, 668, 14);

                // Status Badges
                if (_engine.Player.ImmuneTurns > 0)
                {
                    RectangleF immR = new RectangleF(785, 11, 130, 26);
                    using (SolidBrush ib = new SolidBrush(Color.FromArgb(32, 118, 148)))
                    {
                        VectorArtRenderer.FillRoundedRect(g, ib, immR, 6f);
                    }
                    g.DrawString("IMMUNE (1T)", fSmall, Brushes.Cyan, immR, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
                else if (_engine.Phase == GamePhase.AimingCard)
                {
                    RectangleF aimR = new RectangleF(785, 11, 286, 26);
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(148, 86, 24)))
                    {
                        VectorArtRenderer.FillRoundedRect(g, ab, aimR, 6f);
                    }
                    CardInstance aimCard = _engine.GetCurrentlyAimingCard();
                    string aTxt = aimCard != null ? ("AIMING: " + _engine.Db.Weapons[aimCard.WeaponId].Name + " (Click Grid)") : "AIMING...";
                    g.DrawString(aTxt, fSmall, Brushes.White, aimR, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            }

            DrawButton(g, new RectangleF(1092, 7, 174, 34), "SAVE & EXIT", Color.FromArgb(148, 56, 62), true, "SaveAndExit");
        }

        private void RenderLeftInspectorAndLegend(Graphics g)
        {
            // Top Box: Entity / Tile / Card Inspector
            RectangleF inspBox = new RectangleF(14, 58, 374, 198);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(24, 30, 42)))
            using (Pen bp = new Pen(Color.FromArgb(56, 68, 90), 1.4f))
            {
                VectorArtRenderer.FillRoundedRect(g, bg, inspBox, 8f);
                VectorArtRenderer.DrawRoundedRect(g, bp, inspBox, 8f);
            }

            using (Font hFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font bFont = new Font("Segoe UI", 8.2f, FontStyle.Regular))
            using (Font sFont = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            {
                g.DrawString("TACTICAL INSPECTOR", hFont, Brushes.LightSkyBlue, inspBox.X + 12, inspBox.Y + 8);

                if (_hoveredGridTile.HasValue)
                {
                    GridPos gp = _hoveredGridTile.Value;
                    EnemyInstance enemy = _engine.GetEnemyAt(gp);
                    if (enemy != null)
                    {
                        EnemyDef edef = _engine.Db.Enemies[enemy.EnemyId];
                        VectorArtRenderer.DrawEnemySprite(g, enemy, new RectangleF(inspBox.X + 12, inspBox.Y + 34, 72, 72), _animTime);
                        g.DrawString(edef.Name + (edef.IsBoss ? " [BOSS]" : "") + "  (" + gp.ToString() + ")", hFont, edef.IsBoss ? Brushes.Gold : Brushes.White, inspBox.X + 92, inspBox.Y + 32);
                        g.DrawString("HP: " + enemy.Hp.ToString(CultureInfo.InvariantCulture) + "/" + enemy.MaxHp.ToString(CultureInfo.InvariantCulture) + "   DMG: " + edef.Damage.ToString(CultureInfo.InvariantCulture) + "   DROP: " + edef.CoinDrop.ToString(CultureInfo.InvariantCulture) + "c", sFont, Brushes.LightSalmon, inspBox.X + 92, inspBox.Y + 52);
                        g.DrawString("Intent: " + enemy.IntentLabel, sFont, enemy.State == EnemyAIState.Charging ? Brushes.OrangeRed : Brushes.LightSteelBlue, inspBox.X + 92, inspBox.Y + 70);
                        string status = "";
                        if (enemy.PoisonTurns > 0) status += "POISON (" + enemy.PoisonTurns.ToString(CultureInfo.InvariantCulture) + "T) ";
                        if (enemy.StunTurns > 0) status += "STUNNED ";
                        if (!string.IsNullOrEmpty(status))
                        {
                            g.DrawString(status, sFont, Brushes.LimeGreen, inspBox.X + 92, inspBox.Y + 88);
                        }
                        g.DrawString(edef.Description, bFont, Brushes.Gainsboro, new RectangleF(inspBox.X + 12, inspBox.Y + 114, inspBox.Width - 24, 76));
                    }
                    else if (_engine.Player.Pos == gp)
                    {
                        CharacterDef cdef = _engine.Db.Characters[_engine.Player.CharacterId];
                        VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, new RectangleF(inspBox.X + 12, inspBox.Y + 34, 72, 72), _engine.Player.ImmuneTurns > 0);
                        g.DrawString(cdef.Name + " (HERO - " + gp.ToString() + ")", hFont, Brushes.Cyan, inspBox.X + 92, inspBox.Y + 32);
                        g.DrawString("HP: " + _engine.Player.Hp.ToString(CultureInfo.InvariantCulture) + "/" + _engine.Player.MaxHp.ToString(CultureInfo.InvariantCulture) + "   Special CD: " + _engine.Player.SpecialCooldown.ToString(CultureInfo.InvariantCulture), sFont, Brushes.White, inspBox.X + 92, inspBox.Y + 52);
                        g.DrawString("Special: " + cdef.SpecialName, sFont, Brushes.Gold, inspBox.X + 92, inspBox.Y + 72);
                        g.DrawString(cdef.SpecialDescription, bFont, Brushes.Gainsboro, new RectangleF(inspBox.X + 12, inspBox.Y + 114, inspBox.Width - 24, 76));
                    }
                    else
                    {
                        g.DrawString("Tile " + gp.ToString() + " — Open Ground", sFont, Brushes.White, inspBox.X + 12, inspBox.Y + 36);
                        HazardInstance pHaz = _engine.GetHazardAt(gp, HazardType.Poison);
                        HazardInstance tHaz = _engine.GetHazardAt(gp, HazardType.BearTrap);
                        float ty = inspBox.Y + 60;
                        if (pHaz != null)
                        {
                            g.DrawString("Hazard: Poison Cloud (" + pHaz.RemainingTurns.ToString(CultureInfo.InvariantCulture) + " turns left). Deals 1 dmg/turn to any entity standing here.", bFont, Brushes.LightGreen, new RectangleF(inspBox.X + 12, ty, inspBox.Width - 24, 42));
                            ty += 46;
                        }
                        if (tHaz != null)
                        {
                            string tDesc = tHaz.IsArmed
                                ? "Hazard: ARMED Bear Trap (" + tHaz.Damage.ToString(CultureInfo.InvariantCulture) + " dmg + Stun)."
                                : "Hazard: Unarmed Bear Trap (arms once you move off this tile).";
                            g.DrawString(tDesc, bFont, Brushes.Violet, new RectangleF(inspBox.X + 12, ty, inspBox.Width - 24, 42));
                        }
                        if (pHaz == null && tHaz == null)
                        {
                            g.DrawString("Hover over any Hero, Enemy, or Card to inspect full stats, weapon intent, and tactical effects.\n\nClick an adjacent orthogonal tile (or press WASD/Arrows) to move.", bFont, Brushes.Silver, new RectangleF(inspBox.X + 12, ty, inspBox.Width - 24, 110));
                        }
                    }
                }
                else
                {
                    CharacterDef cdef = _engine.Db.Characters[_engine.Player.CharacterId];
                    VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, new RectangleF(inspBox.X + 12, inspBox.Y + 34, 72, 72), _engine.Player.ImmuneTurns > 0);
                    g.DrawString(cdef.Name + " (" + _engine.Player.Pos.ToString() + ")", hFont, Brushes.Cyan, inspBox.X + 92, inspBox.Y + 32);
                    g.DrawString("Special: " + cdef.SpecialName, sFont, Brushes.Gold, inspBox.X + 92, inspBox.Y + 54);
                    g.DrawString("Status: " + (_engine.Player.SpecialCooldown == 0 ? "READY" : ("CD " + _engine.Player.SpecialCooldown.ToString(CultureInfo.InvariantCulture) + " turns")), sFont, _engine.Player.SpecialCooldown == 0 ? Brushes.LightGreen : Brushes.Orange, inspBox.X + 92, inspBox.Y + 74);
                    g.DrawString(cdef.SpecialDescription, bFont, Brushes.Gainsboro, new RectangleF(inspBox.X + 12, inspBox.Y + 114, inspBox.Width - 24, 76));
                }
            }

            // Bottom Box: Tactical Legend & Controls
            RectangleF legBox = new RectangleF(14, 266, 374, 202);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(24, 30, 42)))
            using (Pen bp = new Pen(Color.FromArgb(56, 68, 90), 1.4f))
            {
                VectorArtRenderer.FillRoundedRect(g, bg, legBox, 8f);
                VectorArtRenderer.DrawRoundedRect(g, bp, legBox, 8f);
            }

            using (Font hFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font bFont = new Font("Segoe UI", 8.0f, FontStyle.Regular))
            {
                g.DrawString("INTENT LEGEND & CONTROLS", hFont, Brushes.LightSkyBlue, legBox.X + 12, legBox.Y + 8);

                DrawLegendItem(g, legBox.X + 12, legBox.Y + 34, Color.FromArgb(220, 52, 52), "Pulsing Red Tile: Enemy charged attack (hits next turn; Friendly Fire active!)");
                DrawLegendItem(g, legBox.X + 12, legBox.Y + 56, Color.FromArgb(255, 155, 45), "Orange Highlight: Your weapon aiming preview (hits/pierces/arc)");
                DrawLegendItem(g, legBox.X + 12, legBox.Y + 78, Color.FromArgb(70, 215, 255), "Cyan Highlight: Valid movement / dash / slide destination");
                DrawLegendItem(g, legBox.X + 12, legBox.Y + 100, Color.FromArgb(72, 224, 92), "Green Glow / Spikes: Ground Poison (1 dmg/T) or Bear Trap (Stun)");

                using (Pen div = new Pen(Color.FromArgb(54, 64, 82), 1f))
                {
                    g.DrawLine(div, legBox.X + 12, legBox.Y + 126, legBox.Right - 12, legBox.Y + 126);
                }

                string controls =
                    "MOVE: WASD / Arrows or click adjacent tile (1 Turn)\n" +
                    "EQUIP: Click hand card or press 1-4 (1 Turn)  |  REORDER: Drag & Drop (0 Turns)\n" +
                    "PLAY QUEUE: Enter or button (1 Turn; aim each card sequentially)\n" +
                    "WAIT: Space (1 Turn)   |   SPECIAL: E (1 Turn)   |   DIAG AIM: Q/E/Z/C or Mouse";
                g.DrawString(controls, bFont, Brushes.LightSteelBlue, new RectangleF(legBox.X + 12, legBox.Y + 132, legBox.Width - 24, 66));
            }
        }

        private void DrawLegendItem(Graphics g, float x, float y, Color c, string text)
        {
            using (SolidBrush b = new SolidBrush(c))
            {
                VectorArtRenderer.FillRoundedRect(g, b, new RectangleF(x, y + 2, 14, 14), 3f);
            }
            using (Font f = new Font("Segoe UI", 7.8f, FontStyle.Regular))
            {
                g.DrawString(text, f, Brushes.Gainsboro, x + 20, y + 1);
            }
        }

        private void RenderTacticalGrid(Graphics g)
        {
            float tileSize = 82f;
            RectangleF frame = RectangleF.Inflate(_gridRect, 5, 5);
            using (SolidBrush fb = new SolidBrush(Color.FromArgb(14, 18, 26)))
            using (Pen fp = new Pen(_engine.Phase == GamePhase.AimingCard ? Color.Orange : Color.FromArgb(72, 88, 114), 2.2f))
            {
                VectorArtRenderer.FillRoundedRect(g, fb, frame, 10f);
                VectorArtRenderer.DrawRoundedRect(g, fp, frame, 10f);
            }

            // Collect all enemy telegraphed tiles
            HashSet<GridPos> telegraphedTiles = new HashSet<GridPos>();
            foreach (EnemyInstance e in _engine.Enemies)
            {
                if (e.State == EnemyAIState.Charging)
                {
                    foreach (GridPos lp in e.LockedTiles)
                    {
                        telegraphedTiles.Add(lp);
                    }
                }
            }

            CardInstance aimingCard = _engine.GetCurrentlyAimingCard();
            bool aimingMove = aimingCard != null && _engine.Db.Weapons[aimingCard.WeaponId].Category == CardCategory.Movement;

            // Draw 5x5 tiles
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    GridPos gp = new GridPos(x, y);
                    RectangleF tr = GetTileRect(gp);
                    bool alt = ((x + y) % 2 == 0);
                    Color baseTile = alt ? Color.FromArgb(30, 37, 50) : Color.FromArgb(25, 31, 43);

                    using (SolidBrush tb = new SolidBrush(baseTile))
                    {
                        VectorArtRenderer.FillRoundedRect(g, tb, RectangleF.Inflate(tr, -1.5f, -1.5f), 5f);
                    }

                    // Coordinate label
                    using (Font cf = new Font("Segoe UI", 6.5f, FontStyle.Regular))
                    using (SolidBrush cbr = new SolidBrush(Color.FromArgb(75, 90, 115)))
                    {
                        g.DrawString(gp.ToString(), cf, cbr, tr.X + 4, tr.Y + 3);
                    }

                    // Valid orthogonal move highlight on PlayerTurn
                    if (_engine.Phase == GamePhase.PlayerTurn && gp.ManhattanDistance(_engine.Player.Pos) == 1 && _engine.GetEnemyAt(gp) == null)
                    {
                        bool isHov = _hoveredGridTile.HasValue && _hoveredGridTile.Value == gp;
                        using (SolidBrush mv = new SolidBrush(Color.FromArgb(isHov ? 88 : 34, 70, 210, 255)))
                        using (Pen mp = new Pen(Color.FromArgb(isHov ? 220 : 95, 90, 225, 255), isHov ? 2.0f : 1.0f))
                        {
                            VectorArtRenderer.FillRoundedRect(g, mv, RectangleF.Inflate(tr, -3, -3), 5f);
                            VectorArtRenderer.DrawRoundedRect(g, mp, RectangleF.Inflate(tr, -3, -3), 5f);
                        }
                    }

                    // Ground Hazards
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

                    // Enemy Attack Telegraphing
                    if (telegraphedTiles.Contains(gp))
                    {
                        int alpha = (int)(70 + Math.Sin(_animTime * 8.0) * 42);
                        using (SolidBrush warn = new SolidBrush(Color.FromArgb(Math.Max(30, Math.Min(140, alpha)), 235, 42, 42)))
                        using (Pen warnPen = new Pen(Color.FromArgb(225, 255, 92, 72), 2.2f))
                        {
                            warnPen.DashStyle = DashStyle.Dash;
                            VectorArtRenderer.FillRoundedRect(g, warn, RectangleF.Inflate(tr, -3, -3), 5f);
                            VectorArtRenderer.DrawRoundedRect(g, warnPen, RectangleF.Inflate(tr, -3, -3), 5f);
                        }
                    }

                    // Player Aiming Preview
                    if (_engine.Phase == GamePhase.AimingCard && _engine.CurrentPreviewTiles.Contains(gp))
                    {
                        Color fillC = aimingMove ? Color.FromArgb(105, 65, 225, 255) : Color.FromArgb(120, 255, 138, 36);
                        Color edgeC = aimingMove ? Color.FromArgb(240, 120, 245, 255) : Color.FromArgb(255, 255, 195, 70);
                        using (SolidBrush pv = new SolidBrush(fillC))
                        using (Pen pvp = new Pen(edgeC, 2.6f))
                        {
                            VectorArtRenderer.FillRoundedRect(g, pv, RectangleF.Inflate(tr, -3, -3), 5f);
                            VectorArtRenderer.DrawRoundedRect(g, pvp, RectangleF.Inflate(tr, -3, -3), 5f);
                        }
                        if (!aimingMove)
                        {
                            float tcx = tr.X + tileSize * 0.5f;
                            float tcy = tr.Y + tileSize * 0.5f;
                            using (Pen ch = new Pen(Color.FromArgb(210, 255, 240, 180), 1.6f))
                            {
                                g.DrawEllipse(ch, tcx - 12, tcy - 12, 24, 24);
                                g.DrawLine(ch, tcx - 17, tcy, tcx + 17, tcy);
                                g.DrawLine(ch, tcx, tcy - 17, tcx, tcy + 17);
                            }
                        }
                    }

                    // Hover subtle border
                    if (_hoveredGridTile.HasValue && _hoveredGridTile.Value == gp)
                    {
                        using (Pen hp = new Pen(Color.FromArgb(160, 255, 255, 255), 1.4f))
                        {
                            VectorArtRenderer.DrawRoundedRect(g, hp, RectangleF.Inflate(tr, -2, -2), 5f);
                        }
                    }
                }
            }

            // Draw Enemy Telegraph Directional Arrows
            foreach (EnemyInstance e in _engine.Enemies)
            {
                if (e.State == EnemyAIState.Charging && e.LockedTiles.Count > 0)
                {
                    RectangleF fromR = GetTileRect(e.Pos);
                    RectangleF toR = GetTileRect(e.LockedTiles[0]);
                    PointF p1 = new PointF(fromR.X + fromR.Width * 0.5f, fromR.Y + fromR.Height * 0.5f);
                    PointF p2 = new PointF(toR.X + toR.Width * 0.5f, toR.Y + toR.Height * 0.5f);
                    using (Pen arrowPen = new Pen(Color.FromArgb(215, 255, 80, 65), 2.8f))
                    {
                        arrowPen.CustomEndCap = new AdjustableArrowCap(5f, 6f);
                        g.DrawLine(arrowPen, p1, p2);
                    }
                }
            }

            // Render Player Entity
            RectangleF pRect = GetTileRect(_engine.Player.Pos);
            VectorArtRenderer.DrawCharacterPortrait(g, _engine.Player.CharacterId, new RectangleF(pRect.X + 8, pRect.Y + 12, pRect.Width - 16, pRect.Height - 28), _engine.Player.ImmuneTurns > 0);
            DrawEntityNameAndHpBar(g, pRect, _engine.Db.Characters[_engine.Player.CharacterId].Name, _engine.Player.Hp, _engine.Player.MaxHp, true, false);

            // Render Enemy Entities
            foreach (EnemyInstance enemy in _engine.Enemies)
            {
                if (enemy.Hp <= 0)
                {
                    continue;
                }
                RectangleF eRect = GetTileRect(enemy.Pos);
                VectorArtRenderer.DrawEnemyIntentBadge(g, enemy, eRect, _animTime);
                VectorArtRenderer.DrawEnemySprite(g, enemy, new RectangleF(eRect.X + 6, eRect.Y + 16, eRect.Width - 12, eRect.Height - 32), _animTime);
                EnemyDef edef = _engine.Db.Enemies[enemy.EnemyId];
                DrawEntityNameAndHpBar(g, eRect, edef.Name, enemy.Hp, enemy.MaxHp, false, enemy.PoisonTurns > 0);
            }

            // Floating Combat Popups
            foreach (FloatingText ft in _engine.FloatingTexts)
            {
                RectangleF tr = GetTileRect(ft.Pos);
                float fx = tr.X + tr.Width * 0.5f + ft.OffsetX;
                float fy = tr.Y + 26 - ft.Progress * 34f;
                int alpha = Math.Max(0, Math.Min(255, (int)((1f - ft.Progress) * 255)));
                using (Font ff = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                using (SolidBrush fg = new SolidBrush(Color.FromArgb(alpha, ft.Color)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center })
                {
                    g.DrawString(ft.Text, ff, sh, fx + 1, fy + 1, sf);
                    g.DrawString(ft.Text, ff, fg, fx, fy, sf);
                }
            }
        }

        private void DrawEntityNameAndHpBar(Graphics g, RectangleF tileRect, string name, int hp, int maxHp, bool isPlayer, bool isPoisoned)
        {
            // Name label at bottom above HP bar
            RectangleF nameR = new RectangleF(tileRect.X + 2, tileRect.Bottom - 23, tileRect.Width - 4, 12);
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(165, 10, 14, 20)))
            {
                g.FillRectangle(nb, nameR);
            }
            using (Font nf = new Font("Segoe UI", 6.8f, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(name, nf, isPlayer ? Brushes.Cyan : Brushes.White, nameR, sf);
            }

            // HP Bar
            RectangleF barBg = new RectangleF(tileRect.X + 6, tileRect.Bottom - 10, tileRect.Width - 12, 7);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(30, 16, 18)))
            {
                g.FillRectangle(bg, barBg);
            }
            float ratio = maxHp > 0 ? Math.Max(0f, Math.Min(1f, (float)hp / maxHp)) : 0f;
            if (ratio > 0f)
            {
                Color fill = isPlayer ? Color.FromArgb(62, 212, 138) : (isPoisoned ? Color.LimeGreen : Color.FromArgb(232, 68, 68));
                using (SolidBrush fb = new SolidBrush(fill))
                {
                    g.FillRectangle(fb, barBg.X, barBg.Y, barBg.Width * ratio, barBg.Height);
                }
            }
            g.DrawRectangle(Pens.Black, barBg.X, barBg.Y, barBg.Width, barBg.Height);

            using (Font hf = new Font("Segoe UI", 6.2f, FontStyle.Bold))
            {
                g.DrawString(hp.ToString(CultureInfo.InvariantCulture) + "/" + maxHp.ToString(CultureInfo.InvariantCulture), hf, Brushes.White, tileRect.Right - 26, tileRect.Y + 19);
            }
        }

        private RectangleF GetTileRect(GridPos gp)
        {
            float tileSize = 82f;
            return new RectangleF(_gridRect.X + gp.X * tileSize, _gridRect.Y + gp.Y * tileSize, tileSize, tileSize);
        }

        private void RenderRightWaveAndCombatLog(Graphics g)
        {
            // Wave Progress & Roster
            RectangleF waveBox = new RectangleF(832, 58, 434, 92);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(24, 30, 42)))
            using (Pen bp = new Pen(Color.FromArgb(56, 68, 90), 1.4f))
            {
                VectorArtRenderer.FillRoundedRect(g, bg, waveBox, 8f);
                VectorArtRenderer.DrawRoundedRect(g, bp, waveBox, 8f);
            }

            using (Font hf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font sf = new Font("Segoe UI", 8.0f, FontStyle.Regular))
            {
                g.DrawString("LEVEL 1 PROGRESS", hf, Brushes.LightSkyBlue, waveBox.X + 12, waveBox.Y + 8);
                g.DrawString("Enemies Left: " + _engine.Enemies.Count.ToString(CultureInfo.InvariantCulture), hf, Brushes.Orange, waveBox.Right - 122, waveBox.Y + 8);

                for (int w = 1; w <= 10; w++)
                {
                    float wx = waveBox.X + 12 + (w - 1) * 41.2f;
                    RectangleF wr = new RectangleF(wx, waveBox.Y + 34, 36, 26);
                    Color wc = Color.FromArgb(36, 44, 58);
                    if (w < _engine.CurrentWave)
                    {
                        wc = Color.FromArgb(46, 132, 78);
                    }
                    else if (w == _engine.CurrentWave)
                    {
                        wc = (w == 10) ? Color.FromArgb(196, 54, 48) : Color.FromArgb(54, 134, 212);
                    }
                    using (SolidBrush wb = new SolidBrush(wc))
                    {
                        VectorArtRenderer.FillRoundedRect(g, wb, wr, 5f);
                    }
                    string lbl = (w == 10) ? "BOSS" : w.ToString(CultureInfo.InvariantCulture);
                    using (Font wf = new Font("Segoe UI", 7.2f, FontStyle.Bold))
                    using (StringFormat wsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(lbl, wf, Brushes.White, wr, wsf);
                    }
                    if (w == 2 || w == 6)
                    {
                        g.DrawString("CARD", new Font("Segoe UI", 5.8f, FontStyle.Bold), Brushes.LightGreen, wr.X + 4, wr.Bottom + 2);
                    }
                    else if (w == 4 || w == 8)
                    {
                        g.DrawString("UPG", new Font("Segoe UI", 5.8f, FontStyle.Bold), Brushes.Gold, wr.X + 7, wr.Bottom + 2);
                    }
                }
            }

            // Live Combat Log (Fixed 8 Entries, No Scrollbars)
            RectangleF logBox = new RectangleF(832, 160, 434, 308);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 25, 36)))
            using (Pen bp = new Pen(Color.FromArgb(56, 68, 90), 1.4f))
            {
                VectorArtRenderer.FillRoundedRect(g, bg, logBox, 8f);
                VectorArtRenderer.DrawRoundedRect(g, bp, logBox, 8f);
            }

            using (Font hf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font lf = new Font("Segoe UI", 8.0f, FontStyle.Regular))
            {
                g.DrawString("LIVE COMBAT LOG (Latest 8 Entries — Full log saved to Logs/)", hf, Brushes.LightSkyBlue, logBox.X + 12, logBox.Y + 8);
                using (Pen div = new Pen(Color.FromArgb(46, 56, 74), 1f))
                {
                    g.DrawLine(div, logBox.X + 10, logBox.Y + 30, logBox.Right - 10, logBox.Y + 30);
                }

                float ly = logBox.Y + 36;
                List<CombatLogEntry> entries = _engine.Logger.RecentEntries;
                for (int i = 0; i < entries.Count; i++)
                {
                    CombatLogEntry entry = entries[i];
                    RectangleF row = new RectangleF(logBox.X + 10, ly, logBox.Width - 20, 32);
                    if (i % 2 == 1)
                    {
                        using (SolidBrush stripe = new SolidBrush(Color.FromArgb(26, 32, 44)))
                        {
                            g.FillRectangle(stripe, row);
                        }
                    }
                    using (SolidBrush tb = new SolidBrush(entry.Color))
                    {
                        g.DrawString("[" + entry.Timestamp + "] " + entry.Text, lf, tb, row);
                    }
                    ly += 33.5f;
                }
            }
        }

        private void RenderBottomCardDock(Graphics g)
        {
            // Hand Panel
            RectangleF handBox = new RectangleF(14, 478, 608, 232);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(22, 28, 40)))
            using (Pen bp = new Pen(Color.FromArgb(54, 66, 88), 1.4f))
            {
                VectorArtRenderer.FillRoundedRect(g, bg, handBox, 8f);
                VectorArtRenderer.DrawRoundedRect(g, bp, handBox, 8f);
            }

            using (Font hf = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            {
                g.DrawString("HAND  (Click available card to Equip — 1 Turn)", hf, Brushes.LightSteelBlue, handBox.X + 12, handBox.Y + 7);
            }

            for (int i = 0; i < _engine.Hand.Count; i++)
            {
                CardInstance c = _engine.Hand[i];
                WeaponDef wdef = _engine.Db.Weapons[c.WeaponId];
                RectangleF cr = new RectangleF(handBox.X + 10 + i * 148, handBox.Y + 32, 142, 192);
                bool canEquip = _engine.Phase == GamePhase.PlayerTurn && c.CurrentCooldown == 0;
                bool hov = IsMouseOver(cr);
                if (hov)
                {
                    _hoveredCard = c;
                }
                VectorArtRenderer.DrawPhysicalCard(g, wdef, c, _engine.Db, _engine.Profile, cr, hov && canEquip, false, false, 0, false);
                if (canEquip)
                {
                    RegisterClick(cr, "EquipCard", i);
                }
            }

            // Equipped Queue Panel (with Drag & Drop reordering)
            RectangleF eqBox = new RectangleF(632, 478, 484, 232);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(24, 30, 44)))
            using (Pen bp = new Pen(_engine.Phase == GamePhase.AimingCard ? Color.Gold : Color.FromArgb(62, 78, 104), _engine.Phase == GamePhase.AimingCard ? 2.2f : 1.4f))
            {
                VectorArtRenderer.FillRoundedRect(g, bg, eqBox, 8f);
                VectorArtRenderer.DrawRoundedRect(g, bp, eqBox, 8f);
            }

            using (Font hf = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            {
                string eqTitle = _engine.Phase == GamePhase.AimingCard
                    ? "EXECUTING QUEUE — Aim highlighted card on grid!"
                    : "EQUIPPED QUEUE  (Drag & Drop to Reorder — 0 Turns)";
                g.DrawString(eqTitle, hf, _engine.Phase == GamePhase.AimingCard ? Brushes.Gold : Brushes.LightSkyBlue, eqBox.X + 12, eqBox.Y + 7);
            }

            if (_engine.EquippedQueue.Count == 0)
            {
                using (Font ef = new Font("Segoe UI", 9.5f, FontStyle.Italic))
                using (StringFormat esf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("Equip cards from your Hand to build a combo queue.", ef, Brushes.SlateGray, eqBox, esf);
                }
            }
            else
            {
                for (int i = 0; i < _engine.EquippedQueue.Count; i++)
                {
                    CardInstance c = _engine.EquippedQueue[i];
                    WeaponDef wdef = _engine.Db.Weapons[c.WeaponId];
                    RectangleF cr = GetEquippedSlotRect(i);
                    bool isExec = _engine.Phase == GamePhase.AimingCard && _engine.ExecutingCardIndex == i;
                    bool isDropTarget = _isDraggingQueueCard && _dragHoverIndex == i && _dragFromIndex != i;
                    bool hov = IsMouseOver(cr);
                    if (hov)
                    {
                        _hoveredCard = c;
                    }
                    if (!(_isDraggingQueueCard && _dragFromIndex == i))
                    {
                        VectorArtRenderer.DrawPhysicalCard(g, wdef, c, _engine.Db, _engine.Profile, cr, hov && _engine.Phase == GamePhase.PlayerTurn, isDropTarget, isExec, i + 1, true);
                    }
                    else
                    {
                        using (Pen dash = new Pen(Color.LightSkyBlue, 2f) { DashStyle = DashStyle.Dash })
                        {
                            VectorArtRenderer.DrawRoundedRect(g, dash, cr, 8f);
                        }
                    }
                }
            }

            // Action Buttons Column
            float bx = 1126f;
            float bw = 140f;
            if (_engine.Phase == GamePhase.AimingCard)
            {
                DrawButton(g, new RectangleF(bx, 480, bw, 68), "SKIP CARD", Color.FromArgb(165, 92, 34), true, "SkipAimCard", 0, null, "Skip current aim");
                DrawButton(g, new RectangleF(bx, 558, bw, 68), "CONFIRM AIM", Color.FromArgb(46, 152, 86), true, "ConfirmAimCard", 0, null, "Or click grid");
                DrawButton(g, new RectangleF(bx, 636, bw, 72), "WAIT", Color.FromArgb(62, 74, 92), false, "WaitTurn", 0, null, "Finish aim first");
            }
            else
            {
                bool canPlay = _engine.Phase == GamePhase.PlayerTurn && _engine.EquippedQueue.Count > 0;
                DrawButton(g, new RectangleF(bx, 480, bw, 68), "PLAY QUEUE", Color.FromArgb(44, 154, 86), canPlay, "PlayQueue", 0, null, _engine.EquippedQueue.Count.ToString(CultureInfo.InvariantCulture) + " card(s) • 1 Turn");

                bool canWait = _engine.Phase == GamePhase.PlayerTurn;
                string waitSub = _engine.Profile.UnlockedUpgrades.Contains("tactical_master") ? "1 Turn (-1 CD)" : "Skip Turn";
                DrawButton(g, new RectangleF(bx, 558, bw, 68), "WAIT", Color.FromArgb(58, 92, 138), canWait, "WaitTurn", 0, null, waitSub);

                bool canSpec = _engine.Phase == GamePhase.PlayerTurn && _engine.Player.SpecialCooldown == 0;
                CharacterDef cdef = _engine.Db.Characters[_engine.Player.CharacterId];
                string specSub = _engine.Player.SpecialCooldown == 0 ? cdef.SpecialName : ("CD: " + _engine.Player.SpecialCooldown.ToString(CultureInfo.InvariantCulture) + " Turns");
                DrawButton(g, new RectangleF(bx, 636, bw, 72), "SPECIAL", Color.FromArgb(132, 64, 186), canSpec, "UseSpecial", 0, null, specSub);
            }

            // Dragging Ghost Card on top of everything
            if (_isDraggingQueueCard && _dragFromIndex >= 0 && _dragFromIndex < _engine.EquippedQueue.Count)
            {
                CardInstance dc = _engine.EquippedQueue[_dragFromIndex];
                WeaponDef dw = _engine.Db.Weapons[dc.WeaponId];
                RectangleF ghost = new RectangleF(_mousePos.X - 57, _mousePos.Y - 96, 114, 192);
                VectorArtRenderer.DrawPhysicalCard(g, dw, dc, _engine.Db, _engine.Profile, ghost, true, true, false, _dragFromIndex + 1, true);
            }
        }

        private RectangleF GetEquippedSlotRect(int index)
        {
            return new RectangleF(642 + index * 118, 510, 114, 192);
        }

        private void RenderStartingDraftOverlay(Graphics g)
        {
            DrawModalBackdrop(g);
            RectangleF modal = new RectangleF(200, 110, 880, 495);
            DrawModalFrame(g, modal, "STARTING HAND DRAFT — CHOOSE 2 OF 4 CARDS", "Select 2 cards from your unlocked pool to begin Wave 1.");

            for (int i = 0; i < _engine.Reward.OfferedWeaponIds.Count; i++)
            {
                string wid = _engine.Reward.OfferedWeaponIds[i];
                WeaponDef w = _engine.Db.Weapons[wid];
                RectangleF cr = new RectangleF(modal.X + 46 + i * 204, modal.Y + 96, 176, 260);
                bool sel = _engine.Reward.StartingDraftSelectedIndices.Contains(i);
                bool hov = IsMouseOver(cr);
                VectorArtRenderer.DrawPhysicalCard(g, w, null, _engine.Db, _engine.Profile, cr, hov, sel, false, sel ? (_engine.Reward.StartingDraftSelectedIndices.IndexOf(i) + 1) : 0, false);
                RegisterClick(cr, "ToggleDraftCard", i);
            }

            int selCount = _engine.Reward.StartingDraftSelectedIndices.Count;
            DrawButton(g, new RectangleF(modal.X + 300, modal.Bottom - 86, 280, 54), "CONFIRM STARTING HAND (" + selCount.ToString(CultureInfo.InvariantCulture) + "/2)", Color.FromArgb(44, 152, 86), selCount == 2, "ConfirmDraft");
        }

        private void RenderPickCardOverlay(Graphics g)
        {
            DrawModalBackdrop(g);
            RectangleF modal = new RectangleF(260, 105, 760, 510);
            DrawModalFrame(g, modal, "REWARD — PICK A CARD (WAVE " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + " CLEARED!)", "All card & Special cooldowns have been reset to 0!");

            for (int i = 0; i < _engine.Reward.OfferedWeaponIds.Count; i++)
            {
                string wid = _engine.Reward.OfferedWeaponIds[i];
                WeaponDef w = _engine.Db.Weapons[wid];
                RectangleF cr = new RectangleF(modal.X + 148 + i * 268, modal.Y + 92, 196, 272);
                bool hov = IsMouseOver(cr);
                VectorArtRenderer.DrawPhysicalCard(g, w, null, _engine.Db, _engine.Profile, cr, hov, false, false, 0, false);
                DrawButton(g, new RectangleF(cr.X + 12, cr.Bottom + 12, cr.Width - 24, 34), "PICK CARD", Color.FromArgb(46, 152, 86), true, "PickRewardCard", i);
            }

            bool isFree = _engine.Player.FreeRerollsLeft > 0;
            bool canReroll = isFree || _engine.Profile.Coins >= _engine.Reward.RerollCost;
            string rerollLbl = isFree ? "REROLL (FREE!)" : ("REROLL (" + _engine.Reward.RerollCost.ToString(CultureInfo.InvariantCulture) + " COINS)");
            DrawButton(g, new RectangleF(modal.X + 170, modal.Bottom - 62, 200, 42), rerollLbl, Color.FromArgb(182, 126, 38), canReroll, "RerollReward");
            DrawButton(g, new RectangleF(modal.X + 390, modal.Bottom - 62, 200, 42), "SKIP REWARD", Color.FromArgb(78, 88, 106), true, "SkipReward");
        }

        private void RenderUpgradeOverlay(Graphics g)
        {
            DrawModalBackdrop(g);
            RectangleF modal = new RectangleF(180, 68, 920, 588);
            DrawModalFrame(g, modal, "REWARD — UPGRADE A CARD (WAVE " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + " CLEARED!)", "All cooldowns reset! Step 1: Select a modifier. Step 2: Click one of your cards below to attach it.");

            // Step 1: Offered Modifiers
            for (int i = 0; i < _engine.Reward.OfferedModifiers.Count; i++)
            {
                ModifierType mt = _engine.Reward.OfferedModifiers[i];
                bool sel = _engine.Reward.SelectedModifier.HasValue && _engine.Reward.SelectedModifier.Value == mt;
                RectangleF mr = new RectangleF(modal.X + 150 + i * 320, modal.Y + 86, 300, 92);
                using (SolidBrush mb = new SolidBrush(sel ? Color.FromArgb(46, 78, 56) : Color.FromArgb(26, 34, 48)))
                using (Pen mp = new Pen(sel ? Color.LightGreen : Color.FromArgb(72, 88, 114), sel ? 2.6f : 1.5f))
                {
                    VectorArtRenderer.FillRoundedRect(g, mb, mr, 8f);
                    VectorArtRenderer.DrawRoundedRect(g, mp, mr, 8f);
                }
                using (Font mf1 = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (Font mf2 = new Font("Segoe UI", 8.2f, FontStyle.Regular))
                {
                    g.DrawString(GameEngine.GetModifierLabel(mt), mf1, mt == ModifierType.Poison ? Brushes.LimeGreen : Brushes.Gold, mr.X + 14, mr.Y + 10);
                    g.DrawString(GameEngine.GetModifierDescription(mt), mf2, Brushes.Gainsboro, new RectangleF(mr.X + 14, mr.Y + 38, mr.Width - 28, 48));
                }
                RegisterClick(mr, "SelectModifier", i);
            }

            // Step 2: Player's Cards (Hand + Equipped)
            List<CardInstance> allCards = _engine.Hand.Concat(_engine.EquippedQueue).ToList();
            using (Font sf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                string step2 = _engine.Reward.SelectedModifier.HasValue
                    ? "STEP 2: CLICK A COMPATIBLE CARD BELOW TO ATTACH [" + GameEngine.GetModifierLabel(_engine.Reward.SelectedModifier.Value) + "]"
                    : "STEP 2: SELECT A MODIFIER ABOVE FIRST";
                g.DrawString(step2, sf, _engine.Reward.SelectedModifier.HasValue ? Brushes.LightGreen : Brushes.Silver, modal.X + 36, modal.Y + 194);
            }

            float startX = modal.X + (modal.Width - (allCards.Count * 170 - 14)) * 0.5f;
            for (int i = 0; i < allCards.Count; i++)
            {
                CardInstance c = allCards[i];
                WeaponDef w = _engine.Db.Weapons[c.WeaponId];
                RectangleF cr = new RectangleF(startX + i * 170, modal.Y + 224, 156, 224);
                bool compatible = true;
                if (_engine.Reward.SelectedModifier.HasValue)
                {
                    ModifierType selM = _engine.Reward.SelectedModifier.Value;
                    if ((selM == ModifierType.DamagePlus1 || selM == ModifierType.DamagePlus2 || selM == ModifierType.Poison) && w.Category == CardCategory.Movement)
                    {
                        compatible = false;
                    }
                }
                bool clickable = _engine.Reward.SelectedModifier.HasValue && compatible;
                bool hov = clickable && IsMouseOver(cr);
                VectorArtRenderer.DrawPhysicalCard(g, w, c, _engine.Db, _engine.Profile, cr, hov, false, false, 0, false);

                if (!compatible)
                {
                    using (SolidBrush dis = new SolidBrush(Color.FromArgb(140, 16, 18, 24)))
                    {
                        VectorArtRenderer.FillRoundedRect(g, dis, cr, 9f);
                    }
                    using (Font wf = new Font("Segoe UI", 8f, FontStyle.Bold))
                    using (StringFormat wsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("NOT VALID FOR\nMOVEMENT CARD", wf, Brushes.IndianRed, cr, wsf);
                    }
                }
                else if (clickable)
                {
                    RegisterClick(cr, "AttachUpgradeCard", i, c.InstanceUid);
                }
            }

            bool isFree = _engine.Player.FreeRerollsLeft > 0;
            bool canReroll = isFree || _engine.Profile.Coins >= _engine.Reward.RerollCost;
            string rerollLbl = isFree ? "REROLL (FREE!)" : ("REROLL (" + _engine.Reward.RerollCost.ToString(CultureInfo.InvariantCulture) + " COINS)");
            DrawButton(g, new RectangleF(modal.X + 250, modal.Bottom - 62, 200, 42), rerollLbl, Color.FromArgb(182, 126, 38), canReroll, "RerollReward");
            DrawButton(g, new RectangleF(modal.X + 470, modal.Bottom - 62, 200, 42), "SKIP REWARD", Color.FromArgb(78, 88, 106), true, "SkipReward");
        }

        private void RenderEndRunOverlay(Graphics g)
        {
            DrawModalBackdrop(g);
            bool won = _engine.Phase == GamePhase.Victory;
            RectangleF modal = new RectangleF(330, 140, 620, 430);
            DrawModalFrame(g, modal, won ? "VICTORY — LEVEL 1 CONQUERED!" : "DEFEAT — FALLEN IN BATTLE", won ? "You defeated the Viking Chief! Area 2 expansion hook unlocked." : "Your run has ended on Wave " + _engine.CurrentWave.ToString(CultureInfo.InvariantCulture) + ".");

            CharacterDef cdef = _engine.Db.Characters[_engine.Player.CharacterId];
            VectorArtRenderer.DrawCharacterPortrait(g, cdef.Id, new RectangleF(modal.X + 245, modal.Y + 92, 130, 120), false);

            RectangleF statBox = new RectangleF(modal.X + 60, modal.Y + 220, modal.Width - 120, 118);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(20, 26, 38)))
            {
                VectorArtRenderer.FillRoundedRect(g, sb, statBox, 8f);
            }
            using (Font sf = new Font("Segoe UI", 10.5f, FontStyle.Bold))
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

        private void DrawModalBackdrop(Graphics g)
        {
            using (SolidBrush veil = new SolidBrush(Color.FromArgb(182, 8, 10, 16)))
            {
                g.FillRectangle(veil, 0, 0, Width, Height);
            }
        }

        private void DrawModalFrame(Graphics g, RectangleF r, string title, string subtitle)
        {
            using (LinearGradientBrush bg = new LinearGradientBrush(r, Color.FromArgb(30, 38, 54), Color.FromArgb(18, 24, 34), 90f))
            {
                VectorArtRenderer.FillRoundedRect(g, bg, r, 12f);
            }
            using (Pen bp = new Pen(Color.FromArgb(95, 138, 188), 2.2f))
            {
                VectorArtRenderer.DrawRoundedRect(g, bp, r, 12f);
            }
            using (Font tf = new Font("Segoe UI", 15f, FontStyle.Bold))
            using (Font sf = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (StringFormat ctr = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString(title, tf, Brushes.White, new RectangleF(r.X + 16, r.Y + 18, r.Width - 32, 30), ctr);
                g.DrawString(subtitle, sf, Brushes.LightSkyBlue, new RectangleF(r.X + 16, r.Y + 50, r.Width - 32, 24), ctr);
            }
        }

        private void RenderAchievementsAndStats(Graphics g)
        {
            // Header
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(24, 30, 42)))
            {
                g.FillRectangle(hb, 0, 0, Width, 52);
            }
            using (Pen div = new Pen(Color.FromArgb(56, 70, 94), 1.5f))
            {
                g.DrawLine(div, 0, 52, Width, 52);
            }
            using (Font tf = new Font("Segoe UI", 16f, FontStyle.Bold))
            {
                g.DrawString("ACHIEVEMENTS & LIFETIME STATISTICS", tf, Brushes.White, 22, 11);
            }
            using (Font cf = new Font("Segoe UI", 12f, FontStyle.Bold))
            {
                g.DrawString("COINS: " + _engine.Profile.Coins.ToString(CultureInfo.InvariantCulture) + "   |   UNLOCKED: " + _engine.Profile.CompletedAchievements.Count.ToString(CultureInfo.InvariantCulture) + " / 10", cf, Brushes.Gold, 740, 15);
            }
            DrawButton(g, new RectangleF(1088, 9, 170, 34), "BACK TO MENU", Color.FromArgb(56, 108, 178), true, "ReturnToMenu");

            // Top Panel: Lifetime Statistics (Compact 4-column summary, zero scrolling)
            RectangleF statsBox = new RectangleF(20, 62, 1240, 166);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(22, 28, 40)))
            using (Pen sp = new Pen(Color.FromArgb(56, 70, 94), 1.5f))
            {
                VectorArtRenderer.FillRoundedRect(g, sb, statsBox, 9f);
                VectorArtRenderer.DrawRoundedRect(g, sp, statsBox, 9f);
            }

            StatsData st = _engine.Stats;
            using (Font hFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Font lFont = new Font("Segoe UI", 8.8f, FontStyle.Regular))
            using (Font vFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            {
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
                int pKills = st.EnemiesDefeatedByType.ContainsKey("pawn") ? st.EnemiesDefeatedByType["pawn"] : 0;
                int aKills = st.EnemiesDefeatedByType.ContainsKey("archer") ? st.EnemiesDefeatedByType["archer"] : 0;
                int sKills = st.EnemiesDefeatedByType.ContainsKey("spearman") ? st.EnemiesDefeatedByType["spearman"] : 0;
                int shKills = st.EnemiesDefeatedByType.ContainsKey("shaman") ? st.EnemiesDefeatedByType["shaman"] : 0;
                int bKills = st.EnemiesDefeatedByType.ContainsKey("viking_chief") ? st.EnemiesDefeatedByType["viking_chief"] : 0;
                DrawStatRow(g, 985, 96, "Pawns:", pKills.ToString(CultureInfo.InvariantCulture), lFont, vFont);
                DrawStatRow(g, 985, 118, "Archers:", aKills.ToString(CultureInfo.InvariantCulture), lFont, vFont);
                DrawStatRow(g, 985, 140, "Spearmen:", sKills.ToString(CultureInfo.InvariantCulture), lFont, vFont);
                DrawStatRow(g, 985, 162, "Shamans:", shKills.ToString(CultureInfo.InvariantCulture), lFont, vFont);
                DrawStatRow(g, 985, 184, "Viking Chiefs (Boss):", bKills.ToString(CultureInfo.InvariantCulture), lFont, vFont);
            }

            // Bottom Panel: 2x5 Achievements Grid (Fits 10 cards on one screen with zero scrollbars!)
            float gridStartX = 20f;
            float gridStartY = 240f;
            float cardW = 238f;
            float cardH = 224f;
            float gapX = 12.5f;
            float gapY = 14f;

            for (int i = 0; i < Math.Min(10, _engine.Db.AchievementList.Count); i++)
            {
                AchievementDef ach = _engine.Db.AchievementList[i];
                int col = i % 5;
                int row = i / 5;
                RectangleF cr = new RectangleF(gridStartX + col * (cardW + gapX), gridStartY + row * (cardH + gapY), cardW, cardH);

                bool done = _engine.Profile.CompletedAchievements.Contains(ach.Id);
                int prog = _engine.Profile.AchievementProgress.ContainsKey(ach.Id) ? _engine.Profile.AchievementProgress[ach.Id] : 0;
                if (done)
                {
                    prog = ach.TargetValue;
                }

                Color bg1 = done ? Color.FromArgb(42, 40, 26) : Color.FromArgb(24, 29, 40);
                Color bg2 = done ? Color.FromArgb(26, 24, 18) : Color.FromArgb(18, 22, 30);
                using (LinearGradientBrush gb = new LinearGradientBrush(cr, bg1, bg2, 90f))
                {
                    VectorArtRenderer.FillRoundedRect(g, gb, cr, 9f);
                }
                using (Pen bp = new Pen(done ? Color.Gold : Color.FromArgb(58, 68, 86), done ? 2.0f : 1.3f))
                {
                    VectorArtRenderer.DrawRoundedRect(g, bp, cr, 9f);
                }

                // Trophy Icon & Status Badge
                VectorArtRenderer.DrawTrophyIcon(g, new RectangleF(cr.X + 12, cr.Y + 12, 44, 44), done);
                RectangleF stTag = new RectangleF(cr.Right - 88, cr.Y + 14, 76, 20);
                using (SolidBrush stb = new SolidBrush(done ? Color.FromArgb(54, 148, 76) : Color.FromArgb(56, 62, 74)))
                {
                    VectorArtRenderer.FillRoundedRect(g, stb, stTag, 4f);
                }
                using (Font tagF = new Font("Segoe UI", 7.2f, FontStyle.Bold))
                using (StringFormat csf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(done ? "UNLOCKED" : "LOCKED", tagF, done ? Brushes.White : Brushes.LightGray, stTag, csf);
                }

                using (Font tf = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (Font df = new Font("Segoe UI", 8.0f, FontStyle.Regular))
                using (Font rf = new Font("Segoe UI", 7.8f, FontStyle.Bold))
                {
                    g.DrawString(ach.Title, tf, done ? Brushes.Gold : Brushes.White, new RectangleF(cr.X + 12, cr.Y + 62, cr.Width - 24, 22));
                    g.DrawString(ach.Description, df, Brushes.Gainsboro, new RectangleF(cr.X + 12, cr.Y + 86, cr.Width - 24, 56));

                    // Progress bar
                    RectangleF pBar = new RectangleF(cr.X + 12, cr.Y + 148, cr.Width - 24, 18);
                    using (SolidBrush pbg = new SolidBrush(Color.FromArgb(14, 18, 26)))
                    {
                        VectorArtRenderer.FillRoundedRect(g, pbg, pBar, 4f);
                    }
                    float ratio = ach.TargetValue > 0 ? Math.Min(1f, (float)prog / ach.TargetValue) : 0f;
                    if (ratio > 0f)
                    {
                        using (SolidBrush pf = new SolidBrush(done ? Color.Gold : Color.FromArgb(62, 152, 224)))
                        {
                            VectorArtRenderer.FillRoundedRect(g, pf, new RectangleF(pBar.X, pBar.Y, pBar.Width * ratio, pBar.Height), 4f);
                        }
                    }
                    using (StringFormat csf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(prog.ToString(CultureInfo.InvariantCulture) + " / " + ach.TargetValue.ToString(CultureInfo.InvariantCulture), rf, done ? Brushes.Black : Brushes.White, pBar, csf);
                    }

                    // Reward line
                    string rew = "Reward: " + ach.CoinReward.ToString(CultureInfo.InvariantCulture) + " Coins";
                    if (!string.IsNullOrEmpty(ach.SpecialUnlock) && ach.SpecialUnlock != "None")
                    {
                        rew += " + " + ach.SpecialUnlock;
                    }
                    g.DrawString(rew, rf, Brushes.Gold, new RectangleF(cr.X + 12, cr.Bottom - 36, cr.Width - 24, 28));
                }
            }
        }

        private void DrawStatRow(Graphics g, float x, float y, string label, string value, Font lFont, Font vFont)
        {
            g.DrawString(label, lFont, Brushes.LightSteelBlue, x, y);
            g.DrawString(value, vFont, Brushes.White, x + 178, y);
        }

        private void RenderToastBanner(Graphics g)
        {
            if (_engine.ActiveToast == null)
            {
                return;
            }
            ToastMessage t = _engine.ActiveToast;
            RectangleF tr = new RectangleF(Width * 0.5f - 220, 12, 440, 56);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(238, 22, 28, 40)))
            using (Pen bp = new Pen(t.AccentColor, 2.4f))
            {
                VectorArtRenderer.FillRoundedRect(g, bg, tr, 10f);
                VectorArtRenderer.DrawRoundedRect(g, bp, tr, 10f);
            }
            VectorArtRenderer.DrawTrophyIcon(g, new RectangleF(tr.X + 12, tr.Y + 8, 40, 40), true);
            using (Font f1 = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Font f2 = new Font("Segoe UI", 8.8f, FontStyle.Regular))
            using (SolidBrush b1 = new SolidBrush(t.AccentColor))
            {
                g.DrawString(t.Title, f1, b1, tr.X + 60, tr.Y + 8);
                g.DrawString(t.Subtitle, f2, Brushes.White, tr.X + 60, tr.Y + 30);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.Location;

            if (_screen == ScreenMode.Gameplay)
            {
                UpdateHoveredGridTile();

                if (_engine.Phase == GamePhase.AimingCard && _hoveredGridTile.HasValue)
                {
                    _engine.UpdateAimPreviewFromHoverTile(_hoveredGridTile.Value);
                }

                if (_dragFromIndex >= 0)
                {
                    if (!_isDraggingQueueCard && (Math.Abs(e.X - _dragStartPoint.X) > 6 || Math.Abs(e.Y - _dragStartPoint.Y) > 6))
                    {
                        _isDraggingQueueCard = true;
                    }
                    if (_isDraggingQueueCard)
                    {
                        _dragHoverIndex = GetEquippedIndexAtPoint(e.Location);
                    }
                }
            }
            Invalidate();
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

            // Begin drag if clicking an equipped queue card during PlayerTurn
            if (_screen == ScreenMode.Gameplay && _engine.Phase == GamePhase.PlayerTurn)
            {
                int eqIdx = GetEquippedIndexAtPoint(e.Location);
                if (eqIdx >= 0)
                {
                    _dragFromIndex = eqIdx;
                    _dragHoverIndex = eqIdx;
                    _dragStartPoint = e.Location;
                    _isDraggingQueueCard = false;
                    return;
                }
            }

            // Check registered UI buttons & card clicks
            for (int i = _clickRegions.Count - 1; i >= 0; i--)
            {
                ClickRegion reg = _clickRegions[i];
                if (reg.Bounds.Contains(e.Location))
                {
                    HandleClickAction(reg);
                    Invalidate();
                    return;
                }
            }

            // Grid click interactions
            if (_screen == ScreenMode.Gameplay && _hoveredGridTile.HasValue)
            {
                GridPos clicked = _hoveredGridTile.Value;
                if (_engine.Phase == GamePhase.AimingCard)
                {
                    _engine.UpdateAimPreviewFromHoverTile(clicked);
                    _engine.ConfirmAimedCard();
                    Invalidate();
                }
                else if (_engine.Phase == GamePhase.PlayerTurn)
                {
                    int dx = clicked.X - _engine.Player.Pos.X;
                    int dy = clicked.Y - _engine.Player.Pos.Y;
                    if (Math.Abs(dx) + Math.Abs(dy) == 1)
                    {
                        _engine.TryMovePlayer(dx, dy);
                        Invalidate();
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingQueueCard && _dragFromIndex >= 0)
            {
                int targetIdx = GetEquippedIndexAtPoint(e.Location);
                if (targetIdx >= 0 && targetIdx != _dragFromIndex)
                {
                    _engine.ReorderEquippedCards(_dragFromIndex, targetIdx);
                }
            }
            _dragFromIndex = -1;
            _dragHoverIndex = -1;
            _isDraggingQueueCard = false;
            Invalidate();
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
            if (_gridRect.Contains(_mousePos))
            {
                int gx = (int)((_mousePos.X - _gridRect.X) / 82f);
                int gy = (int)((_mousePos.Y - _gridRect.Y) / 82f);
                GridPos gp = new GridPos(gx, gy);
                if (gp.IsInBounds())
                {
                    _hoveredGridTile = gp;
                    return;
                }
            }
            _hoveredGridTile = null;
        }

        private void HandleClickAction(ClickRegion reg)
        {
            string act = reg.ActionId;
            if (act == "DismissNotification")
            {
                _engine.MenuNotification = null;
            }
            else if (act == "SelectHero")
            {
                _selectedCharacterId = reg.Tag;
            }
            else if (act == "StartNewRun")
            {
                string heroId = string.IsNullOrEmpty(reg.Tag) ? _selectedCharacterId : reg.Tag;
                _engine.StartNewRun(heroId);
                _screen = ScreenMode.Gameplay;
            }
            else if (act == "ContinueRun")
            {
                if (_engine.Saves.TryLoadActiveRun(_engine, _engine.Db, out string err))
                {
                    _engine.MenuNotification = null;
                    _screen = ScreenMode.Gameplay;
                }
                else
                {
                    _engine.MenuNotification = err;
                }
            }
            else if (act == "OpenAchievements")
            {
                _screen = ScreenMode.AchievementsAndStats;
            }
            else if (act == "ReturnToMenu")
            {
                _screen = ScreenMode.MainMenu;
            }
            else if (act == "BuyWeapon")
            {
                _engine.TryBuyWeaponUnlock(reg.Tag);
            }
            else if (act == "BuyUpgrade")
            {
                _engine.TryBuyUpgradeUnlock(reg.Tag);
            }
            else if (act == "SaveAndExit")
            {
                _engine.Saves.SaveActiveRun(_engine);
                _screen = ScreenMode.MainMenu;
            }
            else if (act == "EquipCard")
            {
                _engine.TryEquipHandCard(reg.Index);
            }
            else if (act == "PlayQueue")
            {
                _engine.BeginPlayEquippedQueue();
            }
            else if (act == "WaitTurn")
            {
                _engine.WaitTurn();
            }
            else if (act == "UseSpecial")
            {
                _engine.TryUseSpecial();
            }
            else if (act == "SkipAimCard")
            {
                _engine.SkipCurrentAimedCard();
            }
            else if (act == "ConfirmAimCard")
            {
                _engine.ConfirmAimedCard();
            }
            else if (act == "ToggleDraftCard")
            {
                _engine.ToggleStartingDraftCard(reg.Index);
            }
            else if (act == "ConfirmDraft")
            {
                _engine.ConfirmStartingDraft();
            }
            else if (act == "PickRewardCard")
            {
                _engine.PickRewardCard(reg.Index);
            }
            else if (act == "SelectModifier")
            {
                _engine.SelectUpgradeModifier(reg.Index);
            }
            else if (act == "AttachUpgradeCard")
            {
                CardInstance target = _engine.Hand.Concat(_engine.EquippedQueue).FirstOrDefault(c => c.InstanceUid == reg.Tag);
                if (target != null)
                {
                    _engine.TryAttachSelectedModifierToCard(target);
                }
            }
            else if (act == "RerollReward")
            {
                _engine.TryRerollReward();
            }
            else if (act == "SkipReward")
            {
                _engine.SkipReward();
            }
        }

        public void HandleKeyInput(Keys keyCode)
        {
            if (_screen != ScreenMode.Gameplay)
            {
                if (_screen == ScreenMode.AchievementsAndStats && keyCode == Keys.Escape)
                {
                    _screen = ScreenMode.MainMenu;
                    Invalidate();
                }
                return;
            }

            if (_engine.Phase == GamePhase.PlayerTurn)
            {
                if (keyCode == Keys.W || keyCode == Keys.Up)
                {
                    _engine.TryMovePlayer(0, -1);
                }
                else if (keyCode == Keys.S || keyCode == Keys.Down)
                {
                    _engine.TryMovePlayer(0, 1);
                }
                else if (keyCode == Keys.A || keyCode == Keys.Left)
                {
                    _engine.TryMovePlayer(-1, 0);
                }
                else if (keyCode == Keys.D || keyCode == Keys.Right)
                {
                    _engine.TryMovePlayer(1, 0);
                }
                else if (keyCode == Keys.Space)
                {
                    _engine.WaitTurn();
                }
                else if (keyCode == Keys.E)
                {
                    _engine.TryUseSpecial();
                }
                else if (keyCode == Keys.Enter)
                {
                    _engine.BeginPlayEquippedQueue();
                }
                else if (keyCode == Keys.D1)
                {
                    _engine.TryEquipHandCard(0);
                }
                else if (keyCode == Keys.D2)
                {
                    _engine.TryEquipHandCard(1);
                }
                else if (keyCode == Keys.D3)
                {
                    _engine.TryEquipHandCard(2);
                }
                else if (keyCode == Keys.D4)
                {
                    _engine.TryEquipHandCard(3);
                }
                Invalidate();
            }
            else if (_engine.Phase == GamePhase.AimingCard)
            {
                CardInstance aimCard = _engine.GetCurrentlyAimingCard();
                if (aimCard == null)
                {
                    return;
                }
                WeaponDef wdef = _engine.Db.Weapons[aimCard.WeaponId];
                if (keyCode == Keys.Enter || keyCode == Keys.Space)
                {
                    _engine.ConfirmAimedCard();
                }
                else if (wdef.AimMode == AimMode.Orthogonal)
                {
                    GridPos? nextDir = null;
                    if (keyCode == Keys.W || keyCode == Keys.Up) nextDir = new GridPos(0, -1);
                    else if (keyCode == Keys.S || keyCode == Keys.Down) nextDir = new GridPos(0, 1);
                    else if (keyCode == Keys.A || keyCode == Keys.Left) nextDir = new GridPos(-1, 0);
                    else if (keyCode == Keys.D || keyCode == Keys.Right) nextDir = new GridPos(1, 0);

                    if (nextDir.HasValue)
                    {
                        if (_engine.CurrentAimDir == nextDir.Value)
                        {
                            _engine.ConfirmAimedCard();
                        }
                        else
                        {
                            _engine.UpdateAimPreview(nextDir.Value);
                        }
                    }
                }
                else if (wdef.AimMode == AimMode.Diagonal)
                {
                    GridPos? nextDir = null;
                    if (keyCode == Keys.Q) nextDir = new GridPos(-1, -1);
                    else if (keyCode == Keys.E) nextDir = new GridPos(1, -1);
                    else if (keyCode == Keys.Z) nextDir = new GridPos(-1, 1);
                    else if (keyCode == Keys.C) nextDir = new GridPos(1, 1);

                    if (nextDir.HasValue)
                    {
                        if (_engine.CurrentAimDir == nextDir.Value)
                        {
                            _engine.ConfirmAimedCard();
                        }
                        else
                        {
                            _engine.UpdateAimPreview(nextDir.Value);
                        }
                    }
                }
                Invalidate();
            }
        }
    }

    public class MainForm : Form
    {
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

            _canvas = new GameCanvas(engine);
            _canvas.Dock = DockStyle.Fill;
            Controls.Add(_canvas);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            if (keyCode == Keys.Up || keyCode == Keys.Down || keyCode == Keys.Left || keyCode == Keys.Right ||
                keyCode == Keys.W || keyCode == Keys.A || keyCode == Keys.S || keyCode == Keys.D ||
                keyCode == Keys.Q || keyCode == Keys.E || keyCode == Keys.Z || keyCode == Keys.C ||
                keyCode == Keys.Space || keyCode == Keys.Enter || keyCode == Keys.Escape ||
                keyCode == Keys.D1 || keyCode == Keys.D2 || keyCode == Keys.D3 || keyCode == Keys.D4)
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
            SaveManager saves = new SaveManager(savesDir, logsDir);
            ProfileData profile = saves.LoadProfile(db);
            StatsData stats = saves.LoadStats(db);
            CombatLogger logger = new CombatLogger(logsDir);

            GameEngine engine = new GameEngine(db, profile, stats, saves, logger);

            using (MainForm form = new MainForm(engine))
            {
                Application.Run(form);
            }
        }
    }

}