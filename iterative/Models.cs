#nullable disable

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

    public readonly struct GridPos(int x, int y) : IEquatable<GridPos>
    {
        public int X { get; } = x;
        public int Y { get; } = y;

        public bool IsValid() => X >= 0 && X < GameEngine.GridSize && Y >= 0 && Y < GameEngine.GridSize;

        public GridPos Offset(int dx, int dy) => new(X + dx, Y + dy);

        public int ManhattanDistance(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public int ChebyshevDistance(GridPos other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(GridPos left, GridPos right) => left.Equals(right);

        public static bool operator !=(GridPos left, GridPos right) => !left.Equals(right);

        public override string ToString() => $"({X},{Y})";
    }

    public class WeaponDefinition
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public CardCategory Category { get; init; }
        public int BaseDamage { get; init; }
        public int BaseCooldown { get; init; }
        public int Range { get; init; }
        public TargetingType Targeting { get; init; }
        public WeaponEffectType Effect { get; init; }
        public string Description { get; init; }
        public int UnlockCost { get; init; }
        public int UnlockOrder { get; init; }
        public bool UnlockedByDefault { get; init; }
    }

    public class EnemyDefinition
    {
        public string Id { get; init; }
        public EnemyType Type { get; init; }
        public string Name { get; init; }
        public int MaxHp { get; init; }
        public int AttackDamage { get; init; }
        public int AttackRange { get; init; }
        public int CoinDrop { get; init; }
        public bool SpawnNearEdge { get; init; }
        public bool IsBoss { get; init; }
        public string WeaponName { get; init; }
    }

    public class WaveSpawnEntry
    {
        public EnemyType EnemyType { get; init; }
        public int Count { get; init; }
    }

    public class WaveDefinition
    {
        public int WaveNumber { get; init; }
        public List<WaveSpawnEntry> Spawns { get; init; } = [];
        public RewardType RewardAfterClear { get; init; }
    }

    public class PermanentUpgradeDefinition
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public int Cost { get; init; }
        public int UnlockOrder { get; init; }
        public PermanentUpgradeEffectType EffectType { get; init; }
        public int EffectValue { get; init; }
    }

    public class CharacterDefinition
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public int BaseHp { get; init; }
        public string SpecialName { get; init; }
        public string SpecialDescription { get; init; }
        public int SpecialCooldown { get; init; }
        public bool UnlockedByDefault { get; init; }
        public string UnlockAchievementId { get; init; }
    }

    public class AchievementDefinition
    {
        public string Id { get; init; }
        public string Title { get; init; }
        public string Description { get; init; }
        public int TargetValue { get; init; }
        public int CoinReward { get; init; }
        public string UnlockCharacterId { get; init; }
    }

    public class CardInstance
    {
        public int InstanceId { get; init; }
        public string DefinitionId { get; init; }
        public WeaponDefinition Definition { get; init; }
        public int CurrentCooldown { get; set; }
        public int BonusDamage { get; set; }
        public int CooldownModifier { get; set; }
        public bool HasPoison { get; set; }
        public List<string> AppliedModifiers { get; init; } = [];

        public bool IsReady => CurrentCooldown <= 0;

        public int GetEffectiveDamage(int permanentCategoryBonus)
        {
            if (Definition == null || (Definition.BaseDamage == 0 && BonusDamage == 0))
            {
                return 0;
            }

            return Math.Max(0, Definition.BaseDamage + BonusDamage + permanentCategoryBonus);
        }

        public int GetEffectiveMaxCooldown(int permanentReduction)
        {
            if (Definition == null)
            {
                return 1;
            }

            return Math.Max(1, Definition.BaseCooldown + CooldownModifier - permanentReduction);
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
        public List<CardInstance> Hand { get; } = [];
        public List<CardInstance> EquippedQueue { get; } = [];
    }

    public class EnemyInstance
    {
        public int InstanceId { get; init; }
        public EnemyType Type { get; init; }
        public string DefinitionId { get; init; }
        public string Name { get; init; }
        public GridPos Position { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; init; }
        public int CoinDrop { get; init; }
        public bool IsBoss { get; init; }
        public EnemyState State { get; set; }
        public string EquippedWeaponLabel { get; set; }
        public int ChargeTurnsRemaining { get; set; }
        public List<GridPos> LockedTargetTiles { get; set; } = [];
        public GridPos LockedDirection { get; set; }
        public string LockedAttackType { get; set; }
        public bool IsStunned { get; set; }
        public int PoisonTurnsRemaining { get; set; }
        public int BossCycleIndex { get; set; }
        public bool BossSpawnedDuringCharge { get; set; }
    }

    public class GroundHazard
    {
        public HazardType Type { get; init; }
        public GridPos Position { get; init; }
        public int RemainingTurns { get; set; }
        public int Damage { get; init; }
        public bool IsArmed { get; set; }
        public bool AppliesStun { get; init; }
    }

    public class UpgradeOption
    {
        public UpgradeModifierType ModifierType { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
    }

    public class PendingRewardState
    {
        public RewardType RewardType { get; set; }
        public List<string> DraftCardIds { get; set; } = [];
        public List<string> DraftSelectedIds { get; set; } = [];
        public List<UpgradeOption> UpgradeOptions { get; } = [];
        public int RerollCount { get; set; }
        public int NextRerollCost { get; set; }
    }

    public class ActiveRunState
    {
        public PlayerState Player { get; set; } = new();
        public int CurrentWave { get; set; }
        public int TurnNumber { get; set; }
        public List<EnemyInstance> Enemies { get; } = [];
        public List<GroundHazard> Hazards { get; } = [];
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
        public List<string> RecentLogEntries { get; } = [];
    }

    public class PlayerProfile
    {
        public int Coins { get; set; }
        public HashSet<string> UnlockedWeaponIds { get; } = [];
        public HashSet<string> UnlockedUpgradeIds { get; } = [];
        public HashSet<string> UnlockedCharacterIds { get; } = [];
        public HashSet<string> CompletedAchievementIds { get; } = [];
        public Dictionary<string, int> AchievementProgress { get; } = [];
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
        public Dictionary<EnemyType, int> EnemiesDefeatedByType { get; } = [];
        public int TotalFriendlyFireKills { get; set; }
        public int TotalDamageDealt { get; set; }
        public int TotalCoinsEarned { get; set; }
        public int TotalCardsPlayed { get; set; }
        public Dictionary<string, int> WeaponUseCounts { get; } = [];
        public int BearTrapStunsTriggered { get; set; }
    }

    public class GameDatabase
    {
        public Dictionary<string, WeaponDefinition> Weapons { get; } = [];
        public List<WeaponDefinition> WeaponList { get; } = [];
        public Dictionary<EnemyType, EnemyDefinition> Enemies { get; } = [];
        public List<WaveDefinition> Waves { get; } = [];
        public Dictionary<string, PermanentUpgradeDefinition> Upgrades { get; } = [];
        public List<PermanentUpgradeDefinition> UpgradeList { get; } = [];
        public Dictionary<string, CharacterDefinition> Characters { get; } = [];
        public List<CharacterDefinition> CharacterList { get; } = [];
        public Dictionary<string, AchievementDefinition> Achievements { get; } = [];
        public List<AchievementDefinition> AchievementList { get; } = [];
    }

    public class AchievementToast
    {
        public string Title { get; init; }
        public string Subtitle { get; init; }
        public float RemainingSeconds { get; set; }
    }
}