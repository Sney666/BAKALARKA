#nullable disable
using System.Drawing.Drawing2D;
using Timer = System.Windows.Forms.Timer;

namespace RogueCards
{
    public sealed class MainForm : Form
    {
        private readonly PersistenceManager _persistenceManager;
        private readonly AchievementManager _achievementManager;
        private readonly GameDatabase _database;
        private readonly PlayerProfile _profile;
        private readonly MenuRenderer _menuRenderer;
        private readonly GameEngine _gameEngine;
        private readonly ArenaRenderer _arenaRenderer;
        private readonly Timer _animationTimer;

        private GameScreen _currentScreen;
        private Point _mouseLocation;
        private string _menuStatusMessage;
        private float _specialAnimProgress;
        private float _telegraphPulsePhase;
        private int _lastSeenSpecialSeqId;

        private GridPos _currentAimDirection;
        private bool _hasAimDirection;
        private int _dragSourceIndex = -1;
        private int _dragHoverTargetIndex = -1;

        private int _selectedUpgradeModifierIndex = -1;
        private int _selectedUpgradeCardInstanceId = -1;

        public MainForm()
        {
            ConfigureWindow();

            DataRepository dataRepository = new();
            _database = dataRepository.InitializeAndLoad();
            _persistenceManager = new PersistenceManager(dataRepository);
            _profile = _persistenceManager.LoadProfile(_database, out string profileWarning);
            _achievementManager = new AchievementManager(dataRepository, _database, _profile, _persistenceManager);
            _menuRenderer = new MenuRenderer();
            _gameEngine = new GameEngine(_database, _profile, _persistenceManager, _achievementManager);
            _arenaRenderer = new ArenaRenderer();

            _animationTimer = new Timer { Interval = 40 };
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();

            _currentScreen = GameScreen.Menu;
            _menuStatusMessage = BuildInitialStatusMessage(profileWarning, dataRepository.ParseErrors.Count);

            MouseMove += OnFormMouseMove;
            MouseDown += OnFormMouseDown;
            MouseUp += OnFormMouseUp;
            KeyDown += OnFormKeyDown;
        }

