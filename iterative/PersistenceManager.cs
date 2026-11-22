#nullable disable
using System.Diagnostics;
using System.Globalization;

namespace RogueCards
{
    public class PersistenceManager(DataRepository dataRepository)
    {
        private const int SaveFormatVersion = 1;

        private readonly string _savesDirectory = dataRepository.SavesDirectory;
        private readonly string _profileFilePath = Path.Combine(dataRepository.SavesDirectory, "profile.txt");
        private readonly string _activeRunFilePath = dataRepository.ActiveRunFilePath;
        private readonly string _logsDirectory = dataRepository.LogsDirectory;

        public bool HasActiveRunSave() => File.Exists(_activeRunFilePath);

        public string CreateNewCombatLogPath()
        {
            try
            {
                Directory.CreateDirectory(_logsDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to create logs directory: {ex.Message}");
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(_logsDirectory, $"combat_log_{stamp}.txt");
        }

        public static void AppendCombatLogLine(string logFilePath, string entry)
        {
            if (string.IsNullOrWhiteSpace(logFilePath) || string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string timeStamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                File.AppendAllText(logFilePath, $"[{timeStamp}] {entry}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to append combat log: {ex.Message}");
            }
        }

        public PlayerProfile LoadProfile(GameDatabase database, out string warningMessage)
        {
            warningMessage = string.Empty;
            PlayerProfile profile = DataRepository.CreateDefaultProfile(database);

            if (!File.Exists(_profileFilePath))
            {
                SaveProfile(profile);
                return profile;
            }

            try
            {
                foreach (string raw in File.ReadAllLines(_profileFilePath))
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith('#') || !TrySplitKeyValue(line, out string key, out string value))
                    {
                        continue;
                    }

                    ApplyProfileKeyValue(profile, database, key, value);
                }

                if (!profile.UnlockedCharacterIds.Contains(profile.SelectedCharacterId))
                {
                    profile.SelectedCharacterId = "viking";
                }

                return profile;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                warningMessage = $"Profile save was corrupted and reset to defaults ({ex.Message}).";
                PlayerProfile fallback = DataRepository.CreateDefaultProfile(database);
                SaveProfile(fallback);
                return fallback;
            }
        }

        private static void ApplyProfileKeyValue(PlayerProfile profile, GameDatabase database, string key, string value)
        {
            switch (key)
            {
                case "Coins" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int coins):
                    profile.Coins = Math.Max(0, coins);
                    break;
                case "SelectedCharacterId" when !string.IsNullOrWhiteSpace(value):
                    profile.SelectedCharacterId = value;
                    break;
                case "Area2Unlocked" when bool.TryParse(value, out bool area2):
                    profile.Area2Unlocked = area2;
                    break;
                case "UnlockedWeapons":
                    ParseIdList(value, profile.UnlockedWeaponIds, database.Weapons.ContainsKey);
                    break;
                case "UnlockedUpgrades":
                    ParseIdList(value, profile.UnlockedUpgradeIds, database.Upgrades.ContainsKey);
                    break;
                case "UnlockedCharacters":
                    ParseIdList(value, profile.UnlockedCharacterIds, database.Characters.ContainsKey);
                    break;
                case "CompletedAchievements":
                    ParseIdList(value, profile.CompletedAchievementIds, database.Achievements.ContainsKey);
                    break;
                case "AchievementProgress":
                    ParseProgressDictionary(value, profile.AchievementProgress);
                    break;
            }
        }

        public void SaveProfile(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_savesDirectory);

                List<string> lines =
                [
                    "# Rogue Cards - Meta-Progression Profile",
                    $"FORMAT_VERSION={SaveFormatVersion}",
                    $"Coins={profile.Coins.ToString(CultureInfo.InvariantCulture)}",
                    $"SelectedCharacterId={SanitizeValue(profile.SelectedCharacterId)}",
                    $"Area2Unlocked={profile.Area2Unlocked.ToString().ToLowerInvariant()}",
                    $"UnlockedWeapons={string.Join(",", profile.UnlockedWeaponIds.OrderBy(x => x))}",
                    $"UnlockedUpgrades={string.Join(",", profile.UnlockedUpgradeIds.OrderBy(x => x))}",
                    $"UnlockedCharacters={string.Join(",", profile.UnlockedCharacterIds.OrderBy(x => x))}",
                    $"CompletedAchievements={string.Join(",", profile.CompletedAchievementIds.OrderBy(x => x))}",
                    $"AchievementProgress={SerializeProgressDictionary(profile.AchievementProgress)}"
                ];

