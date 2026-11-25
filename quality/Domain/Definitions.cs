using System;
using System.Collections.Generic;
using System.Linq;
namespace RogueCards.Domain
{
    public sealed class WeaponDefinition
    {
        public string Id { get; }
        public string Name { get; }

        public WeaponCategory Category { get; }

        public int BaseDamage { get; }

        public int BaseCooldown { get; }

        public int Range { get; }

        public TargetingType Targeting { get; }

        public WeaponEffectType EffectType { get; }

        public int UnlockCost { get; }

        public int UnlockOrder { get; }

        public bool IsDefaultUnlocked { get; }

        public string Description { get; }

        public WeaponDefinition(
            string id,
            string name,
            WeaponCategory category,
            int baseDamage,
            int baseCooldown,
            int range,
            TargetingType targeting,
            WeaponEffectType effectType,
            int unlockCost,
            int unlockOrder,
            bool isDefaultUnlocked,
            string description)
        {
            Id = id;
            Name = name;
            Category = category;
            BaseDamage = baseDamage;
            BaseCooldown = baseCooldown;
            Range = range;
            Targeting = targeting;
            EffectType = effectType;
            UnlockCost = unlockCost;
            UnlockOrder = unlockOrder;
            IsDefaultUnlocked = isDefaultUnlocked;
            Description = description;
        }
    }

    public sealed class EnemyDefinition
    {
        public EnemyType Type { get; }

        public string Name { get; }

        public int MaxHp { get; }

        public int Damage { get; }

        public int Range { get; }

        public int CoinReward { get; }

        public bool SpawnOnEdge { get; }

        public string WeaponLabel { get; }

        public string Description { get; }

        public EnemyDefinition(
            EnemyType type,
            string name,
            int maxHp,
            int damage,
            int range,
            int coinReward,
            bool spawnOnEdge,
            string weaponLabel,
            string description)
        {
            Type = type;
            Name = name;
            MaxHp = maxHp;
            Damage = damage;
            Range = range;
            CoinReward = coinReward;
            SpawnOnEdge = spawnOnEdge;
            WeaponLabel = weaponLabel;
            Description = description;
        }
    }

    public sealed class WaveEnemyEntry
    {
        public EnemyType EnemyType { get; }

        public int Count { get; }

        public WaveEnemyEntry(EnemyType enemyType, int count)
        {
            EnemyType = enemyType;
            Count = count;
        }
    }

    public sealed class WaveDefinition
    {
        public int WaveNumber { get; }

        public IReadOnlyList<WaveEnemyEntry> Enemies { get; }

        public RewardType RewardType { get; }

        public WaveDefinition(int waveNumber, IReadOnlyList<WaveEnemyEntry> enemies, RewardType rewardType)
        {
            WaveNumber = waveNumber;
            Enemies = enemies;
            RewardType = rewardType;
        }
    }

    public sealed class PermanentUpgradeDefinition
    {
        public string Id { get; }

        public string Name { get; }

        public PermanentUpgradeType UpgradeType { get; }

        public int Cost { get; }

        public int UnlockOrder { get; }

        public string Description { get; }

        public PermanentUpgradeDefinition(
            string id,
            string name,
            PermanentUpgradeType upgradeType,
            int cost,
            int unlockOrder,
            string description)
        {
            Id = id;
            Name = name;
            UpgradeType = upgradeType;
            Cost = cost;
            UnlockOrder = unlockOrder;
            Description = description;
        }
    }

    public sealed class CharacterDefinition
    {
        public string Id { get; }

        public string Name { get; }

        public int BaseMaxHp { get; }

        public string SpecialName { get; }

        public CharacterSpecialType SpecialType { get; }

        public int SpecialCooldown { get; }

        public bool IsDefaultUnlocked { get; }

        public string UnlockAchievementId { get; }

        public string Description { get; }

        public CharacterDefinition(
            string id,
            string name,
            int baseMaxHp,
            string specialName,
            CharacterSpecialType specialType,
            int specialCooldown,
            bool isDefaultUnlocked,
            string unlockAchievementId,
            string description)
        {
            Id = id;
            Name = name;
            BaseMaxHp = baseMaxHp;
            SpecialName = specialName;
            SpecialType = specialType;
            SpecialCooldown = specialCooldown;
            IsDefaultUnlocked = isDefaultUnlocked;
            UnlockAchievementId = unlockAchievementId;
            Description = description;
        }
    }

    public sealed class AchievementDefinition
    {
        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        public int TargetValue { get; }

        public int CoinReward { get; }

        public string UnlockCharacterId { get; }

        public AchievementDefinition(
            string id,
            string name,
            string description,
            int targetValue,
            int coinReward,
            string unlockCharacterId)
        {
            Id = id;
            Name = name;
            Description = description;
            TargetValue = targetValue;
            CoinReward = coinReward;
            UnlockCharacterId = unlockCharacterId;
        }
    }

    public sealed class GameDatabase
    {
        public IReadOnlyDictionary<string, WeaponDefinition> Weapons { get; }

        public IReadOnlyList<WeaponDefinition> WeaponList { get; }

        public IReadOnlyDictionary<EnemyType, EnemyDefinition> Enemies { get; }

        public IReadOnlyList<WaveDefinition> Waves { get; }

        public IReadOnlyList<PermanentUpgradeDefinition> UpgradeList { get; }

        public IReadOnlyDictionary<string, PermanentUpgradeDefinition> Upgrades { get; }

        public IReadOnlyDictionary<string, CharacterDefinition> Characters { get; }

        public IReadOnlyList<CharacterDefinition> CharacterList { get; }

        public IReadOnlyList<AchievementDefinition> AchievementList { get; }

        public IReadOnlyDictionary<string, AchievementDefinition> Achievements { get; }

        public GameDatabase(
            IReadOnlyList<WeaponDefinition> weapons,
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyList<WaveDefinition> waves,
            IReadOnlyList<PermanentUpgradeDefinition> upgrades,
            IReadOnlyList<CharacterDefinition> characters,
            IReadOnlyList<AchievementDefinition> achievements)
        {
            WeaponList = weapons;
            Weapons = weapons.ToDictionary(w => w.Id, StringComparer.OrdinalIgnoreCase);

            Enemies = enemies.ToDictionary(e => e.Type);

            Waves = waves.OrderBy(w => w.WaveNumber).ToList();

            UpgradeList = upgrades.OrderBy(u => u.UnlockOrder).ToList();
            Upgrades = UpgradeList.ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);

            CharacterList = characters;
            Characters = characters.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

            AchievementList = achievements;
            Achievements = achievements.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        }
    }
}