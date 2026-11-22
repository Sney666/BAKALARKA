#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RogueCards
{
    public class GameEngine
    {
        public const int GridSize = 5;
        public const int MaxEquippedSlots = 3;

        private readonly GameDatabase database;
        private readonly PlayerProfile profile;
        private readonly Random random;
        private readonly CardResolver cardResolver;
        private readonly EnemyAI enemyAI;

        private int currentExecutingQueueIndex;
        private bool isExecutingQueue;
        private readonly HashSet<int> cardsPlayedThisTurn = new HashSet<int>();

        public ActiveRunState CurrentRun { get; private set; }
        public string LastActionFeedback { get; private set; }
        public string LastSpecialEffectType { get; private set; }
        public GridPos LastSpecialOrigin { get; private set; }
        public int LastSpecialSequenceId { get; private set; }

        public bool IsExecutingQueue
        {
            get { return isExecutingQueue; }
        }

        public int CurrentExecutingQueueIndex
        {
            get { return currentExecutingQueueIndex; }
        }

        public CardInstance CurrentAimingCard
        {
            get
            {
                if (!isExecutingQueue || CurrentRun == null || CurrentRun.Player == null)
                {
                    return null;
                }

                if (currentExecutingQueueIndex < 0 || currentExecutingQueueIndex >= CurrentRun.Player.EquippedQueue.Count)
                {
                    return null;
                }

                return CurrentRun.Player.EquippedQueue[currentExecutingQueueIndex];
            }
        }

        public GameEngine(GameDatabase database, PlayerProfile profile)
        {
            this.database = database;
            this.profile = profile;
            random = new Random();
            cardResolver = new CardResolver();
            enemyAI = new EnemyAI(random);
            LastActionFeedback = string.Empty;
            LastSpecialEffectType = string.Empty;
        }

        public GameDatabase Database
        {
            get { return database; }
        }

        public void StartNewRun()
        {
            CharacterDefinition character = GetSelectedCharacterDefinition();
            int maxHpBonus = GetMaxHpBonus();
            int maxHp = character.BaseHp + maxHpBonus;
            int freeRerolls = GetFreeRerollBonus();

            PlayerState player = new PlayerState
            {
                CharacterId = character.Id,
                Position = new GridPos(2, 2),
                CurrentHp = maxHp,
                MaxHp = maxHp,
                SpecialCooldown = 0,
                SpecialMaxCooldown = character.SpecialCooldown,
                IsImmuneThisTurn = false,
                FreeRerollsRemaining = freeRerolls
            };

            CurrentRun = new ActiveRunState
            {
                Player = player,
                CurrentWave = 1,
                TurnNumber = 1,
                IsStartingDraft = true,
                UsedRerollInRun = false,
                TookDamageInCurrentWave = false,
                IsVictory = false,
                IsGameOver = false,
                GameOverReason = string.Empty,
                NextEnemyInstanceId = 1,
                NextCardInstanceId = 1
            };

            isExecutingQueue = false;
            currentExecutingQueueIndex = 0;
            cardsPlayedThisTurn.Clear();

            BuildStartingDraftOptions();

            LastActionFeedback = $"Choose 2 starting cards for {character.Name}.";
            LastSpecialEffectType = string.Empty;

            AddCombatLog($"New run started as {character.Name} ({maxHp}/{maxHp} HP).");

            List<string> activeUpgrades = GetActiveUpgradeNames();
            if (activeUpgrades.Count > 0)
            {
                AddCombatLog($"Permanent upgrades active: {string.Join(", ", activeUpgrades)}.");
            }
        }

        private void BuildStartingDraftOptions()
        {
            List<WeaponDefinition> unlocked = database.WeaponList
                .Where(w => profile.UnlockedWeaponIds.Contains(w.Id))
                .OrderBy(_ => random.Next())
                .Take(4)
                .ToList();

            CurrentRun.PendingReward = new PendingRewardState
            {
                RewardType = RewardType.PickCard,
                DraftCardIds = unlocked.Select(w => w.Id).ToList(),
                DraftSelectedIds = new List<string>(),
                RerollCount = 0,
                NextRerollCost = 5
            };
        }

        public bool ToggleStartingDraftSelection(string weaponId)
        {
            if (CurrentRun == null || !CurrentRun.IsStartingDraft || CurrentRun.PendingReward == null)
            {
                return false;
            }

            PendingRewardState draft = CurrentRun.PendingReward;
            if (!draft.DraftCardIds.Contains(weaponId))
            {
                return false;
            }

            if (draft.DraftSelectedIds.Contains(weaponId))
            {
                draft.DraftSelectedIds.Remove(weaponId);
                return true;
            }

            int maxSelect = Math.Min(2, draft.DraftCardIds.Count);
            if (draft.DraftSelectedIds.Count >= maxSelect)
            {
                draft.DraftSelectedIds.RemoveAt(0);
            }

            draft.DraftSelectedIds.Add(weaponId);
            return true;
        }

        public bool ConfirmStartingDraft()
        {
            if (CurrentRun == null || !CurrentRun.IsStartingDraft || CurrentRun.PendingReward == null)
            {
                return false;
            }

            PendingRewardState draft = CurrentRun.PendingReward;
            int requiredCount = Math.Min(2, draft.DraftCardIds.Count);
            if (draft.DraftSelectedIds.Count < requiredCount)
            {
                LastActionFeedback = $"Select {requiredCount} cards before starting Wave 1.";
                return false;
            }

            CurrentRun.Player.Hand.Clear();
            List<string> chosenNames = new List<string>();

            foreach (string weaponId in draft.DraftSelectedIds)
            {
                if (database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
                {
                    CurrentRun.Player.Hand.Add(CreateCardInstance(weapon));
                    chosenNames.Add(weapon.Name);
                }
            }

            CurrentRun.IsStartingDraft = false;
            CurrentRun.PendingReward = null;
            CurrentRun.TookDamageInCurrentWave = false;
            LastActionFeedback = $"Wave 1 begun! Starting hand: {string.Join(" & ", chosenNames)}.";
            AddCombatLog($"Drafted starting hand: {string.Join(", ", chosenNames)}.");

            enemyAI.SpawnWave(this, CurrentRun.CurrentWave);
            return true;
        }

        public bool TryEquipCardFromHand(int handIndex)
        {
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver || isExecutingQueue)
            {
                return false;
            }

            PlayerState player = CurrentRun.Player;
            if (handIndex < 0 || handIndex >= player.Hand.Count)
            {
                return false;
            }

            if (player.EquippedQueue.Count >= MaxEquippedSlots)
            {
                LastActionFeedback = $"Equipped queue is full ({MaxEquippedSlots}/{MaxEquippedSlots}).";
                return false;
            }

            CardInstance card = player.Hand[handIndex];
            if (!card.IsReady)
            {
                LastActionFeedback = $"{card.Definition.Name} is on cooldown ({card.CurrentCooldown} turn(s) remaining).";
                return false;
            }

            player.Hand.RemoveAt(handIndex);
            player.EquippedQueue.Add(card);

            CharacterDefinition hero = GetActiveCharacterDefinition();
            LastActionFeedback = $"Equipped {card.Definition.Name} into slot #{player.EquippedQueue.Count}.";
            AddCombatLog($"Turn {CurrentRun.TurnNumber}: {hero.Name} equipped {card.Definition.Name} (Slot #{player.EquippedQueue.Count}).");

            EndPlayerTurn(false, false);
            return true;
        }

        public bool ReorderEquippedCard(int fromIndex, int toIndex)
        {
            if (CurrentRun == null || CurrentRun.Player == null || isExecutingQueue)
            {
                return false;
            }

            List<CardInstance> queue = CurrentRun.Player.EquippedQueue;
            if (fromIndex < 0 || fromIndex >= queue.Count || toIndex < 0 || toIndex >= queue.Count || fromIndex == toIndex)
            {
                return false;
            }

            CardInstance moved = queue[fromIndex];
            queue.RemoveAt(fromIndex);
            queue.Insert(toIndex, moved);

            LastActionFeedback = $"Reordered {moved.Definition.Name} to Slot #{toIndex + 1} (0 turns).";
            AddCombatLog($"Reordered queue: {moved.Definition.Name} moved to Slot #{toIndex + 1} (free action).");
            return true;
        }

        public bool BeginPlayEquippedQueue()
        {
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver || isExecutingQueue)
            {
                return false;
            }

            if (CurrentRun.Player.EquippedQueue.Count == 0)
            {
                LastActionFeedback = "No equipped cards in the queue to play.";
                return false;
            }

            isExecutingQueue = true;
            currentExecutingQueueIndex = 0;
            cardsPlayedThisTurn.Clear();

            AdvanceQueueThroughAutoCards();
            return true;
        }

        public bool CancelAimingIfPossible()
        {
            if (!isExecutingQueue || currentExecutingQueueIndex > 0)
            {
                return false;
            }

            isExecutingQueue = false;
            currentExecutingQueueIndex = 0;
            cardsPlayedThisTurn.Clear();
            LastActionFeedback = "Cancelled queue execution.";
            return true;
        }

        public AimPreviewResult GetAimPreview(GridPos direction)
        {
            CardInstance card = CurrentAimingCard;
            if (card == null)
            {
                return new AimPreviewResult { IsValidAim = false };
            }

            return cardResolver.ComputeAimPreview(CurrentRun, card, direction);
        }

        public bool ConfirmAimDirection(GridPos direction)
        {
            if (!isExecutingQueue || CurrentRun == null || CurrentRun.Player == null)
            {
                return false;
            }

            CardInstance card = CurrentAimingCard;
            if (card == null)
            {
                FinishEquippedQueueExecution();
                return true;
            }

            AimPreviewResult preview = cardResolver.ComputeAimPreview(CurrentRun, card, direction);
            if (!preview.IsValidAim)
            {
                LastActionFeedback = $"Invalid aim direction for {card.Definition.Name}.";
                return false;
            }

            bool resolved = cardResolver.ResolveCard(this, card, direction);
            if (!resolved)
            {
                return false;
            }

            card.CurrentCooldown = GetEffectiveCardMaxCooldown(card);
            cardsPlayedThisTurn.Add(card.InstanceId);
            LastActionFeedback = $"Executed {card.Definition.Name}!";
            currentExecutingQueueIndex++;

            if (CurrentRun.IsGameOver || CurrentRun.Enemies.All(e => e.CurrentHp <= 0))
            {
                FinishEquippedQueueExecution();
                return true;
            }

            AdvanceQueueThroughAutoCards();
            return true;
        }

        private void AdvanceQueueThroughAutoCards()
        {
            while (isExecutingQueue && currentExecutingQueueIndex < CurrentRun.Player.EquippedQueue.Count)
            {
                CardInstance nextCard = CurrentRun.Player.EquippedQueue[currentExecutingQueueIndex];
                if (nextCard.Definition.Targeting == TargetingType.Self || nextCard.Definition.Targeting == TargetingType.None)
                {
                    cardResolver.ResolveCard(this, nextCard, new GridPos(0, 0));
                    nextCard.CurrentCooldown = GetEffectiveCardMaxCooldown(nextCard);
                    cardsPlayedThisTurn.Add(nextCard.InstanceId);
                    currentExecutingQueueIndex++;
                }
                else
                {
                    LastActionFeedback = $"Aim {nextCard.Definition.Name} ({nextCard.Definition.Targeting}).";
                    return;
                }
            }

            if (isExecutingQueue && currentExecutingQueueIndex >= CurrentRun.Player.EquippedQueue.Count)
            {
                FinishEquippedQueueExecution();
            }
        }

        private void FinishEquippedQueueExecution()
        {
            PlayerState player = CurrentRun.Player;
            int playedCount = currentExecutingQueueIndex;

            foreach (CardInstance queuedCard in player.EquippedQueue)
            {
                player.Hand.Add(queuedCard);
            }

            player.EquippedQueue.Clear();
            isExecutingQueue = false;
            currentExecutingQueueIndex = 0;

            LastActionFeedback = $"Executed {playedCount} card(s).";
            EndPlayerTurn(false, false);
            cardsPlayedThisTurn.Clear();
        }

        public void ResetAllCooldowns()
        {
            if (CurrentRun == null || CurrentRun.Player == null)
            {
                return;
            }

            foreach (CardInstance card in CurrentRun.Player.Hand)
            {
                card.CurrentCooldown = 0;
            }

            foreach (CardInstance card in CurrentRun.Player.EquippedQueue)
            {
                card.CurrentCooldown = 0;
            }

            CurrentRun.Player.SpecialCooldown = 0;
            AddCombatLog("All card and Special cooldowns reset to 0!");
        }

        public GameScreen TriggerWaveClearReward(int clearedWaveNumber)
        {
            WaveDefinition wave = database.Waves.FirstOrDefault(w => w.WaveNumber == clearedWaveNumber);
            RewardType rewardType = wave != null ? wave.RewardAfterClear : RewardType.None;

            if (rewardType == RewardType.PickCard)
            {
                ResetAllCooldowns();
                CurrentRun.PendingReward = new PendingRewardState
                {
                    RewardType = RewardType.PickCard,
                    RerollCount = 0,
                    NextRerollCost = 5
                };

                PopulatePickCardChoices(CurrentRun.PendingReward);
                LastActionFeedback = $"Wave {clearedWaveNumber} cleared! Pick a Card (all cooldowns reset).";
                AddCombatLog($"Wave {clearedWaveNumber} cleared! Reward: Pick a Card.");
                return GameScreen.RewardCard;
            }

            if (rewardType == RewardType.Upgrade)
            {
                ResetAllCooldowns();
                CurrentRun.PendingReward = new PendingRewardState
                {
                    RewardType = RewardType.Upgrade,
                    RerollCount = 0,
                    NextRerollCost = 5
                };

                PopulateUpgradeChoices(CurrentRun.PendingReward);
                LastActionFeedback = $"Wave {clearedWaveNumber} cleared! Upgrade a Card (all cooldowns reset).";
                AddCombatLog($"Wave {clearedWaveNumber} cleared! Reward: Upgrade a Card.");
                return GameScreen.RewardUpgrade;
            }

            return GameScreen.Arena;
        }

        private void PopulatePickCardChoices(PendingRewardState reward)
        {
            reward.DraftCardIds.Clear();

            List<WeaponDefinition> unlocked = database.WeaponList
                .Where(w => profile.UnlockedWeaponIds.Contains(w.Id))
                .ToList();

            List<WeaponDefinition> offensePool = unlocked
                .Where(w => w.Category == CardCategory.Melee || w.Category == CardCategory.Ranged)
                .ToList();

            List<WeaponDefinition> utilityPool = unlocked
                .Where(w => w.Category == CardCategory.Movement || w.Category == CardCategory.Special)
                .ToList();

            if (offensePool.Count > 0)
            {
                WeaponDefinition pick1 = offensePool[random.Next(offensePool.Count)];
                reward.DraftCardIds.Add(pick1.Id);
            }

            if (utilityPool.Count > 0)
            {
                WeaponDefinition pick2 = utilityPool[random.Next(utilityPool.Count)];
                reward.DraftCardIds.Add(pick2.Id);
            }
        }

        private void PopulateUpgradeChoices(PendingRewardState reward)
        {
            reward.UpgradeOptions.Clear();

            List<UpgradeOption> allOptions = new List<UpgradeOption>
            {
                new UpgradeOption
                {
                    ModifierType = UpgradeModifierType.PlusOneDamage,
                    Name = "+1 Damage",
                    Description = "Permanently adds +1 damage to the chosen card for this run."
                },
                new UpgradeOption
                {
                    ModifierType = UpgradeModifierType.PlusTwoDamage,
                    Name = "+2 Damage",
                    Description = "Permanently adds +2 damage to the chosen card for this run."
                },
                new UpgradeOption
                {
                    ModifierType = UpgradeModifierType.MinusOneCooldown,
                    Name = "-1 Cooldown",
                    Description = "Reduces the maximum cooldown of the chosen card by 1 turn (min 1)."
                },
                new UpgradeOption
                {
                    ModifierType = UpgradeModifierType.MinusTwoCooldown,
                    Name = "-2 Cooldown",
                    Description = "Reduces the maximum cooldown of the chosen card by 2 turns (min 1)."
                },
                new UpgradeOption
                {
                    ModifierType = UpgradeModifierType.PoisonEffect,
                    Name = "Poison",
                    Description = "Adds +1 to max cooldown cost. Hit enemies take 1 poison dmg/turn for 3 turns."
                }
            };

            List<UpgradeOption> picked = allOptions
                .OrderBy(_ => random.Next())
                .Take(3)
                .ToList();

            reward.UpgradeOptions.AddRange(picked);
        }

        public bool PickRewardCard(string weaponId)
        {
            if (CurrentRun == null || CurrentRun.PendingReward == null || CurrentRun.PendingReward.RewardType != RewardType.PickCard)
            {
                return false;
            }

            if (!CurrentRun.PendingReward.DraftCardIds.Contains(weaponId) ||
                !database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
            {
                return false;
            }

            CurrentRun.Player.Hand.Add(CreateCardInstance(weapon));
            CurrentRun.PendingReward = null;
            LastActionFeedback = $"Added {weapon.Name} to your hand!";
            AddCombatLog($"Reward chosen: Added {weapon.Name} to hand.");
            AdvanceToNextWave();
            return true;
        }

        public bool ApplyUpgradeModifierToCard(UpgradeModifierType modifierType, int cardInstanceId)
        {
            if (CurrentRun == null || CurrentRun.PendingReward == null || CurrentRun.PendingReward.RewardType != RewardType.Upgrade)
            {
                return false;
            }

            CardInstance targetCard = GetAllOwnedCards().FirstOrDefault(c => c.InstanceId == cardInstanceId);
            if (targetCard == null)
            {
                return false;
            }

            switch (modifierType)
            {
                case UpgradeModifierType.PlusOneDamage:
                    targetCard.BonusDamage += 1;
                    targetCard.AppliedModifiers.Add("+1 DMG");
                    break;

                case UpgradeModifierType.PlusTwoDamage:
                    targetCard.BonusDamage += 2;
                    targetCard.AppliedModifiers.Add("+2 DMG");
                    break;

                case UpgradeModifierType.MinusOneCooldown:
                    targetCard.CooldownModifier -= 1;
                    targetCard.AppliedModifiers.Add("-1 CD");
                    break;

                case UpgradeModifierType.MinusTwoCooldown:
                    targetCard.CooldownModifier -= 2;
                    targetCard.AppliedModifiers.Add("-2 CD");
                    break;

                case UpgradeModifierType.PoisonEffect:
                    targetCard.HasPoison = true;
                    targetCard.CooldownModifier += 1;
                    targetCard.AppliedModifiers.Add("POISON (+1 CD)");
                    break;
            }

            CurrentRun.PendingReward = null;
            LastActionFeedback = $"Upgraded {targetCard.Definition.Name}!";
            AddCombatLog($"Reward chosen: Applied {modifierType} to {targetCard.Definition.Name}.");
            AdvanceToNextWave();
            return true;
        }

        public bool SkipCurrentReward()
        {
            if (CurrentRun == null || CurrentRun.PendingReward == null)
            {
                return false;
            }

            RewardType skipped = CurrentRun.PendingReward.RewardType;
            CurrentRun.PendingReward = null;
            LastActionFeedback = $"Skipped {skipped} reward.";
            AddCombatLog($"Skipped {skipped} reward.");
            AdvanceToNextWave();
            return true;
        }

        public bool TryRerollCurrentReward()
        {
            if (CurrentRun == null || CurrentRun.PendingReward == null)
            {
                return false;
            }

            PendingRewardState reward = CurrentRun.PendingReward;
            PlayerState player = CurrentRun.Player;

            if (player.FreeRerollsRemaining > 0)
            {
                player.FreeRerollsRemaining--;
                CurrentRun.UsedRerollInRun = true;
                LastActionFeedback = "Used Free Reroll (Charisma)!";
                AddCombatLog("Used Free Reroll from Charisma upgrade.");
            }
            else
            {
                int cost = reward.NextRerollCost;
                if (profile.Coins < cost)
                {
                    LastActionFeedback = $"Not enough coins to reroll (requires {cost} Coins).";
                    return false;
                }

                profile.Coins -= cost;
                reward.RerollCount++;
                reward.NextRerollCost = cost * 2;
                CurrentRun.UsedRerollInRun = true;
                LastActionFeedback = $"Rerolled reward for {cost} Coins (next reroll: {reward.NextRerollCost} Coins).";
                AddCombatLog($"Rerolled reward for {cost} Coins.");
            }

            if (reward.RewardType == RewardType.PickCard)
            {
                PopulatePickCardChoices(reward);
            }
            else if (reward.RewardType == RewardType.Upgrade)
            {
                PopulateUpgradeChoices(reward);
            }

            return true;
        }

        private void AdvanceToNextWave()
        {
            CurrentRun.CurrentWave++;
            CurrentRun.TookDamageInCurrentWave = false;
            enemyAI.SpawnWave(this, CurrentRun.CurrentWave);
        }

        private void CheckWaveClearOrVictory()
        {
            if (CurrentRun == null || CurrentRun.IsGameOver || CurrentRun.PendingReward != null)
            {
                return;
            }

            if (CurrentRun.Enemies.Any(e => e.CurrentHp > 0))
            {
                return;
            }

            int maxWave = database.Waves.Count > 0 ? database.Waves.Max(w => w.WaveNumber) : 10;
            if (CurrentRun.CurrentWave >= maxWave)
            {
                CurrentRun.IsVictory = true;
                CurrentRun.IsGameOver = true;
                CurrentRun.GameOverReason = "Victory! You defeated the Viking Chief and conquered Level 1!";
                profile.Area2Unlocked = true;
                LastActionFeedback = "VICTORY! Viking Chief defeated!";
                AddCombatLog("VICTORY! Level 1 completed and Area 2 unlocked!");
                return;
            }

            GameScreen rewardScreen = TriggerWaveClearReward(CurrentRun.CurrentWave);
            if (rewardScreen == GameScreen.Arena)
            {
                AddCombatLog($"Wave {CurrentRun.CurrentWave} cleared!");
                AdvanceToNextWave();
            }
        }

        public List<CardInstance> GetAllOwnedCards()
        {
            List<CardInstance> cards = new List<CardInstance>();
            if (CurrentRun == null || CurrentRun.Player == null)
            {
                return cards;
            }

            cards.AddRange(CurrentRun.Player.Hand);
            cards.AddRange(CurrentRun.Player.EquippedQueue);
            return cards;
        }

        public void ApplyDamageToEnemy(EnemyInstance enemy, int damage, bool applyPoison, string sourceName)
        {
            if (enemy == null || enemy.CurrentHp <= 0)
            {
                return;
            }

            if (damage > 0)
            {
                enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - damage);
            }

            string poisonText = string.Empty;
            if (applyPoison && enemy.CurrentHp > 0)
            {
                enemy.PoisonTurnsRemaining = 3;
                poisonText = " + Poison (3T)";
            }

            AddCombatLog($"{sourceName} hit {enemy.Name} for {damage} dmg{poisonText} ({enemy.CurrentHp}/{enemy.MaxHp} HP).");

            if (enemy.CurrentHp <= 0)
            {
                profile.Coins += enemy.CoinDrop;
                AddCombatLog($"{enemy.Name} defeated! (+{enemy.CoinDrop} Coins)");
                RemoveDefeatedEnemies();
            }
        }

        public void ApplyFriendlyFireDamage(EnemyInstance attacker, EnemyInstance victim, int damage, string weaponLabel)
        {
            if (victim == null || victim.CurrentHp <= 0)
            {
                return;
            }

            victim.CurrentHp = Math.Max(0, victim.CurrentHp - damage);
            AddCombatLog($"FRIENDLY FIRE! {attacker.Name}'s {weaponLabel} hit {victim.Name} for {damage} dmg ({victim.CurrentHp}/{victim.MaxHp} HP).");

            if (victim.CurrentHp <= 0)
            {
                profile.Coins += victim.CoinDrop;
                AddCombatLog($"{victim.Name} killed by friendly fire! (+{victim.CoinDrop} Coins)");
                RemoveDefeatedEnemies();
            }
        }

        public void ApplyEnemyAttackToPlayer(EnemyInstance attacker, int damage, string weaponLabel)
        {
            PlayerState player = CurrentRun.Player;
            if (player.IsImmuneThisTurn)
            {
                AddCombatLog($"Iron Bulwark blocked {damage} dmg from {attacker.Name}'s {weaponLabel}!");
                return;
            }

            player.CurrentHp = Math.Max(0, player.CurrentHp - damage);
            CurrentRun.TookDamageInCurrentWave = true;
            AddCombatLog($"{attacker.Name} hit you with {weaponLabel} for {damage} dmg ({player.CurrentHp}/{player.MaxHp} HP).");

            if (player.CurrentHp <= 0)
            {
                TriggerPlayerDefeat($"Slain by {attacker.Name} ({weaponLabel}) on Wave {CurrentRun.CurrentWave}.");
            }
        }

        private void TriggerPlayerDefeat(string reason)
        {
            CurrentRun.IsVictory = false;
            CurrentRun.IsGameOver = true;
            CurrentRun.GameOverReason = reason;
            isExecutingQueue = false;
            LastActionFeedback = "DEFEAT! You have fallen in battle.";
            AddCombatLog($"DEFEAT: {reason}");
        }

        public void TriggerHazardsForEnemy(EnemyInstance enemy)
        {
            if (enemy == null || enemy.CurrentHp <= 0)
            {
                return;
            }

            GroundHazard trap = CurrentRun.Hazards.FirstOrDefault(
                h => h.Type == HazardType.BearTrap && h.IsArmed && h.Position == enemy.Position);

            if (trap != null)
            {
                CurrentRun.Hazards.Remove(trap);
                enemy.IsStunned = true;
                enemy.State = EnemyState.Armed;
                enemy.ChargeTurnsRemaining = 0;
                enemy.LockedTargetTiles.Clear();
                ApplyDamageToEnemy(enemy, trap.Damage, false, "Bear Trap");
                if (enemy.CurrentHp > 0)
                {
                    AddCombatLog($"{enemy.Name} is stunned by Bear Trap and loses next turn!");
                }
            }

            if (enemy.CurrentHp > 0)
            {
                GroundHazard poison = CurrentRun.Hazards.FirstOrDefault(
                    h => h.Type == HazardType.Poison && h.Position == enemy.Position);

                if (poison != null)
                {
                    ApplyDamageToEnemy(enemy, poison.Damage, false, "Poison Pool");
                }
            }
        }

        public void TriggerHazardsForPlayer()
        {
            PlayerState player = CurrentRun.Player;
            GroundHazard poison = CurrentRun.Hazards.FirstOrDefault(
                h => h.Type == HazardType.Poison && h.Position == player.Position);

            if (poison != null)
            {
                if (player.IsImmuneThisTurn)
                {
                    AddCombatLog("Iron Bulwark blocked Poison Pool damage!");
                    return;
                }

                player.CurrentHp = Math.Max(0, player.CurrentHp - poison.Damage);
                CurrentRun.TookDamageInCurrentWave = true;
                AddCombatLog($"Poison Pool dealt {poison.Damage} dmg ({player.CurrentHp}/{player.MaxHp} HP).");

                if (player.CurrentHp <= 0)
                {
                    TriggerPlayerDefeat($"Succumbed to Poison on Wave {CurrentRun.CurrentWave}.");
                }
            }
        }

        public CharacterDefinition GetSelectedCharacterDefinition()
        {
            if (profile != null &&
                !string.IsNullOrEmpty(profile.SelectedCharacterId) &&
                database.Characters.TryGetValue(profile.SelectedCharacterId, out CharacterDefinition selected))
            {
                return selected;
            }

            if (database.Characters.TryGetValue("viking", out CharacterDefinition viking))
            {
                return viking;
            }

            return database.CharacterList.First();
        }

        public CharacterDefinition GetActiveCharacterDefinition()
        {
            if (CurrentRun != null &&
                CurrentRun.Player != null &&
                database.Characters.TryGetValue(CurrentRun.Player.CharacterId, out CharacterDefinition active))
            {
                return active;
            }

            return GetSelectedCharacterDefinition();
        }

        public int GetMaxHpBonus()
        {
            return SumUpgradeEffect(PermanentUpgradeEffectType.MaxHp);
        }

        public int GetMovementCooldownReduction()
        {
            return SumUpgradeEffect(PermanentUpgradeEffectType.MovementCooldown);
        }

        public int GetFreeRerollBonus()
        {
            return SumUpgradeEffect(PermanentUpgradeEffectType.FreeReroll);
        }

        public int GetMeleeDamageBonus()
        {
            return SumUpgradeEffect(PermanentUpgradeEffectType.MeleeDamage);
        }

        public int GetRangedDamageBonus()
        {
            return SumUpgradeEffect(PermanentUpgradeEffectType.RangedDamage);
        }

        public int GetWaitEquippedCooldownBonus()
        {
            return SumUpgradeEffect(PermanentUpgradeEffectType.WaitEquippedCooldown);
        }

        public int GetSpecialDamageBonus()
        {
            return SumUpgradeEffect(PermanentUpgradeEffectType.SpecialDamage);
        }

        public List<string> GetActiveUpgradeNames()
        {
            List<string> names = new List<string>();
            foreach (PermanentUpgradeDefinition upgrade in database.UpgradeList)
            {
                if (profile.UnlockedUpgradeIds.Contains(upgrade.Id))
                {
                    names.Add(upgrade.Name);
                }
            }

            return names;
        }

        public int GetEffectiveCardDamage(CardInstance card)
        {
            if (card == null || card.Definition == null)
            {
                return 0;
            }

            int permanentBonus = 0;
            if (card.Definition.Category == CardCategory.Melee)
            {
                permanentBonus = GetMeleeDamageBonus();
            }
            else if (card.Definition.Category == CardCategory.Ranged)
            {
                permanentBonus = GetRangedDamageBonus();
            }

            return card.GetEffectiveDamage(permanentBonus);
        }

        public int GetEffectiveCardMaxCooldown(CardInstance card)
        {
            if (card == null || card.Definition == null)
            {
                return 1;
            }

            int permanentReduction = 0;
            if (card.Definition.Category == CardCategory.Movement)
            {
                permanentReduction = GetMovementCooldownReduction();
            }

            return card.GetEffectiveMaxCooldown(permanentReduction);
        }

        public List<GridPos> GetValidMoveTiles()
        {
            List<GridPos> validTiles = new List<GridPos>();
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver || isExecutingQueue)
            {
                return validTiles;
            }

            GridPos origin = CurrentRun.Player.Position;
            GridPos[] candidates = new GridPos[]
            {
                origin.Offset(0, -1),
                origin.Offset(0, 1),
                origin.Offset(-1, 0),
                origin.Offset(1, 0)
            };

            foreach (GridPos tile in candidates)
            {
                if (tile.IsValid(GridSize) && !IsTileOccupiedByEnemy(tile))
                {
                    validTiles.Add(tile);
                }
            }

            return validTiles;
        }

        public bool TryMovePlayerToTile(GridPos target)
        {
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver || isExecutingQueue)
            {
                return false;
            }

            int dx = target.X - CurrentRun.Player.Position.X;
            int dy = target.Y - CurrentRun.Player.Position.Y;

            if (Math.Abs(dx) + Math.Abs(dy) != 1)
            {
                return false;
            }

            return TryMovePlayer(dx, dy);
        }

        public bool TryMovePlayer(int dx, int dy)
        {
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver || isExecutingQueue)
            {
                return false;
            }

            if (Math.Abs(dx) + Math.Abs(dy) != 1)
            {
                return false;
            }

            GridPos oldPosition = CurrentRun.Player.Position;
            GridPos target = oldPosition.Offset(dx, dy);

            if (!target.IsValid(GridSize))
            {
                LastActionFeedback = "Cannot move outside the 5x5 arena.";
                return false;
            }

            if (IsTileOccupiedByEnemy(target))
            {
                LastActionFeedback = "That tile is occupied by an enemy.";
                return false;
            }

            CurrentRun.Player.Position = target;
            ArmBearTrapAt(oldPosition);

            CharacterDefinition hero = GetActiveCharacterDefinition();
            string directionLabel = GetDirectionName(dx, dy);
            LastActionFeedback = $"Moved {directionLabel} to {target}.";
            AddCombatLog($"Turn {CurrentRun.TurnNumber}: {hero.Name} moved {directionLabel} to {target}.");
            TriggerHazardsForPlayer();

            if (!CurrentRun.IsGameOver)
            {
                EndPlayerTurn(false, false);
            }

            return true;
        }

        public bool WaitTurn()
        {
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver || isExecutingQueue)
            {
                return false;
            }

            CharacterDefinition hero = GetActiveCharacterDefinition();
            int extraReduction = GetWaitEquippedCooldownBonus();

            if (extraReduction > 0)
            {
                LastActionFeedback = $"Waited 1 turn (Tactical Master: -{extraReduction} extra CD).";
                AddCombatLog($"Turn {CurrentRun.TurnNumber}: {hero.Name} waited (Tactical Master active).");
            }
            else
            {
                LastActionFeedback = "Waited 1 turn.";
                AddCombatLog($"Turn {CurrentRun.TurnNumber}: {hero.Name} waited.");
            }

            EndPlayerTurn(true, false);
            return true;
        }

        public bool TryUseSpecial()
        {
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver || isExecutingQueue)
            {
                return false;
            }

            PlayerState player = CurrentRun.Player;
            CharacterDefinition hero = GetActiveCharacterDefinition();

            if (player.SpecialCooldown > 0)
            {
                LastActionFeedback = $"{hero.SpecialName} is on cooldown ({player.SpecialCooldown} turn(s) left).";
                return false;
            }

            player.SpecialCooldown = player.SpecialMaxCooldown;
            int specialDamage = GetSpecialDamageBonus();

            if (string.Equals(player.CharacterId, "shieldmaiden", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteIronBulwark(player, hero, specialDamage);
            }
            else
            {
                ExecuteVikingScream(player, hero, specialDamage);
            }

            EndPlayerTurn(false, true);
            return true;
        }

        private void ExecuteVikingScream(PlayerState player, CharacterDefinition hero, int specialDamage)
        {
            int pushedCount = 0;
            int damagedCount = 0;
            List<EnemyInstance> adjacentEnemies = CurrentRun.Enemies
                .Where(e => e.CurrentHp > 0 && e.Position.ChebyshevDistance(player.Position) == 1)
                .ToList();

            foreach (EnemyInstance enemy in adjacentEnemies)
            {
                int pushDx = Math.Sign(enemy.Position.X - player.Position.X);
                int pushDy = Math.Sign(enemy.Position.Y - player.Position.Y);
                GridPos pushTarget = enemy.Position.Offset(pushDx, pushDy);

                if (pushTarget.IsValid(GridSize) &&
                    pushTarget != player.Position &&
                    !IsTileOccupiedByEnemy(pushTarget))
                {
                    enemy.Position = pushTarget;
                    pushedCount++;
                    TriggerHazardsForEnemy(enemy);
                }

                if (specialDamage > 0 && enemy.CurrentHp > 0)
                {
                    ApplyDamageToEnemy(enemy, specialDamage, false, hero.SpecialName);
                    damagedCount++;
                }
            }

            LastSpecialEffectType = "VikingScream";
            LastSpecialOrigin = player.Position;
            LastSpecialSequenceId++;

            string damagePart = specialDamage > 0
                ? $" and dealt {specialDamage} dmg to {damagedCount} target(s)"
                : string.Empty;

            LastActionFeedback = $"{hero.SpecialName}! Knocked back {pushedCount} enemy(s){damagePart}.";
            AddCombatLog($"Turn {CurrentRun.TurnNumber}: {hero.Name} used {hero.SpecialName}! Pushed {pushedCount} enemy(s){damagePart}.");
        }

        private void ExecuteIronBulwark(PlayerState player, CharacterDefinition hero, int specialDamage)
        {
            player.IsImmuneThisTurn = true;

            int reducedCards = 0;
            foreach (CardInstance card in GetAllOwnedCards())
            {
                if (card.CurrentCooldown > 0)
                {
                    card.CurrentCooldown = Math.Max(0, card.CurrentCooldown - 1);
                    reducedCards++;
                }
            }

            int damagedCount = 0;
            if (specialDamage > 0)
            {
                List<EnemyInstance> orthogonalEnemies = CurrentRun.Enemies
                    .Where(e => e.CurrentHp > 0 && e.Position.ManhattanDistance(player.Position) == 1)
                    .ToList();

                foreach (EnemyInstance enemy in orthogonalEnemies)
                {
                    ApplyDamageToEnemy(enemy, specialDamage, false, hero.SpecialName);
                    damagedCount++;
                }
            }

            LastSpecialEffectType = "IronBulwark";
            LastSpecialOrigin = player.Position;
            LastSpecialSequenceId++;

            string damagePart = specialDamage > 0
                ? $" Dealt {specialDamage} dmg to {damagedCount} adjacent enemy(s)."
                : string.Empty;

            LastActionFeedback = $"{hero.SpecialName}! Damage immunity active & {reducedCards} card(s) cooled down.{damagePart}";
            AddCombatLog($"Turn {CurrentRun.TurnNumber}: {hero.Name} used {hero.SpecialName}! Immune this turn; cooldowns -1.{damagePart}");
        }

        private void EndPlayerTurn(bool usedWait, bool usedSpecial)
        {
            PlayerState player = CurrentRun.Player;
            int extraReduction = usedWait ? GetWaitEquippedCooldownBonus() : 0;

            foreach (CardInstance card in player.Hand)
            {
                if (cardsPlayedThisTurn.Contains(card.InstanceId))
                {
                    continue;
                }

                if (card.CurrentCooldown > 0)
                {
                    card.CurrentCooldown = Math.Max(0, card.CurrentCooldown - 1 - extraReduction);
                }
            }

            foreach (CardInstance card in player.EquippedQueue)
            {
                if (card.CurrentCooldown > 0)
                {
                    card.CurrentCooldown = Math.Max(0, card.CurrentCooldown - 1 - extraReduction);
                }
            }

            if (!usedSpecial && player.SpecialCooldown > 0)
            {
                player.SpecialCooldown--;
            }

            TickPoisonAndHazards();

            if (!CurrentRun.IsGameOver && CurrentRun.Enemies.Any(e => e.CurrentHp > 0))
            {
                enemyAI.ExecuteEnemyTurns(this);
            }

            player.IsImmuneThisTurn = false;

            if (!CurrentRun.IsGameOver)
            {
                CurrentRun.TurnNumber++;
                CheckWaveClearOrVictory();
            }
        }

        private void TickPoisonAndHazards()
        {
            List<EnemyInstance> poisonedEnemies = CurrentRun.Enemies
                .Where(e => e.CurrentHp > 0 && e.PoisonTurnsRemaining > 0)
                .ToList();

            foreach (EnemyInstance enemy in poisonedEnemies)
            {
                enemy.PoisonTurnsRemaining--;
                ApplyDamageToEnemy(enemy, 1, false, "Poison");
            }

            for (int i = CurrentRun.Hazards.Count - 1; i >= 0; i--)
            {
                GroundHazard hazard = CurrentRun.Hazards[i];
                if (hazard.RemainingTurns > 0)
                {
                    hazard.RemainingTurns--;
                    if (hazard.RemainingTurns == 0)
                    {
                        CurrentRun.Hazards.RemoveAt(i);
                    }
                }
            }
        }

        public CardInstance CreateCardInstance(WeaponDefinition definition)
        {
            CardInstance card = new CardInstance
            {
                InstanceId = CurrentRun.NextCardInstanceId,
                DefinitionId = definition.Id,
                Definition = definition,
                CurrentCooldown = 0,
                BonusDamage = 0,
                CooldownModifier = 0,
                HasPoison = false
            };

            CurrentRun.NextCardInstanceId++;
            return card;
        }

        private int SumUpgradeEffect(PermanentUpgradeEffectType effectType)
        {
            int sum = 0;
            foreach (PermanentUpgradeDefinition upgrade in database.UpgradeList)
            {
                if (upgrade.EffectType == effectType && profile.UnlockedUpgradeIds.Contains(upgrade.Id))
                {
                    sum += upgrade.EffectValue;
                }
            }

            return sum;
        }

        private bool IsTileOccupiedByEnemy(GridPos pos)
        {
            return CurrentRun.Enemies.Any(e => e.CurrentHp > 0 && e.Position == pos);
        }

        private void RemoveDefeatedEnemies()
        {
            CurrentRun.Enemies.RemoveAll(e => e.CurrentHp <= 0);
        }

        public void ArmBearTrapAt(GridPos pos)
        {
            foreach (GroundHazard hazard in CurrentRun.Hazards)
            {
                if (hazard.Type == HazardType.BearTrap && hazard.Position == pos && !hazard.IsArmed)
                {
                    hazard.IsArmed = true;
                    AddCombatLog($"Bear Trap at {pos} is now armed!");
                }
            }
        }

        public void AddCombatLog(string entry)
        {
            if (CurrentRun == null)
            {
                return;
            }

            CurrentRun.RecentLogEntries.Add(entry);
            while (CurrentRun.RecentLogEntries.Count > 50)
            {
                CurrentRun.RecentLogEntries.RemoveAt(0);
            }
        }

        private static string GetDirectionName(int dx, int dy)
        {
            if (dx == 0 && dy == -1)
            {
                return "Up";
            }

            if (dx == 0 && dy == 1)
            {
                return "Down";
            }

            if (dx == -1 && dy == 0)
            {
                return "Left";
            }

            if (dx == 1 && dy == 0)
            {
                return "Right";
            }

            return "Unknown";
        }
    }
}