                File.WriteAllLines(_profileFilePath, lines);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to save profile: {ex.Message}");
            }
        }

        public bool SaveActiveRun(ActiveRunState run)
        {
            if (run?.Player == null || run.IsGameOver)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(_savesDirectory);
                List<string> lines =
                [
                    "# Rogue Cards - Active Run Save File",
                    $"FORMAT_VERSION={SaveFormatVersion}"
                ];

                AppendRunSection(lines, run);
                AppendPlayerSection(lines, run.Player);
                AppendEntitiesAndHazards(lines, run);
                AppendRewardAndLogSections(lines, run);

                File.WriteAllLines(_activeRunFilePath, lines);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to save active run: {ex.Message}");
                return false;
            }
        }

        private static void AppendRunSection(List<string> lines, ActiveRunState run)
        {
            lines.Add("[RUN]");
            lines.Add($"CurrentWave={run.CurrentWave.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"TurnNumber={run.TurnNumber.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"IsStartingDraft={run.IsStartingDraft.ToString().ToLowerInvariant()}");
            lines.Add($"UsedRerollInRun={run.UsedRerollInRun.ToString().ToLowerInvariant()}");
            lines.Add($"TookDamageInCurrentWave={run.TookDamageInCurrentWave.ToString().ToLowerInvariant()}");
            lines.Add($"NextEnemyInstanceId={run.NextEnemyInstanceId.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"NextCardInstanceId={run.NextCardInstanceId.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"CombatLogFilePath={SanitizeValue(run.CombatLogFilePath)}");
        }

        private static void AppendPlayerSection(List<string> lines, PlayerState p)
        {
            lines.Add("[PLAYER]");
            lines.Add($"CharacterId={SanitizeValue(p.CharacterId)}");
            lines.Add($"PosX={p.Position.X.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"PosY={p.Position.Y.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"CurrentHp={p.CurrentHp.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"MaxHp={p.MaxHp.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"SpecialCooldown={p.SpecialCooldown.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"SpecialMaxCooldown={p.SpecialMaxCooldown.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"IsImmuneThisTurn={p.IsImmuneThisTurn.ToString().ToLowerInvariant()}");
            lines.Add($"FreeRerollsRemaining={p.FreeRerollsRemaining.ToString(CultureInfo.InvariantCulture)}");

            lines.Add("[HAND]");
            lines.AddRange(p.Hand.Select(SerializeCard));

            lines.Add("[EQUIPPED]");
            lines.AddRange(p.EquippedQueue.Select(SerializeCard));
        }

        private static void AppendEntitiesAndHazards(List<string> lines, ActiveRunState run)
        {
            lines.Add("[ENEMIES]");
            lines.AddRange(run.Enemies.Where(e => e.CurrentHp > 0).Select(SerializeEnemy));

            lines.Add("[HAZARDS]");
            lines.AddRange(run.Hazards.Select(SerializeHazard));
        }

        private static void AppendRewardAndLogSections(List<string> lines, ActiveRunState run)
        {
            if (run.PendingReward != null)
            {
                PendingRewardState r = run.PendingReward;
                lines.Add("[REWARD]");
                lines.Add($"RewardType={r.RewardType}");
                lines.Add($"RerollCount={r.RerollCount.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"NextRerollCost={r.NextRerollCost.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"DraftCardIds={string.Join(",", r.DraftCardIds)}");
                lines.Add($"DraftSelectedIds={string.Join(",", r.DraftSelectedIds)}");
                lines.Add($"UpgradeOptions={string.Join(",", r.UpgradeOptions.Select(o => o.ModifierType.ToString()))}");
            }

            lines.Add("[RECENT_LOG]");
            int startIdx = Math.Max(0, run.RecentLogEntries.Count - 8);
            for (int i = startIdx; i < run.RecentLogEntries.Count; i++)
            {
                lines.Add(SanitizeLogLine(run.RecentLogEntries[i]));
            }
        }

        public bool LoadActiveRun(GameDatabase database, out ActiveRunState loadedRun, out string errorMessage)
        {
            loadedRun = null;
            errorMessage = string.Empty;

            if (!File.Exists(_activeRunFilePath))
            {
                errorMessage = "No active run save file found.";
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(_activeRunFilePath);
                return ParseActiveRunLines(lines, database, out loadedRun, out errorMessage);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                errorMessage = $"Corrupted save file ({ex.Message}).";
                return false;
            }
        }

        private static bool ParseActiveRunLines(
            string[] lines,
            GameDatabase database,
            out ActiveRunState loadedRun,
            out string errorMessage)
        {
            loadedRun = null;
            errorMessage = string.Empty;

            int formatVersion = 0;
            string currentSection = string.Empty;
            ActiveRunState run = new();
            PendingRewardState pendingReward = null;
            bool hasRunSection = false;
            bool hasPlayerSection = false;

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    currentSection = line;
                    hasRunSection |= currentSection == "[RUN]";
                    hasPlayerSection |= currentSection == "[PLAYER]";
                    if (currentSection == "[REWARD]")
                    {
                        pendingReward = new PendingRewardState();
                    }

                    continue;
                }

                if (string.IsNullOrEmpty(currentSection))
                {
                    if (TrySplitKeyValue(line, out string rootKey, out string rootVal) && rootKey == "FORMAT_VERSION")
                    {
                        int.TryParse(rootVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out formatVersion);
                    }

                    continue;
                }

                ParseActiveRunSectionLine(currentSection, line, run, ref pendingReward, database);
            }

            return FinalizeLoadedRun(run, pendingReward, formatVersion, hasRunSection, hasPlayerSection, out loadedRun, out errorMessage);
        }

        private static void ParseActiveRunSectionLine(
            string section,
            string line,
            ActiveRunState run,
            ref PendingRewardState pendingReward,
            GameDatabase database)
        {
            switch (section)
            {
                case "[RUN]":
                    ParseRunKeyValue(run, line);
                    break;
                case "[PLAYER]":
                    ParsePlayerKeyValue(run.Player, line, database);
                    break;
                case "[HAND]":
                    run.Player.Hand.Add(DeserializeCard(line, database));
                    break;
                case "[EQUIPPED]":
                    run.Player.EquippedQueue.Add(DeserializeCard(line, database));
                    break;
                case "[ENEMIES]":
                    run.Enemies.Add(DeserializeEnemy(line));
                    break;
                case "[HAZARDS]":
                    run.Hazards.Add(DeserializeHazard(line));
                    break;
                case "[REWARD]" when pendingReward != null:
                    ParseRewardKeyValue(pendingReward, line, database);
                    break;
                case "[RECENT_LOG]":
                    run.RecentLogEntries.Add(line);
                    break;
            }
        }

        private static void ParseRunKeyValue(ActiveRunState run, string line)
        {
            if (!TrySplitKeyValue(line, out string k, out string v))
            {
                return;
            }

            switch (k)
            {
                case "CurrentWave":
                    run.CurrentWave = ParseRequiredInt(v, 1, 20, "CurrentWave");
                    break;
                case "TurnNumber":
                    run.TurnNumber = ParseRequiredInt(v, 1, 100000, "TurnNumber");
                    break;
                case "IsStartingDraft":
                    run.IsStartingDraft = ParseRequiredBool(v, "IsStartingDraft");
                    break;
                case "UsedRerollInRun":
                    run.UsedRerollInRun = ParseRequiredBool(v, "UsedRerollInRun");
                    break;
                case "TookDamageInCurrentWave":
                    run.TookDamageInCurrentWave = ParseRequiredBool(v, "TookDamageInCurrentWave");
                    break;
                case "NextEnemyInstanceId":
                    run.NextEnemyInstanceId = ParseRequiredInt(v, 1, 1000000, "NextEnemyInstanceId");
                    break;
                case "NextCardInstanceId":
                    run.NextCardInstanceId = ParseRequiredInt(v, 1, 1000000, "NextCardInstanceId");
                    break;
                case "CombatLogFilePath":
                    run.CombatLogFilePath = v;
                    break;
            }
        }

        private static void ParsePlayerKeyValue(PlayerState player, string line, GameDatabase database)
        {
            if (!TrySplitKeyValue(line, out string k, out string v))
            {
                return;
            }

            switch (k)
            {
                case "CharacterId":
                    if (!database.Characters.ContainsKey(v))
                    {
                        throw new FormatException($"Unknown CharacterId '{v}'.");
                    }

                    player.CharacterId = v;
                    break;
                case "PosX":
                    player.Position = new GridPos(ParseRequiredInt(v, 0, GameEngine.GridSize - 1, "PosX"), player.Position.Y);
                    break;
                case "PosY":
                    player.Position = new GridPos(player.Position.X, ParseRequiredInt(v, 0, GameEngine.GridSize - 1, "PosY"));
                    break;
                case "CurrentHp":
                    player.CurrentHp = ParseRequiredInt(v, 1, 100, "CurrentHp");
                    break;
                case "MaxHp":
                    player.MaxHp = ParseRequiredInt(v, 1, 100, "MaxHp");
                    break;
                case "SpecialCooldown":
                    player.SpecialCooldown = ParseRequiredInt(v, 0, 50, "SpecialCooldown");
                    break;
                case "SpecialMaxCooldown":
                    player.SpecialMaxCooldown = ParseRequiredInt(v, 1, 50, "SpecialMaxCooldown");
                    break;
                case "IsImmuneThisTurn":
                    player.IsImmuneThisTurn = ParseRequiredBool(v, "IsImmuneThisTurn");
                    break;
                case "FreeRerollsRemaining":
                    player.FreeRerollsRemaining = ParseRequiredInt(v, 0, 50, "FreeRerollsRemaining");
                    break;
            }
        }

        private static void ParseRewardKeyValue(PendingRewardState reward, string line, GameDatabase database)
        {
            if (!TrySplitKeyValue(line, out string k, out string v))
            {
                return;
            }

            switch (k)
            {
                case "RewardType":
                    if (!Enum.TryParse(v, true, out RewardType rType))
                    {
                        throw new FormatException($"Invalid RewardType '{v}'.");
                    }

                    reward.RewardType = rType;
                    break;
                case "RerollCount":
                    reward.RerollCount = ParseRequiredInt(v, 0, 100, "RerollCount");
                    break;
                case "NextRerollCost":
                    reward.NextRerollCost = ParseRequiredInt(v, 1, 100000, "NextRerollCost");
                    break;
                case "DraftCardIds":
                    reward.DraftCardIds = SplitCsvValidated(v, database.Weapons.ContainsKey);
                    break;
                case "DraftSelectedIds":
                    reward.DraftSelectedIds = SplitCsvValidated(v, database.Weapons.ContainsKey);
                    break;
                case "UpgradeOptions":
                    reward.UpgradeOptions.Clear();
                    foreach (string token in v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Enum.TryParse(token.Trim(), true, out UpgradeModifierType modType))
                        {
                            reward.UpgradeOptions.Add(BuildUpgradeOption(modType));
                        }
                    }

                    break;
            }
        }

        private static bool FinalizeLoadedRun(
            ActiveRunState run,
            PendingRewardState pendingReward,
            int formatVersion,
            bool hasRunSection,
            bool hasPlayerSection,
            out ActiveRunState loadedRun,
            out string errorMessage)
        {
            loadedRun = null;
            errorMessage = string.Empty;

            if (formatVersion != SaveFormatVersion)
            {
                errorMessage = $"Incompatible save version ({formatVersion}).";
                return false;
            }

            if (!hasRunSection || !hasPlayerSection || string.IsNullOrWhiteSpace(run.Player.CharacterId))
            {
                errorMessage = "Save file is missing required [RUN] or [PLAYER] data.";
                return false;
            }

            if (run.Player.CurrentHp > run.Player.MaxHp)
            {
                run.Player.CurrentHp = run.Player.MaxHp;
            }

            run.PendingReward = pendingReward;
            loadedRun = run;
            return true;
        }

        public void ClearActiveRunSave()
        {
            try
            {
                if (File.Exists(_activeRunFilePath))
                {
                    File.Delete(_activeRunFilePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to clear active run save: {ex.Message}");
            }
        }

        private static string SerializeCard(CardInstance card)
        {
            string mods = card.AppliedModifiers.Count > 0
                ? string.Join(";", card.AppliedModifiers.Select(SanitizeValue))
                : "-";

            string[] fields =
            [
                card.InstanceId.ToString(CultureInfo.InvariantCulture),
                SanitizeValue(card.DefinitionId),
                card.CurrentCooldown.ToString(CultureInfo.InvariantCulture),
                card.BonusDamage.ToString(CultureInfo.InvariantCulture),
                card.CooldownModifier.ToString(CultureInfo.InvariantCulture),
                card.HasPoison.ToString().ToLowerInvariant(),
                mods
            ];

            return string.Join("|", fields);
        }

        private static CardInstance DeserializeCard(string line, GameDatabase database)
        {
            string[] parts = line.Split('|');
            if (parts.Length != 7)
            {
                throw new FormatException($"Malformed card record ({parts.Length} fields).");
            }

            string defId = parts[1].Trim();
            if (!database.Weapons.TryGetValue(defId, out WeaponDefinition def))
            {
                throw new FormatException($"Unknown weapon '{defId}'.");
            }

            List<string> modifiers = parts[6] == "-"
                ? []
                : parts[6].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();

            return new CardInstance
            {
                InstanceId = ParseRequiredInt(parts[0], 1, 1000000, "Card.InstanceId"),
                DefinitionId = defId,
                Definition = def,
                CurrentCooldown = ParseRequiredInt(parts[2], 0, 50, "Card.CurrentCooldown"),
                BonusDamage = ParseRequiredInt(parts[3], -20, 50, "Card.BonusDamage"),
                CooldownModifier = ParseRequiredInt(parts[4], -20, 20, "Card.CooldownModifier"),
                HasPoison = ParseRequiredBool(parts[5], "Card.HasPoison"),
                AppliedModifiers = modifiers
            };
        }

        private static string SerializeEnemy(EnemyInstance e)
        {
            string lockedTiles = e.LockedTargetTiles.Count > 0
                ? string.Join(",", e.LockedTargetTiles.Select(t => $"{t.X}:{t.Y}"))
                : "-";

            string[] fields =
            [
                e.InstanceId.ToString(CultureInfo.InvariantCulture),
                e.Type.ToString(),
                SanitizeValue(e.DefinitionId),
                SanitizeValue(e.Name),
                e.Position.X.ToString(CultureInfo.InvariantCulture),
                e.Position.Y.ToString(CultureInfo.InvariantCulture),
                e.CurrentHp.ToString(CultureInfo.InvariantCulture),
                e.MaxHp.ToString(CultureInfo.InvariantCulture),
                e.CoinDrop.ToString(CultureInfo.InvariantCulture),
                e.State.ToString(),
                SanitizeValue(e.EquippedWeaponLabel),
                e.ChargeTurnsRemaining.ToString(CultureInfo.InvariantCulture),
                e.LockedDirection.X.ToString(CultureInfo.InvariantCulture),
                e.LockedDirection.Y.ToString(CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(e.LockedAttackType) ? "-" : SanitizeValue(e.LockedAttackType),
                e.IsStunned.ToString().ToLowerInvariant(),
                e.PoisonTurnsRemaining.ToString(CultureInfo.InvariantCulture),
                e.BossCycleIndex.ToString(CultureInfo.InvariantCulture),
                e.BossSpawnedDuringCharge.ToString().ToLowerInvariant(),
                lockedTiles
            ];

            return string.Join("|", fields);
        }

        private static EnemyInstance DeserializeEnemy(string line)
        {
            string[] parts = line.Split('|');
            if (parts.Length != 20)
            {
                throw new FormatException($"Malformed enemy record ({parts.Length} fields).");
            }

            if (!Enum.TryParse(parts[1], true, out EnemyType type))
            {
                throw new FormatException($"Invalid EnemyType '{parts[1]}'.");
            }

            if (!Enum.TryParse(parts[9], true, out EnemyState state))
            {
                throw new FormatException($"Invalid EnemyState '{parts[9]}'.");
            }

            EnemyInstance enemy = new()
            {
                InstanceId = ParseRequiredInt(parts[0], 1, 1000000, "Enemy.InstanceId"),
                Type = type,
                DefinitionId = parts[2].Trim(),
                Name = parts[3].Trim(),
                Position = new GridPos(
                    ParseRequiredInt(parts[4], 0, GameEngine.GridSize - 1, "Enemy.X"),
                    ParseRequiredInt(parts[5], 0, GameEngine.GridSize - 1, "Enemy.Y")),
                CurrentHp = ParseRequiredInt(parts[6], 1, 500, "Enemy.CurrentHp"),
                MaxHp = ParseRequiredInt(parts[7], 1, 500, "Enemy.MaxHp"),
                CoinDrop = ParseRequiredInt(parts[8], 0, 500, "Enemy.CoinDrop"),
                IsBoss = type == EnemyType.VikingChief,
                State = state,
                EquippedWeaponLabel = parts[10].Trim(),
                ChargeTurnsRemaining = ParseRequiredInt(parts[11], 0, 10, "Enemy.ChargeTurns"),
                LockedDirection = new GridPos(
                    ParseRequiredInt(parts[12], -1, 1, "Enemy.DirX"),
                    ParseRequiredInt(parts[13], -1, 1, "Enemy.DirY")),
                LockedAttackType = parts[14] == "-" ? string.Empty : parts[14].Trim(),
                IsStunned = ParseRequiredBool(parts[15], "Enemy.IsStunned"),
                PoisonTurnsRemaining = ParseRequiredInt(parts[16], 0, 20, "Enemy.PoisonTurns"),
                BossCycleIndex = ParseRequiredInt(parts[17], 0, 2, "Enemy.BossCycleIndex"),
                BossSpawnedDuringCharge = ParseRequiredBool(parts[18], "Enemy.BossSpawned")
            };

            if (parts[19] != "-")
            {
                ParseLockedTargetTiles(parts[19], enemy.LockedTargetTiles);
            }

            return enemy;
        }

        private static void ParseLockedTargetTiles(string rawTiles, List<GridPos> target)
        {
            foreach (string tileToken in rawTiles.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] xy = tileToken.Split(':');
                if (xy.Length == 2)
                {
                    int tx = ParseRequiredInt(xy[0], 0, GameEngine.GridSize - 1, "Tile.X");
                    int ty = ParseRequiredInt(xy[1], 0, GameEngine.GridSize - 1, "Tile.Y");
                    target.Add(new GridPos(tx, ty));
                }
            }
        }

        private static string SerializeHazard(GroundHazard h)
        {
            string[] fields =
            [
                h.Type.ToString(),
                h.Position.X.ToString(CultureInfo.InvariantCulture),
                h.Position.Y.ToString(CultureInfo.InvariantCulture),
                h.RemainingTurns.ToString(CultureInfo.InvariantCulture),
                h.Damage.ToString(CultureInfo.InvariantCulture),
                h.IsArmed.ToString().ToLowerInvariant(),
                h.AppliesStun.ToString().ToLowerInvariant()
            ];

            return string.Join("|", fields);
        }

        private static GroundHazard DeserializeHazard(string line)
        {
            string[] parts = line.Split('|');
            if (parts.Length != 7)
            {
                throw new FormatException($"Malformed hazard record ({parts.Length} fields).");
            }

            if (!Enum.TryParse(parts[0], true, out HazardType type))
            {
                throw new FormatException($"Invalid HazardType '{parts[0]}'.");
            }

            return new GroundHazard
            {
                Type = type,
                Position = new GridPos(
                    ParseRequiredInt(parts[1], 0, GameEngine.GridSize - 1, "Hazard.X"),
                    ParseRequiredInt(parts[2], 0, GameEngine.GridSize - 1, "Hazard.Y")),
                RemainingTurns = ParseRequiredInt(parts[3], -1, 50, "Hazard.RemainingTurns"),
                Damage = ParseRequiredInt(parts[4], 0, 50, "Hazard.Damage"),
                IsArmed = ParseRequiredBool(parts[5], "Hazard.IsArmed"),
                AppliesStun = ParseRequiredBool(parts[6], "Hazard.AppliesStun")
            };
        }

        public static UpgradeOption BuildUpgradeOption(UpgradeModifierType modifierType) => modifierType switch
        {
            UpgradeModifierType.PlusOneDamage => new UpgradeOption
            {
                ModifierType = modifierType,
                Name = "+1 Damage",
                Description = "Permanently adds +1 damage to the chosen card for this run."
            },
            UpgradeModifierType.PlusTwoDamage => new UpgradeOption
            {
                ModifierType = modifierType,
                Name = "+2 Damage",
                Description = "Permanently adds +2 damage to the chosen card for this run."
            },
            UpgradeModifierType.MinusOneCooldown => new UpgradeOption
            {
                ModifierType = modifierType,
                Name = "-1 Cooldown",
                Description = "Reduces the maximum cooldown of the chosen card by 1 turn (min 1)."
            },
            UpgradeModifierType.MinusTwoCooldown => new UpgradeOption
            {
                ModifierType = modifierType,
                Name = "-2 Cooldown",
                Description = "Reduces the maximum cooldown of the chosen card by 2 turns (min 1)."
            },
            _ => new UpgradeOption
            {
                ModifierType = UpgradeModifierType.PoisonEffect,
                Name = "Poison",
                Description = "Adds +1 to max cooldown cost. Hit enemies take 1 poison dmg/turn for 3 turns."
            }
        };

        private static void ParseIdList(string csv, HashSet<string> target, Func<string, bool> validator)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return;
            }

            foreach (string token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string id = token.Trim();
                if (validator(id))
                {
                    target.Add(id);
                }
            }
        }

        private static List<string> SplitCsvValidated(string csv, Func<string, bool> validator)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return [];
            }

            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(validator)
                .ToList();
        }

        private static void ParseProgressDictionary(string raw, Dictionary<string, int> target)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (string pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split(':');
                if (parts.Length == 2 &&
                    int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
                {
                    target[parts[0].Trim()] = Math.Max(0, val);
                }
            }
        }

        private static string SerializeProgressDictionary(Dictionary<string, int> dict)
        {
            if (dict == null || dict.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                ",",
                dict.OrderBy(kv => kv.Key)
                    .Select(kv => $"{SanitizeValue(kv.Key)}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));
        }

        private static bool TrySplitKeyValue(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;
            int idx = line.IndexOf('=');
            if (idx <= 0)
            {
                return false;
            }

            key = line.Substring(0, idx).Trim();
            value = line.Substring(idx + 1).Trim();
            return true;
        }

        private static int ParseRequiredInt(string text, int min, int max, string fieldName)
        {
            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ||
                value < min ||
                value > max)
            {
                throw new FormatException($"Invalid integer for {fieldName}: '{text}'.");
            }

            return value;
        }

        private static bool ParseRequiredBool(string text, string fieldName)
        {
            if (!bool.TryParse(text.Trim(), out bool value))
            {
                throw new FormatException($"Invalid boolean for {fieldName}: '{text}'.");
            }

            return value;
        }

        private static string SanitizeValue(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");

        private static string SanitizeLogLine(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\r", " ").Replace("\n", " ");
    }
}