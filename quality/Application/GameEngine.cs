using System;
using System.Collections.Generic;
using System.Linq;
using RogueCards.Domain;
namespace RogueCards.Application
{
    public sealed class GameEngine : ICombatContext
    {
        private const int MaxEquippedCards = 3;
        private const int PoisonDurationTurns = 3;
        private readonly IGameDataRepository _repository;
        private readonly IRunPersistence _persistence;
        private readonly ICombatLogger _combatLogger;
        private readonly CardEffectRegistry _cardEffects;
        private readonly EnemyBehaviorRegistry _enemyBehaviors;
        private readonly RewardService _rewardService;
        private readonly Random _random;

        private readonly HashSet<int> _cardIdsActivatedThisTurn = new HashSet<int>();
        private bool _specialActivatedThisTurn;
        private bool _isExecutingQueue;
        private int _queueExecutionIndex;
        private int _cardsPlayedThisTurn;

        public GameDatabase Database { get; }

        public PlayerProfile Profile { get; }

        public RewardService Rewards
        {
            get
            {
                return _rewardService;
            }
        }

        public string StartupWarning { get; }

        public ActiveRunState? CurrentRun { get; private set; }

        public CardInstance? CurrentAimingCard { get; private set; }

        public int CurrentExecutingQueueIndex
        {
            get
            {
                return _isExecutingQueue ? _queueExecutionIndex : -1;
            }
        }

        public bool CanCancelAiming
        {
            get
            {
                return _isExecutingQueue && _queueExecutionIndex == 0 && CurrentAimingCard != null;
            }
        }

        public ActiveRunState Run
        {
            get
            {
                if (CurrentRun == null)
                {
                    throw new InvalidOperationException("No active run is currently in progress.");
                }

                return CurrentRun;
            }
        }

        public event Action? RunStarted;

        public event Action? TurnCompleted;

        public event Action<int>? DamageDealtToEnemy;

        public event Action<string>? WeaponUsed;

        public event Action<EnemyType, int, bool>? EnemyDefeated;

        public event Action<int, bool>? WaveCleared;

        public event Action<int>? CardsPlayedInTurn;

        public event Action? BearTrapStunTriggered;

        public event Action<bool>? RunWon;

        public event Action? MenuUnlockPurchased;

        public GameEngine(
            IGameDataRepository repository,
            IRunPersistence persistence,
            ICombatLogger combatLogger,
            CardEffectRegistry cardEffects,
            EnemyBehaviorRegistry enemyBehaviors,
            RewardService rewardService,
            Random? random = null)
        {
            _repository = repository;
            _persistence = persistence;
            _combatLogger = combatLogger;
            _cardEffects = cardEffects;
            _enemyBehaviors = enemyBehaviors;
            _rewardService = rewardService;
            _random = random ?? new Random();

            Database = _repository.LoadAll();
            Profile = _persistence.LoadProfile(Database, out string warning);
            StartupWarning = warning;
        }

        public bool HasActiveRunSave()
        {
            return _persistence.HasActiveRunSave();
        }

        public bool HasPermanentUpgrade(PermanentUpgradeType upgradeType)
        {
            foreach (PermanentUpgradeDefinition def in Database.UpgradeList)
            {
                if (def.UpgradeType == upgradeType && Profile.UnlockedUpgradeIds.Contains(def.Id))
                {
                    return true;
                }
            }

            return false;
        }

        public int GetEffectiveCardDamage(CardInstance card)
        {
            bool hasMelee = HasPermanentUpgrade(PermanentUpgradeType.MeleeTraining);
            bool hasRanged = HasPermanentUpgrade(PermanentUpgradeType.RangedTraining);
            return card.GetEffectiveDamage(hasMelee, hasRanged);
        }

        public int GetEffectiveCardMaxCooldown(CardInstance card)
        {
            bool hasSwiftBoots = HasPermanentUpgrade(PermanentUpgradeType.SwiftBoots);
            return card.GetEffectiveMaxCooldown(hasSwiftBoots);
        }

        public CharacterDefinition GetSelectedCharacter()
        {
            return ResolveSelectedCharacter();
        }

        public CharacterDefinition GetCurrentRunCharacter()
        {
            return CurrentRun != null ? ResolveRunCharacter() : ResolveSelectedCharacter();
        }

        public IReadOnlyList<WeaponDefinition> GetNextUnlockableWeapons(int maxCount = 2)
        {
            return Database.WeaponList
                .Where(w => !w.IsDefaultUnlocked && !Profile.UnlockedWeaponIds.Contains(w.Id))
                .OrderBy(w => w.UnlockOrder)
                .Take(maxCount)
                .ToList();
        }

        public IReadOnlyList<PermanentUpgradeDefinition> GetNextUnlockableUpgrades(int maxCount = 2)
        {
            return Database.UpgradeList
                .Where(u => !Profile.UnlockedUpgradeIds.Contains(u.Id))
                .OrderBy(u => u.UnlockOrder)
                .Take(maxCount)
                .ToList();
        }