        private void ConfigureWindow()
        {
            Text = "Rogue Cards";
            ClientSize = new Size(UiTheme.WindowWidth, UiTheme.WindowHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            AutoScroll = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiTheme.Background;
            KeyPreview = true;
            DoubleBuffered = true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            UpdateStyles();
        }

        private static string BuildInitialStatusMessage(string profileWarning, int parseErrorCount)
        {
            if (!string.IsNullOrEmpty(profileWarning))
            {
                return profileWarning;
            }

            return parseErrorCount > 0
                ? $"Warning: {parseErrorCount} malformed data line(s) logged to Logs/data_parser_errors.txt."
                : string.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Space or Keys.Enter ||
                   base.IsInputKey(keyData);
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            bool needsRedraw = false;
            bool hadToast = _achievementManager.ActiveToast != null;

            _achievementManager.UpdateToasts(0.04f);
            if (hadToast || _achievementManager.ActiveToast != null)
            {
                needsRedraw = true;
            }

            if (_specialAnimProgress > 0f)
            {
                _specialAnimProgress = Math.Max(0f, _specialAnimProgress - 0.07f);
                needsRedraw = true;
            }

            if (_currentScreen is GameScreen.Arena or GameScreen.Aiming &&
                _gameEngine.CurrentRun != null &&
                _gameEngine.CurrentRun.Enemies.Any(en => en.CurrentHp > 0 && en.State == EnemyState.Charging))
            {
                _telegraphPulsePhase = (_telegraphPulsePhase + 0.24f) % (float)(Math.PI * 2.0);
                needsRedraw = true;
            }

            if (needsRedraw)
            {
                Invalidate();
            }
        }

        private void CheckTriggerSpecialAnimation()
        {
            if (_gameEngine.LastSpecialSequenceId != _lastSeenSpecialSeqId)
            {
                _lastSeenSpecialSeqId = _gameEngine.LastSpecialSequenceId;
                _specialAnimProgress = 1f;
            }
        }

        private void SetScreen(GameScreen nextScreen)
        {
            if (_currentScreen == nextScreen)
            {
                return;
            }

            _currentScreen = nextScreen;
            _dragSourceIndex = -1;
            _dragHoverTargetIndex = -1;
            _hasAimDirection = false;
            Cursor = Cursors.Default;

            if (nextScreen == GameScreen.RewardUpgrade)
            {
                _selectedUpgradeModifierIndex = -1;
                _selectedUpgradeCardInstanceId = -1;
            }

            Invalidate();
        }

        private void SaveAndReturnToMenu()
        {
            if (_gameEngine.CurrentRun is { IsGameOver: false })
            {
                bool saved = _gameEngine.SaveCurrentRun();
                _persistenceManager.SaveProfile(_profile);
                _menuStatusMessage = saved
                    ? "Run saved to Saves/active_run.txt. Click Continue Saved Run to resume."
                    : "Returned to Main Menu.";
            }
            else
            {
                _menuStatusMessage = "Returned to Main Menu.";
            }

            SetScreen(GameScreen.Menu);
        }

        private void SyncPostActionScreenState()
        {
            ActiveRunState run = _gameEngine.CurrentRun;
            if (run == null)
            {
                SetScreen(GameScreen.Menu);
                return;
            }

            if (run.IsGameOver)
            {
                SetScreen(GameScreen.GameOver);
                return;
            }

            if (run.IsStartingDraft)
            {
                SetScreen(GameScreen.StartingDraft);
                return;
            }

            if (run.PendingReward != null)
            {
                GameScreen rewardScreen = run.PendingReward.RewardType == RewardType.PickCard
                    ? GameScreen.RewardCard
                    : GameScreen.RewardUpgrade;
                SetScreen(rewardScreen);
                return;
            }

            _currentScreen = _gameEngine.IsExecutingQueue ? GameScreen.Aiming : GameScreen.Arena;
            _hasAimDirection = false;
        }

        private void OnFormMouseMove(object sender, MouseEventArgs e)
        {
            _mouseLocation = e.Location;
            UpdateCursorForCurrentScreen();
            Invalidate();
        }

        private void UpdateCursorForCurrentScreen()
        {
            switch (_currentScreen)
            {
                case GameScreen.Menu:
                    Cursor = _menuRenderer.HitTest(_mouseLocation) is { IsEnabled: true } ? Cursors.Hand : Cursors.Default;
                    break;
                case GameScreen.Stats:
                    Cursor = StatsRenderer.IsBackButtonHit(_mouseLocation) ? Cursors.Hand : Cursors.Default;
                    break;
                case GameScreen.Arena when _dragSourceIndex >= 0:
                    _dragHoverTargetIndex = _arenaRenderer.HitTestEquippedSlotIndex(_mouseLocation);
                    Cursor = Cursors.SizeAll;
                    break;
                case GameScreen.Aiming:
                    UpdateMouseAimPreview();
                    Cursor = _arenaRenderer.HitTest(_mouseLocation) is { IsEnabled: true } ? Cursors.Cross : Cursors.Default;
                    break;
                default:
                    Cursor = _arenaRenderer.HitTest(_mouseLocation) is { IsEnabled: true } ? Cursors.Hand : Cursors.Default;
                    break;
            }
        }

        private void UpdateMouseAimPreview()
        {
            CardInstance aimingCard = _gameEngine.CurrentAimingCard;
            if (aimingCard == null || _gameEngine.CurrentRun == null)
            {
                _hasAimDirection = false;
                return;
            }

            if (_arenaRenderer.TryGetTileAtPoint(_mouseLocation, out GridPos hoverTile) &&
                CardResolver.TryGetDirectionFromHover(
                    _gameEngine.CurrentRun.Player.Position,
                    hoverTile,
                    aimingCard.Definition.Targeting,
                    out GridPos dir))
            {
                _currentAimDirection = dir;
                _hasAimDirection = true;
            }
        }

        private void OnFormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _currentScreen == GameScreen.Aiming)
            {
                if (_gameEngine.CancelAimingIfPossible())
                {
                    SyncPostActionScreenState();
                    Invalidate();
                }

                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            switch (_currentScreen)
            {
                case GameScreen.Menu:
                    HandleMenuClick(e.Location);
                    break;
                case GameScreen.Stats when StatsRenderer.IsBackButtonHit(e.Location):
                    SetScreen(GameScreen.Menu);
                    break;
                case GameScreen.Arena or GameScreen.Aiming:
                    HandleArenaMouseDown(e.Location);
                    break;
                default:
                    HandleOverlayClick(e.Location);
                    break;
            }
        }

