#nullable disable
using System.Security.Cryptography;

namespace RogueCards
{
    public static class EnemyAi
    {
        private const int GridSize = GameEngine.GridSize;

        public static void SpawnWave(GameEngine engine, int waveNumber)
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

        private static EnemyInstance SpawnSingleEnemy(GameEngine engine, EnemyType enemyType)
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

            EnemyInstance enemy = new()
            {
                InstanceId = run.NextEnemyInstanceId,
                Type = def.Type,
                DefinitionId = def.Id,
                Name = def.Name,
                Position = spawnPos.Value,
                CurrentHp = def.MaxHp,
                MaxHp = def.MaxHp,
                CoinDrop = def.CoinDrop,
                IsBoss = def.IsBoss,
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

        public static void ExecuteEnemyTurns(GameEngine engine)
        {
            ActiveRunState run = engine.CurrentRun;
            if (run == null || run.IsGameOver)
            {
                return;
            }

            List<EnemyInstance> turnOrder = run.Enemies.Where(e => e.CurrentHp > 0).ToList();
            foreach (EnemyInstance enemy in turnOrder)
            {
                if (run.IsGameOver || run.Player.CurrentHp <= 0)
                {
                    break;
                }

                if (enemy.CurrentHp > 0)
                {
                    ProcessSingleEnemyTurn(engine, enemy);
                }
            }
        }

        private static void ProcessSingleEnemyTurn(GameEngine engine, EnemyInstance enemy)
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

            DispatchArmedEnemyAction(engine, enemy);
        }