        public bool TrySelectCharacter(string characterId)
        {
            if (!Database.Characters.ContainsKey(characterId) || !Profile.UnlockedCharacterIds.Contains(characterId))
            {
                return false;
            }

            Profile.SelectedCharacterId = characterId;
            _persistence.SaveProfile(Profile);
            return true;
        }

        public bool TryPurchaseWeapon(string weaponId)
        {
            if (!Database.Weapons.TryGetValue(weaponId, out WeaponDefinition? weapon))
            {
                return false;
            }

            if (Profile.UnlockedWeaponIds.Contains(weapon.Id) || Profile.Coins < weapon.UnlockCost)
            {
                return false;
            }

            Profile.Coins -= weapon.UnlockCost;
            Profile.UnlockedWeaponIds.Add(weapon.Id);
            _persistence.SaveProfile(Profile);
            MenuUnlockPurchased?.Invoke();
            return true;
        }

        public bool TryPurchaseUpgrade(string upgradeId)
        {
            if (!Database.Upgrades.TryGetValue(upgradeId, out PermanentUpgradeDefinition? upgrade))
            {
                return false;
            }

            if (Profile.UnlockedUpgradeIds.Contains(upgrade.Id) || Profile.Coins < upgrade.Cost)
            {
                return false;
            }

            Profile.Coins -= upgrade.Cost;
            Profile.UnlockedUpgradeIds.Add(upgrade.Id);
            _persistence.SaveProfile(Profile);
            MenuUnlockPurchased?.Invoke();
            return true;
        }

        public void StartNewRun()
        {
            ResetQueueExecutionState();
            CharacterDefinition character = ResolveSelectedCharacter();

            int bonusHp = HasPermanentUpgrade(PermanentUpgradeType.DurabilityTraining) ? 1 : 0;
            int freeRerolls = HasPermanentUpgrade(PermanentUpgradeType.Charisma) ? 1 : 0;

            ActiveRunState run = new ActiveRunState
            {
                CurrentWave = 1,
                TurnNumber = 1,
                IsStartingDraft = true,
                CombatLogPath = _combatLogger.StartNewRunLog()
            };

            run.Player.CharacterId = character.Id;
            run.Player.Position = new GridPos(2, 2);
            run.Player.MaxHp = character.BaseMaxHp + bonusHp;
            run.Player.CurrentHp = run.Player.MaxHp;
            run.Player.SpecialCooldown = 0;
            run.Player.SpecialMaxCooldown = character.SpecialCooldown;
            run.Player.FreeRerollsRemaining = freeRerolls;
            run.PendingReward = _rewardService.CreateStartingDraft(Profile, Database);

            CurrentRun = run;
            Log($"Started new run as {character.Name} ({run.Player.MaxHp} HP). Choose 2 starting cards.");
            RunStarted?.Invoke();
            AutoSaveRun();
        }

        public bool TryContinueSavedRun(out string error)
        {
            ResetQueueExecutionState();
            if (!_persistence.LoadActiveRun(Database, out ActiveRunState? loaded, out error) || loaded == null)
            {
                return false;
            }

            CurrentRun = loaded;
            Log($"Resumed saved run on Wave {loaded.CurrentWave}, Turn {loaded.TurnNumber}.");
            return true;
        }

        public void SaveAndExitToMenu()
        {
            ResetQueueExecutionState();
            if (CurrentRun != null && !CurrentRun.IsGameOver && !CurrentRun.IsVictory)
            {
                AutoSaveRun();
            }
        }

        public bool TryMovePlayer(int dx, int dy)
        {
            if (!CanTakePlayerAction())
            {
                return false;
            }

            GridPos step = new GridPos(dx, dy);
            if (!step.IsOrthogonalDirection())
            {
                return false;
            }

            GridPos destination = Run.Player.Position.Offset(step);
            if (!destination.IsValid() || Run.IsTileOccupied(destination))
            {
                return false;
            }

            GridPos previousPos = Run.Player.Position;
            Run.Player.Position = destination;
            ArmBearTrapAt(previousPos);
            Log($"Moved to {destination}.");
            TriggerHazardsForPlayer();
            CompletePlayerTurn();
            return true;
        }

        public bool TryWait()
        {
            if (!CanTakePlayerAction())
            {
                return false;
            }

            Log("Waited for 1 turn.");

            if (HasPermanentUpgrade(PermanentUpgradeType.TacticalMaster))
            {
                ReduceAllOwnedCardCooldowns(1);
                Log("Tactical Master reduced card cooldowns by 1 extra turn.");
            }

            CompletePlayerTurn();
            return true;
        }