        private void OnFormMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _currentScreen != GameScreen.Arena || _dragSourceIndex < 0)
            {
                return;
            }

            int dropTarget = _arenaRenderer.HitTestEquippedSlotIndex(e.Location);
            if (dropTarget >= 0 && dropTarget != _dragSourceIndex)
            {
                _gameEngine.ReorderEquippedCard(_dragSourceIndex, dropTarget);
            }

            _dragSourceIndex = -1;
            _dragHoverTargetIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        private void HandleMenuClick(Point location)
        {
            MenuHitbox hit = _menuRenderer.HitTest(location);
            if (hit == null)
            {
                return;
            }

            switch (hit.ActionType)
            {
                case MenuActionType.SelectCharacter:
                    HandleSelectCharacter(hit.TargetId);
                    break;
                case MenuActionType.StartNewRun:
                    StartNewRunAndOpenDraft();
                    break;
                case MenuActionType.ContinueRun:
                    HandleContinueSavedRun();
                    break;
                case MenuActionType.OpenStats:
                    SetScreen(GameScreen.Stats);
                    break;
                case MenuActionType.BuyWeapon:
                    HandleBuyWeapon(hit.TargetId);
                    break;
                case MenuActionType.BuyUpgrade:
                    HandleBuyUpgrade(hit.TargetId);
                    break;
                case MenuActionType.Area2Hook:
                    _menuStatusMessage = _profile.Area2Unlocked
                        ? "Area 2 is unlocked and reserved for future expansion!"
                        : "Defeat the Wave 10 Boss (Viking Chief) to unlock Area 2.";
                    Invalidate();
                    break;
            }
        }

        private void StartNewRunAndOpenDraft()
        {
            _menuStatusMessage = string.Empty;
            _gameEngine.StartNewRun();
            _specialAnimProgress = 0f;
            _lastSeenSpecialSeqId = _gameEngine.LastSpecialSequenceId;
            SetScreen(GameScreen.StartingDraft);
        }

        private void HandleContinueSavedRun()
        {
            if (!_persistenceManager.HasActiveRunSave())
            {
                _menuStatusMessage = "No active run save found in Saves/active_run.txt.";
                Invalidate();
                return;
            }

            if (_persistenceManager.LoadActiveRun(_database, out ActiveRunState loadedRun, out string loadError))
            {
                _gameEngine.LoadSavedRun(loadedRun);
                _specialAnimProgress = 0f;
                _lastSeenSpecialSeqId = _gameEngine.LastSpecialSequenceId;
                _menuStatusMessage = string.Empty;
                SyncPostActionScreenState();
            }
            else
            {
                _menuStatusMessage = $"Unable to load saved run: {loadError}";
            }

            Invalidate();
        }

        private void HandleArenaMouseDown(Point location)
        {
            ArenaHitbox hit = _arenaRenderer.HitTest(location);
            if (hit is not { IsEnabled: true })
            {
                return;
            }

            switch (hit.ActionType)
            {
                case ArenaActionType.MoveToTile:
                    ApplyActionAndSync(_gameEngine.TryMovePlayerToTile(hit.TilePos));
                    break;
                case ArenaActionType.AimTile:
                    HandleAimTileClick(hit.TilePos);
                    break;
                case ArenaActionType.EquipHandCard:
                    ApplyActionAndSync(_gameEngine.TryEquipCardFromHand(hit.Index));
                    break;
                case ArenaActionType.EquippedSlot:
                    BeginSlotDrag(hit.Index);
                    break;
                case ArenaActionType.PlayQueue:
                    ApplyActionAndSync(_gameEngine.BeginPlayEquippedQueue());
                    break;
                case ArenaActionType.CancelAim:
                    ApplyActionAndSync(_gameEngine.CancelAimingIfPossible());
                    break;
                case ArenaActionType.WaitTurn:
                    ApplyActionAndSync(_gameEngine.WaitTurn());
                    break;
                case ArenaActionType.UseSpecial:
                    if (_gameEngine.TryUseSpecial())
                    {
                        CheckTriggerSpecialAnimation();
                        SyncPostActionScreenState();
                    }

                    Invalidate();
                    break;
                case ArenaActionType.SaveAndExit:
                    SaveAndReturnToMenu();
                    break;
            }
        }

