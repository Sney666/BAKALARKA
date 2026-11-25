using System;
using System.Collections.Generic;
using System.Linq;
namespace RogueCards.Domain
{
    public sealed class CardInstance
    {
        public int InstanceId { get; set; }
        public WeaponDefinition Definition { get; }

        public int CurrentCooldown { get; set; }

        public int BonusDamage { get; set; }

        public int CooldownModifier { get; set; }

        public bool HasPoison { get; set; }

        public List<string> AppliedModifiers { get; } = new List<string>();

        public bool IsReady
        {
            get
            {
                return CurrentCooldown <= 0;
            }
        }

        public CardInstance(int instanceId, WeaponDefinition definition)
        {
            InstanceId = instanceId;
            Definition = definition;
        }

        public int GetEffectiveMaxCooldown(bool hasSwiftBoots)
        {
            int cooldown = Definition.BaseCooldown + CooldownModifier;

            if (hasSwiftBoots && Definition.Category == WeaponCategory.Movement)
            {
                cooldown -= 1;
            }

            return Math.Max(1, cooldown);
        }

        public int GetEffectiveDamage(bool hasMeleeTraining, bool hasRangedTraining)
        {
            if (Definition.BaseDamage <= 0 && BonusDamage <= 0)
            {
                return 0;
            }

            int damage = Definition.BaseDamage + BonusDamage;

            if (hasMeleeTraining && Definition.Category == WeaponCategory.Melee)
            {
                damage += 1;
            }

            if (hasRangedTraining && Definition.Category == WeaponCategory.Ranged)
            {
                damage += 1;
            }

            return Math.Max(0, damage);
        }
    }

    public sealed class PlayerState
    {
        public string CharacterId { get; set; } = "viking";

        public GridPos Position { get; set; } = new GridPos(2, 2);

        public int CurrentHp { get; set; } = 5;

        public int MaxHp { get; set; } = 5;

        public int SpecialCooldown { get; set; }

        public int SpecialMaxCooldown { get; set; } = 6;

        public bool IsImmuneThisTurn { get; set; }

        public int PoisonTurnsRemaining { get; set; }

        public int FreeRerollsRemaining { get; set; }

        public List<CardInstance> Hand { get; } = new List<CardInstance>();

        public List<CardInstance> EquippedQueue { get; } = new List<CardInstance>();

        public bool IsSpecialReady
        {
            get
            {
                return SpecialCooldown <= 0;
            }
        }

        public IEnumerable<CardInstance> GetAllOwnedCards()
        {
            foreach (CardInstance card in Hand)
            {
                yield return card;
            }

            foreach (CardInstance card in EquippedQueue)
            {
                yield return card;
            }
        }
    }

    public sealed class EnemyInstance
    {
        public int InstanceId { get; set; }

        public EnemyType Type { get; set; }

        public GridPos Position { get; set; }

        public int CurrentHp { get; set; }

        public int MaxHp { get; set; }

        public EnemyState State { get; set; } = EnemyState.Unarmed;

        public int ChargeTurnsRemaining { get; set; }

        public List<GridPos> LockedTargetTiles { get; } = new List<GridPos>();

        public GridPos LockedDirection { get; set; }

        public string LockedAttackType { get; set; } = string.Empty;

        public bool IsStunned { get; set; }

        public int PoisonTurnsRemaining { get; set; }

        public int BossCycleIndex { get; set; }

        public bool IsAlive
        {
            get
            {
                return CurrentHp > 0;
            }
        }
    }

    public sealed class GroundHazard
    {
        public HazardType Type { get; set; }

        public GridPos Position { get; set; }

        public int RemainingTurns { get; set; }

        public int Damage { get; set; }

        public bool IsArmed { get; set; }

        public bool AppliesStun { get; set; }
    }

    public sealed class PendingRewardState
    {
        public RewardType Type { get; set; }

        public List<string> OfferedWeaponIds { get; } = new List<string>();

        public List<string> SelectedStartingWeaponIds { get; } = new List<string>();

        public List<UpgradeModifierType> OfferedUpgrades { get; } = new List<UpgradeModifierType>();

        public int RerollCount { get; set; }