        public bool TryUseSpecial()
        {
            if (!CanTakePlayerAction() || !Run.Player.IsSpecialReady)
            {
                return false;
            }

            CharacterDefinition character = ResolveRunCharacter();
            bool hasSpecialityMaster = HasPermanentUpgrade(PermanentUpgradeType.SpecialityMaster);

            if (character.SpecialType == CharacterSpecialType.VikingScream)
            {
                ExecuteVikingScream(hasSpecialityMaster);
            }
            else if (character.SpecialType == CharacterSpecialType.IronBulwark)
            {
                ExecuteIronBulwark(hasSpecialityMaster);
            }

            Run.Player.SpecialCooldown = Run.Player.SpecialMaxCooldown;
            _specialActivatedThisTurn = true;

            if (CheckAndHandleWaveCleared())
            {
                return true;
            }

            CompletePlayerTurn();
            return true;
        }

        public bool TryEquipCard(int handIndex)
        {
            if (!CanTakePlayerAction())
            {
                return false;
            }

            PlayerState player = Run.Player;
            if (handIndex < 0 || handIndex >= player.Hand.Count || player.EquippedQueue.Count >= MaxEquippedCards)
            {
                return false;
            }

            CardInstance card = player.Hand[handIndex];
            if (!card.IsReady)
            {
                return false;
            }

            player.Hand.RemoveAt(handIndex);
            player.EquippedQueue.Add(card);
            Log($"Equipped {card.Definition.Name} (slot {player.EquippedQueue.Count}).");
            CompletePlayerTurn();
            return true;
        }

        public void ReorderEquippedCard(int fromIndex, int toIndex)
        {
            if (!CanTakePlayerAction())
            {
                return;
            }

            List<CardInstance> queue = Run.Player.EquippedQueue;
            if (fromIndex < 0 || fromIndex >= queue.Count || toIndex < 0 || toIndex >= queue.Count || fromIndex == toIndex)
            {
                return;
            }

            CardInstance card = queue[fromIndex];
            queue.RemoveAt(fromIndex);
            queue.Insert(toIndex, card);
            AutoSaveRun();
        }

        public bool BeginPlayEquippedQueue()
        {
            if (!CanTakePlayerAction() || Run.Player.EquippedQueue.Count == 0)
            {
                return false;
            }

            _isExecutingQueue = true;
            _queueExecutionIndex = 0;
            _cardsPlayedThisTurn = 0;
            AdvanceQueueExecution();
            return true;
        }

        public bool CancelAiming()
        {
            if (!CanCancelAiming)
            {
                return false;
            }

            ResetQueueExecutionState();
            Log("Cancelled card queue execution.");
            return true;
        }

        public GridPos? ResolveAimDirectionFromTile(GridPos targetTile)
        {
            if (CurrentRun == null || CurrentAimingCard == null || !targetTile.IsValid())
            {
                return null;
            }

            int dx = targetTile.X - CurrentRun.Player.Position.X;
            int dy = targetTile.Y - CurrentRun.Player.Position.Y;

            if (dx == 0 && dy == 0)
            {
                return null;
            }

            TargetingType targeting = CurrentAimingCard.Definition.Targeting;
            if (targeting == TargetingType.Orthogonal)
            {
                return Math.Abs(dx) >= Math.Abs(dy)
                    ? new GridPos(Math.Sign(dx), 0)
                    : new GridPos(0, Math.Sign(dy));
            }

            if (targeting == TargetingType.Diagonal && dx != 0 && dy != 0)
            {
                return new GridPos(Math.Sign(dx), Math.Sign(dy));
            }

            return null;
        }

        public AimPreviewResult ComputeAimPreview(GridPos direction)
        {
            if (CurrentRun == null || CurrentAimingCard == null)
            {
                return new AimPreviewResult { IsValid = false };
            }

            if (!_cardEffects.TryGetEffect(CurrentAimingCard.Definition.EffectType, out ICardEffect? effect) || effect == null)
            {
                return new AimPreviewResult { IsValid = false };
            }

            return effect.ComputePreview(CurrentRun, CurrentAimingCard, direction);
        }

        public bool ConfirmAimDirection(GridPos direction)
        {
            if (CurrentRun == null || !_isExecutingQueue || CurrentAimingCard == null)
            {
                return false;
            }

            if (!_cardEffects.TryGetEffect(CurrentAimingCard.Definition.EffectType, out ICardEffect? effect) || effect == null)
            {
                return false;
            }

            AimPreviewResult preview = effect.ComputePreview(CurrentRun, CurrentAimingCard, direction);
            if (!preview.IsValid)
            {
                return false;
            }

            CardInstance card = CurrentAimingCard;
            CurrentAimingCard = null;
            ExecuteSingleCard(card, effect, direction);

            _queueExecutionIndex += 1;
            AdvanceQueueExecution();
            return true;
        }

        public bool PickRewardCard(string weaponId)
        {
            if (CurrentRun?.PendingReward == null || !Database.Weapons.TryGetValue(weaponId, out WeaponDefinition? weapon))
            {
                return false;
            }

            PendingRewardState reward = CurrentRun.PendingReward;
            if (!reward.OfferedWeaponIds.Contains(weaponId))
            {
                return false;
            }

            if (reward.Type == RewardType.StartingDraft)
            {
                return HandleStartingDraftSelection(reward, weapon);
            }

            if (reward.Type != RewardType.PickCard)
            {
                return false;
            }

            AddCardToHand(weapon);
            Log($"Added {weapon.Name} to your hand.");
            CurrentRun.PendingReward = null;
            StartNextWave();
            return true;
        }

