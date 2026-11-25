using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RogueCards.Domain;
namespace RogueCards.Persistence
{
    public sealed class TextRunPersistence : IRunPersistence
    {
        private const int SaveFormatVersion = 1;
        private readonly string _savesDir;
        private readonly string _profilePath;
        private readonly string _statsPath;
        private readonly string _activeRunPath;

        public TextRunPersistence(string savesDir)
        {
            _savesDir = savesDir;
            _profilePath = Path.Combine(savesDir, "profile.txt");
            _statsPath = Path.Combine(savesDir, "stats.txt");
            _activeRunPath = Path.Combine(savesDir, "active_run.txt");
        }

        public bool HasActiveRunSave()
        {
            return File.Exists(_activeRunPath);
        }

        public PlayerProfile LoadProfile(GameDatabase db, out string warning)
        {
            warning = string.Empty;
            Directory.CreateDirectory(_savesDir);

            if (!File.Exists(_profilePath))
            {
                PlayerProfile fresh = CreateDefaultProfile(db);
                SaveProfile(fresh);
                return fresh;
            }

            try
            {
                PlayerProfile loaded = ParseProfileFile(_profilePath, db);
                EnsureDefaultUnlocks(loaded, db);
                return loaded;
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is InvalidDataException)
            {
                PlayerProfile reset = CreateDefaultProfile(db);
                SaveProfile(reset);
                warning = "Profile save was corrupted and has been reset to defaults.";
                return reset;
            }
        }

        public void SaveProfile(PlayerProfile profile)
        {
            Directory.CreateDirectory(_savesDir);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"VERSION={SaveFormatVersion}");
            sb.AppendLine($"COINS={Math.Max(0, profile.Coins)}");
            sb.AppendLine($"AREA2_UNLOCKED={profile.Area2Unlocked}");
            sb.AppendLine($"SELECTED_CHARACTER={SanitizeValue(profile.SelectedCharacterId)}");
            sb.AppendLine($"UNLOCKED_WEAPONS={string.Join(",", profile.UnlockedWeaponIds)}");
            sb.AppendLine($"UNLOCKED_UPGRADES={string.Join(",", profile.UnlockedUpgradeIds)}");
            sb.AppendLine($"UNLOCKED_CHARACTERS={string.Join(",", profile.UnlockedCharacterIds)}");
            sb.AppendLine($"COMPLETED_ACHIEVEMENTS={string.Join(",", profile.CompletedAchievementIds)}");

            IEnumerable<string> progressEntries = profile.AchievementProgress
                .Select(kvp => $"{SanitizeValue(kvp.Key)}:{kvp.Value}");
            sb.AppendLine($"ACHIEVEMENT_PROGRESS={string.Join(",", progressEntries)}");

            File.WriteAllText(_profilePath, sb.ToString(), Encoding.UTF8);
        }

        public GameStatistics LoadStatistics()
        {
            Directory.CreateDirectory(_savesDir);

            if (!File.Exists(_statsPath))
            {
                GameStatistics fresh = new GameStatistics();
                SaveStatistics(fresh);
                return fresh;
            }

            try
            {
                return ParseStatisticsFile(_statsPath);
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is InvalidDataException)
            {
                GameStatistics fresh = new GameStatistics();
                SaveStatistics(fresh);
                return fresh;
            }
        }

        public void SaveStatistics(GameStatistics stats)
        {
            Directory.CreateDirectory(_savesDir);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"VERSION={SaveFormatVersion}");
            sb.AppendLine($"TOTAL_RUNS={stats.TotalRunsPlayed}");
            sb.AppendLine($"RUNS_WON={stats.RunsWon}");
            sb.AppendLine($"HIGHEST_WAVE={stats.HighestWaveReached}");
            sb.AppendLine($"TOTAL_TURNS={stats.TotalTurnsTaken}");
            sb.AppendLine($"TOTAL_ENEMIES={stats.TotalEnemiesDefeated}");
            sb.AppendLine($"FRIENDLY_FIRE_KILLS={stats.TotalFriendlyFireKills}");
            sb.AppendLine($"TOTAL_DAMAGE={stats.TotalDamageDealt}");
            sb.AppendLine($"TOTAL_COINS={stats.TotalCoinsEarned}");
            sb.AppendLine($"TOTAL_CARDS={stats.TotalCardsPlayed}");
            sb.AppendLine($"BEAR_TRAP_STUNS={stats.BearTrapStunsTriggered}");