        private void HandleAimTileClick(GridPos tilePos)
        {
            CardInstance aimingCard = _gameEngine.CurrentAimingCard;
            if (aimingCard != null &&
                CardResolver.TryGetDirectionFromHover(
                    _gameEngine.CurrentRun.Player.Position,
                    tilePos,
                    aimingCard.Definition.Targeting,
                    out GridPos aimDir))
            {
                ApplyActionAndSync(_gameEngine.ConfirmAimDirection(aimDir));
            }
        }

        private void BeginSlotDrag(int slotIndex)
        {
            if (_currentScreen != GameScreen.Arena ||
                slotIndex < 0 ||
                slotIndex >= _gameEngine.CurrentRun.Player.EquippedQueue.Count)
            {
                return;
            }

            _dragSourceIndex = slotIndex;
            _dragHoverTargetIndex = slotIndex;
            Cursor = Cursors.SizeAll;
            Invalidate();
        }

        private void HandleOverlayClick(Point location)
        {
            ArenaHitbox hit = _arenaRenderer.HitTest(location);
            if (hit is not { IsEnabled: true })
            {
                return;
            }

            switch (hit.ActionType)
            {
                case ArenaActionType.SaveAndExit:
                    SaveAndReturnToMenu();
                    break;
                case ArenaActionType.GameOverMenu:
                    _menuStatusMessage = "Returned to Main Menu.";
                    SetScreen(GameScreen.Menu);
                    break;
                case ArenaActionType.GameOverNewRun:
                    StartNewRunAndOpenDraft();
                    break;
                case ArenaActionType.DraftToggleCard when _gameEngine.ToggleStartingDraftSelection(hit.TargetId):
                    Invalidate();
                    break;
                case ArenaActionType.DraftConfirm:
                    ApplyActionAndSync(_gameEngine.ConfirmStartingDraft());
                    break;
                case ArenaActionType.RewardPickCard:
                    ApplyActionAndSync(_gameEngine.PickRewardCard(hit.TargetId));
                    break;
                case ArenaActionType.RewardSelectModifier:
                    _selectedUpgradeModifierIndex = hit.Index;
                    Invalidate();
                    break;
                case ArenaActionType.RewardSelectTargetCard:
                    _selectedUpgradeCardInstanceId = hit.Index;
                    Invalidate();
                    break;
                case ArenaActionType.RewardApplyUpgrade:
                    HandleApplySelectedUpgrade();
                    break;
                case ArenaActionType.RewardReroll when _gameEngine.TryRerollCurrentReward():
                    _selectedUpgradeModifierIndex = -1;
                    Invalidate();
                    break;
                case ArenaActionType.RewardSkip:
                    ApplyActionAndSync(_gameEngine.SkipCurrentReward());
                    break;
            }
        }

        private void HandleApplySelectedUpgrade()
        {
            PendingRewardState reward = _gameEngine.CurrentRun?.PendingReward;
            if (reward != null &&
                _selectedUpgradeModifierIndex >= 0 &&
                _selectedUpgradeModifierIndex < reward.UpgradeOptions.Count)
            {
                UpgradeModifierType modType = reward.UpgradeOptions[_selectedUpgradeModifierIndex].ModifierType;
                ApplyActionAndSync(_gameEngine.ApplyUpgradeModifierToCard(modType, _selectedUpgradeCardInstanceId));
            }
        }

        private void ApplyActionAndSync(bool acted)
        {
            if (acted)
            {
                SyncPostActionScreenState();
                Invalidate();
            }
        }

        private void HandleSelectCharacter(string characterId)
        {
            if (!_database.Characters.TryGetValue(characterId, out CharacterDefinition character))
            {
                return;
            }

            if (!_profile.UnlockedCharacterIds.Contains(character.Id))
            {
                _menuStatusMessage = $"{character.Name} is locked. Complete the 'Kingslayer' achievement to unlock.";
                Invalidate();
                return;
            }

            _profile.SelectedCharacterId = character.Id;
            _persistenceManager.SaveProfile(_profile);
            _menuStatusMessage = $"Selected Hero: {character.Name}.";
            Invalidate();
        }