        public bool ApplyUpgradeToCard(UpgradeModifierType modifier, int cardInstanceId)
        {
            if (CurrentRun?.PendingReward == null || CurrentRun.PendingReward.Type != RewardType.Upgrade)
            {
                return false;
            }

            if (!CurrentRun.PendingReward.OfferedUpgrades.Contains(modifier))
            {
                return false;
            }

            CardInstance? card = CurrentRun.Player.GetAllOwnedCards().FirstOrDefault(c => c.InstanceId == cardInstanceId);
            if (card == null)
            {
                return false;
            }

            _rewardService.ApplyModifierToCard(card, modifier);
            Log($"Upgraded {card.Definition.Name} with {_rewardService.GetModifierLabel(modifier)}.");
            CurrentRun.PendingReward = null;
            StartNextWave();
            return true;
        }

        public bool TryRerollReward()
        {
            if (CurrentRun?.PendingReward == null)
            {
                return false;
            }

            PendingRewardState reward = CurrentRun.PendingReward;
            if (CurrentRun.Player.FreeRerollsRemaining > 0)
            {
                CurrentRun.Player.FreeRerollsRemaining -= 1;
                CurrentRun.UsedRerollInRun = true;
                _rewardService.Reroll(reward, Profile, Database);
                Log("Used a free Charisma reroll.");
                AutoSaveRun();
                return true;
            }

            int cost = reward.GetNextRerollCost(0);
            if (Profile.Coins < cost)
            {
                return false;
            }

            Profile.Coins -= cost;
            _persistence.SaveProfile(Profile);
            reward.RerollCount += 1;
            CurrentRun.UsedRerollInRun = true;
            _rewardService.Reroll(reward, Profile, Database);
            Log($"Rerolled reward choices for {cost} coins.");
            AutoSaveRun();
            return true;
        }

        public bool SkipReward()
        {
            if (CurrentRun?.PendingReward == null || !CurrentRun.PendingReward.CanSkip)
            {
                return false;
            }

            Log("Skipped wave reward.");
            CurrentRun.PendingReward = null;
            StartNextWave();
            return true;
        }

        public void DamageEnemy(EnemyInstance enemy, int damage, bool applyPoison, string source)
        {
            if (CurrentRun == null || !enemy.IsAlive || damage < 0)
            {
                return;
            }

            if (damage > 0)
            {
                enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - damage);
                DamageDealtToEnemy?.Invoke(damage);
                Log($"{source} dealt {damage} damage to {enemy.Type} ({enemy.CurrentHp}/{enemy.MaxHp} HP).");
            }

            if (applyPoison && enemy.IsAlive)
            {
                enemy.PoisonTurnsRemaining = PoisonDurationTurns;
                Log($"{enemy.Type} was poisoned for {PoisonDurationTurns} turns.");
            }

            if (!enemy.IsAlive)
            {
                HandleEnemyDefeated(enemy, friendlyFire: false);
            }
        }

        public void DamageEnemyByFriendlyFire(EnemyInstance attacker, EnemyInstance victim, int damage, string source)
        {
            if (CurrentRun == null || !victim.IsAlive || damage <= 0)
            {
                return;
            }

            victim.CurrentHp = Math.Max(0, victim.CurrentHp - damage);
            Log($"Friendly fire! {attacker.Type} hit {victim.Type} with {source} for {damage} damage.");

            if (!victim.IsAlive)
            {
                HandleEnemyDefeated(victim, friendlyFire: true);
            }
        }

        public void DamagePlayer(EnemyInstance? attacker, int damage, string source)
        {
            if (CurrentRun == null || damage <= 0 || CurrentRun.IsGameOver)
            {
                return;
            }

            if (CurrentRun.Player.IsImmuneThisTurn)
            {
                Log($"Iron Bulwark blocked {damage} damage from {source}.");
                return;
            }

            CurrentRun.Player.CurrentHp = Math.Max(0, CurrentRun.Player.CurrentHp - damage);
            CurrentRun.TookDamageInCurrentWave = true;
            Log($"Player took {damage} damage from {source} ({CurrentRun.Player.CurrentHp}/{CurrentRun.Player.MaxHp} HP).");

            if (CurrentRun.Player.CurrentHp <= 0)
            {
                string reason = attacker != null
                    ? $"Defeated by {attacker.Type} ({source}) on Wave {CurrentRun.CurrentWave}."
                    : $"Defeated by {source} on Wave {CurrentRun.CurrentWave}.";
                TriggerDefeat(reason);
            }
        }