            IEnumerable<string> enemyCounts = stats.EnemiesDefeatedByType
                .Select(kvp => $"{kvp.Key}:{kvp.Value}");
            sb.AppendLine($"ENEMIES_BY_TYPE={string.Join(",", enemyCounts)}");

            IEnumerable<string> weaponCounts = stats.WeaponUseCounts
                .Select(kvp => $"{SanitizeValue(kvp.Key)}:{kvp.Value}");
            sb.AppendLine($"WEAPON_USES={string.Join(",", weaponCounts)}");

            File.WriteAllText(_statsPath, sb.ToString(), Encoding.UTF8);
        }

        public bool SaveActiveRun(ActiveRunState run)
        {
            try
            {
                Directory.CreateDirectory(_savesDir);
                string serialized = SerializeActiveRun(run);
                File.WriteAllText(_activeRunPath, serialized, Encoding.UTF8);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public bool LoadActiveRun(GameDatabase db, out ActiveRunState? run, out string error)
        {
            run = null;
            error = string.Empty;

            if (!File.Exists(_activeRunPath))
            {
                error = "No saved run was found.";
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(_activeRunPath, Encoding.UTF8);
                run = ParseActiveRun(lines, db);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is InvalidDataException)
            {
                error = $"Failed to load saved run: {ex.Message}";
                return false;
            }
        }

        public void ClearActiveRunSave()
        {
            if (!File.Exists(_activeRunPath))
            {
                return;
            }

            try
            {
                File.Delete(_activeRunPath);
            }
            catch (IOException)
            {
                // Ignore delete error to keep gameplay flow uninterrupted.
            }
        }

        private static PlayerProfile CreateDefaultProfile(GameDatabase db)
        {
            PlayerProfile profile = new PlayerProfile
            {
                Coins = 0,
                Area2Unlocked = false,
                SelectedCharacterId = "viking"
            };

            EnsureDefaultUnlocks(profile, db);
            return profile;
        }

        private static void EnsureDefaultUnlocks(PlayerProfile profile, GameDatabase db)
        {
            foreach (WeaponDefinition weapon in db.WeaponList)
            {
                if (weapon.IsDefaultUnlocked)
                {
                    profile.UnlockedWeaponIds.Add(weapon.Id);
                }
            }

            foreach (CharacterDefinition character in db.CharacterList)
            {
                if (character.IsDefaultUnlocked)
                {
                    profile.UnlockedCharacterIds.Add(character.Id);
                }
            }

            if (!profile.UnlockedCharacterIds.Contains(profile.SelectedCharacterId))
            {
                profile.SelectedCharacterId = profile.UnlockedCharacterIds.FirstOrDefault() ?? "viking";
            }
        }

        private static PlayerProfile ParseProfileFile(string path, GameDatabase db)
        {
            Dictionary<string, string> kv = ReadKeyValues(File.ReadAllLines(path, Encoding.UTF8));
            ValidateVersion(kv);

            PlayerProfile profile = new PlayerProfile
            {
                Coins = RequireInt(kv, "COINS", 0, 9999999),
                Area2Unlocked = RequireBool(kv, "AREA2_UNLOCKED"),
                SelectedCharacterId = RequireString(kv, "SELECTED_CHARACTER")
            };

            AddSplitItems(kv, "UNLOCKED_WEAPONS", profile.UnlockedWeaponIds, id => db.Weapons.ContainsKey(id));
            AddSplitItems(kv, "UNLOCKED_UPGRADES", profile.UnlockedUpgradeIds, id => db.Upgrades.ContainsKey(id));
            AddSplitItems(kv, "UNLOCKED_CHARACTERS", profile.UnlockedCharacterIds, id => db.Characters.ContainsKey(id));
            AddSplitItems(kv, "COMPLETED_ACHIEVEMENTS", profile.CompletedAchievementIds, id => db.Achievements.ContainsKey(id));

            if (kv.TryGetValue("ACHIEVEMENT_PROGRESS", out string? rawProgress))
            {
                ParseStringIntMap(rawProgress, profile.AchievementProgress);
            }

            return profile;
        }

        private static GameStatistics ParseStatisticsFile(string path)
        {
            Dictionary<string, string> kv = ReadKeyValues(File.ReadAllLines(path, Encoding.UTF8));
            ValidateVersion(kv);

            GameStatistics stats = new GameStatistics
            {
                TotalRunsPlayed = RequireInt(kv, "TOTAL_RUNS", 0, 9999999),
                RunsWon = RequireInt(kv, "RUNS_WON", 0, 9999999),
                HighestWaveReached = RequireInt(kv, "HIGHEST_WAVE", 0, 100),
                TotalTurnsTaken = RequireInt(kv, "TOTAL_TURNS", 0, 9999999),
                TotalEnemiesDefeated = RequireInt(kv, "TOTAL_ENEMIES", 0, 9999999),
                TotalFriendlyFireKills = RequireInt(kv, "FRIENDLY_FIRE_KILLS", 0, 9999999),
                TotalDamageDealt = RequireInt(kv, "TOTAL_DAMAGE", 0, 9999999),
                TotalCoinsEarned = RequireInt(kv, "TOTAL_COINS", 0, 9999999),
                TotalCardsPlayed = RequireInt(kv, "TOTAL_CARDS", 0, 9999999),
                BearTrapStunsTriggered = RequireInt(kv, "BEAR_TRAP_STUNS", 0, 9999999)
            };

            if (kv.TryGetValue("ENEMIES_BY_TYPE", out string? rawEnemies))
            {
                ParseEnemyCountMap(rawEnemies, stats.EnemiesDefeatedByType);
            }

            if (kv.TryGetValue("WEAPON_USES", out string? rawWeapons))
            {
                ParseStringIntMap(rawWeapons, stats.WeaponUseCounts);
            }

            return stats;
        }

        private static string SerializeActiveRun(ActiveRunState run)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"VERSION={SaveFormatVersion}");

            AppendRunSection(sb, run);
            AppendPlayerSection(sb, run.Player);
            AppendCardsSection(sb, "[HAND]", run.Player.Hand);
            AppendCardsSection(sb, "[EQUIPPED]", run.Player.EquippedQueue);
            AppendEnemiesSection(sb, run.Enemies);
            AppendHazardsSection(sb, run.Hazards);
            AppendRewardSection(sb, run.PendingReward);
            AppendLogSection(sb, run.RecentLogEntries);

            return sb.ToString();
        }

        private static void AppendRunSection(StringBuilder sb, ActiveRunState run)
        {
            sb.AppendLine("[RUN]");
            sb.AppendLine($"CurrentWave={run.CurrentWave}");
            sb.AppendLine($"TurnNumber={run.TurnNumber}");
            sb.AppendLine($"IsStartingDraft={run.IsStartingDraft}");
            sb.AppendLine($"UsedRerollInRun={run.UsedRerollInRun}");
            sb.AppendLine($"TookDamageInCurrentWave={run.TookDamageInCurrentWave}");
            sb.AppendLine($"IsVictory={run.IsVictory}");
            sb.AppendLine($"IsGameOver={run.IsGameOver}");
            sb.AppendLine($"GameOverReason={SanitizeLine(run.GameOverReason)}");
            sb.AppendLine($"NextCardInstanceId={run.NextCardInstanceId}");
            sb.AppendLine($"NextEnemyInstanceId={run.NextEnemyInstanceId}");
            sb.AppendLine($"CombatLogPath={SanitizeLine(run.CombatLogPath)}");
            sb.AppendLine($"CoinsEarnedInRun={run.CoinsEarnedInRun}");
            sb.AppendLine($"EnemiesDefeatedInRun={run.EnemiesDefeatedInRun}");
        }

        private static void AppendPlayerSection(StringBuilder sb, PlayerState player)
        {
            sb.AppendLine("[PLAYER]");
            sb.AppendLine($"CharacterId={SanitizeValue(player.CharacterId)}");
            sb.AppendLine($"PosX={player.Position.X}");
            sb.AppendLine($"PosY={player.Position.Y}");
            sb.AppendLine($"CurrentHp={player.CurrentHp}");
            sb.AppendLine($"MaxHp={player.MaxHp}");
            sb.AppendLine($"SpecialCooldown={player.SpecialCooldown}");
            sb.AppendLine($"SpecialMaxCooldown={player.SpecialMaxCooldown}");
            sb.AppendLine($"IsImmuneThisTurn={player.IsImmuneThisTurn}");
            sb.AppendLine($"PoisonTurnsRemaining={player.PoisonTurnsRemaining}");
            sb.AppendLine($"FreeRerollsRemaining={player.FreeRerollsRemaining}");
        }

        private static void AppendCardsSection(StringBuilder sb, string header, List<CardInstance> cards)
        {
            sb.AppendLine(header);
            foreach (CardInstance card in cards)
            {
                string modifiers = string.Join(";", card.AppliedModifiers.Select(SanitizeField));
                sb.AppendLine(string.Join(
                    "|",
                    card.InstanceId.ToString(CultureInfo.InvariantCulture),
                    SanitizeField(card.Definition.Id),
                    card.CurrentCooldown.ToString(CultureInfo.InvariantCulture),
                    card.BonusDamage.ToString(CultureInfo.InvariantCulture),
                    card.CooldownModifier.ToString(CultureInfo.InvariantCulture),
                    card.HasPoison.ToString(),
                    modifiers));
            }
        }

        private static void AppendEnemiesSection(StringBuilder sb, List<EnemyInstance> enemies)
        {
            sb.AppendLine("[ENEMIES]");
            foreach (EnemyInstance enemy in enemies)
            {
                string tiles = string.Join(";", enemy.LockedTargetTiles.Select(t => $"{t.X}:{t.Y}"));
                sb.AppendLine(string.Join(
                    "|",
                    enemy.InstanceId.ToString(CultureInfo.InvariantCulture),
                    enemy.Type.ToString(),
                    enemy.Position.X.ToString(CultureInfo.InvariantCulture),
                    enemy.Position.Y.ToString(CultureInfo.InvariantCulture),
                    enemy.CurrentHp.ToString(CultureInfo.InvariantCulture),
                    enemy.MaxHp.ToString(CultureInfo.InvariantCulture),
                    enemy.State.ToString(),
                    enemy.ChargeTurnsRemaining.ToString(CultureInfo.InvariantCulture),
                    enemy.LockedDirection.X.ToString(CultureInfo.InvariantCulture),
                    enemy.LockedDirection.Y.ToString(CultureInfo.InvariantCulture),
                    SanitizeField(enemy.LockedAttackType),
                    enemy.IsStunned.ToString(),
                    enemy.PoisonTurnsRemaining.ToString(CultureInfo.InvariantCulture),
                    enemy.BossCycleIndex.ToString(CultureInfo.InvariantCulture),
                    tiles));
            }
        }

        private static void AppendHazardsSection(StringBuilder sb, List<GroundHazard> hazards)
        {
            sb.AppendLine("[HAZARDS]");
            foreach (GroundHazard hazard in hazards)
            {
                sb.AppendLine(string.Join(
                    "|",
                    hazard.Type.ToString(),
                    hazard.Position.X.ToString(CultureInfo.InvariantCulture),
                    hazard.Position.Y.ToString(CultureInfo.InvariantCulture),
                    hazard.RemainingTurns.ToString(CultureInfo.InvariantCulture),
                    hazard.Damage.ToString(CultureInfo.InvariantCulture),
                    hazard.IsArmed.ToString(),
                    hazard.AppliesStun.ToString()));
            }
        }

        private static void AppendRewardSection(StringBuilder sb, PendingRewardState? reward)
        {
            sb.AppendLine("[REWARD]");
            if (reward == null)
            {
                sb.AppendLine("HasReward=False");
                return;
            }

            sb.AppendLine("HasReward=True");
            sb.AppendLine($"Type={reward.Type}");
            sb.AppendLine($"RerollCount={reward.RerollCount}");
            sb.AppendLine($"OfferedWeaponIds={string.Join(",", reward.OfferedWeaponIds)}");
            sb.AppendLine($"SelectedStartingWeaponIds={string.Join(",", reward.SelectedStartingWeaponIds)}");
            sb.AppendLine($"OfferedUpgrades={string.Join(",", reward.OfferedUpgrades)}");
        }

        private static void AppendLogSection(StringBuilder sb, List<string> logEntries)
        {
            sb.AppendLine("[LOG]");
            foreach (string entry in logEntries)
            {
                sb.AppendLine(SanitizeLine(entry));
            }
        }

        private static ActiveRunState ParseActiveRun(string[] lines, GameDatabase db)
        {
            Dictionary<string, List<string>> sections = SplitIntoSections(lines);
            ValidateHeaderVersion(sections);

            ActiveRunState run = new ActiveRunState();
            PopulateRunMetadata(run, ReadKeyValues(GetRequiredSection(sections, "RUN")));
            PopulatePlayerState(run.Player, ReadKeyValues(GetRequiredSection(sections, "PLAYER")), db);

            ParseCardsInto(GetSectionOrEmpty(sections, "HAND"), run.Player.Hand, db);
            ParseCardsInto(GetSectionOrEmpty(sections, "EQUIPPED"), run.Player.EquippedQueue, db);
            ParseEnemiesInto(GetSectionOrEmpty(sections, "ENEMIES"), run.Enemies);
            ParseHazardsInto(GetSectionOrEmpty(sections, "HAZARDS"), run.Hazards);

            run.PendingReward = ParseRewardSection(GetRequiredSection(sections, "REWARD"), db);

            foreach (string logLine in GetSectionOrEmpty(sections, "LOG"))
            {
                run.AddRecentLogEntry(logLine);
            }

            return run;
        }

        private static Dictionary<string, List<string>> SplitIntoSections(string[] lines)
        {
            Dictionary<string, List<string>> sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string currentSection = "HEADER";
            sections[currentSection] = new List<string>();

            foreach (string rawLine in lines)
            {
                string trimmed = rawLine.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    if (!sections.ContainsKey(currentSection))
                    {
                        sections[currentSection] = new List<string>();
                    }

                    continue;
                }

                sections[currentSection].Add(trimmed);
            }

            return sections;
        }

        private static void ValidateHeaderVersion(Dictionary<string, List<string>> sections)
        {
            List<string> headerLines = GetRequiredSection(sections, "HEADER");
            Dictionary<string, string> kv = ReadKeyValues(headerLines);
            ValidateVersion(kv);
        }

        private static void PopulateRunMetadata(ActiveRunState run, Dictionary<string, string> kv)
        {
            run.CurrentWave = RequireInt(kv, "CurrentWave", 1, 100);
            run.TurnNumber = RequireInt(kv, "TurnNumber", 1, 999999);
            run.IsStartingDraft = RequireBool(kv, "IsStartingDraft");
            run.UsedRerollInRun = RequireBool(kv, "UsedRerollInRun");
            run.TookDamageInCurrentWave = RequireBool(kv, "TookDamageInCurrentWave");
            run.IsVictory = RequireBool(kv, "IsVictory");
            run.IsGameOver = RequireBool(kv, "IsGameOver");
            run.GameOverReason = kv.TryGetValue("GameOverReason", out string? reason) ? reason : string.Empty;
            run.NextCardInstanceId = RequireInt(kv, "NextCardInstanceId", 1, 999999);
            run.NextEnemyInstanceId = RequireInt(kv, "NextEnemyInstanceId", 1, 999999);
            run.CombatLogPath = kv.TryGetValue("CombatLogPath", out string? logPath) ? logPath : string.Empty;
            run.CoinsEarnedInRun = RequireInt(kv, "CoinsEarnedInRun", 0, 999999);
            run.EnemiesDefeatedInRun = RequireInt(kv, "EnemiesDefeatedInRun", 0, 999999);
        }

        private static void PopulatePlayerState(PlayerState player, Dictionary<string, string> kv, GameDatabase db)
        {
            string characterId = RequireString(kv, "CharacterId");
            if (!db.Characters.ContainsKey(characterId))
            {
                throw new InvalidDataException($"Unknown character ID '{characterId}'.");
            }

            int posX = RequireInt(kv, "PosX", 0, GridPos.GridSize - 1);
            int posY = RequireInt(kv, "PosY", 0, GridPos.GridSize - 1);
            int maxHp = RequireInt(kv, "MaxHp", 1, 99);
            int currentHp = RequireInt(kv, "CurrentHp", 0, maxHp);

            player.CharacterId = characterId;
            player.Position = new GridPos(posX, posY);
            player.MaxHp = maxHp;
            player.CurrentHp = currentHp;
            player.SpecialCooldown = RequireInt(kv, "SpecialCooldown", 0, 30);
            player.SpecialMaxCooldown = RequireInt(kv, "SpecialMaxCooldown", 1, 30);
            player.IsImmuneThisTurn = RequireBool(kv, "IsImmuneThisTurn");
            player.PoisonTurnsRemaining = RequireInt(kv, "PoisonTurnsRemaining", 0, 20);
            player.FreeRerollsRemaining = RequireInt(kv, "FreeRerollsRemaining", 0, 20);
        }

        private static void ParseCardsInto(List<string> lines, List<CardInstance> destination, GameDatabase db)
        {
            foreach (string line in lines)
            {
                string[] parts = line.Split('|');
                if (parts.Length != 7)
                {
                    throw new InvalidDataException("Invalid serialized card entry.");
                }

                string weaponId = parts[1].Trim();
                if (!db.Weapons.TryGetValue(weaponId, out WeaponDefinition? definition))
                {
                    throw new InvalidDataException($"Unknown weapon ID '{weaponId}' in save file.");
                }

                CardInstance card = new CardInstance(ParseIntStrict(parts[0], 1, 999999), definition)
                {
                    CurrentCooldown = ParseIntStrict(parts[2], 0, 30),
                    BonusDamage = ParseIntStrict(parts[3], -20, 50),
                    CooldownModifier = ParseIntStrict(parts[4], -20, 20),
                    HasPoison = ParseBoolStrict(parts[5])
                };

                if (!string.IsNullOrWhiteSpace(parts[6]))
                {
                    string[] modifiers = parts[6].Split(';', StringSplitOptions.RemoveEmptyEntries);
                    card.AppliedModifiers.AddRange(modifiers);
                }

                destination.Add(card);
            }
        }

        private static void ParseEnemiesInto(List<string> lines, List<EnemyInstance> destination)
        {
            foreach (string line in lines)
            {
                string[] parts = line.Split('|');
                if (parts.Length != 15)
                {
                    throw new InvalidDataException("Invalid serialized enemy entry.");
                }

                EnemyType type = ParseEnumStrict<EnemyType>(parts[1]);
                int x = ParseIntStrict(parts[2], 0, GridPos.GridSize - 1);
                int y = ParseIntStrict(parts[3], 0, GridPos.GridSize - 1);
                int maxHp = ParseIntStrict(parts[5], 1, 999);
                int currentHp = ParseIntStrict(parts[4], 0, maxHp);

                EnemyInstance enemy = new EnemyInstance
                {
                    InstanceId = ParseIntStrict(parts[0], 1, 999999),
                    Type = type,
                    Position = new GridPos(x, y),
                    CurrentHp = currentHp,
                    MaxHp = maxHp,
                    State = ParseEnumStrict<EnemyState>(parts[6]),
                    ChargeTurnsRemaining = ParseIntStrict(parts[7], 0, 10),
                    LockedDirection = new GridPos(ParseIntStrict(parts[8], -1, 1), ParseIntStrict(parts[9], -1, 1)),
                    LockedAttackType = parts[10].Trim(),
                    IsStunned = ParseBoolStrict(parts[11]),
                    PoisonTurnsRemaining = ParseIntStrict(parts[12], 0, 20),
                    BossCycleIndex = ParseIntStrict(parts[13], 0, 10)
                };

                ParseLockedTiles(parts[14], enemy.LockedTargetTiles);
                destination.Add(enemy);
            }
        }

        private static void ParseLockedTiles(string rawTiles, List<GridPos> destination)
        {
            if (string.IsNullOrWhiteSpace(rawTiles))
            {
                return;
            }

            string[] tokens = rawTiles.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                string[] coords = token.Split(':');
                if (coords.Length != 2)
                {
                    throw new InvalidDataException($"Invalid locked tile '{token}'.");
                }

                int tx = ParseIntStrict(coords[0], 0, GridPos.GridSize - 1);
                int ty = ParseIntStrict(coords[1], 0, GridPos.GridSize - 1);
                destination.Add(new GridPos(tx, ty));
            }
        }

        private static void ParseHazardsInto(List<string> lines, List<GroundHazard> destination)
        {
            foreach (string line in lines)
            {
                string[] parts = line.Split('|');
                if (parts.Length != 7)
                {
                    throw new InvalidDataException("Invalid serialized hazard entry.");
                }

                GroundHazard hazard = new GroundHazard
                {
                    Type = ParseEnumStrict<HazardType>(parts[0]),
                    Position = new GridPos(
                        ParseIntStrict(parts[1], 0, GridPos.GridSize - 1),
                        ParseIntStrict(parts[2], 0, GridPos.GridSize - 1)),
                    RemainingTurns = ParseIntStrict(parts[3], -1, 99),
                    Damage = ParseIntStrict(parts[4], 0, 99),
                    IsArmed = ParseBoolStrict(parts[5]),
                    AppliesStun = ParseBoolStrict(parts[6])
                };

                destination.Add(hazard);
            }
        }

        private static PendingRewardState? ParseRewardSection(List<string> lines, GameDatabase db)
        {
            Dictionary<string, string> kv = ReadKeyValues(lines);
            if (!RequireBool(kv, "HasReward"))
            {
                return null;
            }

            PendingRewardState reward = new PendingRewardState
            {
                Type = ParseEnumStrict<RewardType>(RequireString(kv, "Type")),
                RerollCount = RequireInt(kv, "RerollCount", 0, 99)
            };

            if (kv.TryGetValue("OfferedWeaponIds", out string? offeredRaw))
            {
                AddValidatedWeapons(offeredRaw, reward.OfferedWeaponIds, db);
            }

            if (kv.TryGetValue("SelectedStartingWeaponIds", out string? selectedRaw))
            {
                AddValidatedWeapons(selectedRaw, reward.SelectedStartingWeaponIds, db);
            }

            if (kv.TryGetValue("OfferedUpgrades", out string? upgradesRaw) && !string.IsNullOrWhiteSpace(upgradesRaw))
            {
                foreach (string token in upgradesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    reward.OfferedUpgrades.Add(ParseEnumStrict<UpgradeModifierType>(token));
                }
            }

            return reward;
        }

        private static void AddValidatedWeapons(string raw, List<string> target, GameDatabase db)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (string token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = token.Trim();
                if (!db.Weapons.ContainsKey(trimmed))
                {
                    throw new InvalidDataException($"Unknown reward weapon ID '{trimmed}'.");
                }

                target.Add(trimmed);
            }
        }

        private static List<string> GetRequiredSection(Dictionary<string, List<string>> sections, string name)
        {
            if (!sections.TryGetValue(name, out List<string>? lines))
            {
                throw new InvalidDataException($"Missing required save section '[{name}]'.");
            }

            return lines;
        }

        private static List<string> GetSectionOrEmpty(Dictionary<string, List<string>> sections, string name)
        {
            if (sections.TryGetValue(name, out List<string>? lines))
            {
                return lines;
            }

            return new List<string>();
        }

        private static Dictionary<string, string> ReadKeyValues(IEnumerable<string> lines)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in lines)
            {
                string trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0)
                {
                    throw new InvalidDataException($"Malformed key-value line '{trimmed}'.");
                }

                string key = trimmed.Substring(0, eqIndex).Trim();
                string value = trimmed.Substring(eqIndex + 1).Trim();
                map[key] = value;
            }

            return map;
        }

        private static void ValidateVersion(Dictionary<string, string> kv)
        {
            int version = RequireInt(kv, "VERSION", 1, 100);
            if (version != SaveFormatVersion)
            {
                throw new InvalidDataException($"Unsupported save format version {version}.");
            }
        }

        private static string RequireString(Dictionary<string, string> kv, string key)
        {
            if (!kv.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"Missing required key '{key}'.");
            }

            return value;
        }

        private static int RequireInt(Dictionary<string, string> kv, string key, int min, int max)
        {
            if (!kv.TryGetValue(key, out string? raw))
            {
                throw new InvalidDataException($"Missing required integer key '{key}'.");
            }

            return ParseIntStrict(raw, min, max);
        }

        private static bool RequireBool(Dictionary<string, string> kv, string key)
        {
            if (!kv.TryGetValue(key, out string? raw))
            {
                throw new InvalidDataException($"Missing required boolean key '{key}'.");
            }

            return ParseBoolStrict(raw);
        }

        private static int ParseIntStrict(string raw, int min, int max)
        {
            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new FormatException($"Invalid integer '{raw}'.");
            }

            if (value < min || value > max)
            {
                throw new InvalidDataException($"Integer {value} outside range [{min}..{max}].");
            }

            return value;
        }

        private static bool ParseBoolStrict(string raw)
        {
            if (!bool.TryParse(raw.Trim(), out bool value))
            {
                throw new FormatException($"Invalid boolean '{raw}'.");
            }

            return value;
        }

        private static TEnum ParseEnumStrict<TEnum>(string raw)
            where TEnum : struct, Enum
        {
            string trimmed = raw.Trim();
            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                throw new FormatException($"Numeric value '{trimmed}' is not a valid {typeof(TEnum).Name}.");
            }

            if (!Enum.TryParse(trimmed, true, out TEnum parsed) || !Enum.IsDefined(typeof(TEnum), parsed))
            {
                throw new InvalidDataException($"Invalid {typeof(TEnum).Name} value '{trimmed}'.");
            }

            return parsed;
        }

        private static void AddSplitItems(
            Dictionary<string, string> kv,
            string key,
            HashSet<string> target,
            Func<string, bool> isValid)
        {
            if (!kv.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (string token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = token.Trim();
                if (isValid(trimmed))
                {
                    target.Add(trimmed);
                }
            }
        }

        private static void ParseStringIntMap(string raw, Dictionary<string, int> target)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (string entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2)
                {
                    throw new InvalidDataException($"Invalid progress entry '{entry}'.");
                }

                string key = parts[0].Trim();
                int value = ParseIntStrict(parts[1], 0, 9999999);
                target[key] = value;
            }
        }

        private static void ParseEnemyCountMap(string raw, Dictionary<EnemyType, int> target)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (string entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2)
                {
                    throw new InvalidDataException($"Invalid enemy stat entry '{entry}'.");
                }

                EnemyType type = ParseEnumStrict<EnemyType>(parts[0]);
                int count = ParseIntStrict(parts[1], 0, 9999999);
                target[type] = count;
            }
        }

        private static string SanitizeLine(string text)
        {
            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string SanitizeField(string text)
        {
            return SanitizeLine(text).Replace("|", "/").Replace(";", ",");
        }

        private static string SanitizeValue(string text)
        {
            return SanitizeLine(text).Replace(",", string.Empty).Replace(":", string.Empty);
        }
    }
}