using System;
using System.Collections.Generic;
using System.Linq;
using RogueCards.Domain;
namespace RogueCards.Application
{
    public sealed class RewardService
    {
        private static readonly UpgradeModifierType[] AllModifiers =
        {
UpgradeModifierType.PlusOneDamage,
UpgradeModifierType.PlusTwoDamage,
UpgradeModifierType.MinusOneCooldown,
UpgradeModifierType.MinusTwoCooldown,
UpgradeModifierType.Poison
};
        private readonly Random _random;

        public RewardService(Random? random = null)
        {
            _random = random ?? new Random();
        }

        public PendingRewardState CreateStartingDraft(PlayerProfile profile, GameDatabase db)
        {
            PendingRewardState reward = new PendingRewardState
            {
                Type = RewardType.StartingDraft
            };

            PopulateStartingDraftOptions(reward, profile, db);
            return reward;
        }

        public PendingRewardState CreateWaveReward(RewardType type, PlayerProfile profile, GameDatabase db)
        {
            PendingRewardState reward = new PendingRewardState
            {
                Type = type
            };

            PopulateRewardOptions(reward, profile, db);
            return reward;
        }

        public void Reroll(PendingRewardState reward, PlayerProfile profile, GameDatabase db)
        {
            reward.OfferedWeaponIds.Clear();
            reward.SelectedStartingWeaponIds.Clear();
            reward.OfferedUpgrades.Clear();

            if (reward.Type == RewardType.StartingDraft)
            {
                PopulateStartingDraftOptions(reward, profile, db);
                return;
            }

            PopulateRewardOptions(reward, profile, db);
        }

        public void ApplyModifierToCard(CardInstance card, UpgradeModifierType modifier)
        {
            switch (modifier)
            {
                case UpgradeModifierType.PlusOneDamage:
                    card.BonusDamage += 1;
                    card.AppliedModifiers.Add("+1 DMG");
                    break;
                case UpgradeModifierType.PlusTwoDamage:
                    card.BonusDamage += 2;
                    card.AppliedModifiers.Add("+2 DMG");
                    break;
                case UpgradeModifierType.MinusOneCooldown:
                    card.CooldownModifier -= 1;
                    card.AppliedModifiers.Add("-1 CD");
                    break;
                case UpgradeModifierType.MinusTwoCooldown:
                    card.CooldownModifier -= 2;
                    card.AppliedModifiers.Add("-2 CD");
                    break;
                case UpgradeModifierType.Poison:
                    card.HasPoison = true;
                    card.CooldownModifier += 1;
                    card.AppliedModifiers.Add("Poison (+1 CD)");
                    break;
            }
        }

        public string GetModifierLabel(UpgradeModifierType modifier)
        {
            return modifier switch
            {
                UpgradeModifierType.PlusOneDamage => "+1 Damage",
                UpgradeModifierType.PlusTwoDamage => "+2 Damage",
                UpgradeModifierType.MinusOneCooldown => "-1 Cooldown",
                UpgradeModifierType.MinusTwoCooldown => "-2 Cooldown",
                UpgradeModifierType.Poison => "Poison Coating",
                _ => modifier.ToString()
            };
        }

        public string GetModifierDescription(UpgradeModifierType modifier)
        {
            return modifier switch
            {
                UpgradeModifierType.PlusOneDamage => "Permanently increases this card's damage by +1.",
                UpgradeModifierType.PlusTwoDamage => "Permanently increases this card's damage by +2.",
                UpgradeModifierType.MinusOneCooldown => "Reduces this card's cooldown by 1 turn (minimum 1).",
                UpgradeModifierType.MinusTwoCooldown => "Reduces this card's cooldown by 2 turns (minimum 1).",
                UpgradeModifierType.Poison => "Adds Poison (1 dmg/turn for 3 turns) at +1 max cooldown.",
                _ => string.Empty
            };
        }

        private void PopulateStartingDraftOptions(PendingRewardState reward, PlayerProfile profile, GameDatabase db)
        {
            List<WeaponDefinition> unlocked = GetUnlockedWeapons(profile, db);
            Shuffle(unlocked);

            int count = Math.Min(4, unlocked.Count);
            for (int i = 0; i < count; i++)
            {
                reward.OfferedWeaponIds.Add(unlocked[i].Id);
            }
        }

        private void PopulateRewardOptions(PendingRewardState reward, PlayerProfile profile, GameDatabase db)
        {
            if (reward.Type == RewardType.PickCard)
            {
                PopulatePickCardOptions(reward, profile, db);
                return;
            }

            if (reward.Type == RewardType.Upgrade)
            {
                PopulateUpgradeOptions(reward);
            }
        }

        private void PopulatePickCardOptions(PendingRewardState reward, PlayerProfile profile, GameDatabase db)
        {
            List<WeaponDefinition> unlocked = GetUnlockedWeapons(profile, db);
            List<WeaponDefinition> offensive = unlocked
                .Where(w => w.Category == WeaponCategory.Melee || w.Category == WeaponCategory.Ranged)
                .ToList();
            List<WeaponDefinition> utility = unlocked
                .Where(w => w.Category == WeaponCategory.Movement || w.Category == WeaponCategory.Special)
                .ToList();

            WeaponDefinition? first = PickRandom(offensive);
            WeaponDefinition? second = PickRandom(utility);

            if (first != null)
            {
                reward.OfferedWeaponIds.Add(first.Id);
            }

            if (second != null)
            {
                reward.OfferedWeaponIds.Add(second.Id);
            }
        }

        private void PopulateUpgradeOptions(PendingRewardState reward)
        {
            List<UpgradeModifierType> pool = AllModifiers.ToList();
            Shuffle(pool);

            int offerCount = Math.Min(3, pool.Count);
            for (int i = 0; i < offerCount; i++)
            {
                reward.OfferedUpgrades.Add(pool[i]);
            }
        }

        private static List<WeaponDefinition> GetUnlockedWeapons(PlayerProfile profile, GameDatabase db)
        {
            return db.WeaponList
                .Where(w => w.IsDefaultUnlocked || profile.UnlockedWeaponIds.Contains(w.Id))
                .ToList();
        }

        private T? PickRandom<T>(List<T> items)
            where T : class
        {
            if (items.Count == 0)
            {
                return null;
            }

            return items[_random.Next(items.Count)];
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.Next(i + 1);
                T temp = list[i];
                list[i] = list[swapIndex];
                list[swapIndex] = temp;
            }
        }
    }
}