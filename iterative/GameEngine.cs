#nullable disable
using System.Security.Cryptography;

namespace RogueCards
{
    public class GameEngine
    {
        public const int GridSize = 5;
        public const int MaxEquippedSlots = 3;

        private readonly GameDatabase _database;
        private readonly PlayerProfile _profile;
        private readonly PersistenceManager _persistenceManager;
        private readonly AchievementManager _achievementManager;

        private int _currentExecutingQueueIndex;
        private bool _isExecutingQueue;
        private readonly HashSet<int> _cardsPlayedThisTurn = [];

        public ActiveRunState CurrentRun { get; private set; }
        public string LastActionFeedback { get; private set; }
        public string LastSpecialEffectType { get; private set; }
        public GridPos LastSpecialOrigin { get; private set; }
        public int LastSpecialSequenceId { get; private set; }

        public bool IsExecutingQueue => _isExecutingQueue;
        public int CurrentExecutingQueueIndex => _currentExecutingQueueIndex;
        public GameDatabase Database => _database;

        public CardInstance CurrentAimingCard
        {
            get
            {
                if (!_isExecutingQueue || CurrentRun?.Player == null)
                {
                    return null;
                }

                if (_currentExecutingQueueIndex < 0 || _currentExecutingQueueIndex >= CurrentRun.Player.EquippedQueue.Count)
                {
                    return null;
                }

                return CurrentRun.Player.EquippedQueue[_currentExecutingQueueIndex];
            }
        }

        public GameEngine(
            GameDatabase database,
            PlayerProfile profile,
            PersistenceManager persistenceManager,
            AchievementManager achievementManager)
        {
            _database = database;
            _profile = profile;
            _persistenceManager = persistenceManager;
            _achievementManager = achievementManager;
            LastActionFeedback = string.Empty;
            LastSpecialEffectType = string.Empty;
        }

        public void StartNewRun()
        {
            CharacterDefinition character = GetSelectedCharacterDefinition();
            int maxHp = character.BaseHp + GetMaxHpBonus();

            CurrentRun = new ActiveRunState
            {
                Player = new PlayerState
                {
                    CharacterId = character.Id,
                    Position = new GridPos(2, 2),
                    CurrentHp = maxHp,
                    MaxHp = maxHp,
                    SpecialCooldown = 0,
                    SpecialMaxCooldown = character.SpecialCooldown,
                    IsImmuneThisTurn = false,
                    FreeRerollsRemaining = GetFreeRerollBonus()
                },
                CurrentWave = 1,
                TurnNumber = 1,
                IsStartingDraft = true,
                UsedRerollInRun = false,
                TookDamageInCurrentWave = false,
                IsVictory = false,
                IsGameOver = false,
                GameOverReason = string.Empty,
                NextEnemyInstanceId = 1,
                NextCardInstanceId = 1,
                CombatLogFilePath = _persistenceManager.CreateNewCombatLogPath()
            };

            _isExecutingQueue = false;
            _currentExecutingQueueIndex = 0;
            _cardsPlayedThisTurn.Clear();

            BuildStartingDraftOptions();
            _achievementManager.RecordRunStarted();

            LastActionFeedback = $"Choose 2 starting cards for {character.Name}.";
            LastSpecialEffectType = string.Empty;
            AddCombatLog($"New run started as {character.Name} ({maxHp}/{maxHp} HP).");

            List<string> activeUpgrades = GetActiveUpgradeNames();
            if (activeUpgrades.Count > 0)
            {
                AddCombatLog($"Permanent upgrades active: {string.Join(", ", activeUpgrades)}.");
            }

            SaveCurrentRun();
        }

        public void LoadSavedRun(ActiveRunState loadedRun)
        {
            CurrentRun = loadedRun;
            _isExecutingQueue = false;
            _currentExecutingQueueIndex = 0;
            _cardsPlayedThisTurn.Clear();
            LastSpecialEffectType = string.Empty;

            if (string.IsNullOrWhiteSpace(CurrentRun.CombatLogFilePath))
            {
                CurrentRun.CombatLogFilePath = _persistenceManager.CreateNewCombatLogPath();
            }

            LastActionFeedback = $"Resumed saved run (Wave {CurrentRun.CurrentWave}, Turn {CurrentRun.TurnNumber}).";
            AddCombatLog($"--- Resumed saved run (Wave {CurrentRun.CurrentWave}, Turn {CurrentRun.TurnNumber}) ---");
        }

