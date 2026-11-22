#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace RogueCards
{
    public class EnemyAI
    {
        private const int GridSize = GameEngine.GridSize;
        private readonly Random random;

        public EnemyAI(Random random)
        {
            this.random = random;
        }

        public void SpawnWave(GameEngine engine, int waveNumber)
        {
            ActiveRunState run = engine.CurrentRun;
            if (run == null)
            {
                return;
            }

            run.Enemies.Clear();

            WaveDefinition waveDef = engine.Database.Waves.FirstOrDefault(w => w.WaveNumber == waveNumber);
            if (waveDef == null)
            {
                return;
            }

            int totalSpawned = 0;
            foreach (WaveSpawnEntry entry in waveDef.Spawns)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    if (SpawnSingleEnemy(engine, entry.EnemyType) != null)
                    {
                        totalSpawned++;
                    }
                }
            }

            string waveLabel = waveNumber == 10
                ? "WAVE 10 BOSS: Viking Chief has entered the arena!"
                : $"Wave {waveNumber} spawned ({totalSpawned} enemy unit(s)).";

            engine.AddCombatLog(waveLabel);
        }

        public EnemyInstance SpawnSingleEnemy(GameEngine engine, EnemyType enemyType)
        {
            ActiveRunState run = engine.CurrentRun;
            if (!engine.Database.Enemies.TryGetValue(enemyType, out EnemyDefinition def))
            {
                return null;
            }

            GridPos? spawnPos = FindFreeSpawnTile(run, def.SpawnNearEdge);
            if (!spawnPos.HasValue)
            {
                return null;
            }

            EnemyInstance enemy = new EnemyInstance
            {
                InstanceId = run.NextEnemyInstanceId,
                Type = def.Type,
                DefinitionId = def.Id,
                Name = def.Name,
                Position = spawnPos.Value,
                CurrentHp = def.MaxHp,
                MaxHp = def.MaxHp,
                CoinDrop = def.CoinDrop,
                State = EnemyState.Unarmed,
                EquippedWeaponLabel = "Unarmed",
                ChargeTurnsRemaining = 0,
                LockedDirection = new GridPos(0, 0),
                LockedAttackType = string.Empty,
                IsStunned = false,
                PoisonTurnsRemaining = 0,
                BossCycleIndex = 0,
                BossSpawnedDuringCharge = false
            };

            run.NextEnemyInstanceId++;
            run.Enemies.Add(enemy);
            return enemy;
        }

        public void ExecuteEnemyTurns(GameEngine engine)
        {
            ActiveRunState run = engine.CurrentRun;
            if (run == null || run.IsGameOver)
            {
                return;
            }

            List<EnemyInstance> turnOrder = run.Enemies
                .Where(e => e.CurrentHp > 0)
                .ToList();

            foreach (EnemyInstance enemy in turnOrder)
            {
                if (run.IsGameOver || run.Player.CurrentHp <= 0)
                {
                    break;
                }

                if (enemy.CurrentHp <= 0)
                {
                    continue;
                }

                ProcessSingleEnemyTurn(engine, enemy);
            }
        }

        private void ProcessSingleEnemyTurn(GameEngine engine, EnemyInstance enemy)
        {
            if (enemy.IsStunned)
            {
                enemy.IsStunned = false;
                engine.AddCombatLog($"{enemy.Name} is stunned and loses this turn.");
                return;
            }

            if (enemy.State == EnemyState.Unarmed)
            {
                EquipEnemyWeapon(engine, enemy);
                return;
            }

            if (enemy.State == EnemyState.Charging)
            {
                HandleChargingEnemy(engine, enemy);
                return;
            }

            switch (enemy.Type)
            {
                case EnemyType.Pawn:
                    ActPawn(engine, enemy);
                    break;

                case EnemyType.Archer:
                    ActArcher(engine, enemy);
                    break;

                case EnemyType.Spearman:
                    ActSpearman(engine, enemy);
                    break;

                case EnemyType.Shaman:
                    ActShaman(engine, enemy);
                    break;

                case EnemyType.VikingChief:
                    ActVikingChief(engine, enemy);
                    break;
            }
        }

        private void EquipEnemyWeapon(GameEngine engine, EnemyInstance enemy)
        {
            string weaponName = "Weapon";
            if (engine.Database.Enemies.TryGetValue(enemy.Type, out EnemyDefinition def))
            {
                weaponName = def.WeaponName;
            }

            enemy.State = EnemyState.Armed;
            enemy.EquippedWeaponLabel = weaponName;
            engine.AddCombatLog($"{enemy.Name} at {enemy.Position} equipped {weaponName}.");
        }

        private void HandleChargingEnemy(GameEngine engine, EnemyInstance enemy)
        {
            if (enemy.ChargeTurnsRemaining > 1)
            {
                enemy.ChargeTurnsRemaining--;

                if (enemy.Type == EnemyType.VikingChief &&
                    enemy.LockedAttackType == "BossHookClub" &&
                    !enemy.BossSpawnedDuringCharge)
                {
                    enemy.BossSpawnedDuringCharge = true;
                    EnemyType[] summonPool = new EnemyType[]
                    {
                        EnemyType.Pawn,
                        EnemyType.Archer,
                        EnemyType.Spearman,
                        EnemyType.Shaman
                    };

                    EnemyType chosen = summonPool[random.Next(summonPool.Length)];
                    EnemyInstance summoned = SpawnSingleEnemy(engine, chosen);
                    if (summoned != null)
                    {
                        engine.AddCombatLog($"Viking Chief summoned a {summoned.Name} at {summoned.Position} while charging!");
                    }
                }
                else
                {
                    engine.AddCombatLog($"{enemy.Name} continues charging ({enemy.ChargeTurnsRemaining} turn left).");
                }

                return;
            }

            ExecuteLockedAttack(engine, enemy);
        }

        private void ExecuteLockedAttack(GameEngine engine, EnemyInstance enemy)
        {
            ActiveRunState run = engine.CurrentRun;
            string attackType = enemy.LockedAttackType;

            if (attackType == "Axe")
            {
                ResolveAreaDamageAttack(engine, enemy, enemy.LockedTargetTiles, 2, "Axe");
            }
            else if (attackType == "Spear")
            {
                ResolveAreaDamageAttack(engine, enemy, enemy.LockedTargetTiles, 2, "Spear");
            }
            else if (attackType == "Arrow")
            {
                ResolveFirstHitLineAttack(engine, enemy, enemy.LockedTargetTiles, 2, "Arrow");
            }
            else if (attackType == "Poison")
            {
                ResolveShamanPoisonAttack(engine, enemy);
            }
            else if (attackType == "BossThrowingAxe")
            {
                ResolveFirstHitLineAttack(engine, enemy, enemy.LockedTargetTiles, 2, "Throwing Axe");
            }
            else if (attackType == "BossHookClub")
            {
                ResolveBossHookAndClub(engine, enemy);
            }
            else if (attackType == "BossEvolvedAxe")
            {
                ResolveAreaDamageAttack(engine, enemy, enemy.LockedTargetTiles, 3, "Evolved Viking Axe");
            }

            enemy.State = EnemyState.Armed;
            enemy.ChargeTurnsRemaining = 0;
            enemy.LockedTargetTiles.Clear();
            enemy.LockedAttackType = string.Empty;

            if (enemy.Type == EnemyType.VikingChief)
            {
                enemy.BossCycleIndex = (enemy.BossCycleIndex + 1) % 3;
                enemy.BossSpawnedDuringCharge = false;
                enemy.EquippedWeaponLabel = GetBossCycleWeaponLabel(enemy.BossCycleIndex);
            }
        }

        private void ResolveAreaDamageAttack(
            GameEngine engine,
            EnemyInstance attacker,
            List<GridPos> tiles,
            int damage,
            string weaponLabel)
        {
            bool hitAnything = false;

            foreach (GridPos tile in tiles)
            {
                if (HitTileWithEnemyAttack(engine, attacker, tile, damage, weaponLabel))
                {
                    hitAnything = true;
                }
            }

            if (!hitAnything)
            {
                engine.AddCombatLog($"{attacker.Name} struck with {weaponLabel} and missed.");
            }
        }

        private void ResolveFirstHitLineAttack(
            GameEngine engine,
            EnemyInstance attacker,
            List<GridPos> orderedRayTiles,
            int damage,
            string weaponLabel)
        {
            foreach (GridPos tile in orderedRayTiles)
            {
                if (HitTileWithEnemyAttack(engine, attacker, tile, damage, weaponLabel))
                {
                    return;
                }
            }

            engine.AddCombatLog($"{attacker.Name} fired {weaponLabel} and missed.");
        }

        private void ResolveShamanPoisonAttack(GameEngine engine, EnemyInstance shaman)
        {
            ActiveRunState run = engine.CurrentRun;
            if (shaman.LockedTargetTiles.Count == 0)
            {
                return;
            }

            GridPos center = shaman.LockedTargetTiles[0];
            HitTileWithEnemyAttack(engine, shaman, center, 1, "Poison Potion");

            foreach (GridPos tile in shaman.LockedTargetTiles)
            {
                if (!tile.IsValid(GridSize))
                {
                    continue;
                }

                run.Hazards.RemoveAll(h => h.Type == HazardType.Poison && h.Position == tile);
                run.Hazards.Add(new GroundHazard
                {
                    Type = HazardType.Poison,
                    Position = tile,
                    Damage = 1,
                    RemainingTurns = 3,
                    IsArmed = true,
                    AppliesStun = false
                });
            }

            engine.AddCombatLog($"{shaman.Name} shattered a Poison Potion at {center} (3 turns hazard).");
        }

        private void ResolveBossHookAndClub(GameEngine engine, EnemyInstance boss)
        {
            ActiveRunState run = engine.CurrentRun;
            GridPos dir = boss.LockedDirection;
            GridPos adjacentTile = boss.Position.Offset(dir.X, dir.Y);

            bool hookedSomething = false;
            for (int step = 1; step <= 3; step++)
            {
                GridPos checkTile = boss.Position.Offset(dir.X * step, dir.Y * step);
                if (!checkTile.IsValid(GridSize))
                {
                    break;
                }

                if (run.Player.Position == checkTile)
                {
                    engine.ApplyEnemyAttackToPlayer(boss, 1, "Boat Hook");
                    if (step > 1 && adjacentTile.IsValid(GridSize) && !IsOccupied(run, adjacentTile))
                    {
                        GridPos oldPos = run.Player.Position;
                        run.Player.Position = adjacentTile;
                        engine.ArmBearTrapAt(oldPos);
                        engine.AddCombatLog($"Viking Chief pulled you to {adjacentTile}!");
                        engine.TriggerHazardsForPlayer();
                    }

                    hookedSomething = true;
                    break;
                }

                EnemyInstance otherEnemy = GetEnemyAt(run, checkTile, boss.InstanceId);
                if (otherEnemy != null)
                {
                    engine.ApplyFriendlyFireDamage(boss, otherEnemy, 1, "Boat Hook");
                    if (otherEnemy.CurrentHp > 0 && step > 1 && adjacentTile.IsValid(GridSize) && !IsOccupied(run, adjacentTile))
                    {
                        otherEnemy.Position = adjacentTile;
                        engine.AddCombatLog($"Viking Chief pulled {otherEnemy.Name} to {adjacentTile}!");
                        engine.TriggerHazardsForEnemy(otherEnemy);
                    }

                    hookedSomething = true;
                    break;
                }
            }

            if (!hookedSomething)
            {
                engine.AddCombatLog("Viking Chief cast Boat Hook and missed.");
            }

            if (run.IsGameOver || !adjacentTile.IsValid(GridSize))
            {
                return;
            }

            if (run.Player.Position == adjacentTile)
            {
                engine.ApplyEnemyAttackToPlayer(boss, 2, "Wooden Club");
                if (!run.IsGameOver)
                {
                    GridPos knockTile = adjacentTile.Offset(dir.X, dir.Y);
                    if (knockTile.IsValid(GridSize) && !IsOccupied(run, knockTile))
                    {
                        GridPos oldPos = run.Player.Position;
                        run.Player.Position = knockTile;
                        engine.ArmBearTrapAt(oldPos);
                        engine.AddCombatLog($"Wooden Club knocked you back to {knockTile}!");
                        engine.TriggerHazardsForPlayer();
                    }
                }
            }
            else
            {
                EnemyInstance victim = GetEnemyAt(run, adjacentTile, boss.InstanceId);
                if (victim != null)
                {
                    engine.ApplyFriendlyFireDamage(boss, victim, 2, "Wooden Club");
                    if (victim.CurrentHp > 0)
                    {
                        GridPos knockTile = adjacentTile.Offset(dir.X, dir.Y);
                        if (knockTile.IsValid(GridSize) && !IsOccupied(run, knockTile))
                        {
                            victim.Position = knockTile;
                            engine.TriggerHazardsForEnemy(victim);
                        }
                    }
                }
            }
        }

        private bool HitTileWithEnemyAttack(
            GameEngine engine,
            EnemyInstance attacker,
            GridPos tile,
            int damage,
            string weaponLabel)
        {
            ActiveRunState run = engine.CurrentRun;

            if (run.Player.Position == tile)
            {
                engine.ApplyEnemyAttackToPlayer(attacker, damage, weaponLabel);
                return true;
            }

            EnemyInstance otherEnemy = GetEnemyAt(run, tile, attacker.InstanceId);
            if (otherEnemy != null)
            {
                engine.ApplyFriendlyFireDamage(attacker, otherEnemy, damage, weaponLabel);
                return true;
            }

            return false;
        }

        private void ActPawn(GameEngine engine, EnemyInstance pawn)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;
            int dist = pawn.Position.ManhattanDistance(playerPos);

            if (dist == 1)
            {
                pawn.State = EnemyState.Charging;
                pawn.ChargeTurnsRemaining = 1;
                pawn.LockedAttackType = "Axe";
                pawn.LockedDirection = new GridPos(playerPos.X - pawn.Position.X, playerPos.Y - pawn.Position.Y);
                pawn.LockedTargetTiles = new List<GridPos> { playerPos };
                engine.AddCombatLog($"Pawn at {pawn.Position} is charging an Axe strike at {playerPos}!");
                return;
            }

            MoveEnemyToward(engine, pawn, playerPos);
        }

        private void ActArcher(GameEngine engine, EnemyInstance archer)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;
            int dist = archer.Position.ManhattanDistance(playerPos);
            bool aligned = (archer.Position.X == playerPos.X || archer.Position.Y == playerPos.Y);

            if (dist <= 2)
            {
                if (TryMoveEnemyAway(engine, archer, playerPos))
                {
                    return;
                }

                ChargeArcherShot(engine, archer, playerPos);
                return;
            }

            if (aligned)
            {
                ChargeArcherShot(engine, archer, playerPos);
                return;
            }

            MoveArcherToAlign(engine, archer, playerPos);
        }

        private void ChargeArcherShot(GameEngine engine, EnemyInstance archer, GridPos playerPos)
        {
            GridPos dir = GetPrimaryOrthogonalDirection(archer.Position, playerPos);
            List<GridPos> ray = BuildRayTiles(archer.Position, dir, GridSize);

            archer.State = EnemyState.Charging;
            archer.ChargeTurnsRemaining = 1;
            archer.LockedAttackType = "Arrow";
            archer.LockedDirection = dir;
            archer.LockedTargetTiles = ray;
            engine.AddCombatLog($"Archer at {archer.Position} is aiming a Bow shot!");
        }

        private void ActSpearman(GameEngine engine, EnemyInstance spearman)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;
            int dist = spearman.Position.ManhattanDistance(playerPos);
            bool aligned = (spearman.Position.X == playerPos.X || spearman.Position.Y == playerPos.Y);

            if (aligned && dist <= 2)
            {
                GridPos dir = GetPrimaryOrthogonalDirection(spearman.Position, playerPos);
                List<GridPos> targets = BuildRayTiles(spearman.Position, dir, 2);

                spearman.State = EnemyState.Charging;
                spearman.ChargeTurnsRemaining = 1;
                spearman.LockedAttackType = "Spear";
                spearman.LockedDirection = dir;
                spearman.LockedTargetTiles = targets;
                engine.AddCombatLog($"Spearman at {spearman.Position} is charging a Spear thrust!");
                return;
            }

            MoveEnemyToward(engine, spearman, playerPos);
        }

        private void ActShaman(GameEngine engine, EnemyInstance shaman)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;
            int dist = shaman.Position.ManhattanDistance(playerPos);

            if (dist <= 2)
            {
                if (TryMoveEnemyAway(engine, shaman, playerPos))
                {
                    return;
                }

                ChargeShamanPoison(engine, shaman, playerPos);
                return;
            }

            if (dist == 3)
            {
                ChargeShamanPoison(engine, shaman, playerPos);
                return;
            }

            MoveEnemyToward(engine, shaman, playerPos);
        }

        private void ChargeShamanPoison(GameEngine engine, EnemyInstance shaman, GridPos targetCenter)
        {
            List<GridPos> splashTiles = new List<GridPos> { targetCenter };
            GridPos[] diagonals = new GridPos[]
            {
                targetCenter.Offset(-1, -1),
                targetCenter.Offset(1, -1),
                targetCenter.Offset(-1, 1),
                targetCenter.Offset(1, 1)
            };

            foreach (GridPos diag in diagonals)
            {
                if (diag.IsValid(GridSize))
                {
                    splashTiles.Add(diag);
                }
            }

            shaman.State = EnemyState.Charging;
            shaman.ChargeTurnsRemaining = 1;
            shaman.LockedAttackType = "Poison";
            shaman.LockedDirection = GetPrimaryOrthogonalDirection(shaman.Position, targetCenter);
            shaman.LockedTargetTiles = splashTiles;
            engine.AddCombatLog($"Shaman at {shaman.Position} is preparing a Poison Potion at {targetCenter}!");
        }

        private void ActVikingChief(GameEngine engine, EnemyInstance boss)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;

            if (boss.BossCycleIndex == 0)
            {
                bool aligned = (boss.Position.X == playerPos.X || boss.Position.Y == playerPos.Y);
                if (!aligned && TryMoveToAlign(engine, boss, playerPos))
                {
                    return;
                }

                GridPos dir = GetPrimaryOrthogonalDirection(boss.Position, playerPos);
                boss.State = EnemyState.Charging;
                boss.ChargeTurnsRemaining = 1;
                boss.EquippedWeaponLabel = "Throwing Axe";
                boss.LockedAttackType = "BossThrowingAxe";
                boss.LockedDirection = dir;
                boss.LockedTargetTiles = BuildRayTiles(boss.Position, dir, GridSize);
                engine.AddCombatLog("Viking Chief is charging Throwing Axe!");
            }
            else if (boss.BossCycleIndex == 1)
            {
                GridPos dir = GetPrimaryOrthogonalDirection(boss.Position, playerPos);
                boss.State = EnemyState.Charging;
                boss.ChargeTurnsRemaining = 2;
                boss.BossSpawnedDuringCharge = false;
                boss.EquippedWeaponLabel = "Hook & Club";
                boss.LockedAttackType = "BossHookClub";
                boss.LockedDirection = dir;
                boss.LockedTargetTiles = BuildRayTiles(boss.Position, dir, 3);
                engine.AddCombatLog("Viking Chief equips Boat Hook & Wooden Club (2-turn charge)!");
            }
            else
            {
                GridPos forward = GetPrimaryOrthogonalDirection(boss.Position, playerPos);
                List<GridPos> evolvedTiles = BuildEvolvedVikingAxeTiles(boss.Position, forward);

                boss.State = EnemyState.Charging;
                boss.ChargeTurnsRemaining = 1;
                boss.EquippedWeaponLabel = "Evolved Axe";
                boss.LockedAttackType = "BossEvolvedAxe";
                boss.LockedDirection = forward;
                boss.LockedTargetTiles = evolvedTiles;
                engine.AddCombatLog("Viking Chief raises the Evolved Viking Axe (6-tile cleave)!");
            }
        }

        private static List<GridPos> BuildEvolvedVikingAxeTiles(GridPos origin, GridPos forward)
        {
            int fx = forward.X;
            int fy = forward.Y;
            int lx = -fy;
            int ly = fx;

            GridPos[] raw = new GridPos[]
            {
                origin.Offset(fx, fy),
                origin.Offset(fx * 2, fy * 2),
                origin.Offset(fx + lx, fy + ly),
                origin.Offset(fx - lx, fy - ly),
                origin.Offset(lx, ly),
                origin.Offset(-lx, -ly)
            };

            List<GridPos> valid = new List<GridPos>();
            foreach (GridPos tile in raw)
            {
                if (tile.IsValid(GridSize) && !valid.Contains(tile))
                {
                    valid.Add(tile);
                }
            }

            return valid;
        }

        private void MoveEnemyToward(GameEngine engine, EnemyInstance enemy, GridPos target)
        {
            ActiveRunState run = engine.CurrentRun;
            List<GridPos> neighbors = GetFreeOrthogonalNeighbors(run, enemy.Position);
            if (neighbors.Count == 0)
            {
                return;
            }

            int currentDist = enemy.Position.ManhattanDistance(target);
            GridPos best = neighbors
                .OrderBy(n => n.ManhattanDistance(target))
                .ThenBy(n => Math.Min(Math.Abs(n.X - target.X), Math.Abs(n.Y - target.Y)))
                .First();

            if (best.ManhattanDistance(target) < currentDist)
            {
                MoveEnemyToTile(engine, enemy, best);
            }
        }

        private bool TryMoveEnemyAway(GameEngine engine, EnemyInstance enemy, GridPos threat)
        {
            ActiveRunState run = engine.CurrentRun;
            List<GridPos> neighbors = GetFreeOrthogonalNeighbors(run, enemy.Position);
            if (neighbors.Count == 0)
            {
                return false;
            }

            int currentDist = enemy.Position.ManhattanDistance(threat);
            GridPos best = neighbors
                .OrderByDescending(n => n.ManhattanDistance(threat))
                .First();

            if (best.ManhattanDistance(threat) > currentDist)
            {
                MoveEnemyToTile(engine, enemy, best);
                return true;
            }

            return false;
        }

        private void MoveArcherToAlign(GameEngine engine, EnemyInstance archer, GridPos playerPos)
        {
            ActiveRunState run = engine.CurrentRun;
            List<GridPos> neighbors = GetFreeOrthogonalNeighbors(run, archer.Position);
            if (neighbors.Count == 0)
            {
                return;
            }

            GridPos best = neighbors
                .OrderBy(n => n.ManhattanDistance(playerPos) <= 2 ? 1 : 0)
                .ThenBy(n => Math.Min(Math.Abs(n.X - playerPos.X), Math.Abs(n.Y - playerPos.Y)))
                .ThenByDescending(n => n.ManhattanDistance(playerPos))
                .First();

            MoveEnemyToTile(engine, archer, best);
        }

        private bool TryMoveToAlign(GameEngine engine, EnemyInstance enemy, GridPos playerPos)
        {
            ActiveRunState run = engine.CurrentRun;
            List<GridPos> neighbors = GetFreeOrthogonalNeighbors(run, enemy.Position);
            if (neighbors.Count == 0)
            {
                return false;
            }

            int currentAlignDist = Math.Min(Math.Abs(enemy.Position.X - playerPos.X), Math.Abs(enemy.Position.Y - playerPos.Y));
            GridPos best = neighbors
                .OrderBy(n => Math.Min(Math.Abs(n.X - playerPos.X), Math.Abs(n.Y - playerPos.Y)))
                .ThenBy(n => n.ManhattanDistance(playerPos))
                .First();

            int newAlignDist = Math.Min(Math.Abs(best.X - playerPos.X), Math.Abs(best.Y - playerPos.Y));
            if (newAlignDist < currentAlignDist)
            {
                MoveEnemyToTile(engine, enemy, best);
                return true;
            }

            return false;
        }

        private void MoveEnemyToTile(GameEngine engine, EnemyInstance enemy, GridPos dest)
        {
            enemy.Position = dest;
            engine.AddCombatLog($"{enemy.Name} moved to {dest}.");
            engine.TriggerHazardsForEnemy(enemy);
        }

        private GridPos? FindFreeSpawnTile(ActiveRunState run, bool preferEdge)
        {
            List<GridPos> edgeFree = new List<GridPos>();
            List<GridPos> allFree = new List<GridPos>();

            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    GridPos pos = new GridPos(x, y);
                    if (IsOccupied(run, pos))
                    {
                        continue;
                    }

                    allFree.Add(pos);
                    if (x == 0 || x == GridSize - 1 || y == 0 || y == GridSize - 1)
                    {
                        edgeFree.Add(pos);
                    }
                }
            }

            if (preferEdge && edgeFree.Count > 0)
            {
                return edgeFree[random.Next(edgeFree.Count)];
            }

            if (allFree.Count > 0)
            {
                return allFree[random.Next(allFree.Count)];
            }

            return null;
        }

        private static List<GridPos> GetFreeOrthogonalNeighbors(ActiveRunState run, GridPos origin)
        {
            List<GridPos> result = new List<GridPos>();
            GridPos[] candidates = new GridPos[]
            {
                origin.Offset(0, -1),
                origin.Offset(0, 1),
                origin.Offset(-1, 0),
                origin.Offset(1, 0)
            };

            foreach (GridPos tile in candidates)
            {
                if (tile.IsValid(GridSize) && !IsOccupied(run, tile))
                {
                    result.Add(tile);
                }
            }

            return result;
        }

        private static List<GridPos> BuildRayTiles(GridPos origin, GridPos dir, int maxSteps)
        {
            List<GridPos> tiles = new List<GridPos>();
            for (int step = 1; step <= maxSteps; step++)
            {
                GridPos next = origin.Offset(dir.X * step, dir.Y * step);
                if (!next.IsValid(GridSize))
                {
                    break;
                }

                tiles.Add(next);
            }

            return tiles;
        }

        private static GridPos GetPrimaryOrthogonalDirection(GridPos from, GridPos to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            if (dx == 0 && dy == 0)
            {
                return new GridPos(0, 1);
            }

            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return new GridPos(Math.Sign(dx), 0);
            }

            return new GridPos(0, Math.Sign(dy));
        }

        private static string GetBossCycleWeaponLabel(int cycleIndex)
        {
            if (cycleIndex == 0)
            {
                return "Throwing Axe";
            }

            if (cycleIndex == 1)
            {
                return "Hook & Club";
            }

            return "Evolved Axe";
        }

        private static bool IsOccupied(ActiveRunState run, GridPos pos)
        {
            if (run.Player != null && run.Player.Position == pos)
            {
                return true;
            }

            return run.Enemies.Any(e => e.CurrentHp > 0 && e.Position == pos);
        }

        private static EnemyInstance GetEnemyAt(ActiveRunState run, GridPos pos, int excludeInstanceId)
        {
            return run.Enemies.FirstOrDefault(
                e => e.CurrentHp > 0 && e.InstanceId != excludeInstanceId && e.Position == pos);
        }
    }
}