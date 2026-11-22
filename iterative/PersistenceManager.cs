#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RogueCards
{
    public class PersistenceManager
    {
        private const int SaveFormatVersion = 1;

        private readonly DataRepository dataRepository;
        public string ProfileFilePath { get; }
        public string ActiveRunFilePath { get; }
        public string LogsDirectory { get; }

        public PersistenceManager(DataRepository dataRepository)
        {
            this.dataRepository = dataRepository;
            ProfileFilePath = Path.Combine(dataRepository.SavesDirectory, "profile.txt");
            ActiveRunFilePath = dataRepository.ActiveRunFilePath;
            LogsDirectory = dataRepository.LogsDirectory;
        }

        public bool HasActiveRunSave()
        {
            return File.Exists(ActiveRunFilePath);
        }

        public string CreateNewCombatLogPath()
        {
            try
            {
                Directory.CreateDirectory(LogsDirectory);
            }
            catch
            {
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(LogsDirectory, $"combat_log_{stamp}.txt");
        }

        public void AppendCombatLogLine(string logFilePath, string entry)
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
            catch
            {
            }
        }

        public PlayerProfile LoadProfile(GameDatabase database, out string warningMessage)
        {
            warningMessage = string.Empty;
            PlayerProfile profile = dataRepository.CreateDefaultProfile(database);

            if (!File.Exists(ProfileFilePath))
            {
                SaveProfile(profile);
                return profile;
            }

            try
            {
                string[] lines = File.ReadAllLines(ProfileFilePath);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    int sepIndex = line.IndexOf('=');
                    if (sepIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, sepIndex).Trim();
                    string value = line.Substring(sepIndex + 1).Trim();

                    if (key == "Coins" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int coins))
                    {
                        profile.Coins = Math.Max(0, coins);
                    }
                    else if (key == "SelectedCharacterId" && !string.IsNullOrWhiteSpace(value))
                    {
                        profile.SelectedCharacterId = value;
                    }
                    else if (key == "Area2Unlocked" && bool.TryParse(value, out bool area2))
                    {
                        profile.Area2Unlocked = area2;
                    }
                    else if (key == "UnlockedWeapons")
                    {
                        ParseIdList(value, profile.UnlockedWeaponIds, id => database.Weapons.ContainsKey(id));
                    }
                    else if (key == "UnlockedUpgrades")
                    {
                        ParseIdList(value, profile.UnlockedUpgradeIds, id => database.Upgrades.ContainsKey(id));
                    }
                    else if (key == "UnlockedCharacters")
                    {
                        ParseIdList(value, profile.UnlockedCharacterIds, id => database.Characters.ContainsKey(id));
                    }
                    else if (key == "CompletedAchievements")
                    {
                        ParseIdList(value, profile.CompletedAchievementIds, id => database.Achievements.ContainsKey(id));
                    }
                    else if (key == "AchievementProgress")
                    {
                        ParseProgressDictionary(value, profile.AchievementProgress);
                    }
                }

                if (!profile.UnlockedCharacterIds.Contains(profile.SelectedCharacterId))
                {
                    profile.SelectedCharacterId = "viking";
                }

                return profile;
            }
            catch (Exception ex)
            {
                warningMessage = $"Profile save was corrupted and reset to defaults ({ex.Message}).";
                PlayerProfile fallback = dataRepository.CreateDefaultProfile(database);
                SaveProfile(fallback);
                return fallback;
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
                Directory.CreateDirectory(dataRepository.SavesDirectory);

                List<string> lines = new List<string>
                {
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
                };

                File.WriteAllLines(ProfileFilePath, lines);
            }
            catch
            {
            }
        }

        public bool SaveActiveRun(ActiveRunState run)
        {
            if (run == null || run.Player == null || run.IsGameOver)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(dataRepository.SavesDirectory);
                List<string> lines = new List<string>();

                lines.Add("# Rogue Cards - Active Run Save File");
                lines.Add($"FORMAT_VERSION={SaveFormatVersion}");

                lines.Add("[RUN]");
                lines.Add($"CurrentWave={run.CurrentWave.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"TurnNumber={run.TurnNumber.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"IsStartingDraft={run.IsStartingDraft.ToString().ToLowerInvariant()}");
                lines.Add($"UsedRerollInRun={run.UsedRerollInRun.ToString().ToLowerInvariant()}");
                lines.Add($"TookDamageInCurrentWave={run.TookDamageInCurrentWave.ToString().ToLowerInvariant()}");
                lines.Add($"NextEnemyInstanceId={run.NextEnemyInstanceId.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"NextCardInstanceId={run.NextCardInstanceId.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"CombatLogFilePath={SanitizeValue(run.CombatLogFilePath)}");

                PlayerState p = run.Player;
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
                foreach (CardInstance card in p.Hand)
                {
                    lines.Add(SerializeCard(card));
                }

                lines.Add("[EQUIPPED]");
                foreach (CardInstance card in p.EquippedQueue)
                {
                    lines.Add(SerializeCard(card));
                }

                lines.Add("[ENEMIES]");
                foreach (EnemyInstance enemy in run.Enemies)
                {
                    if (enemy.CurrentHp > 0)
                    {
                        lines.Add(SerializeEnemy(enemy));
                    }
                }

                lines.Add("[HAZARDS]");
                foreach (GroundHazard hazard in run.Hazards)
                {
                    lines.Add(SerializeHazard(hazard));
                }

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

                File.WriteAllLines(ActiveRunFilePath, lines);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool LoadActiveRun(GameDatabase database, out ActiveRunState loadedRun, out string errorMessage)
        {
            loadedRun = null;
            errorMessage = string.Empty;

            if (!File.Exists(ActiveRunFilePath))
            {
                errorMessage = "No active run save file found.";
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(ActiveRunFilePath);
                int formatVersion = 0;
                string currentSection = string.Empty;

                ActiveRunState run = new ActiveRunState();
                PlayerState player = new PlayerState();
                run.Player = player;
                PendingRewardState pendingReward = null;

                bool hasRunSection = false;
                bool hasPlayerSection = false;

                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line;
                        if (currentSection == "[RUN]")
                        {
                            hasRunSection = true;
                        }
                        else if (currentSection == "[PLAYER]")
                        {
                            hasPlayerSection = true;
                        }
                        else if (currentSection == "[REWARD]")
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

                    if (currentSection == "[RUN]")
                    {
                        if (!TrySplitKeyValue(line, out string k, out string v))
                        {
                            continue;
                        }

                        if (k == "CurrentWave")
                        {
                            run.CurrentWave = ParseRequiredInt(v, 1, 20, "CurrentWave");
                        }
                        else if (k == "TurnNumber")
                        {
                            run.TurnNumber = ParseRequiredInt(v, 1, 100000, "TurnNumber");
                        }
                        else if (k == "IsStartingDraft")
                        {
                            run.IsStartingDraft = ParseRequiredBool(v, "IsStartingDraft");
                        }
                        else if (k == "UsedRerollInRun")
                        {
                            run.UsedRerollInRun = ParseRequiredBool(v, "UsedRerollInRun");
                        }
                        else if (k == "TookDamageInCurrentWave")
                        {
                            run.TookDamageInCurrentWave = ParseRequiredBool(v, "TookDamageInCurrentWave");
                        }
                        else if (k == "NextEnemyInstanceId")
                        {
                            run.NextEnemyInstanceId = ParseRequiredInt(v, 1, 1000000, "NextEnemyInstanceId");
                        }
                        else if (k == "NextCardInstanceId")
                        {
                            run.NextCardInstanceId = ParseRequiredInt(v, 1, 1000000, "NextCardInstanceId");
                        }
                        else if (k == "CombatLogFilePath")
                        {
                            run.CombatLogFilePath = v;
                        }
                    }
                    else if (currentSection == "[PLAYER]")
                    {
                        if (!TrySplitKeyValue(line, out string k, out string v))
                        {
                            continue;
                        }

                        if (k == "CharacterId")
                        {
                            if (!database.Characters.ContainsKey(v))
                            {
                                throw new FormatException($"Unknown CharacterId '{v}'.");
                            }

                            player.CharacterId = v;
                        }
                        else if (k == "PosX")
                        {
                            int x = ParseRequiredInt(v, 0, GameEngine.GridSize - 1, "PosX");
                            player.Position = new GridPos(x, player.Position.Y);
                        }
                        else if (k == "PosY")
                        {
                            int y = ParseRequiredInt(v, 0, GameEngine.GridSize - 1, "PosY");
                            player.Position = new GridPos(player.Position.X, y);
                        }
                        else if (k == "CurrentHp")
                        {
                            player.CurrentHp = ParseRequiredInt(v, 1, 100, "CurrentHp");
                        }
                        else if (k == "MaxHp")
                        {
                            player.MaxHp = ParseRequiredInt(v, 1, 100, "MaxHp");
                        }
                        else if (k == "SpecialCooldown")
                        {
                            player.SpecialCooldown = ParseRequiredInt(v, 0, 50, "SpecialCooldown");
                        }
                        else if (k == "SpecialMaxCooldown")
                        {
                            player.SpecialMaxCooldown = ParseRequiredInt(v, 1, 50, "SpecialMaxCooldown");
                        }
                        else if (k == "IsImmuneThisTurn")
                        {
                            player.IsImmuneThisTurn = ParseRequiredBool(v, "IsImmuneThisTurn");
                        }
                        else if (k == "FreeRerollsRemaining")
                        {
                            player.FreeRerollsRemaining = ParseRequiredInt(v, 0, 50, "FreeRerollsRemaining");
                        }
                    }
                    else if (currentSection == "[HAND]")
                    {
                        player.Hand.Add(DeserializeCard(line, database));
                    }
                    else if (currentSection == "[EQUIPPED]")
                    {
                        player.EquippedQueue.Add(DeserializeCard(line, database));
                    }
                    else if (currentSection == "[ENEMIES]")
                    {
                        run.Enemies.Add(DeserializeEnemy(line));
                    }
                    else if (currentSection == "[HAZARDS]")
                    {
                        run.Hazards.Add(DeserializeHazard(line));
                    }
                    else if (currentSection == "[REWARD]" && pendingReward != null)
                    {
                        if (!TrySplitKeyValue(line, out string k, out string v))
                        {
                            continue;
                        }

                        if (k == "RewardType")
                        {
                            if (!Enum.TryParse(v, true, out RewardType rType))
                            {
                                throw new FormatException($"Invalid RewardType '{v}'.");
                            }

                            pendingReward.RewardType = rType;
                        }
                        else if (k == "RerollCount")
                        {
                            pendingReward.RerollCount = ParseRequiredInt(v, 0, 100, "RerollCount");
                        }
                        else if (k == "NextRerollCost")
                        {
                            pendingReward.NextRerollCost = ParseRequiredInt(v, 1, 100000, "NextRerollCost");
                        }
                        else if (k == "DraftCardIds")
                        {
                            pendingReward.DraftCardIds = SplitCsvValidated(v, id => database.Weapons.ContainsKey(id));
                        }
                        else if (k == "DraftSelectedIds")
                        {
                            pendingReward.DraftSelectedIds = SplitCsvValidated(v, id => database.Weapons.ContainsKey(id));
                        }
                        else if (k == "UpgradeOptions")
                        {
                            pendingReward.UpgradeOptions.Clear();
                            foreach (string token in v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (Enum.TryParse(token.Trim(), true, out UpgradeModifierType modType))
                                {
                                    pendingReward.UpgradeOptions.Add(BuildUpgradeOption(modType));
                                }
                            }
                        }
                    }
                    else if (currentSection == "[RECENT_LOG]")
                    {
                        run.RecentLogEntries.Add(line);
                    }
                }

                if (formatVersion != SaveFormatVersion)
                {
                    errorMessage = $"Incompatible save version ({formatVersion}).";
                    return false;
                }

                if (!hasRunSection || !hasPlayerSection || string.IsNullOrWhiteSpace(player.CharacterId))
                {
                    errorMessage = "Save file is missing required [RUN] or [PLAYER] data.";
                    return false;
                }

                if (player.CurrentHp > player.MaxHp)
                {
                    player.CurrentHp = player.MaxHp;
                }

                run.PendingReward = pendingReward;
                loadedRun = run;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Corrupted save file ({ex.Message}).";
                return false;
            }
        }

        public void ClearActiveRunSave()
        {
            try
            {
                if (File.Exists(ActiveRunFilePath))
                {
                    File.Delete(ActiveRunFilePath);
                }
            }
            catch
            {
            }
        }

        private static string SerializeCard(CardInstance card)
        {
            string mods = card.AppliedModifiers.Count > 0
                ? string.Join(";", card.AppliedModifiers.Select(SanitizeValue))
                : "-";

            return string.Join("|", new string[]
            {
                card.InstanceId.ToString(CultureInfo.InvariantCulture),
                SanitizeValue(card.DefinitionId),
                card.CurrentCooldown.ToString(CultureInfo.InvariantCulture),
                card.BonusDamage.ToString(CultureInfo.InvariantCulture),
                card.CooldownModifier.ToString(CultureInfo.InvariantCulture),
                card.HasPoison.ToString().ToLowerInvariant(),
                mods
            });
        }

        private static CardInstance DeserializeCard(string line, GameDatabase database)
        {
            string[] parts = line.Split('|');
            if (parts.Length != 7)
            {
                throw new FormatException($"Malformed card record ({parts.Length} fields).");
            }

            int instanceId = ParseRequiredInt(parts[0], 1, 1000000, "Card.InstanceId");
            string defId = parts[1].Trim();
            if (!database.Weapons.TryGetValue(defId, out WeaponDefinition def))
            {
                throw new FormatException($"Unknown weapon '{defId}'.");
            }

            int currentCd = ParseRequiredInt(parts[2], 0, 50, "Card.CurrentCooldown");
            int bonusDmg = ParseRequiredInt(parts[3], -20, 50, "Card.BonusDamage");
            int cdMod = ParseRequiredInt(parts[4], -20, 20, "Card.CooldownModifier");
            bool hasPoison = ParseRequiredBool(parts[5], "Card.HasPoison");

            List<string> modifiers = new List<string>();
            if (parts[6] != "-")
            {
                foreach (string token in parts[6].Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    modifiers.Add(token.Trim());
                }
            }

            return new CardInstance
            {
                InstanceId = instanceId,
                DefinitionId = defId,
                Definition = def,
                CurrentCooldown = currentCd,
                BonusDamage = bonusDmg,
                CooldownModifier = cdMod,
                HasPoison = hasPoison,
                AppliedModifiers = modifiers
            };
        }

        private static string SerializeEnemy(EnemyInstance e)
        {
            string lockedTiles = e.LockedTargetTiles.Count > 0
                ? string.Join(",", e.LockedTargetTiles.Select(t => $"{t.X}:{t.Y}"))
                : "-";

            return string.Join("|", new string[]
            {
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
            });
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

            EnemyInstance enemy = new EnemyInstance
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
                foreach (string tileToken in parts[19].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] xy = tileToken.Split(':');
                    if (xy.Length == 2)
                    {
                        int tx = ParseRequiredInt(xy[0], 0, GameEngine.GridSize - 1, "Tile.X");
                        int ty = ParseRequiredInt(xy[1], 0, GameEngine.GridSize - 1, "Tile.Y");
                        enemy.LockedTargetTiles.Add(new GridPos(tx, ty));
                    }
                }
            }

            return enemy;
        }

        private static string SerializeHazard(GroundHazard h)
        {
            return string.Join("|", new string[]
            {
                h.Type.ToString(),
                h.Position.X.ToString(CultureInfo.InvariantCulture),
                h.Position.Y.ToString(CultureInfo.InvariantCulture),
                h.RemainingTurns.ToString(CultureInfo.InvariantCulture),
                h.Damage.ToString(CultureInfo.InvariantCulture),
                h.IsArmed.ToString().ToLowerInvariant(),
                h.AppliesStun.ToString().ToLowerInvariant()
            });
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

        public static UpgradeOption BuildUpgradeOption(UpgradeModifierType modifierType)
        {
            switch (modifierType)
            {
                case UpgradeModifierType.PlusOneDamage:
                    return new UpgradeOption
                    {
                        ModifierType = modifierType,
                        Name = "+1 Damage",
                        Description = "Permanently adds +1 damage to the chosen card for this run."
                    };

                case UpgradeModifierType.PlusTwoDamage:
                    return new UpgradeOption
                    {
                        ModifierType = modifierType,
                        Name = "+2 Damage",
                        Description = "Permanently adds +2 damage to the chosen card for this run."
                    };

                case UpgradeModifierType.MinusOneCooldown:
                    return new UpgradeOption
                    {
                        ModifierType = modifierType,
                        Name = "-1 Cooldown",
                        Description = "Reduces the maximum cooldown of the chosen card by 1 turn (min 1)."
                    };

                case UpgradeModifierType.MinusTwoCooldown:
                    return new UpgradeOption
                    {
                        ModifierType = modifierType,
                        Name = "-2 Cooldown",
                        Description = "Reduces the maximum cooldown of the chosen card by 2 turns (min 1)."
                    };

                default:
                    return new UpgradeOption
                    {
                        ModifierType = UpgradeModifierType.PoisonEffect,
                        Name = "Poison",
                        Description = "Adds +1 to max cooldown cost. Hit enemies take 1 poison dmg/turn for 3 turns."
                    };
            }
        }

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
            List<string> list = new List<string>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return list;
            }

            foreach (string token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string id = token.Trim();
                if (validator(id))
                {
                    list.Add(id);
                }
            }

            return list;
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

            return string.Join(",", dict.OrderBy(kv => kv.Key).Select(kv => $"{SanitizeValue(kv.Key)}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));
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

        private static string SanitizeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }

        private static string SanitizeLogLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r", " ").Replace("\n", " ");
        }
    }
}