        public void TriggerHazardsForEnemy(EnemyInstance enemy)
        {
            if (CurrentRun == null || !enemy.IsAlive)
            {
                return;
            }

            List<GroundHazard> matching = CurrentRun.Hazards.Where(h => h.Position == enemy.Position).ToList();
            foreach (GroundHazard hazard in matching)
            {
                if (!enemy.IsAlive)
                {
                    break;
                }

                ApplyHazardToEnemy(enemy, hazard);
            }
        }

        public void TriggerHazardsForPlayer()
        {
            if (CurrentRun == null || CurrentRun.Player.CurrentHp <= 0)
            {
                return;
            }

            bool standingInPoison = CurrentRun.Hazards.Any(
                h => h.Type == HazardType.PoisonPool && h.Position == CurrentRun.Player.Position);

            if (standingInPoison && !CurrentRun.Player.IsImmuneThisTurn)
            {
                CurrentRun.Player.PoisonTurnsRemaining = PoisonDurationTurns;
                Log("Stepped into a poison pool! Poisoned for 3 turns.");
            }
        }

        public void ArmBearTrapAt(GridPos pos)
        {
            if (CurrentRun == null)
            {
                return;
            }

            foreach (GroundHazard hazard in CurrentRun.Hazards)
            {
                if (hazard.Type == HazardType.BearTrap && hazard.Position == pos && !hazard.IsArmed)
                {
                    hazard.IsArmed = true;
                    Log($"Bear Trap at {pos} is now armed.");
                }
            }
        }

        public EnemyInstance? SpawnEnemy(EnemyType type)
        {
            if (CurrentRun == null || !Database.Enemies.TryGetValue(type, out EnemyDefinition? def))
            {
                return null;
            }

            GridPos? spawnPos = FindSpawnPosition(def.SpawnOnEdge);
            if (!spawnPos.HasValue)
            {
                return null;
            }

            EnemyInstance enemy = new EnemyInstance
            {
                InstanceId = CurrentRun.NextEnemyInstanceId++,
                Type = def.Type,
                Position = spawnPos.Value,
                CurrentHp = def.MaxHp,
                MaxHp = def.MaxHp,
                State = EnemyState.Unarmed
            };

            CurrentRun.Enemies.Add(enemy);
            Log($"{def.Name} spawned at {enemy.Position}.");
            return enemy;
        }

