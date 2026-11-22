#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RogueCards
{
    public class GameEngine
    {
        public const int GridSize = 5;

        private readonly GameDatabase database;
        private readonly PlayerProfile profile;
        private readonly Random random;

        public ActiveRunState CurrentRun { get; private set; }
        public string LastActionFeedback { get; private set; }
        public string LastSpecialEffectType { get; private set; }
        public GridPos LastSpecialOrigin { get; private set; }
        public int LastSpecialSequenceId { get; private set; }

        public GameEngine(GameDatabase database, PlayerProfile profile)
        {
            this.database = database;
            this.profile = profile;
            random = new Random();
            LastActionFeedback = string.Empty;
            LastSpecialEffectType = string.Empty;
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
                IsStartingDraft = false,
                UsedRerollInRun = false,
                TookDamageInCurrentWave = false,
                IsVictory = false,
                IsGameOver = false,
                GameOverReason = string.Empty,
                NextEnemyInstanceId = 1,
                NextCardInstanceId = 1
            };

            PopulateInitialHand();

            LastActionFeedback = $"Run started as {character.Name}.";
            LastSpecialEffectType = string.Empty;

            AddCombatLog($"New run started as {character.Name} ({maxHp}/{maxHp} HP).");

            List<string> activeUpgrades = GetActiveUpgradeNames();
            if (activeUpgrades.Count > 0)
            {
                AddCombatLog($"Permanent upgrades active: {string.Join(", ", activeUpgrades)}.");
            }

            AddCombatLog("Wave 1 ready. Use WASD/Arrows or click an adjacent tile to move.");
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
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver)
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
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver)
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
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver)
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

            EndPlayerTurn(false, false);
            return true;
        }

        public bool WaitTurn()
        {
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver)
            {
                return false;
            }

            CharacterDefinition hero = GetActiveCharacterDefinition();
            int extraReduction = GetWaitEquippedCooldownBonus();

            if (extraReduction > 0 && CurrentRun.Player.EquippedQueue.Count > 0)
            {
                LastActionFeedback = $"Waited 1 turn (Tactical Master: -{extraReduction} extra equipped CD).";
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
            if (CurrentRun == null || CurrentRun.Player == null || CurrentRun.IsGameOver)
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
                }

                if (specialDamage > 0)
                {
                    enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - specialDamage);
                    damagedCount++;
                }
            }

            RemoveDefeatedEnemies();

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
            foreach (CardInstance card in player.EquippedQueue)
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
                    enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - specialDamage);
                    damagedCount++;
                }

                RemoveDefeatedEnemies();
            }

            LastSpecialEffectType = "IronBulwark";
            LastSpecialOrigin = player.Position;
            LastSpecialSequenceId++;

            string damagePart = specialDamage > 0
                ? $" Dealt {specialDamage} dmg to {damagedCount} adjacent enemy(s)."
                : string.Empty;

            LastActionFeedback = $"{hero.SpecialName}! Damage immunity active & {reducedCards} equipped card(s) cooled down.{damagePart}";
            AddCombatLog($"Turn {CurrentRun.TurnNumber}: {hero.Name} used {hero.SpecialName}! Immune this turn; equipped CD -1.{damagePart}");
        }

        private void EndPlayerTurn(bool usedWait, bool usedSpecial)
        {
            PlayerState player = CurrentRun.Player;

            foreach (CardInstance card in player.Hand)
            {
                if (card.CurrentCooldown > 0)
                {
                    card.CurrentCooldown--;
                }
            }

            int extraEquippedReduction = usedWait ? GetWaitEquippedCooldownBonus() : 0;
            foreach (CardInstance card in player.EquippedQueue)
            {
                if (card.CurrentCooldown > 0)
                {
                    card.CurrentCooldown = Math.Max(0, card.CurrentCooldown - 1 - extraEquippedReduction);
                }
            }

            if (!usedSpecial && player.SpecialCooldown > 0)
            {
                player.SpecialCooldown--;
            }

            player.IsImmuneThisTurn = false;
            CurrentRun.TurnNumber++;
        }

        private void PopulateInitialHand()
        {
            List<WeaponDefinition> unlocked = database.WeaponList
                .Where(w => profile.UnlockedWeaponIds.Contains(w.Id))
                .ToList();

            List<WeaponDefinition> chosen = new List<WeaponDefinition>();

            if (database.Weapons.TryGetValue("viking_axe", out WeaponDefinition axe) &&
                profile.UnlockedWeaponIds.Contains(axe.Id))
            {
                chosen.Add(axe);
            }

            if (database.Weapons.TryGetValue("throwing_axe", out WeaponDefinition throwingAxe) &&
                profile.UnlockedWeaponIds.Contains(throwingAxe.Id))
            {
                chosen.Add(throwingAxe);
            }

            foreach (WeaponDefinition candidate in unlocked)
            {
                if (chosen.Count >= 2)
                {
                    break;
                }

                if (!chosen.Any(c => c.Id == candidate.Id))
                {
                    chosen.Add(candidate);
                }
            }

            foreach (WeaponDefinition weapon in chosen)
            {
                CurrentRun.Player.Hand.Add(CreateCardInstance(weapon));
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

        private void ArmBearTrapAt(GridPos pos)
        {
            foreach (GroundHazard hazard in CurrentRun.Hazards)
            {
                if (hazard.Type == HazardType.BearTrap && hazard.Position == pos && !hazard.IsArmed)
                {
                    hazard.IsArmed = true;
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