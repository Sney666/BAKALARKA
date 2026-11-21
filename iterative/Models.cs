#nullable disable
using System;
using System.Collections.Generic;

namespace RogueCards
{
    public enum GameScreen
    {
        Menu,
        StartingDraft,
        Arena,
        Aiming,
        RewardCard,
        RewardUpgrade,
        Stats,
        GameOver
    }

    public enum CardCategory
    {
        Melee,
        Ranged,
        Movement,
        Special
    }

    public enum TargetingType
    {
        None,
        Self,
        Orthogonal,
        Diagonal
    }

    public enum WeaponEffectType
    {
        SingleAdjacent,
        KnockbackAdjacent,
        ArcSweep,
        LineProjectile,
        DiagonalProjectile,
        PierceTwoLine,
        PlaceBearTrap,
        HarpoonPull,
        DiagonalDash,
        SlideTwo
    }

    public enum EnemyType
    {
        Pawn,
        Archer,
        Spearman,
        Shaman,
        VikingChief
    }

    public enum EnemyState
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

    public enum RewardType
    {
        None,
        PickCard,
        Upgrade
    }

    public enum UpgradeModifierType
    {
        PlusOneDamage,
        PlusTwoDamage,
        MinusOneCooldown,
        MinusTwoCooldown,
        PoisonEffect
    }

    public enum PermanentUpgradeEffectType
    {
        MaxHp,
        MovementCooldown,
        FreeReroll,
        MeleeDamage,
        WaitEquippedCooldown,
        RangedDamage,
        SpecialDamage
    }

    public struct GridPos : IEquatable<GridPos>
    {
        public int X { get; set; }
        public int Y { get; set; }

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsValid(int gridSize = 5)
        {
            return X >= 0 && X < gridSize && Y >= 0 && Y < gridSize;
        }

        public GridPos Offset(int dx, int dy)
        {
            return new GridPos(X + dx, Y + dy);
        }

        public int ManhattanDistance(GridPos other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public int ChebyshevDistance(GridPos other)
        {
            return Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
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
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(GridPos left, GridPos right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridPos left, GridPos right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }
    }

    public class WeaponDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public CardCategory Category { get; set; }
        public int BaseDamage { get; set; }
        public int BaseCooldown { get; set; }
        public int Range { get; set; }
        public TargetingType Targeting { get; set; }
        public WeaponEffectType Effect { get; set; }
        public string Description { get; set; }
        public int UnlockCost { get; set; }
        public int UnlockOrder { get; set; }
        public bool UnlockedByDefault { get; set; }
    }

    public class EnemyDefinition
    {
        public string Id { get; set; }
        public EnemyType Type { get; set; }
        public string Name { get; set; }
        public int MaxHp { get; set; }
        public int AttackDamage { get; set; }
        public int AttackRange { get; set; }
        public int CoinDrop { get; set; }
        public bool SpawnNearEdge { get; set; }
        public bool IsBoss { get; set; }
        public string WeaponName { get; set; }
        public string Description { get; set; }
    }

    public class WaveSpawnEntry
    {
        public EnemyType EnemyType { get; set; }
        public int Count { get; set; }
    }

    public class WaveDefinition
    {
        public int WaveNumber { get; set; }
        public List<WaveSpawnEntry> Spawns { get; set; } = new List<WaveSpawnEntry>();
        public RewardType RewardAfterClear { get; set; }
    }

    public class PermanentUpgradeDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Cost { get; set; }
        public int UnlockOrder { get; set; }
        public PermanentUpgradeEffectType EffectType { get; set; }
        public int EffectValue { get; set; }
    }