        public void Log(string message)
        {
            if (CurrentRun == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            CurrentRun.AddRecentLogEntry(message);
            _combatLogger.Append(CurrentRun.CombatLogPath, message);
        }

        private bool CanTakePlayerAction()
        {
            return CurrentRun != null
                && !CurrentRun.IsGameOver
                && !CurrentRun.IsVictory
                && CurrentRun.PendingReward == null
                && !_isExecutingQueue
                && CurrentAimingCard == null
                && CurrentRun.Player.CurrentHp > 0;
        }

        private bool HandleStartingDraftSelection(PendingRewardState reward, WeaponDefinition weapon)
        {
            if (reward.SelectedStartingWeaponIds.Contains(weapon.Id))
            {
                return false;
            }

            reward.SelectedStartingWeaponIds.Add(weapon.Id);
            Log($"Selected starting card: {weapon.Name} ({reward.SelectedStartingWeaponIds.Count}/2).");

            if (reward.SelectedStartingWeaponIds.Count < 2)
            {
                AutoSaveRun();
                return true;
            }

            foreach (string selectedId in reward.SelectedStartingWeaponIds)
            {
                AddCardToHand(Database.Weapons[selectedId]);
            }

            Run.IsStartingDraft = false;
            Run.PendingReward = null;
            SpawnWave(Run.CurrentWave);
            AutoSaveRun();
            return true;
        }

        private void AddCardToHand(WeaponDefinition weapon)
        {
            CardInstance instance = new CardInstance(Run.NextCardInstanceId++, weapon);
            Run.Player.Hand.Add(instance);
        }

        private void ExecuteVikingScream(bool dealsDamage)
        {
            int damage = dealsDamage ? 1 : 0;
            Log(dealsDamage
                ? "Used Viking Scream! Knocking back all adjacent enemies and dealing 1 damage."
                : "Used Viking Scream! Knocking back all adjacent enemies.");

            List<(EnemyInstance Enemy, GridPos Dir)> targets = new List<(EnemyInstance, GridPos)>();
            foreach (GridPos dir in GridPos.AllEightDirections)
            {
                GridPos checkPos = Run.Player.Position.Offset(dir);
                EnemyInstance? enemy = Run.GetEnemyAt(checkPos);
                if (enemy != null)
                {
                    targets.Add((enemy, dir));
                }
            }

            foreach ((EnemyInstance enemy, GridPos dir) in targets)
            {
                GridPos pushedPos = enemy.Position.Offset(dir);
                if (pushedPos.IsValid() && !Run.IsTileOccupied(pushedPos))
                {
                    enemy.Position = pushedPos;
                    Log($"{enemy.Type} was pushed to {pushedPos}.");
                    TriggerHazardsForEnemy(enemy);
                }

                if (damage > 0 && enemy.IsAlive)
                {
                    DamageEnemy(enemy, damage, false, "Viking Scream");
                }
            }
        }

        private void ExecuteIronBulwark(bool dealsDamage)
        {
            Run.Player.IsImmuneThisTurn = true;
            ReduceAllOwnedCardCooldowns(1);
            Log("Used Iron Bulwark! Immune to damage this turn and card cooldowns reduced by 1.");

            if (!dealsDamage)
            {
                return;
            }

            foreach (GridPos dir in GridPos.OrthogonalDirections)
            {
                GridPos targetPos = Run.Player.Position.Offset(dir);
                EnemyInstance? enemy = Run.GetEnemyAt(targetPos);
                if (enemy != null && enemy.IsAlive)
                {
                    DamageEnemy(enemy, 1, false, "Iron Bulwark");
                }
            }
        }

        private void AdvanceQueueExecution()
        {
            if (CurrentRun == null)
            {
                ResetQueueExecutionState();
                return;
            }

            PlayerState player = CurrentRun.Player;

            while (_queueExecutionIndex < player.EquippedQueue.Count)
            {
                if (CurrentRun.IsGameOver || !CurrentRun.HasLivingEnemies())
                {
                    FinishQueueExecution();
                    return;
                }

                CardInstance card = player.EquippedQueue[_queueExecutionIndex];
                if (card.Definition.Targeting == TargetingType.Self)
                {
                    if (_cardEffects.TryGetEffect(card.Definition.EffectType, out ICardEffect? selfEffect) && selfEffect != null)
                    {
                        ExecuteSingleCard(card, selfEffect, new GridPos(0, 0));
                    }

                    _queueExecutionIndex += 1;
                    continue;
                }

                if (!HasAnyValidDirectionForCard(card))
                {
                    Log($"Skipped {card.Definition.Name} because no valid target direction was available.");
                    _queueExecutionIndex += 1;
                    continue;
                }

                CurrentAimingCard = card;
                return;
            }

            FinishQueueExecution();
        }

        private bool HasAnyValidDirectionForCard(CardInstance card)
        {
            if (CurrentRun == null || !_cardEffects.TryGetEffect(card.Definition.EffectType, out ICardEffect? effect) || effect == null)
            {
                return false;
            }

            GridPos[] directions = card.Definition.Targeting == TargetingType.Diagonal
                ? GridPos.DiagonalDirections
                : GridPos.OrthogonalDirections;

            foreach (GridPos dir in directions)
            {
                if (effect.ComputePreview(CurrentRun, card, dir).IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private void ExecuteSingleCard(CardInstance card, ICardEffect effect, GridPos direction)
        {
            int damage = GetEffectiveCardDamage(card);
            effect.Execute(this, card, direction, damage);

            card.CurrentCooldown = GetEffectiveCardMaxCooldown(card);
            _cardIdsActivatedThisTurn.Add(card.InstanceId);
            _cardsPlayedThisTurn += 1;
            WeaponUsed?.Invoke(card.Definition.Id);
        }

        private void FinishQueueExecution()
        {
            if (CurrentRun == null)
            {
                ResetQueueExecutionState();
                return;
            }

            int playedCount = _cardsPlayedThisTurn;
            ReturnEquippedCardsToHand();
            ResetQueueExecutionState();

            if (playedCount > 0)
            {
                CardsPlayedInTurn?.Invoke(playedCount);
            }

            if (CurrentRun.IsGameOver || CheckAndHandleWaveCleared())
            {
                return;
            }

            CompletePlayerTurn();
        }

        private void ReturnEquippedCardsToHand()
        {
            if (CurrentRun == null)
            {
                return;
            }

            CurrentRun.Player.Hand.AddRange(CurrentRun.Player.EquippedQueue);
            CurrentRun.Player.EquippedQueue.Clear();
        }

        private void CompletePlayerTurn()
        {
            if (CurrentRun == null || CurrentRun.IsGameOver || CurrentRun.IsVictory)
            {
                return;
            }

            RunEnemyTurns();

            if (CurrentRun.IsGameOver)
            {
                ClearTurnActivationFlags();
                return;
            }

            ResolveEndOfRoundEffects();

            if (CurrentRun.IsGameOver)
            {
                ClearTurnActivationFlags();
                return;
            }

            CurrentRun.Player.IsImmuneThisTurn = false;
            CurrentRun.TurnNumber += 1;
            TurnCompleted?.Invoke();
            ClearTurnActivationFlags();

            if (!CheckAndHandleWaveCleared())
            {
                AutoSaveRun();
            }
        }

        private void RunEnemyTurns()
        {
            List<EnemyInstance> snapshot = Run.Enemies.Where(e => e.IsAlive).ToList();

            foreach (EnemyInstance enemy in snapshot)
            {
                if (!enemy.IsAlive || Run.IsGameOver)
                {
                    continue;
                }

                ProcessSingleEnemyTurn(enemy);
            }

            Run.Enemies.RemoveAll(e => !e.IsAlive);
        }

        private void ProcessSingleEnemyTurn(EnemyInstance enemy)
        {
            if (enemy.IsStunned)
            {
                enemy.IsStunned = false;
                Log($"{enemy.Type} recovered from stun.");
                return;
            }

            if (enemy.State == EnemyState.Unarmed)
            {
                enemy.State = EnemyState.Armed;
                Log($"{enemy.Type} equipped its weapon.");
                return;
            }

            if (enemy.State == EnemyState.Charging)
            {
                enemy.ChargeTurnsRemaining -= 1;
                if (enemy.ChargeTurnsRemaining <= 0)
                {
                    ExecuteEnemyChargedAttack(enemy);
                }
                else
                {
                    Log($"{enemy.Type} continues charging {enemy.LockedAttackType} ({enemy.ChargeTurnsRemaining} turn remaining).");
                }

                return;
            }

            if (_enemyBehaviors.TryGetBehavior(enemy.Type, out IEnemyBehavior? behavior) && behavior != null)
            {
                behavior.DecideAction(this, enemy);
            }
        }

        private void ExecuteEnemyChargedAttack(EnemyInstance enemy)
        {
            if (_enemyBehaviors.TryGetBehavior(enemy.Type, out IEnemyBehavior? behavior) && behavior != null)
            {
                behavior.ExecuteChargedAttack(this, enemy);
                return;
            }

            enemy.State = EnemyState.Armed;
            enemy.LockedTargetTiles.Clear();
            enemy.LockedAttackType = string.Empty;
        }

        private void ResolveEndOfRoundEffects()
        {
            TickCooldowns();
            TickPoisonStatuses();
            TickGroundHazards();
            Run.Enemies.RemoveAll(e => !e.IsAlive);
        }

        private void TickCooldowns()
        {
            if (!_specialActivatedThisTurn && Run.Player.SpecialCooldown > 0)
            {
                Run.Player.SpecialCooldown -= 1;
            }

            foreach (CardInstance card in Run.Player.GetAllOwnedCards())
            {
                if (!_cardIdsActivatedThisTurn.Contains(card.InstanceId) && card.CurrentCooldown > 0)
                {
                    card.CurrentCooldown -= 1;
                }
            }
        }

        private void TickPoisonStatuses()
        {
            if (Run.Player.PoisonTurnsRemaining > 0 && Run.Player.CurrentHp > 0)
            {
                Run.Player.PoisonTurnsRemaining -= 1;
                DamagePlayer(null, 1, "Poison");
            }

            foreach (EnemyInstance enemy in Run.Enemies.Where(e => e.IsAlive && e.PoisonTurnsRemaining > 0).ToList())
            {
                enemy.PoisonTurnsRemaining -= 1;
                DamageEnemy(enemy, 1, false, "Poison");
            }
        }

        private void TickGroundHazards()
        {
            for (int i = Run.Hazards.Count - 1; i >= 0; i--)
            {
                GroundHazard hazard = Run.Hazards[i];
                if (hazard.Type != HazardType.PoisonPool)
                {
                    continue;
                }

                hazard.RemainingTurns -= 1;
                if (hazard.RemainingTurns <= 0)
                {
                    Run.Hazards.RemoveAt(i);
                }
            }
        }

        private void ApplyHazardToEnemy(EnemyInstance enemy, GroundHazard hazard)
        {
            if (hazard.Type == HazardType.BearTrap && hazard.IsArmed)
            {
                Run.Hazards.Remove(hazard);
                enemy.IsStunned = true;
                enemy.State = EnemyState.Armed;
                enemy.ChargeTurnsRemaining = 0;
                enemy.LockedTargetTiles.Clear();
                enemy.LockedAttackType = string.Empty;
                BearTrapStunTriggered?.Invoke();
                Log($"{enemy.Type} stepped on an armed Bear Trap and was stunned!");
                DamageEnemy(enemy, hazard.Damage, false, "Bear Trap");
                return;
            }

            if (hazard.Type == HazardType.PoisonPool)
            {
                enemy.PoisonTurnsRemaining = PoisonDurationTurns;
                Log($"{enemy.Type} stepped into a poison pool.");
            }
        }

        private void HandleEnemyDefeated(EnemyInstance enemy, bool friendlyFire)
        {
            int coinReward = Database.Enemies.TryGetValue(enemy.Type, out EnemyDefinition? def) ? def.CoinReward : 2;
            Profile.Coins += coinReward;
            Run.CoinsEarnedInRun += coinReward;
            Run.EnemiesDefeatedInRun += 1;
            _persistence.SaveProfile(Profile);

            Log($"{enemy.Type} was defeated! Earned {coinReward} coins.");
            EnemyDefeated?.Invoke(enemy.Type, coinReward, friendlyFire);
        }

        private bool CheckAndHandleWaveCleared()
        {
            if (CurrentRun == null
                || CurrentRun.IsGameOver
                || CurrentRun.IsVictory
                || CurrentRun.IsStartingDraft
                || CurrentRun.PendingReward != null
                || CurrentRun.HasLivingEnemies())
            {
                return false;
            }

            int clearedWave = CurrentRun.CurrentWave;
            Log($"Wave {clearedWave} cleared!");
            WaveCleared?.Invoke(clearedWave, CurrentRun.TookDamageInCurrentWave);

            WaveDefinition? waveDef = Database.Waves.FirstOrDefault(w => w.WaveNumber == clearedWave);
            if (clearedWave >= Database.Waves.Count)
            {
                TriggerVictory();
                return true;
            }

            if (waveDef != null && (waveDef.RewardType == RewardType.PickCard || waveDef.RewardType == RewardType.Upgrade))
            {
                ResetAllCooldownsForReward();
                CurrentRun.PendingReward = _rewardService.CreateWaveReward(waveDef.RewardType, Profile, Database);
                Log($"Reward round ({waveDef.RewardType})! All card and Special cooldowns reset to 0.");
                AutoSaveRun();
                return true;
            }

            StartNextWave();
            return true;
        }

        private void StartNextWave()
        {
            Run.CurrentWave += 1;
            Run.TookDamageInCurrentWave = false;
            Run.Enemies.Clear();
            Run.Hazards.RemoveAll(h => h.Type == HazardType.PoisonPool);
            SpawnWave(Run.CurrentWave);
            AutoSaveRun();
        }

        private void SpawnWave(int waveNumber)
        {
            WaveDefinition? waveDef = Database.Waves.FirstOrDefault(w => w.WaveNumber == waveNumber);
            if (waveDef == null)
            {
                return;
            }

            Log($"--- Wave {waveNumber} Begins ---");
            foreach (WaveEnemyEntry entry in waveDef.Enemies)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    SpawnEnemy(entry.EnemyType);
                }
            }
        }

        private void ResetAllCooldownsForReward()
        {
            Run.Player.SpecialCooldown = 0;
            foreach (CardInstance card in Run.Player.GetAllOwnedCards())
            {
                card.CurrentCooldown = 0;
            }
        }

        private void ReduceAllOwnedCardCooldowns(int amount)
        {
            foreach (CardInstance card in Run.Player.GetAllOwnedCards())
            {
                if (card.CurrentCooldown > 0)
                {
                    card.CurrentCooldown = Math.Max(0, card.CurrentCooldown - amount);
                }
            }
        }

        private void TriggerVictory()
        {
            Run.IsVictory = true;
            Run.GameOverReason = "You defeated the Viking Chief and conquered Level 1!";
            Profile.Area2Unlocked = true;
            _persistence.SaveProfile(Profile);
            _persistence.ClearActiveRunSave();
            Log(Run.GameOverReason);
            RunWon?.Invoke(Run.UsedRerollInRun);
        }

        private void TriggerDefeat(string reason)
        {
            Run.IsGameOver = true;
            Run.GameOverReason = reason;
            _persistence.ClearActiveRunSave();
            Log(reason);
        }

        private GridPos? FindSpawnPosition(bool edgeOnly)
        {
            List<GridPos> preferred = new List<GridPos>();
            List<GridPos> fallback = new List<GridPos>();

            for (int y = 0; y < GridPos.GridSize; y++)
            {
                for (int x = 0; x < GridPos.GridSize; x++)
                {
                    GridPos pos = new GridPos(x, y);
                    if (Run.IsTileOccupied(pos))
                    {
                        continue;
                    }

                    if (edgeOnly && !pos.IsEdgeTile())
                    {
                        continue;
                    }

                    if (pos.ManhattanDistance(Run.Player.Position) >= 2)
                    {
                        preferred.Add(pos);
                    }
                    else
                    {
                        fallback.Add(pos);
                    }
                }
            }

            List<GridPos> pool = preferred.Count > 0 ? preferred : fallback;
            if (pool.Count == 0 && edgeOnly)
            {
                return FindSpawnPosition(false);
            }

            return pool.Count > 0 ? pool[_random.Next(pool.Count)] : null;
        }

        private CharacterDefinition ResolveSelectedCharacter()
        {
            if (Database.Characters.TryGetValue(Profile.SelectedCharacterId, out CharacterDefinition? selected))
            {
                return selected;
            }

            return Database.CharacterList[0];
        }

        private CharacterDefinition ResolveRunCharacter()
        {
            if (Database.Characters.TryGetValue(Run.Player.CharacterId, out CharacterDefinition? character))
            {
                return character;
            }

            return Database.CharacterList[0];
        }

        private void ResetQueueExecutionState()
        {
            _isExecutingQueue = false;
            _queueExecutionIndex = 0;
            _cardsPlayedThisTurn = 0;
            CurrentAimingCard = null;
        }

        private void ClearTurnActivationFlags()
        {
            _cardIdsActivatedThisTurn.Clear();
            _specialActivatedThisTurn = false;
        }

        private void AutoSaveRun()
        {
            if (CurrentRun != null && !CurrentRun.IsGameOver && !CurrentRun.IsVictory)
            {
                _persistence.SaveActiveRun(CurrentRun);
            }
        }
    }
}