        private void HandleBuyWeapon(string weaponId)
        {
            if (!_database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon) ||
                _profile.UnlockedWeaponIds.Contains(weapon.Id))
            {
                return;
            }

            if (_profile.Coins < weapon.UnlockCost)
            {
                _menuStatusMessage = $"Not enough coins to unlock {weapon.Name} (requires {weapon.UnlockCost} Coins).";
                Invalidate();
                return;
            }

            _profile.Coins -= weapon.UnlockCost;
            _profile.UnlockedWeaponIds.Add(weapon.Id);
            _persistenceManager.SaveProfile(_profile);
            _achievementManager.EvaluateWellEquipped();
            _menuStatusMessage = $"Unlocked weapon '{weapon.Name}' for {weapon.UnlockCost} Coins!";
            Invalidate();
        }

        private void HandleBuyUpgrade(string upgradeId)
        {
            if (!_database.Upgrades.TryGetValue(upgradeId, out PermanentUpgradeDefinition upgrade) ||
                _profile.UnlockedUpgradeIds.Contains(upgrade.Id))
            {
                return;
            }

            if (_profile.Coins < upgrade.Cost)
            {
                _menuStatusMessage = $"Not enough coins to purchase {upgrade.Name} (requires {upgrade.Cost} Coins).";
                Invalidate();
                return;
            }

            _profile.Coins -= upgrade.Cost;
            _profile.UnlockedUpgradeIds.Add(upgrade.Id);
            _persistenceManager.SaveProfile(_profile);
            _achievementManager.EvaluateWellEquipped();
            _menuStatusMessage = $"Purchased permanent upgrade '{upgrade.Name}' for {upgrade.Cost} Coins!";
            Invalidate();
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (_currentScreen == GameScreen.Aiming)
            {
                HandleAimingKeyDown(e.KeyCode);
                return;
            }

            if (e.KeyCode == Keys.Escape && _currentScreen != GameScreen.Menu)
            {
                if (_currentScreen is GameScreen.Arena or GameScreen.StartingDraft or GameScreen.RewardCard or GameScreen.RewardUpgrade)
                {
                    SaveAndReturnToMenu();
                }
                else
                {
                    SetScreen(GameScreen.Menu);
                }

                return;
            }

            if (_currentScreen == GameScreen.Arena)
            {
                HandleArenaKeyDown(e.KeyCode);
            }
        }

        private void HandleArenaKeyDown(Keys key)
        {
            bool acted = key switch
            {
                Keys.W or Keys.Up => _gameEngine.TryMovePlayer(0, -1),
                Keys.S or Keys.Down => _gameEngine.TryMovePlayer(0, 1),
                Keys.A or Keys.Left => _gameEngine.TryMovePlayer(-1, 0),
                Keys.D or Keys.Right => _gameEngine.TryMovePlayer(1, 0),
                Keys.Space => _gameEngine.WaitTurn(),
                Keys.E => TryUseSpecialWithAnim(),
                Keys.Enter => _gameEngine.BeginPlayEquippedQueue(),
                _ => false
            };

            if (acted)
            {
                SyncPostActionScreenState();
                Invalidate();
            }
            else if (key is Keys.E or Keys.Enter)
            {
                Invalidate();
            }
        }

        private bool TryUseSpecialWithAnim()
        {
            bool acted = _gameEngine.TryUseSpecial();
            if (acted)
            {
                CheckTriggerSpecialAnimation();
            }

            return acted;
        }

        private void HandleAimingKeyDown(Keys key)
        {
            if (key == Keys.Escape)
            {
                ApplyActionAndSync(_gameEngine.CancelAimingIfPossible());
                return;
            }

            CardInstance aimingCard = _gameEngine.CurrentAimingCard;
            if (aimingCard == null)
            {
                SyncPostActionScreenState();
                Invalidate();
                return;
            }

            if (key is Keys.Enter or Keys.Space && _hasAimDirection)
            {
                ApplyActionAndSync(_gameEngine.ConfirmAimDirection(_currentAimDirection));
                return;
            }

            GridPos? pressedDir = MapAimKeyToDirection(key, aimingCard.Definition.Targeting);
            if (!pressedDir.HasValue)
            {
                return;
            }

            if (_hasAimDirection && _currentAimDirection == pressedDir.Value)
            {
                if (_gameEngine.ConfirmAimDirection(_currentAimDirection))
                {
                    SyncPostActionScreenState();
                }
            }
            else
            {
                _currentAimDirection = pressedDir.Value;
                _hasAimDirection = true;
            }

            Invalidate();
        }

        private static GridPos? MapAimKeyToDirection(Keys key, TargetingType targeting)
        {
            if (targeting == TargetingType.Orthogonal)
            {
                return key switch
                {
                    Keys.W or Keys.Up => new GridPos(0, -1),
                    Keys.S or Keys.Down => new GridPos(0, 1),
                    Keys.A or Keys.Left => new GridPos(-1, 0),
                    Keys.D or Keys.Right => new GridPos(1, 0),
                    _ => null
                };
            }

            if (targeting == TargetingType.Diagonal)
            {
                return key switch
                {
                    Keys.Q => new GridPos(-1, -1),
                    Keys.E => new GridPos(1, -1),
                    Keys.Z => new GridPos(-1, 1),
                    Keys.C => new GridPos(1, 1),
                    _ => null
                };
            }

            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            RenderActiveScreen(g);

            if (_achievementManager.ActiveToast != null)
            {
                DrawAchievementToast(g, _achievementManager.ActiveToast);
            }
        }

        private void RenderActiveScreen(Graphics g)
        {
            switch (_currentScreen)
            {
                case GameScreen.Menu:
                    _menuRenderer.Render(g, _database, _profile, _persistenceManager.HasActiveRunSave(), _mouseLocation, _menuStatusMessage);
                    break;
                case GameScreen.Stats:
                    StatsRenderer.Render(g, _database, _profile, _achievementManager, _mouseLocation);
                    break;
                case GameScreen.StartingDraft:
                    _arenaRenderer.RenderStartingDraft(g, _gameEngine, _profile, _mouseLocation);
                    break;
                case GameScreen.RewardCard:
                    _arenaRenderer.RenderRewardCard(g, _gameEngine, _profile, _mouseLocation);
                    break;
                case GameScreen.RewardUpgrade:
                    _arenaRenderer.RenderRewardUpgrade(g, _gameEngine, _profile, _mouseLocation, _selectedUpgradeModifierIndex, _selectedUpgradeCardInstanceId);
                    break;
                case GameScreen.GameOver:
                    _arenaRenderer.RenderGameOver(g, _gameEngine, _profile, _mouseLocation);
                    break;
                default:
                    AimPreviewResult preview = _currentScreen == GameScreen.Aiming && _hasAimDirection
                        ? _gameEngine.GetAimPreview(_currentAimDirection)
                        : null;
                    _arenaRenderer.RenderArena(g, _gameEngine, _profile, _mouseLocation, _specialAnimProgress, _telegraphPulsePhase, preview, _dragSourceIndex, _dragHoverTargetIndex);
                    break;
            }
        }

        private static void DrawAchievementToast(Graphics g, AchievementToast toast)
        {
            Rectangle toastRect = new((UiTheme.WindowWidth / 2) - 270, 12, 540, 58);
            Rectangle shadowRect = toastRect with { X = toastRect.X + 4, Y = toastRect.Y + 4 };

            UiHelpers.DrawRoundedBox(g, shadowRect, 10, Color.FromArgb(145, 0, 0, 0), Color.Transparent, 0f);
            UiHelpers.DrawRoundedBox(g, toastRect, 10, Color.FromArgb(242, 26, 38, 34), UiTheme.Gold, 2f);

            using Font titleFont = new("Segoe UI", 10.5f, FontStyle.Bold);
            using Font subFont = new("Segoe UI", 9.2f, FontStyle.Bold);
            using SolidBrush goldBrush = new(UiTheme.Gold);
            using SolidBrush whiteBrush = new(UiTheme.TextPrimary);
            using StringFormat sf = UiHelpers.CreateStringFormat();

            g.DrawString(toast.Title, titleFont, goldBrush, new Rectangle(toastRect.X + 12, toastRect.Y + 6, toastRect.Width - 24, 24), sf);
            g.DrawString(toast.Subtitle, subFont, whiteBrush, new Rectangle(toastRect.X + 12, toastRect.Y + 30, toastRect.Width - 24, 22), sf);
        }
    }
}