        private static void DispatchArmedEnemyAction(GameEngine engine, EnemyInstance enemy)
        {
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

        private static void EquipEnemyWeapon(GameEngine engine, EnemyInstance enemy)
        {
            string weaponName = engine.Database.Enemies.TryGetValue(enemy.Type, out EnemyDefinition def)
                ? def.WeaponName
                : "Weapon";

            enemy.State = EnemyState.Armed;
            enemy.EquippedWeaponLabel = weaponName;
            engine.AddCombatLog($"{enemy.Name} at {enemy.Position} equipped {weaponName}.");
        }

        private static void HandleChargingEnemy(GameEngine engine, EnemyInstance enemy)
        {
            if (enemy.ChargeTurnsRemaining <= 1)
            {
                ExecuteLockedAttack(engine, enemy);
                return;
            }

            enemy.ChargeTurnsRemaining--;
            if (enemy.IsBoss && enemy.LockedAttackType == "BossHookClub" && !enemy.BossSpawnedDuringCharge)
            {
                TryBossSummonMinion(engine, enemy);
            }
            else
            {
                engine.AddCombatLog($"{enemy.Name} continues charging ({enemy.ChargeTurnsRemaining} turn left).");
            }
        }

        private static void TryBossSummonMinion(GameEngine engine, EnemyInstance boss)
        {
            boss.BossSpawnedDuringCharge = true;
            EnemyType[] summonPool = [EnemyType.Pawn, EnemyType.Archer, EnemyType.Spearman, EnemyType.Shaman];
            EnemyType chosen = summonPool[RandomNumberGenerator.GetInt32(summonPool.Length)];
            EnemyInstance summoned = SpawnSingleEnemy(engine, chosen);

            if (summoned != null)
            {
                engine.AddCombatLog($"Viking Chief summoned a {summoned.Name} at {summoned.Position} while charging!");
            }
        }

        private static void ExecuteLockedAttack(GameEngine engine, EnemyInstance enemy)
        {
            int baseDamage = GetEnemyAttackDamage(engine, enemy, 2);

            switch (enemy.LockedAttackType)
            {
                case "Axe":
                    ResolveAreaDamageAttack(engine, enemy, enemy.LockedTargetTiles, baseDamage, "Axe");
                    break;
                case "Spear":
                    ResolveAreaDamageAttack(engine, enemy, enemy.LockedTargetTiles, baseDamage, "Spear");
                    break;
                case "Arrow":
                    ResolveFirstHitLineAttack(engine, enemy, enemy.LockedTargetTiles, baseDamage, "Arrow");
                    break;
                case "Poison":
                    ResolveShamanPoisonAttack(engine, enemy, GetEnemyAttackDamage(engine, enemy, 1));
                    break;
                case "BossThrowingAxe":
                    ResolveFirstHitLineAttack(engine, enemy, enemy.LockedTargetTiles, 2, "Throwing Axe");
                    break;
                case "BossHookClub":
                    ResolveBossHookAndClub(engine, enemy);
                    break;
                case "BossEvolvedAxe":
                    ResolveAreaDamageAttack(engine, enemy, enemy.LockedTargetTiles, GetEnemyAttackDamage(engine, enemy, 3), "Evolved Viking Axe");
                    break;
            }

            ResetEnemyPostAttack(enemy);
        }

        private static void ResetEnemyPostAttack(EnemyInstance enemy)
        {
            enemy.State = EnemyState.Armed;
            enemy.ChargeTurnsRemaining = 0;
            enemy.LockedTargetTiles.Clear();
            enemy.LockedAttackType = string.Empty;

            if (enemy.IsBoss)
            {
                enemy.BossCycleIndex = (enemy.BossCycleIndex + 1) % 3;
                enemy.BossSpawnedDuringCharge = false;
                enemy.EquippedWeaponLabel = GetBossCycleWeaponLabel(enemy.BossCycleIndex);
            }
        }

        private static void ResolveAreaDamageAttack(
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

        private static void ResolveFirstHitLineAttack(
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

        private static void ResolveShamanPoisonAttack(GameEngine engine, EnemyInstance shaman, int impactDamage)
        {
            ActiveRunState run = engine.CurrentRun;
            if (shaman.LockedTargetTiles.Count == 0)
            {
                return;
            }

            GridPos center = shaman.LockedTargetTiles[0];
            HitTileWithEnemyAttack(engine, shaman, center, impactDamage, "Poison Potion");

            foreach (GridPos tile in shaman.LockedTargetTiles.Where(t => t.IsValid()))
            {
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

        private static void ResolveBossHookAndClub(GameEngine engine, EnemyInstance boss)
        {
            GridPos dir = boss.LockedDirection;
            GridPos adjacentTile = boss.Position.Offset(dir.X, dir.Y);

            ResolveBossHookPull(engine, boss, dir, adjacentTile);
            if (!engine.CurrentRun.IsGameOver && adjacentTile.IsValid())
            {
                ResolveBossClubStrike(engine, boss, dir, adjacentTile);
            }
        }

        private static void ResolveBossHookPull(
            GameEngine engine,
            EnemyInstance boss,
            GridPos dir,
            GridPos adjacentTile)
        {
            ActiveRunState run = engine.CurrentRun;
            for (int step = 1; step <= 3; step++)
            {
                GridPos checkTile = boss.Position.Offset(dir.X * step, dir.Y * step);
                if (!checkTile.IsValid())
                {
                    break;
                }

                if (run.Player.Position == checkTile)
                {
                    engine.ApplyEnemyAttackToPlayer(boss, 1, "Boat Hook");
                    if (step > 1 && adjacentTile.IsValid() && !IsOccupied(run, adjacentTile))
                    {
                        MovePlayerByBossEffect(engine, adjacentTile, $"Viking Chief pulled you to {adjacentTile}!");
                    }

                    return;
                }

                EnemyInstance otherEnemy = GetEnemyAt(run, checkTile, boss.InstanceId);
                if (otherEnemy != null)
                {
                    engine.ApplyFriendlyFireDamage(boss, otherEnemy, 1, "Boat Hook");
                    if (otherEnemy.CurrentHp > 0 && step > 1 && adjacentTile.IsValid() && !IsOccupied(run, adjacentTile))
                    {
                        otherEnemy.Position = adjacentTile;
                        engine.AddCombatLog($"Viking Chief pulled {otherEnemy.Name} to {adjacentTile}!");
                        engine.TriggerHazardsForEnemy(otherEnemy);
                    }

                    return;
                }
            }

            engine.AddCombatLog("Viking Chief cast Boat Hook and missed.");
        }

        private static void ResolveBossClubStrike(
            GameEngine engine,
            EnemyInstance boss,
            GridPos dir,
            GridPos adjacentTile)
        {
            ActiveRunState run = engine.CurrentRun;
            GridPos knockTile = adjacentTile.Offset(dir.X, dir.Y);

            if (run.Player.Position == adjacentTile)
            {
                engine.ApplyEnemyAttackToPlayer(boss, 2, "Wooden Club");
                if (!run.IsGameOver && knockTile.IsValid() && !IsOccupied(run, knockTile))
                {
                    MovePlayerByBossEffect(engine, knockTile, $"Wooden Club knocked you back to {knockTile}!");
                }

                return;
            }

            EnemyInstance victim = GetEnemyAt(run, adjacentTile, boss.InstanceId);
            if (victim == null)
            {
                return;
            }

            engine.ApplyFriendlyFireDamage(boss, victim, 2, "Wooden Club");
            if (victim.CurrentHp > 0 && knockTile.IsValid() && !IsOccupied(run, knockTile))
            {
                victim.Position = knockTile;
                engine.TriggerHazardsForEnemy(victim);
            }
        }

        private static void MovePlayerByBossEffect(GameEngine engine, GridPos destination, string logMessage)
        {
            GridPos oldPos = engine.CurrentRun.Player.Position;
            engine.CurrentRun.Player.Position = destination;
            engine.ArmBearTrapAt(oldPos);
            engine.AddCombatLog(logMessage);
            engine.TriggerHazardsForPlayer();
        }

        private static bool HitTileWithEnemyAttack(
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

        private static void ActPawn(GameEngine engine, EnemyInstance pawn)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;
            int attackRange = GetEnemyAttackRange(engine, pawn, 1);

            if (pawn.Position.ManhattanDistance(playerPos) <= attackRange)
            {
                pawn.State = EnemyState.Charging;
                pawn.ChargeTurnsRemaining = 1;
                pawn.LockedAttackType = "Axe";
                pawn.LockedDirection = new GridPos(playerPos.X - pawn.Position.X, playerPos.Y - pawn.Position.Y);
                pawn.LockedTargetTiles = [playerPos];
                engine.AddCombatLog($"Pawn at {pawn.Position} is charging an Axe strike at {playerPos}!");
                return;
            }

            MoveEnemyToward(engine, pawn, playerPos);
        }

        private static void ActArcher(GameEngine engine, EnemyInstance archer)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;
            int dist = archer.Position.ManhattanDistance(playerPos);
            bool aligned = archer.Position.X == playerPos.X || archer.Position.Y == playerPos.Y;

            if (dist <= 2)
            {
                if (!TryMoveEnemyAway(engine, archer, playerPos))
                {
                    ChargeArcherShot(engine, archer, playerPos);
                }

                return;
            }

            if (aligned)
            {
                ChargeArcherShot(engine, archer, playerPos);
                return;
            }

            MoveArcherToAlign(engine, archer, playerPos);
        }

        private static void ChargeArcherShot(GameEngine engine, EnemyInstance archer, GridPos playerPos)
        {
            GridPos dir = GetPrimaryOrthogonalDirection(archer.Position, playerPos);
            int range = GetEnemyAttackRange(engine, archer, GridSize);

            archer.State = EnemyState.Charging;
            archer.ChargeTurnsRemaining = 1;
            archer.LockedAttackType = "Arrow";
            archer.LockedDirection = dir;
            archer.LockedTargetTiles = BuildRayTiles(archer.Position, dir, range);
            engine.AddCombatLog($"Archer at {archer.Position} is aiming a Bow shot!");
        }

        private static void ActSpearman(GameEngine engine, EnemyInstance spearman)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;
            int dist = spearman.Position.ManhattanDistance(playerPos);
            bool aligned = spearman.Position.X == playerPos.X || spearman.Position.Y == playerPos.Y;
            int range = GetEnemyAttackRange(engine, spearman, 2);

            if (aligned && dist <= range)
            {
                GridPos dir = GetPrimaryOrthogonalDirection(spearman.Position, playerPos);
                spearman.State = EnemyState.Charging;
                spearman.ChargeTurnsRemaining = 1;
                spearman.LockedAttackType = "Spear";
                spearman.LockedDirection = dir;
                spearman.LockedTargetTiles = BuildRayTiles(spearman.Position, dir, range);
                engine.AddCombatLog($"Spearman at {spearman.Position} is charging a Spear thrust!");
                return;
            }

            MoveEnemyToward(engine, spearman, playerPos);
        }

        private static void ActShaman(GameEngine engine, EnemyInstance shaman)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;
            int dist = shaman.Position.ManhattanDistance(playerPos);
            int preferredRange = GetEnemyAttackRange(engine, shaman, 3);

            if (dist <= 2)
            {
                if (!TryMoveEnemyAway(engine, shaman, playerPos))
                {
                    ChargeShamanPoison(engine, shaman, playerPos);
                }

                return;
            }

            if (dist == preferredRange)
            {
                ChargeShamanPoison(engine, shaman, playerPos);
                return;
            }

            MoveEnemyToward(engine, shaman, playerPos);
        }

        private static void ChargeShamanPoison(GameEngine engine, EnemyInstance shaman, GridPos targetCenter)
        {
            List<GridPos> splashTiles = [targetCenter];
            GridPos[] diagonals =
            [
                targetCenter.Offset(-1, -1),
                targetCenter.Offset(1, -1),
                targetCenter.Offset(-1, 1),
                targetCenter.Offset(1, 1)
            ];

            splashTiles.AddRange(diagonals.Where(d => d.IsValid()));

            shaman.State = EnemyState.Charging;
            shaman.ChargeTurnsRemaining = 1;
            shaman.LockedAttackType = "Poison";
            shaman.LockedDirection = GetPrimaryOrthogonalDirection(shaman.Position, targetCenter);
            shaman.LockedTargetTiles = splashTiles;
            engine.AddCombatLog($"Shaman at {shaman.Position} is preparing a Poison Potion at {targetCenter}!");
        }

        private static void ActVikingChief(GameEngine engine, EnemyInstance boss)
        {
            GridPos playerPos = engine.CurrentRun.Player.Position;

            if (boss.BossCycleIndex == 0)
            {
                ChargeBossThrowingAxe(engine, boss, playerPos);
            }
            else if (boss.BossCycleIndex == 1)
            {
                ChargeBossHookClub(engine, boss, playerPos);
            }
            else
            {
                ChargeBossEvolvedAxe(engine, boss, playerPos);
            }
        }

        private static void ChargeBossThrowingAxe(GameEngine engine, EnemyInstance boss, GridPos playerPos)
        {
            bool aligned = boss.Position.X == playerPos.X || boss.Position.Y == playerPos.Y;
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
            boss.LockedTargetTiles = BuildRayTiles(boss.Position, dir, GetEnemyAttackRange(engine, boss, GridSize));
            engine.AddCombatLog("Viking Chief is charging Throwing Axe!");
        }

        private static void ChargeBossHookClub(GameEngine engine, EnemyInstance boss, GridPos playerPos)
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

        private static void ChargeBossEvolvedAxe(GameEngine engine, EnemyInstance boss, GridPos playerPos)
        {
            GridPos forward = GetPrimaryOrthogonalDirection(boss.Position, playerPos);
            boss.State = EnemyState.Charging;
            boss.ChargeTurnsRemaining = 1;
            boss.EquippedWeaponLabel = "Evolved Axe";
            boss.LockedAttackType = "BossEvolvedAxe";
            boss.LockedDirection = forward;
            boss.LockedTargetTiles = BuildEvolvedVikingAxeTiles(boss.Position, forward);
            engine.AddCombatLog("Viking Chief raises the Evolved Viking Axe (6-tile cleave)!");
        }

        private static List<GridPos> BuildEvolvedVikingAxeTiles(GridPos origin, GridPos forward)
        {
            int fx = forward.X;
            int fy = forward.Y;
            int lx = -fy;
            int ly = fx;

            GridPos[] raw =
            [
                origin.Offset(fx, fy),
                origin.Offset(fx * 2, fy * 2),
                origin.Offset(fx + lx, fy + ly),
                origin.Offset(fx - lx, fy - ly),
                origin.Offset(lx, ly),
                origin.Offset(-lx, -ly)
            ];

            List<GridPos> valid = [];
            foreach (GridPos tile in raw)
            {
                if (tile.IsValid() && !valid.Contains(tile))
                {
                    valid.Add(tile);
                }
            }

            return valid;
        }

        private static void MoveEnemyToward(GameEngine engine, EnemyInstance enemy, GridPos target)
        {
            List<GridPos> neighbors = GetFreeOrthogonalNeighbors(engine.CurrentRun, enemy.Position);
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

        private static bool TryMoveEnemyAway(GameEngine engine, EnemyInstance enemy, GridPos threat)
        {
            List<GridPos> neighbors = GetFreeOrthogonalNeighbors(engine.CurrentRun, enemy.Position);
            if (neighbors.Count == 0)
            {
                return false;
            }

            int currentDist = enemy.Position.ManhattanDistance(threat);
            GridPos best = neighbors.OrderByDescending(n => n.ManhattanDistance(threat)).First();

            if (best.ManhattanDistance(threat) > currentDist)
            {
                MoveEnemyToTile(engine, enemy, best);
                return true;
            }

            return false;
        }

        private static void MoveArcherToAlign(GameEngine engine, EnemyInstance archer, GridPos playerPos)
        {
            List<GridPos> neighbors = GetFreeOrthogonalNeighbors(engine.CurrentRun, archer.Position);
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

        private static bool TryMoveToAlign(GameEngine engine, EnemyInstance enemy, GridPos playerPos)
        {
            List<GridPos> neighbors = GetFreeOrthogonalNeighbors(engine.CurrentRun, enemy.Position);
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

        private static void MoveEnemyToTile(GameEngine engine, EnemyInstance enemy, GridPos dest)
        {
            enemy.Position = dest;
            engine.AddCombatLog($"{enemy.Name} moved to {dest}.");
            engine.TriggerHazardsForEnemy(enemy);
        }

        private static GridPos? FindFreeSpawnTile(ActiveRunState run, bool preferEdge)
        {
            List<GridPos> edgeFree = [];
            List<GridPos> allFree = [];

            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    GridPos pos = new(x, y);
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
                return edgeFree[RandomNumberGenerator.GetInt32(edgeFree.Count)];
            }

            return allFree.Count > 0 ? allFree[RandomNumberGenerator.GetInt32(allFree.Count)] : null;
        }

        private static List<GridPos> GetFreeOrthogonalNeighbors(ActiveRunState run, GridPos origin)
        {
            GridPos[] candidates =
            [
                origin.Offset(0, -1),
                origin.Offset(0, 1),
                origin.Offset(-1, 0),
                origin.Offset(1, 0)
            ];

            return candidates.Where(tile => tile.IsValid() && !IsOccupied(run, tile)).ToList();
        }

        private static List<GridPos> BuildRayTiles(GridPos origin, GridPos dir, int maxSteps)
        {
            List<GridPos> tiles = [];
            for (int step = 1; step <= maxSteps; step++)
            {
                GridPos next = origin.Offset(dir.X * step, dir.Y * step);
                if (!next.IsValid())
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

            return Math.Abs(dx) >= Math.Abs(dy)
                ? new GridPos(Math.Sign(dx), 0)
                : new GridPos(0, Math.Sign(dy));
        }

        private static int GetEnemyAttackDamage(GameEngine engine, EnemyInstance enemy, int fallback) =>
            engine.Database.Enemies.TryGetValue(enemy.Type, out EnemyDefinition def) ? def.AttackDamage : fallback;

        private static int GetEnemyAttackRange(GameEngine engine, EnemyInstance enemy, int fallback) =>
            engine.Database.Enemies.TryGetValue(enemy.Type, out EnemyDefinition def) ? def.AttackRange : fallback;

        private static string GetBossCycleWeaponLabel(int cycleIndex) => cycleIndex switch
        {
            0 => "Throwing Axe",
            1 => "Hook & Club",
            _ => "Evolved Axe"
        };

        private static bool IsOccupied(ActiveRunState run, GridPos pos) =>
            (run.Player != null && run.Player.Position == pos) ||
            run.Enemies.Any(e => e.CurrentHp > 0 && e.Position == pos);

        private static EnemyInstance GetEnemyAt(ActiveRunState run, GridPos pos, int excludeInstanceId) =>
            run.Enemies.FirstOrDefault(e => e.CurrentHp > 0 && e.InstanceId != excludeInstanceId && e.Position == pos);
    }
}