    public class CharacterDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int BaseHp { get; set; }
        public string SpecialName { get; set; }
        public string SpecialDescription { get; set; }
        public int SpecialCooldown { get; set; }
        public bool UnlockedByDefault { get; set; }
        public string UnlockAchievementId { get; set; }
    }

    public class AchievementDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int TargetValue { get; set; }
        public int CoinReward { get; set; }
        public string UnlockCharacterId { get; set; }
    }

    public class CardInstance
    {
        public int InstanceId { get; set; }
        public string DefinitionId { get; set; }
        public WeaponDefinition Definition { get; set; }
        public int CurrentCooldown { get; set; }
        public int BonusDamage { get; set; }
        public int CooldownModifier { get; set; }
        public bool HasPoison { get; set; }
        public List<string> AppliedModifiers { get; set; } = new List<string>();

        public bool IsReady
        {
            get { return CurrentCooldown <= 0; }
        }

        public int GetEffectiveDamage(int permanentCategoryBonus)
        {
            if (Definition == null)
            {
                return 0;
            }

            if (Definition.BaseDamage == 0 && BonusDamage == 0)
            {
                return 0;
            }

            int total = Definition.BaseDamage + BonusDamage + permanentCategoryBonus;
            return Math.Max(0, total);
        }

        public int GetEffectiveMaxCooldown(int permanentReduction)
        {
            if (Definition == null)
            {
                return 1;
            }

            int total = Definition.BaseCooldown + CooldownModifier - permanentReduction;
            return Math.Max(1, total);
        }
    }

    public class PlayerState
    {
        public string CharacterId { get; set; }
        public GridPos Position { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int SpecialCooldown { get; set; }
        public int SpecialMaxCooldown { get; set; }
        public bool IsImmuneThisTurn { get; set; }
        public int FreeRerollsRemaining { get; set; }
        public List<CardInstance> Hand { get; set; } = new List<CardInstance>();
        public List<CardInstance> EquippedQueue { get; set; } = new List<CardInstance>();
    }

    public class EnemyInstance
    {
        public int InstanceId { get; set; }
        public EnemyType Type { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public GridPos Position { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int CoinDrop { get; set; }
        public EnemyState State { get; set; }
        public string EquippedWeaponLabel { get; set; }
        public int ChargeTurnsRemaining { get; set; }
        public List<GridPos> LockedTargetTiles { get; set; } = new List<GridPos>();
        public GridPos LockedDirection { get; set; }
        public string LockedAttackType { get; set; }
        public bool IsStunned { get; set; }
        public int PoisonTurnsRemaining { get; set; }
        public int BossCycleIndex { get; set; }
        public bool BossSpawnedDuringCharge { get; set; }
    }

    public class GroundHazard
    {
        public HazardType Type { get; set; }
        public GridPos Position { get; set; }
        public int RemainingTurns { get; set; }
        public int Damage { get; set; }
        public bool IsArmed { get; set; }
        public bool AppliesStun { get; set; }
    }

    public class UpgradeOption
    {
        public UpgradeModifierType ModifierType { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class PendingRewardState
    {
        public RewardType RewardType { get; set; }
        public List<string> DraftCardIds { get; set; } = new List<string>();
        public List<string> DraftSelectedIds { get; set; } = new List<string>();
        public List<UpgradeOption> UpgradeOptions { get; set; } = new List<UpgradeOption>();
        public int RerollCount { get; set; }
        public int NextRerollCost { get; set; }
    }

    public class ActiveRunState
    {
        public PlayerState Player { get; set; } = new PlayerState();
        public int CurrentWave { get; set; }
        public int TurnNumber { get; set; }
        public List<EnemyInstance> Enemies { get; set; } = new List<EnemyInstance>();
        public List<GroundHazard> Hazards { get; set; } = new List<GroundHazard>();
        public PendingRewardState PendingReward { get; set; }
        public bool IsStartingDraft { get; set; }
        public bool UsedRerollInRun { get; set; }
        public bool TookDamageInCurrentWave { get; set; }
        public bool IsVictory { get; set; }
        public bool IsGameOver { get; set; }
        public string GameOverReason { get; set; }
        public int NextEnemyInstanceId { get; set; } = 1;
        public int NextCardInstanceId { get; set; } = 1;
        public string CombatLogFilePath { get; set; }
        public List<string> RecentLogEntries { get; set; } = new List<string>();
    }

    public class PlayerProfile
    {
        public int Coins { get; set; }
        public HashSet<string> UnlockedWeaponIds { get; set; } = new HashSet<string>();
        public HashSet<string> UnlockedUpgradeIds { get; set; } = new HashSet<string>();
        public HashSet<string> UnlockedCharacterIds { get; set; } = new HashSet<string>();
        public HashSet<string> CompletedAchievementIds { get; set; } = new HashSet<string>();
        public Dictionary<string, int> AchievementProgress { get; set; } = new Dictionary<string, int>();
        public bool Area2Unlocked { get; set; }
        public string SelectedCharacterId { get; set; } = "viking";
    }

    public class GameStatistics
    {
        public int TotalRunsPlayed { get; set; }
        public int RunsWon { get; set; }
        public int HighestWaveReached { get; set; }
        public int TotalTurnsTaken { get; set; }
        public int TotalEnemiesDefeated { get; set; }
        public Dictionary<EnemyType, int> EnemiesDefeatedByType { get; set; } = new Dictionary<EnemyType, int>();
        public int TotalFriendlyFireKills { get; set; }
        public int TotalDamageDealt { get; set; }
        public int TotalCoinsEarned { get; set; }
        public int TotalCardsPlayed { get; set; }
        public Dictionary<string, int> WeaponUseCounts { get; set; } = new Dictionary<string, int>();
        public int BearTrapStunsTriggered { get; set; }
    }

    public class GameDatabase
    {
        public Dictionary<string, WeaponDefinition> Weapons { get; set; } = new Dictionary<string, WeaponDefinition>();
        public List<WeaponDefinition> WeaponList { get; set; } = new List<WeaponDefinition>();
        public Dictionary<EnemyType, EnemyDefinition> Enemies { get; set; } = new Dictionary<EnemyType, EnemyDefinition>();
        public List<WaveDefinition> Waves { get; set; } = new List<WaveDefinition>();
        public Dictionary<string, PermanentUpgradeDefinition> Upgrades { get; set; } = new Dictionary<string, PermanentUpgradeDefinition>();
        public List<PermanentUpgradeDefinition> UpgradeList { get; set; } = new List<PermanentUpgradeDefinition>();
        public Dictionary<string, CharacterDefinition> Characters { get; set; } = new Dictionary<string, CharacterDefinition>();
        public List<CharacterDefinition> CharacterList { get; set; } = new List<CharacterDefinition>();
        public Dictionary<string, AchievementDefinition> Achievements { get; set; } = new Dictionary<string, AchievementDefinition>();
        public List<AchievementDefinition> AchievementList { get; set; } = new List<AchievementDefinition>();
    }

    public class AchievementToast
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public float RemainingSeconds { get; set; }
    }
}