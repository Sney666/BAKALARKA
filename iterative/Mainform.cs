#nullable disable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace RogueCards
{
    public class MainForm : Form
    {
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;

        private readonly DataRepository dataRepository;
        private readonly PersistenceManager persistenceManager;
        private readonly GameDatabase database;
        private readonly PlayerProfile profile;
        private readonly MenuRenderer menuRenderer;
        private readonly GameEngine gameEngine;
        private readonly ArenaRenderer arenaRenderer;
        private readonly Timer animationTimer;

        private GameScreen currentScreen;
        private Point mouseLocation;
        private string menuStatusMessage;
        private Rectangle backToMenuButtonRect;
        private float specialAnimProgress;
        private float telegraphPulsePhase;
        private int lastSeenSpecialSeqId;

        private GridPos currentAimDirection;
        private bool hasAimDirection;
        private int dragSourceIndex;
        private int dragHoverTargetIndex;

        private int selectedUpgradeModifierIndex;
        private int selectedUpgradeCardInstanceId;

        private readonly Color colorBackground = Color.FromArgb(18, 22, 29);
        private readonly Color colorPanel = Color.FromArgb(28, 34, 45);
        private readonly Color colorPanelBorder = Color.FromArgb(58, 69, 88);
        private readonly Color colorAccent = Color.FromArgb(228, 174, 62);
        private readonly Color colorTextPrimary = Color.FromArgb(236, 240, 246);
        private readonly Color colorTextSecondary = Color.FromArgb(156, 169, 189);

        public MainForm()
        {
            Text = "Rogue Cards";
            ClientSize = new Size(WindowWidth, WindowHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            AutoScroll = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = colorBackground;
            KeyPreview = true;

            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            UpdateStyles();

            dataRepository = new DataRepository();
            database = dataRepository.InitializeAndLoad();
            persistenceManager = new PersistenceManager(dataRepository);
            profile = persistenceManager.LoadProfile(database, out string profileWarning);
            menuRenderer = new MenuRenderer();
            gameEngine = new GameEngine(database, profile, persistenceManager);
            arenaRenderer = new ArenaRenderer();

            animationTimer = new Timer();
            animationTimer.Interval = 40;
            animationTimer.Tick += OnAnimationTick;
            animationTimer.Start();

            currentScreen = GameScreen.Menu;
            backToMenuButtonRect = new Rectangle(28, 18, 180, 38);
            specialAnimProgress = 0f;
            telegraphPulsePhase = 0f;
            lastSeenSpecialSeqId = 0;
            dragSourceIndex = -1;
            dragHoverTargetIndex = -1;
            selectedUpgradeModifierIndex = -1;
            selectedUpgradeCardInstanceId = -1;

            if (!string.IsNullOrEmpty(profileWarning))
            {
                menuStatusMessage = profileWarning;
            }
            else if (dataRepository.ParseErrors.Count > 0)
            {
                menuStatusMessage = $"Warning: {dataRepository.ParseErrors.Count} malformed data line(s) logged to Logs/data_parser_errors.txt.";
            }
            else
            {
                menuStatusMessage = string.Empty;
            }

            MouseMove += OnFormMouseMove;
            MouseDown += OnFormMouseDown;
            MouseUp += OnFormMouseUp;
            KeyDown += OnFormKeyDown;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Up ||
                key == Keys.Down ||
                key == Keys.Left ||
                key == Keys.Right ||
                key == Keys.Space ||
                key == Keys.Enter)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            bool needsRedraw = false;

            if (specialAnimProgress > 0f)
            {
                specialAnimProgress = Math.Max(0f, specialAnimProgress - 0.07f);
                needsRedraw = true;
            }

            if ((currentScreen == GameScreen.Arena || currentScreen == GameScreen.Aiming) &&
                gameEngine.CurrentRun != null &&
                gameEngine.CurrentRun.Enemies.Any(en => en.CurrentHp > 0 && en.State == EnemyState.Charging))
            {
                telegraphPulsePhase += 0.24f;
                if (telegraphPulsePhase > (float)(Math.PI * 2.0))
                {
                    telegraphPulsePhase -= (float)(Math.PI * 2.0);
                }

                needsRedraw = true;
            }

            if (needsRedraw)
            {
                Invalidate();
            }
        }

        private void CheckTriggerSpecialAnimation()
        {
            if (gameEngine.LastSpecialSequenceId != lastSeenSpecialSeqId)
            {
                lastSeenSpecialSeqId = gameEngine.LastSpecialSequenceId;
                specialAnimProgress = 1f;
            }
        }

        private void SetScreen(GameScreen nextScreen)
        {
            if (currentScreen != nextScreen)
            {
                currentScreen = nextScreen;
                dragSourceIndex = -1;
                dragHoverTargetIndex = -1;
                hasAimDirection = false;
                Cursor = Cursors.Default;

                if (nextScreen == GameScreen.RewardUpgrade)
                {
                    selectedUpgradeModifierIndex = -1;
                    selectedUpgradeCardInstanceId = -1;
                }

                Invalidate();
            }
        }

        private void SaveAndReturnToMenu()
        {
            if (gameEngine.CurrentRun != null && !gameEngine.CurrentRun.IsGameOver)
            {
                bool saved = gameEngine.SaveCurrentRun();
                persistenceManager.SaveProfile(profile);
                menuStatusMessage = saved
                    ? "Run saved to Saves/active_run.txt. Click Continue Saved Run to resume."
                    : "Returned to Main Menu.";
            }
            else
            {
                menuStatusMessage = "Returned to Main Menu.";
            }

            SetScreen(GameScreen.Menu);
        }

        private void SyncPostActionScreenState()
        {
            if (gameEngine.CurrentRun == null)
            {
                SetScreen(GameScreen.Menu);
                return;
            }

            if (gameEngine.CurrentRun.IsGameOver)
            {
                SetScreen(GameScreen.GameOver);
                return;
            }

            if (gameEngine.CurrentRun.IsStartingDraft)
            {
                SetScreen(GameScreen.StartingDraft);
                return;
            }

            if (gameEngine.CurrentRun.PendingReward != null)
            {
                if (gameEngine.CurrentRun.PendingReward.RewardType == RewardType.PickCard)
                {
                    SetScreen(GameScreen.RewardCard);
                    return;
                }

                if (gameEngine.CurrentRun.PendingReward.RewardType == RewardType.Upgrade)
                {
                    SetScreen(GameScreen.RewardUpgrade);
                    return;
                }
            }

            if (gameEngine.IsExecutingQueue)
            {
                currentScreen = GameScreen.Aiming;
                hasAimDirection = false;
            }
            else
            {
                currentScreen = GameScreen.Arena;
                hasAimDirection = false;
            }
        }

        private void OnFormMouseMove(object sender, MouseEventArgs e)
        {
            mouseLocation = e.Location;

            if (currentScreen == GameScreen.Menu)
            {
                MenuHitbox hit = menuRenderer.HitTest(mouseLocation);
                Cursor = (hit != null && hit.IsEnabled) ? Cursors.Hand : Cursors.Default;
            }
            else if (currentScreen == GameScreen.Arena)
            {
                if (dragSourceIndex >= 0)
                {
                    dragHoverTargetIndex = arenaRenderer.HitTestEquippedSlotIndex(mouseLocation);
                    Cursor = Cursors.SizeAll;
                }
                else
                {
                    ArenaHitbox hit = arenaRenderer.HitTest(mouseLocation);
                    Cursor = (hit != null && hit.IsEnabled) ? Cursors.Hand : Cursors.Default;
                }
            }
            else if (currentScreen == GameScreen.Aiming)
            {
                UpdateMouseAimPreview();
                ArenaHitbox hit = arenaRenderer.HitTest(mouseLocation);
                Cursor = (hit != null && hit.IsEnabled) ? Cursors.Cross : Cursors.Default;
            }
            else if (currentScreen == GameScreen.StartingDraft ||
                     currentScreen == GameScreen.RewardCard ||
                     currentScreen == GameScreen.RewardUpgrade ||
                     currentScreen == GameScreen.GameOver)
            {
                ArenaHitbox hit = arenaRenderer.HitTest(mouseLocation);
                Cursor = (hit != null && hit.IsEnabled) ? Cursors.Hand : Cursors.Default;
            }
            else
            {
                Cursor = backToMenuButtonRect.Contains(mouseLocation) ? Cursors.Hand : Cursors.Default;
            }

            Invalidate();
        }

        private void UpdateMouseAimPreview()
        {
            CardInstance aimingCard = gameEngine.CurrentAimingCard;
            if (aimingCard == null || gameEngine.CurrentRun == null)
            {
                hasAimDirection = false;
                return;
            }

            if (arenaRenderer.TryGetTileAtPoint(mouseLocation, out GridPos hoverTile))
            {
                if (CardResolver.TryGetDirectionFromHover(
                    gameEngine.CurrentRun.Player.Position,
                    hoverTile,
                    aimingCard.Definition.Targeting,
                    out GridPos dir))
                {
                    currentAimDirection = dir;
                    hasAimDirection = true;
                }
            }
        }

        private void OnFormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && currentScreen == GameScreen.Aiming)
            {
                if (gameEngine.CancelAimingIfPossible())
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

            if (currentScreen == GameScreen.Menu)
            {
                HandleMenuClick(e.Location);
                return;
            }

            if (currentScreen == GameScreen.Arena || currentScreen == GameScreen.Aiming)
            {
                HandleArenaMouseDown(e.Location);
                return;
            }

            if (currentScreen == GameScreen.StartingDraft ||
                currentScreen == GameScreen.RewardCard ||
                currentScreen == GameScreen.RewardUpgrade ||
                currentScreen == GameScreen.GameOver)
            {
                HandleOverlayClick(e.Location);
                return;
            }

            if (backToMenuButtonRect.Contains(e.Location))
            {
                SetScreen(GameScreen.Menu);
            }
        }

        private void OnFormMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (currentScreen == GameScreen.Arena && dragSourceIndex >= 0)
            {
                int dropTarget = arenaRenderer.HitTestEquippedSlotIndex(e.Location);
                if (dropTarget >= 0 && dropTarget != dragSourceIndex)
                {
                    gameEngine.ReorderEquippedCard(dragSourceIndex, dropTarget);
                }

                dragSourceIndex = -1;
                dragHoverTargetIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        private void HandleMenuClick(Point location)
        {
            MenuHitbox hit = menuRenderer.HitTest(location);
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
                    menuStatusMessage = string.Empty;
                    gameEngine.StartNewRun();
                    specialAnimProgress = 0f;
                    lastSeenSpecialSeqId = gameEngine.LastSpecialSequenceId;
                    SetScreen(GameScreen.StartingDraft);
                    break;

                case MenuActionType.ContinueRun:
                    if (persistenceManager.HasActiveRunSave())
                    {
                        if (persistenceManager.LoadActiveRun(database, out ActiveRunState loadedRun, out string loadError))
                        {
                            gameEngine.LoadSavedRun(loadedRun);
                            specialAnimProgress = 0f;
                            lastSeenSpecialSeqId = gameEngine.LastSpecialSequenceId;
                            menuStatusMessage = string.Empty;
                            SyncPostActionScreenState();
                            Invalidate();
                        }
                        else
                        {
                            menuStatusMessage = $"Unable to load saved run: {loadError}";
                            Invalidate();
                        }
                    }
                    else
                    {
                        menuStatusMessage = "No active run save found in Saves/active_run.txt.";
                        Invalidate();
                    }
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
                    menuStatusMessage = profile.Area2Unlocked
                        ? "Area 2 is unlocked and reserved for future expansion!"
                        : "Defeat the Wave 10 Boss (Viking Chief) to unlock Area 2.";
                    Invalidate();
                    break;
            }
        }

        private void HandleArenaMouseDown(Point location)
        {
            ArenaHitbox hit = arenaRenderer.HitTest(location);
            if (hit == null || !hit.IsEnabled)
            {
                return;
            }

            switch (hit.ActionType)
            {
                case ArenaActionType.MoveToTile:
                    if (gameEngine.TryMovePlayerToTile(hit.TilePos))
                    {
                        SyncPostActionScreenState();
                        Invalidate();
                    }
                    break;

                case ArenaActionType.AimTile:
                    CardInstance aimingCard = gameEngine.CurrentAimingCard;
                    if (aimingCard != null &&
                        CardResolver.TryGetDirectionFromHover(
                            gameEngine.CurrentRun.Player.Position,
                            hit.TilePos,
                            aimingCard.Definition.Targeting,
                            out GridPos aimDir))
                    {
                        if (gameEngine.ConfirmAimDirection(aimDir))
                        {
                            SyncPostActionScreenState();
                            Invalidate();
                        }
                    }
                    break;

                case ArenaActionType.EquipHandCard:
                    if (gameEngine.TryEquipCardFromHand(hit.Index))
                    {
                        SyncPostActionScreenState();
                        Invalidate();
                    }
                    break;

                case ArenaActionType.EquippedSlot:
                    if (currentScreen == GameScreen.Arena &&
                        hit.Index >= 0 &&
                        hit.Index < gameEngine.CurrentRun.Player.EquippedQueue.Count)
                    {
                        dragSourceIndex = hit.Index;
                        dragHoverTargetIndex = hit.Index;
                        Cursor = Cursors.SizeAll;
                        Invalidate();
                    }
                    break;

                case ArenaActionType.PlayQueue:
                    if (gameEngine.BeginPlayEquippedQueue())
                    {
                        SyncPostActionScreenState();
                        Invalidate();
                    }
                    break;

                case ArenaActionType.CancelAim:
                    if (gameEngine.CancelAimingIfPossible())
                    {
                        SyncPostActionScreenState();
                        Invalidate();
                    }
                    break;

                case ArenaActionType.WaitTurn:
                    if (gameEngine.WaitTurn())
                    {
                        SyncPostActionScreenState();
                        Invalidate();
                    }
                    break;

                case ArenaActionType.UseSpecial:
                    if (gameEngine.TryUseSpecial())
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

        private void HandleOverlayClick(Point location)
        {
            ArenaHitbox hit = arenaRenderer.HitTest(location);
            if (hit == null || !hit.IsEnabled)
            {
                return;
            }

            switch (hit.ActionType)
            {
                case ArenaActionType.SaveAndExit:
                    SaveAndReturnToMenu();
                    break;

                case ArenaActionType.GameOverMenu:
                    menuStatusMessage = "Returned to Main Menu.";
                    SetScreen(GameScreen.Menu);
                    break;

                case ArenaActionType.GameOverNewRun:
                    gameEngine.StartNewRun();
                    specialAnimProgress = 0f;
                    lastSeenSpecialSeqId = gameEngine.LastSpecialSequenceId;
                    SetScreen(GameScreen.StartingDraft);
                    break;

                case ArenaActionType.DraftToggleCard:
                    if (gameEngine.ToggleStartingDraftSelection(hit.TargetId))
                    {
                        Invalidate();
                    }
                    break;

                case ArenaActionType.DraftConfirm:
                    if (gameEngine.ConfirmStartingDraft())
                    {
                        SyncPostActionScreenState();
                        Invalidate();
                    }
                    break;

                case ArenaActionType.RewardPickCard:
                    if (gameEngine.PickRewardCard(hit.TargetId))
                    {
                        SyncPostActionScreenState();
                        Invalidate();
                    }
                    break;

                case ArenaActionType.RewardSelectModifier:
                    selectedUpgradeModifierIndex = hit.Index;
                    Invalidate();
                    break;

                case ArenaActionType.RewardSelectTargetCard:
                    selectedUpgradeCardInstanceId = hit.Index;
                    Invalidate();
                    break;

                case ArenaActionType.RewardApplyUpgrade:
                    PendingRewardState reward = gameEngine.CurrentRun?.PendingReward;
                    if (reward != null &&
                        selectedUpgradeModifierIndex >= 0 &&
                        selectedUpgradeModifierIndex < reward.UpgradeOptions.Count)
                    {
                        UpgradeModifierType modType = reward.UpgradeOptions[selectedUpgradeModifierIndex].ModifierType;
                        if (gameEngine.ApplyUpgradeModifierToCard(modType, selectedUpgradeCardInstanceId))
                        {
                            SyncPostActionScreenState();
                            Invalidate();
                        }
                    }
                    break;

                case ArenaActionType.RewardReroll:
                    if (gameEngine.TryRerollCurrentReward())
                    {
                        selectedUpgradeModifierIndex = -1;
                        Invalidate();
                    }
                    break;

                case ArenaActionType.RewardSkip:
                    if (gameEngine.SkipCurrentReward())
                    {
                        SyncPostActionScreenState();
                        Invalidate();
                    }
                    break;
            }
        }

        private void HandleSelectCharacter(string characterId)
        {
            if (!database.Characters.TryGetValue(characterId, out CharacterDefinition character))
            {
                return;
            }

            if (!profile.UnlockedCharacterIds.Contains(character.Id))
            {
                menuStatusMessage = $"{character.Name} is locked. Complete the 'Kingslayer' achievement to unlock.";
                Invalidate();
                return;
            }

            profile.SelectedCharacterId = character.Id;
            persistenceManager.SaveProfile(profile);
            menuStatusMessage = $"Selected Hero: {character.Name}.";
            Invalidate();
        }

        private void HandleBuyWeapon(string weaponId)
        {
            if (!database.Weapons.TryGetValue(weaponId, out WeaponDefinition weapon))
            {
                return;
            }

            if (profile.UnlockedWeaponIds.Contains(weapon.Id))
            {
                return;
            }

            if (profile.Coins < weapon.UnlockCost)
            {
                menuStatusMessage = $"Not enough coins to unlock {weapon.Name} (requires {weapon.UnlockCost} Coins).";
                Invalidate();
                return;
            }

            profile.Coins -= weapon.UnlockCost;
            profile.UnlockedWeaponIds.Add(weapon.Id);
            persistenceManager.SaveProfile(profile);
            menuStatusMessage = $"Unlocked weapon '{weapon.Name}' for {weapon.UnlockCost} Coins!";
            Invalidate();
        }

        private void HandleBuyUpgrade(string upgradeId)
        {
            if (!database.Upgrades.TryGetValue(upgradeId, out PermanentUpgradeDefinition upgrade))
            {
                return;
            }

            if (profile.UnlockedUpgradeIds.Contains(upgrade.Id))
            {
                return;
            }

            if (profile.Coins < upgrade.Cost)
            {
                menuStatusMessage = $"Not enough coins to purchase {upgrade.Name} (requires {upgrade.Cost} Coins).";
                Invalidate();
                return;
            }

            profile.Coins -= upgrade.Cost;
            profile.UnlockedUpgradeIds.Add(upgrade.Id);
            persistenceManager.SaveProfile(profile);
            menuStatusMessage = $"Purchased permanent upgrade '{upgrade.Name}' for {upgrade.Cost} Coins!";
            Invalidate();
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (currentScreen == GameScreen.Aiming)
            {
                HandleAimingKeyDown(e.KeyCode);
                return;
            }

            if (e.KeyCode == Keys.Escape && currentScreen != GameScreen.Menu)
            {
                if (currentScreen == GameScreen.Arena ||
                    currentScreen == GameScreen.StartingDraft ||
                    currentScreen == GameScreen.RewardCard ||
                    currentScreen == GameScreen.RewardUpgrade)
                {
                    SaveAndReturnToMenu();
                }
                else
                {
                    SetScreen(GameScreen.Menu);
                }

                return;
            }

            if (currentScreen == GameScreen.Arena)
            {
                HandleArenaKeyDown(e.KeyCode);
            }
        }

        private void HandleArenaKeyDown(Keys key)
        {
            bool acted = false;

            if (key == Keys.W || key == Keys.Up)
            {
                acted = gameEngine.TryMovePlayer(0, -1);
            }
            else if (key == Keys.S || key == Keys.Down)
            {
                acted = gameEngine.TryMovePlayer(0, 1);
            }
            else if (key == Keys.A || key == Keys.Left)
            {
                acted = gameEngine.TryMovePlayer(-1, 0);
            }
            else if (key == Keys.D || key == Keys.Right)
            {
                acted = gameEngine.TryMovePlayer(1, 0);
            }
            else if (key == Keys.Space)
            {
                acted = gameEngine.WaitTurn();
            }
            else if (key == Keys.E)
            {
                acted = gameEngine.TryUseSpecial();
                if (acted)
                {
                    CheckTriggerSpecialAnimation();
                }
            }
            else if (key == Keys.Enter)
            {
                acted = gameEngine.BeginPlayEquippedQueue();
            }

            if (acted)
            {
                SyncPostActionScreenState();
                Invalidate();
            }
            else if (key == Keys.E || key == Keys.Enter)
            {
                Invalidate();
            }
        }

        private void HandleAimingKeyDown(Keys key)
        {
            if (key == Keys.Escape)
            {
                if (gameEngine.CancelAimingIfPossible())
                {
                    SyncPostActionScreenState();
                    Invalidate();
                }
                return;
            }

            CardInstance aimingCard = gameEngine.CurrentAimingCard;
            if (aimingCard == null)
            {
                SyncPostActionScreenState();
                Invalidate();
                return;
            }

            if ((key == Keys.Enter || key == Keys.Space) && hasAimDirection)
            {
                if (gameEngine.ConfirmAimDirection(currentAimDirection))
                {
                    SyncPostActionScreenState();
                    Invalidate();
                }
                return;
            }

            GridPos? pressedDir = null;
            if (aimingCard.Definition.Targeting == TargetingType.Orthogonal)
            {
                if (key == Keys.W || key == Keys.Up)
                {
                    pressedDir = new GridPos(0, -1);
                }
                else if (key == Keys.S || key == Keys.Down)
                {
                    pressedDir = new GridPos(0, 1);
                }
                else if (key == Keys.A || key == Keys.Left)
                {
                    pressedDir = new GridPos(-1, 0);
                }
                else if (key == Keys.D || key == Keys.Right)
                {
                    pressedDir = new GridPos(1, 0);
                }
            }
            else if (aimingCard.Definition.Targeting == TargetingType.Diagonal)
            {
                if (key == Keys.Q)
                {
                    pressedDir = new GridPos(-1, -1);
                }
                else if (key == Keys.E)
                {
                    pressedDir = new GridPos(1, -1);
                }
                else if (key == Keys.Z)
                {
                    pressedDir = new GridPos(-1, 1);
                }
                else if (key == Keys.C)
                {
                    pressedDir = new GridPos(1, 1);
                }
            }

            if (pressedDir.HasValue)
            {
                if (hasAimDirection && currentAimDirection == pressedDir.Value)
                {
                    if (gameEngine.ConfirmAimDirection(currentAimDirection))
                    {
                        SyncPostActionScreenState();
                    }
                }
                else
                {
                    currentAimDirection = pressedDir.Value;
                    hasAimDirection = true;
                }

                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (currentScreen == GameScreen.Menu)
            {
                menuRenderer.Render(
                    g,
                    database,
                    profile,
                    persistenceManager.HasActiveRunSave(),
                    mouseLocation,
                    menuStatusMessage);
                return;
            }

            if (currentScreen == GameScreen.StartingDraft)
            {
                arenaRenderer.RenderStartingDraft(g, gameEngine, profile, mouseLocation);
                return;
            }

            if (currentScreen == GameScreen.RewardCard)
            {
                arenaRenderer.RenderRewardCard(g, gameEngine, profile, mouseLocation);
                return;
            }

            if (currentScreen == GameScreen.RewardUpgrade)
            {
                arenaRenderer.RenderRewardUpgrade(
                    g,
                    gameEngine,
                    profile,
                    mouseLocation,
                    selectedUpgradeModifierIndex,
                    selectedUpgradeCardInstanceId);
                return;
            }

            if (currentScreen == GameScreen.GameOver)
            {
                arenaRenderer.RenderGameOver(g, gameEngine, profile, mouseLocation);
                return;
            }

            if (currentScreen == GameScreen.Arena || currentScreen == GameScreen.Aiming)
            {
                AimPreviewResult preview = (currentScreen == GameScreen.Aiming && hasAimDirection)
                    ? gameEngine.GetAimPreview(currentAimDirection)
                    : null;

                arenaRenderer.RenderArena(
                    g,
                    gameEngine,
                    profile,
                    mouseLocation,
                    specialAnimProgress,
                    telegraphPulsePhase,
                    preview,
                    dragSourceIndex,
                    dragHoverTargetIndex);
                return;
            }

            DrawSecondaryScreenView(g);
        }

        private void DrawSecondaryScreenView(Graphics g)
        {
            Rectangle fullRect = new Rectangle(0, 0, WindowWidth, WindowHeight);
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                fullRect,
                Color.FromArgb(16, 20, 27),
                Color.FromArgb(24, 30, 41),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, fullRect);
            }

            Rectangle headerRect = new Rectangle(0, 0, WindowWidth, 72);
            using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(20, 25, 34)))
            {
                g.FillRectangle(barBrush, headerRect);
            }

            using (Pen borderPen = new Pen(colorPanelBorder, 1.5f))
            {
                g.DrawLine(borderPen, 0, 72, WindowWidth, 72);
            }

            bool hoverBack = backToMenuButtonRect.Contains(mouseLocation);
            DrawRoundedBox(
                g,
                backToMenuButtonRect,
                8,
                hoverBack ? Color.FromArgb(52, 65, 86) : Color.FromArgb(37, 46, 61),
                hoverBack ? colorAccent : colorPanelBorder);

            using (Font btnFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush whiteBrush = new SolidBrush(colorTextPrimary))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("< Back to Menu", btnFont, whiteBrush, backToMenuButtonRect, sf);
            }

            Rectangle stageRect = new Rectangle(28, 92, WindowWidth - 56, WindowHeight - 120);
            DrawRoundedBox(g, stageRect, 12, colorPanel, colorPanelBorder);

            string title = currentScreen == GameScreen.Stats
                ? "ACHIEVEMENTS & LIFETIME STATISTICS"
                : $"ACTIVE VIEW: {currentScreen.ToString().ToUpperInvariant()}";

            string heroName = database.Characters.TryGetValue(profile.SelectedCharacterId, out CharacterDefinition selectedHero)
                ? selectedHero.Name
                : "Viking";

            string subtitle = currentScreen == GameScreen.Stats
                ? $"Loaded {database.AchievementList.Count} Achievements from Data/achievements.txt. Press ESC or click Back to Menu."
                : $"Selected Hero: {heroName} | Unlocked Weapons: {profile.UnlockedWeaponIds.Count} | Press ESC or click Back to Menu.";

            using (Font titleFont = new Font("Segoe UI", 24f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 12f, FontStyle.Regular))
            using (SolidBrush accentBrush = new SolidBrush(colorAccent))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(title, titleFont, accentBrush, new Rectangle(80, 260, WindowWidth - 160, 50), sf);
                g.DrawString(subtitle, subFont, mutedBrush, new Rectangle(120, 325, WindowWidth - 240, 50), sf);
            }
        }

        private static void DrawRoundedBox(Graphics g, Rectangle bounds, int radius, Color fillColor, Color borderColor)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                int diameter = radius * 2;
                path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();

                using (SolidBrush fillBrush = new SolidBrush(fillColor))
                using (Pen borderPen = new Pen(borderColor, 1.5f))
                {
                    g.FillPath(fillBrush, path);
                    g.DrawPath(borderPen, path);
                }
            }
        }
    }
}