        public bool CanSkip
        {
            get
            {
                return Type != RewardType.StartingDraft;
            }
        }

        public int GetNextRerollCost(int freeRerollsRemaining)
        {
            if (freeRerollsRemaining > 0)
            {
                return 0;
            }

            int clampedExponent = Math.Min(RerollCount, 10);
            return 5 * (1 << clampedExponent);
        }
    }

    public sealed class ActiveRunState
    {
        private const int MaxRecentLogEntries = 8;

        public PlayerState Player { get; set; } = new PlayerState();

        public int CurrentWave { get; set; } = 1;

        public int TurnNumber { get; set; } = 1;

        public List<EnemyInstance> Enemies { get; } = new List<EnemyInstance>();

        public List<GroundHazard> Hazards { get; } = new List<GroundHazard>();

        public PendingRewardState? PendingReward { get; set; }

        public bool IsStartingDraft { get; set; }

        public bool UsedRerollInRun { get; set; }

        public bool TookDamageInCurrentWave { get; set; }

        public bool IsVictory { get; set; }

        public bool IsGameOver { get; set; }

        public string GameOverReason { get; set; } = string.Empty;

        public List<string> RecentLogEntries { get; } = new List<string>();

        public int NextCardInstanceId { get; set; } = 1;

        public int NextEnemyInstanceId { get; set; } = 1;

        public string CombatLogPath { get; set; } = string.Empty;

        public int CoinsEarnedInRun { get; set; }

        public int EnemiesDefeatedInRun { get; set; }

        public EnemyInstance? GetEnemyAt(GridPos pos)
        {
            return Enemies.FirstOrDefault(e => e.IsAlive && e.Position == pos);
        }

        public bool IsTileOccupied(GridPos pos)
        {
            if (Player.Position == pos)
            {
                return true;
            }

            return GetEnemyAt(pos) != null;
        }

        public bool HasLivingEnemies()
        {
            return Enemies.Any(e => e.IsAlive);
        }

        public void AddRecentLogEntry(string message)
        {
            RecentLogEntries.Add(message);

            while (RecentLogEntries.Count > MaxRecentLogEntries)
            {
                RecentLogEntries.RemoveAt(0);
            }
        }
    }

    public sealed class PlayerProfile
    {
        public int Coins { get; set; }

        public HashSet<string> UnlockedWeaponIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> UnlockedUpgradeIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> UnlockedCharacterIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> CompletedAchievementIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, int> AchievementProgress { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public bool Area2Unlocked { get; set; }

        public string SelectedCharacterId { get; set; } = "viking";

        public bool HasUpgrade(string upgradeId)
        {
            return UnlockedUpgradeIds.Contains(upgradeId);
        }
    }

    public sealed class GameStatistics
    {
        public int TotalRunsPlayed { get; set; }

        public int RunsWon { get; set; }

        public int HighestWaveReached { get; set; }

        public int TotalTurnsTaken { get; set; }

        public int TotalEnemiesDefeated { get; set; }

        public Dictionary<EnemyType, int> EnemiesDefeatedByType { get; } = new Dictionary<EnemyType, int>();

        public int TotalFriendlyFireKills { get; set; }

        public int TotalDamageDealt { get; set; }

        public int TotalCoinsEarned { get; set; }

        public int TotalCardsPlayed { get; set; }

        public Dictionary<string, int> WeaponUseCounts { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int BearTrapStunsTriggered { get; set; }
    }

    public sealed class AchievementToast
    {
        public string Title { get; }

        public string Description { get; }

        public int CoinReward { get; }

        public float RemainingSeconds { get; set; }

        public float TotalSeconds { get; }

        public AchievementToast(string title, string description, int coinReward, float durationSeconds = 4.0f)
        {
            Title = title;
            Description = description;
            CoinReward = coinReward;
            RemainingSeconds = durationSeconds;
            TotalSeconds = durationSeconds;
        }
    }

    public sealed class AimPreviewResult
    {
        public bool IsValid { get; set; }

        public GridPos Direction { get; set; }

        public List<GridPos> AffectedTiles { get; } = new List<GridPos>();

        public List<GridPos> ImpactTiles { get; } = new List<GridPos>();

        public GridPos? MoveDestination { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}