        public bool SaveCurrentRun() =>
            CurrentRun != null && !CurrentRun.IsGameOver && _persistenceManager.SaveActiveRun(CurrentRun);

        private void BuildStartingDraftOptions()
        {
            List<WeaponDefinition> unlocked = PickRandomItems(
                _database.WeaponList.Where(w => _profile.UnlockedWeaponIds.Contains(w.Id)),
                4);

            CurrentRun.PendingReward = new PendingRewardState
            {
                RewardType = RewardType.PickCard,
                DraftCardIds = unlocked.Select(w => w.Id).ToList(),
                DraftSelectedIds = [],
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

            if (draft.DraftSelectedIds.Remove(weaponId))
            {
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
            List<string> chosenNames = [];

            foreach (string weaponId in draft.DraftSelectedIds)
            {
                if (_database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
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

            EnemyAi.SpawnWave(this, CurrentRun.CurrentWave);
            _achievementManager.RecordWaveReached(CurrentRun.CurrentWave);
            SaveCurrentRun();
            return true;
        }

        public bool TryEquipCardFromHand(int handIndex)
        {
            if (!CanTakeArenaAction())
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

        public void ReorderEquippedCard(int fromIndex, int toIndex)
        {
            if (CurrentRun?.Player == null || _isExecutingQueue)
            {
                return;
            }

            List<CardInstance> queue = CurrentRun.Player.EquippedQueue;
            if (fromIndex < 0 || fromIndex >= queue.Count || toIndex < 0 || toIndex >= queue.Count || fromIndex == toIndex)
            {
                return;
            }

            CardInstance moved = queue[fromIndex];
            queue.RemoveAt(fromIndex);
            queue.Insert(toIndex, moved);

            LastActionFeedback = $"Reordered {moved.Definition.Name} to Slot #{toIndex + 1} (0 turns).";
            AddCombatLog($"Reordered queue: {moved.Definition.Name} moved to Slot #{toIndex + 1} (free action).");
            SaveCurrentRun();
        }

        public bool BeginPlayEquippedQueue()
        {
            if (!CanTakeArenaAction())
            {
                return false;
            }

            if (CurrentRun.Player.EquippedQueue.Count == 0)
            {
                LastActionFeedback = "No equipped cards in the queue to play.";
                return false;
            }

            _isExecutingQueue = true;
            _currentExecutingQueueIndex = 0;
            _cardsPlayedThisTurn.Clear();

            AdvanceQueueThroughAutoCards();
            return true;
        }

        public bool CancelAimingIfPossible()
        {
            if (!_isExecutingQueue || _currentExecutingQueueIndex > 0)
            {
                return false;
            }

            _isExecutingQueue = false;
            _currentExecutingQueueIndex = 0;
            _cardsPlayedThisTurn.Clear();
            LastActionFeedback = "Cancelled queue execution.";
            return true;
        }

        public AimPreviewResult GetAimPreview(GridPos direction)
        {
            CardInstance card = CurrentAimingCard;
            return card == null
                ? new AimPreviewResult { IsValidAim = false }
                : CardResolver.ComputeAimPreview(CurrentRun, card, direction);
        }

        public bool ConfirmAimDirection(GridPos direction)
        {
            if (!_isExecutingQueue || CurrentRun?.Player == null)
            {
                return false;
            }

            CardInstance card = CurrentAimingCard;
            if (card == null)
            {
                FinishEquippedQueueExecution();
                return true;
            }

            AimPreviewResult preview = CardResolver.ComputeAimPreview(CurrentRun, card, direction);
            if (!preview.IsValidAim)
            {
                LastActionFeedback = $"Invalid aim direction for {card.Definition.Name}.";
                return false;
            }

            if (!CardResolver.ResolveCard(this, card, direction))
            {
                return false;
            }

            card.CurrentCooldown = GetEffectiveCardMaxCooldown(card);
            _cardsPlayedThisTurn.Add(card.InstanceId);
            _achievementManager.RecordCardPlayed(card.Definition.Name);
            LastActionFeedback = $"Executed {card.Definition.Name}!";
            _currentExecutingQueueIndex++;

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
            while (_isExecutingQueue && _currentExecutingQueueIndex < CurrentRun.Player.EquippedQueue.Count)
            {
                CardInstance nextCard = CurrentRun.Player.EquippedQueue[_currentExecutingQueueIndex];
                if (nextCard.Definition.Targeting is TargetingType.Self or TargetingType.None)
                {
                    CardResolver.ResolveCard(this, nextCard, new GridPos(0, 0));
                    nextCard.CurrentCooldown = GetEffectiveCardMaxCooldown(nextCard);
                    _cardsPlayedThisTurn.Add(nextCard.InstanceId);
                    _achievementManager.RecordCardPlayed(nextCard.Definition.Name);
                    _currentExecutingQueueIndex++;
                }
                else
                {
                    LastActionFeedback = $"Aim {nextCard.Definition.Name} ({nextCard.Definition.Targeting}).";
                    return;
                }
            }

            if (_isExecutingQueue && _currentExecutingQueueIndex >= CurrentRun.Player.EquippedQueue.Count)
            {
                FinishEquippedQueueExecution();
            }
        }

        private void FinishEquippedQueueExecution()
        {
            PlayerState player = CurrentRun.Player;
            int playedCount = _currentExecutingQueueIndex;

            if (playedCount > 0)
            {
                _achievementManager.RecordCardsPlayedInSingleTurn(playedCount);
            }

            player.Hand.AddRange(player.EquippedQueue);
            player.EquippedQueue.Clear();
            _isExecutingQueue = false;
            _currentExecutingQueueIndex = 0;

            LastActionFeedback = $"Executed {playedCount} card(s).";
            EndPlayerTurn(false, false);
            _cardsPlayedThisTurn.Clear();
        }

        private void ResetAllCooldowns()
        {
            if (CurrentRun?.Player == null)
            {
                return;
            }

            foreach (CardInstance card in GetAllOwnedCards())
            {
                card.CurrentCooldown = 0;
            }

            CurrentRun.Player.SpecialCooldown = 0;
            AddCombatLog("All card and Special cooldowns reset to 0!");
        }

        private GameScreen TriggerWaveClearReward(int clearedWaveNumber)
        {
            WaveDefinition wave = _database.Waves.FirstOrDefault(w => w.WaveNumber == clearedWaveNumber);
            RewardType rewardType = wave?.RewardAfterClear ?? RewardType.None;

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
                SaveCurrentRun();
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
                SaveCurrentRun();
                return GameScreen.RewardUpgrade;
            }

            return GameScreen.Arena;
        }

        private void PopulatePickCardChoices(PendingRewardState reward)
        {
            reward.DraftCardIds.Clear();

            List<WeaponDefinition> unlocked = _database.WeaponList
                .Where(w => _profile.UnlockedWeaponIds.Contains(w.Id))
                .ToList();

            List<WeaponDefinition> offensePool = unlocked
                .Where(w => w.Category is CardCategory.Melee or CardCategory.Ranged)
                .ToList();

            List<WeaponDefinition> utilityPool = unlocked
                .Where(w => w.Category is CardCategory.Movement or CardCategory.Special)
                .ToList();

            if (offensePool.Count > 0)
            {
                reward.DraftCardIds.Add(offensePool[RandomNumberGenerator.GetInt32(offensePool.Count)].Id);
            }

            if (utilityPool.Count > 0)
            {
                reward.DraftCardIds.Add(utilityPool[RandomNumberGenerator.GetInt32(utilityPool.Count)].Id);
            }
        }

        private static void PopulateUpgradeChoices(PendingRewardState reward)
        {
            reward.UpgradeOptions.Clear();

            UpgradeModifierType[] allTypes =
            [
                UpgradeModifierType.PlusOneDamage,
                UpgradeModifierType.PlusTwoDamage,
                UpgradeModifierType.MinusOneCooldown,
                UpgradeModifierType.MinusTwoCooldown,
                UpgradeModifierType.PoisonEffect
            ];

            List<UpgradeOption> picked = PickRandomItems(allTypes, 3)
                .Select(PersistenceManager.BuildUpgradeOption)
                .ToList();

            reward.UpgradeOptions.AddRange(picked);
        }

        public bool PickRewardCard(string weaponId)
        {
            if (CurrentRun?.PendingReward?.RewardType != RewardType.PickCard)
            {
                return false;
            }

            if (!CurrentRun.PendingReward.DraftCardIds.Contains(weaponId) ||
                !_database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
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
            if (CurrentRun?.PendingReward?.RewardType != RewardType.Upgrade)
            {
                return false;
            }

            CardInstance targetCard = GetAllOwnedCards().FirstOrDefault(c => c.InstanceId == cardInstanceId);
            if (targetCard == null)
            {
                return false;
            }

            ApplyModifierToCard(targetCard, modifierType);
            CurrentRun.PendingReward = null;
            LastActionFeedback = $"Upgraded {targetCard.Definition.Name}!";
            AddCombatLog($"Reward chosen: Applied {modifierType} to {targetCard.Definition.Name}.");
            AdvanceToNextWave();
            return true;
        }

        private static void ApplyModifierToCard(CardInstance targetCard, UpgradeModifierType modifierType)
        {
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
        }

        public bool SkipCurrentReward()
        {
            if (CurrentRun?.PendingReward == null)
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
            if (CurrentRun?.PendingReward == null)
            {
                return false;
            }

            PendingRewardState reward = CurrentRun.PendingReward;
            if (!ConsumeRerollCost(reward))
            {
                return false;
            }

            if (reward.RewardType == RewardType.PickCard)
            {
                PopulatePickCardChoices(reward);
            }
            else if (reward.RewardType == RewardType.Upgrade)
            {
                PopulateUpgradeChoices(reward);
            }

            SaveCurrentRun();
            return true;
        }

        private bool ConsumeRerollCost(PendingRewardState reward)
        {
            PlayerState player = CurrentRun.Player;
            if (player.FreeRerollsRemaining > 0)
            {
                player.FreeRerollsRemaining--;
                CurrentRun.UsedRerollInRun = true;
                LastActionFeedback = "Used Free Reroll (Charisma)!";
                AddCombatLog("Used Free Reroll from Charisma upgrade.");
                return true;
            }

            int cost = reward.NextRerollCost;
            if (_profile.Coins < cost)
            {
                LastActionFeedback = $"Not enough coins to reroll (requires {cost} Coins).";
                return false;
            }

            _profile.Coins -= cost;
            reward.RerollCount++;
            reward.NextRerollCost = cost * 2;
            CurrentRun.UsedRerollInRun = true;
            _persistenceManager.SaveProfile(_profile);
            LastActionFeedback = $"Rerolled reward for {cost} Coins (next reroll: {reward.NextRerollCost} Coins).";
            AddCombatLog($"Rerolled reward for {cost} Coins.");
            return true;
        }

        private void AdvanceToNextWave()
        {
            CurrentRun.CurrentWave++;
            CurrentRun.TookDamageInCurrentWave = false;
            EnemyAi.SpawnWave(this, CurrentRun.CurrentWave);
            _achievementManager.RecordWaveReached(CurrentRun.CurrentWave);
            SaveCurrentRun();
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

            _achievementManager.RecordWaveCleared(CurrentRun.CurrentWave, CurrentRun.TookDamageInCurrentWave);

            int maxWave = _database.Waves.Count > 0 ? _database.Waves.Max(w => w.WaveNumber) : 10;
            if (CurrentRun.CurrentWave >= maxWave)
            {
                TriggerPlayerVictory();
                return;
            }

            if (TriggerWaveClearReward(CurrentRun.CurrentWave) == GameScreen.Arena)
            {
                AddCombatLog($"Wave {CurrentRun.CurrentWave} cleared!");
                AdvanceToNextWave();
            }
        }

        private void TriggerPlayerVictory()
        {
            CurrentRun.IsVictory = true;
            CurrentRun.IsGameOver = true;
            CurrentRun.GameOverReason = "Victory! You defeated the Viking Chief and conquered Level 1!";
            _profile.Area2Unlocked = true;
            _achievementManager.RecordRunWon(CurrentRun.UsedRerollInRun);
            _persistenceManager.SaveProfile(_profile);
            _persistenceManager.ClearActiveRunSave();
            LastActionFeedback = "VICTORY! Viking Chief defeated!";
            AddCombatLog("VICTORY! Level 1 completed and Area 2 unlocked!");
        }

        public List<CardInstance> GetAllOwnedCards()
        {
            if (CurrentRun?.Player == null)
            {
                return [];
            }

            List<CardInstance> cards = [];
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

            int actualDamage = Math.Min(enemy.CurrentHp, Math.Max(0, damage));
            if (actualDamage > 0)
            {
                enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - actualDamage);
                _achievementManager.RecordDamageDealt(actualDamage);
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
                HandleEnemyDefeated(enemy, false);
            }
        }

        public void ApplyFriendlyFireDamage(EnemyInstance attacker, EnemyInstance victim, int damage, string weaponLabel)
        {
            if (victim == null || victim.CurrentHp <= 0)
            {
                return;
            }

            int actualDamage = Math.Min(victim.CurrentHp, Math.Max(0, damage));
            victim.CurrentHp = Math.Max(0, victim.CurrentHp - actualDamage);
            if (actualDamage > 0)
            {
                _achievementManager.RecordDamageDealt(actualDamage);
            }

            AddCombatLog($"FRIENDLY FIRE! {attacker.Name}'s {weaponLabel} hit {victim.Name} for {damage} dmg ({victim.CurrentHp}/{victim.MaxHp} HP).");

            if (victim.CurrentHp <= 0)
            {
                HandleEnemyDefeated(victim, true);
            }
        }

        private void HandleEnemyDefeated(EnemyInstance enemy, bool isFriendlyFire)
        {
            _profile.Coins += enemy.CoinDrop;
            _achievementManager.RecordEnemyDefeated(enemy.Type, enemy.CoinDrop, isFriendlyFire);
            _persistenceManager.SaveProfile(_profile);
            string suffix = isFriendlyFire ? "killed by friendly fire!" : "defeated!";
            AddCombatLog($"{enemy.Name} {suffix} (+{enemy.CoinDrop} Coins)");
            CurrentRun.Enemies.RemoveAll(e => e.CurrentHp <= 0);
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
            _isExecutingQueue = false;
            _persistenceManager.SaveProfile(_profile);
            _persistenceManager.ClearActiveRunSave();
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
                _achievementManager.RecordBearTrapStun();
                ApplyDamageToEnemy(enemy, trap.Damage, false, "Bear Trap");
                if (enemy.CurrentHp > 0)
                {
                    AddCombatLog($"{enemy.Name} is stunned by Bear Trap and loses next turn!");
                }
            }

            if (enemy.CurrentHp <= 0)
            {
                return;
            }

            GroundHazard poison = CurrentRun.Hazards.FirstOrDefault(
                h => h.Type == HazardType.Poison && h.Position == enemy.Position);

            if (poison != null)
            {
                ApplyDamageToEnemy(enemy, poison.Damage, false, "Poison Pool");
            }
        }

        public void TriggerHazardsForPlayer()
        {
            PlayerState player = CurrentRun.Player;
            GroundHazard poison = CurrentRun.Hazards.FirstOrDefault(
                h => h.Type == HazardType.Poison && h.Position == player.Position);

            if (poison == null)
            {
                return;
            }

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

        private CharacterDefinition GetSelectedCharacterDefinition()
        {
            if (_profile != null &&
                !string.IsNullOrEmpty(_profile.SelectedCharacterId) &&
                _database.Characters.TryGetValue(_profile.SelectedCharacterId, out CharacterDefinition selected))
            {
                return selected;
            }

            return _database.Characters.TryGetValue("viking", out CharacterDefinition viking)
                ? viking
                : _database.CharacterList.First();
        }

        public CharacterDefinition GetActiveCharacterDefinition()
        {
            if (CurrentRun?.Player != null &&
                _database.Characters.TryGetValue(CurrentRun.Player.CharacterId, out CharacterDefinition active))
            {
                return active;
            }

            return GetSelectedCharacterDefinition();
        }

        public int GetMaxHpBonus() => SumUpgradeEffect(PermanentUpgradeEffectType.MaxHp);
        public int GetMovementCooldownReduction() => SumUpgradeEffect(PermanentUpgradeEffectType.MovementCooldown);
        public int GetFreeRerollBonus() => SumUpgradeEffect(PermanentUpgradeEffectType.FreeReroll);
        public int GetMeleeDamageBonus() => SumUpgradeEffect(PermanentUpgradeEffectType.MeleeDamage);
        public int GetRangedDamageBonus() => SumUpgradeEffect(PermanentUpgradeEffectType.RangedDamage);
        public int GetWaitEquippedCooldownBonus() => SumUpgradeEffect(PermanentUpgradeEffectType.WaitEquippedCooldown);
        public int GetSpecialDamageBonus() => SumUpgradeEffect(PermanentUpgradeEffectType.SpecialDamage);

        public List<string> GetActiveUpgradeNames() =>
            _database.UpgradeList
                .Where(u => _profile.UnlockedUpgradeIds.Contains(u.Id))
                .Select(u => u.Name)
                .ToList();

        public int GetEffectiveCardDamage(CardInstance card)
        {
            if (card?.Definition == null)
            {
                return 0;
            }

            int permanentBonus = card.Definition.Category switch
            {
                CardCategory.Melee => GetMeleeDamageBonus(),
                CardCategory.Ranged => GetRangedDamageBonus(),
                _ => 0
            };

            return card.GetEffectiveDamage(permanentBonus);
        }

        public int GetEffectiveCardMaxCooldown(CardInstance card)
        {
            if (card?.Definition == null)
            {
                return 1;
            }

            int permanentReduction = card.Definition.Category == CardCategory.Movement
                ? GetMovementCooldownReduction()
                : 0;

            return card.GetEffectiveMaxCooldown(permanentReduction);
        }

        public List<GridPos> GetValidMoveTiles()
        {
            if (!CanTakeArenaAction())
            {
                return [];
            }

            GridPos origin = CurrentRun.Player.Position;
            GridPos[] candidates =
            [
                origin.Offset(0, -1),
                origin.Offset(0, 1),
                origin.Offset(-1, 0),
                origin.Offset(1, 0)
            ];

            return candidates.Where(tile => tile.IsValid() && !IsTileOccupiedByEnemy(tile)).ToList();
        }

        public bool TryMovePlayerToTile(GridPos target)
        {
            if (!CanTakeArenaAction())
            {
                return false;
            }

            int dx = target.X - CurrentRun.Player.Position.X;
            int dy = target.Y - CurrentRun.Player.Position.Y;
            return Math.Abs(dx) + Math.Abs(dy) == 1 && TryMovePlayer(dx, dy);
        }

        public bool TryMovePlayer(int dx, int dy)
        {
            if (!CanTakeArenaAction() || Math.Abs(dx) + Math.Abs(dy) != 1)
            {
                return false;
            }

            GridPos oldPosition = CurrentRun.Player.Position;
            GridPos target = oldPosition.Offset(dx, dy);

            if (!target.IsValid())
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
            if (!CanTakeArenaAction())
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
            if (!CanTakeArenaAction())
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
                GridPos pushTarget = enemy.Position.Offset(
                    Math.Sign(enemy.Position.X - player.Position.X),
                    Math.Sign(enemy.Position.Y - player.Position.Y));

                if (pushTarget.IsValid() && pushTarget != player.Position && !IsTileOccupiedByEnemy(pushTarget))
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
            int reducedCards = ReduceAllOwnedCardCooldowns(1);
            int damagedCount = ApplyShieldmaidenSpecialDamage(player, hero, specialDamage);

            LastSpecialEffectType = "IronBulwark";
            LastSpecialOrigin = player.Position;
            LastSpecialSequenceId++;

            string damagePart = specialDamage > 0
                ? $" Dealt {specialDamage} dmg to {damagedCount} adjacent enemy(s)."
                : string.Empty;

            LastActionFeedback = $"{hero.SpecialName}! Damage immunity active & {reducedCards} card(s) cooled down.{damagePart}";
            AddCombatLog($"Turn {CurrentRun.TurnNumber}: {hero.Name} used {hero.SpecialName}! Immune this turn; cooldowns -1.{damagePart}");
        }

        private int ReduceAllOwnedCardCooldowns(int amount)
        {
            int reduced = 0;
            foreach (CardInstance card in GetAllOwnedCards().Where(c => c.CurrentCooldown > 0))
            {
                card.CurrentCooldown = Math.Max(0, card.CurrentCooldown - amount);
                reduced++;
            }

            return reduced;
        }

        private int ApplyShieldmaidenSpecialDamage(PlayerState player, CharacterDefinition hero, int specialDamage)
        {
            if (specialDamage <= 0)
            {
                return 0;
            }

            List<EnemyInstance> orthogonalEnemies = CurrentRun.Enemies
                .Where(e => e.CurrentHp > 0 && e.Position.ManhattanDistance(player.Position) == 1)
                .ToList();

            foreach (EnemyInstance enemy in orthogonalEnemies)
            {
                ApplyDamageToEnemy(enemy, specialDamage, false, hero.SpecialName);
            }

            return orthogonalEnemies.Count;
        }

        private void EndPlayerTurn(bool usedWait, bool usedSpecial)
        {
            _achievementManager.RecordTurnTaken();
            TickPlayerCooldowns(usedWait, usedSpecial);
            TickPoisonAndHazards();

            if (!CurrentRun.IsGameOver && CurrentRun.Enemies.Any(e => e.CurrentHp > 0))
            {
                EnemyAi.ExecuteEnemyTurns(this);
            }

            CurrentRun.Player.IsImmuneThisTurn = false;

            if (CurrentRun.IsGameOver)
            {
                return;
            }

            CurrentRun.TurnNumber++;
            CheckWaveClearOrVictory();
            if (!CurrentRun.IsGameOver)
            {
                SaveCurrentRun();
            }
        }

        private void TickPlayerCooldowns(bool usedWait, bool usedSpecial)
        {
            PlayerState player = CurrentRun.Player;
            int reduction = 1 + (usedWait ? GetWaitEquippedCooldownBonus() : 0);

            foreach (CardInstance card in player.Hand)
            {
                if (!_cardsPlayedThisTurn.Contains(card.InstanceId) && card.CurrentCooldown > 0)
                {
                    card.CurrentCooldown = Math.Max(0, card.CurrentCooldown - reduction);
                }
            }

            foreach (CardInstance card in player.EquippedQueue.Where(c => c.CurrentCooldown > 0))
            {
                card.CurrentCooldown = Math.Max(0, card.CurrentCooldown - reduction);
            }

            if (!usedSpecial && player.SpecialCooldown > 0)
            {
                player.SpecialCooldown--;
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
                if (hazard.RemainingTurns > 0 && --hazard.RemainingTurns == 0)
                {
                    CurrentRun.Hazards.RemoveAt(i);
                }
            }
        }

        private CardInstance CreateCardInstance(WeaponDefinition definition)
        {
            CardInstance card = new()
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

        private bool CanTakeArenaAction() =>
            CurrentRun?.Player != null && !CurrentRun.IsGameOver && !_isExecutingQueue;

        private int SumUpgradeEffect(PermanentUpgradeEffectType effectType) =>
            _database.UpgradeList
                .Where(u => u.EffectType == effectType && _profile.UnlockedUpgradeIds.Contains(u.Id))
                .Sum(u => u.EffectValue);

        private bool IsTileOccupiedByEnemy(GridPos pos) =>
            CurrentRun.Enemies.Any(e => e.CurrentHp > 0 && e.Position == pos);

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
            if (CurrentRun == null || string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            CurrentRun.RecentLogEntries.Add(entry);
            while (CurrentRun.RecentLogEntries.Count > 50)
            {
                CurrentRun.RecentLogEntries.RemoveAt(0);
            }

            PersistenceManager.AppendCombatLogLine(CurrentRun.CombatLogFilePath, entry);
        }

        private static List<T> PickRandomItems<T>(IEnumerable<T> source, int count)
        {
            List<T> pool = source.ToList();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            return pool.Take(count).ToList();
        }

        private static string GetDirectionName(int dx, int dy) => (dx, dy) switch
        {
            (0, -1) => "Up",
            (0, 1) => "Down",
            (-1, 0) => "Left",
            (1, 0) => "Right",
            _ => "Unknown"
        };
    }
}