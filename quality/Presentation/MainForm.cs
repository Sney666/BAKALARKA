using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using RogueCards.Application;
using RogueCards.Domain;
using Timer = System.Windows.Forms.Timer;
namespace RogueCards.Presentation
{
    public sealed class MainForm : Form
    {
        private readonly GameEngine _engine;
        private readonly UiRenderState _renderState;
        private readonly Dictionary<ScreenMode, IScreenRenderer> _renderers;
        private readonly Timer _animationTimer;
        private ScreenMode _currentScreen = ScreenMode.Menu;

        public MainForm(GameEngine engine)
        {
            _engine = engine;
            _renderState = new UiRenderState(engine);
            _renderers = new Dictionary<ScreenMode, IScreenRenderer>
        {
            { ScreenMode.Menu, new MenuScreenRenderer() },
            { ScreenMode.Arena, new ArenaScreenRenderer() },
            { ScreenMode.Stats, new StatsScreenRenderer() },
            { ScreenMode.GameOver, new GameOverScreenRenderer() }
        };

            ConfigureWindow();

            _animationTimer = new Timer { Interval = 33 };
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();
        }

        private void ConfigureWindow()
        {
            Text = "Rogue Cards";
            ClientSize = new Size(UiTheme.WindowWidth, UiTheme.WindowHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScroll = false;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            DoubleBuffered = true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            _renderState.BeginFrame();
            if (_renderers.TryGetValue(_currentScreen, out IScreenRenderer? renderer))
            {
                renderer.Render(g, _renderState);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _renderState.MousePosition = e.Location;

            if (_currentScreen == ScreenMode.Arena)
            {
                UpdateArenaHoverAndPreview(e.Location);
            }

            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _renderState.MousePosition = e.Location;
            UiHitTarget? hit = _renderState.FindHitAt(e.Location);
            if (hit == null)
            {
                return;
            }

            if (hit.Action == "EquippedSlot")
            {
                _renderState.DraggingEquippedIndex = hit.Index;
                _renderState.DragCurrentPoint = e.Location;
                Invalidate();
                return;
            }

            HandleHitAction(hit);
            SyncScreenWithRunState();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_renderState.DraggingEquippedIndex < 0)
            {
                return;
            }

            UiHitTarget? dropTarget = _renderState.FindHitAt(e.Location);
            if (dropTarget != null && dropTarget.Action == "EquippedSlot")
            {
                _engine.ReorderEquippedCard(_renderState.DraggingEquippedIndex, dropTarget.Index);
            }

            _renderState.DraggingEquippedIndex = -1;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_currentScreen != ScreenMode.Arena || _engine.CurrentRun == null || _engine.CurrentRun.PendingReward != null)
            {
                return;
            }

            if (e.KeyCode == Keys.Escape && _engine.CanCancelAiming)
            {
                _engine.CancelAiming();
                _renderState.CurrentAimPreview = null;
                Invalidate();
                return;
            }

            if (_engine.CurrentAimingCard != null)
            {
                HandleAimKey(e.KeyCode);
            }
            else
            {
                HandleArenaActionKey(e.KeyCode);
            }

            SyncScreenWithRunState();
            Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
            _engine.SaveAndExitToMenu();
            base.OnFormClosing(e);
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            _renderState.PulsePhase += 0.033f;
            bool hadToast = _engine.Achievements.ActiveToast != null;
            _engine.Achievements.UpdateToasts(0.033f);
            bool hasToast = _engine.Achievements.ActiveToast != null;

            if (_currentScreen == ScreenMode.Arena || hadToast || hasToast)
            {
                Invalidate();
            }
        }

        private void UpdateArenaHoverAndPreview(Point location)
        {
            if (_renderState.DraggingEquippedIndex >= 0)
            {
                _renderState.DragCurrentPoint = location;
            }

            GridPos? hoveredTile = ArenaScreenRenderer.ScreenToGrid(location);
            _renderState.HoveredGridTile = hoveredTile;

            if (_engine.CurrentAimingCard == null || !hoveredTile.HasValue)
            {
                _renderState.CurrentAimPreview = null;
                return;
            }

            GridPos? dir = _engine.ResolveAimDirectionFromTile(hoveredTile.Value);
            _renderState.CurrentAimPreview = dir.HasValue ? _engine.ComputeAimPreview(dir.Value) : null;
        }

        private void HandleHitAction(UiHitTarget hit)
        {
            switch (hit.Action)
            {
                case "OpenStats":
                    _currentScreen = ScreenMode.Stats;
                    break;
                case "BackToMenu":
                    _renderState.BannerMessage = string.Empty;
                    _currentScreen = ScreenMode.Menu;
                    break;
                case "SelectCharacter":
                    _engine.TrySelectCharacter(hit.Payload);
                    break;
                case "BuyWeapon":
                    _engine.TryPurchaseWeapon(hit.Payload);
                    break;
                case "BuyUpgrade":
                    _engine.TryPurchaseUpgrade(hit.Payload);
                    break;
                case "NewRun":
                    StartNewRunAndEnterArena();
                    break;
                case "ContinueRun":
                    ContinueSavedRun();
                    break;
                case "SaveAndExit":
                    _engine.SaveAndExitToMenu();
                    _currentScreen = ScreenMode.Menu;
                    break;
                case "EquipCard":
                    _engine.TryEquipCard(hit.Index);
                    break;
                case "PlayQueue":
                    _engine.BeginPlayEquippedQueue();
                    break;
                case "CancelAim":
                    _engine.CancelAiming();
                    _renderState.CurrentAimPreview = null;
                    break;
                case "Wait":
                    _engine.TryWait();
                    break;
                case "Special":
                    _engine.TryUseSpecial();
                    break;
                case "GridTile":
                    HandleGridTileClick(hit.Index);
                    break;
                case "PickRewardCard":
                    _engine.PickRewardCard(hit.Payload);
                    break;
                case "SelectUpgradeMod":
                    if (Enum.TryParse(hit.Payload, out UpgradeModifierType mod))
                    {
                        _renderState.SelectedUpgradeModifier = mod;
                    }

                    break;
                case "ApplyUpgradeToCard":
                    if (_renderState.SelectedUpgradeModifier.HasValue
                        && _engine.ApplyUpgradeToCard(_renderState.SelectedUpgradeModifier.Value, hit.Index))
                    {
                        _renderState.SelectedUpgradeModifier = null;
                    }

                    break;
                case "RerollReward":
                    if (_engine.TryRerollReward())
                    {
                        _renderState.SelectedUpgradeModifier = null;
                    }

                    break;
                case "SkipReward":
                    _renderState.SelectedUpgradeModifier = null;
                    _engine.SkipReward();
                    break;
            }
        }

        private void StartNewRunAndEnterArena()
        {
            _renderState.BannerMessage = string.Empty;
            _renderState.SelectedUpgradeModifier = null;
            _renderState.CurrentAimPreview = null;
            _engine.StartNewRun();
            _currentScreen = ScreenMode.Arena;
        }

        private void ContinueSavedRun()
        {
            if (_engine.TryContinueSavedRun(out string error))
            {
                _renderState.BannerMessage = string.Empty;
                _renderState.SelectedUpgradeModifier = null;
                _currentScreen = ScreenMode.Arena;
                return;
            }

            _renderState.BannerMessage = error;
        }

        private void HandleGridTileClick(int flatIndex)
        {
            if (_engine.CurrentRun == null || flatIndex < 0)
            {
                return;
            }

            GridPos clickedTile = new GridPos(flatIndex % GridPos.GridSize, flatIndex / GridPos.GridSize);

            if (_engine.CurrentAimingCard != null)
            {
                GridPos? dir = _engine.ResolveAimDirectionFromTile(clickedTile);
                if (dir.HasValue && _engine.ConfirmAimDirection(dir.Value))
                {
                    _renderState.CurrentAimPreview = null;
                }

                return;
            }

            GridPos playerPos = _engine.CurrentRun.Player.Position;
            if (clickedTile.ManhattanDistance(playerPos) == 1)
            {
                _engine.TryMovePlayer(clickedTile.X - playerPos.X, clickedTile.Y - playerPos.Y);
            }
        }

        private void HandleAimKey(Keys key)
        {
            GridPos? dir = key switch
            {
                Keys.W or Keys.Up => new GridPos(0, -1),
                Keys.S or Keys.Down => new GridPos(0, 1),
                Keys.A or Keys.Left => new GridPos(-1, 0),
                Keys.D or Keys.Right => new GridPos(1, 0),
                Keys.Q => new GridPos(-1, -1),
                Keys.E => new GridPos(1, -1),
                Keys.Z => new GridPos(-1, 1),
                Keys.C => new GridPos(1, 1),
                _ => null
            };

            if (!dir.HasValue)
            {
                return;
            }

            if (_engine.ConfirmAimDirection(dir.Value))
            {
                _renderState.CurrentAimPreview = null;
            }
        }

        private void HandleArenaActionKey(Keys key)
        {
            switch (key)
            {
                case Keys.W:
                case Keys.Up:
                    _engine.TryMovePlayer(0, -1);
                    break;
                case Keys.S:
                case Keys.Down:
                    _engine.TryMovePlayer(0, 1);
                    break;
                case Keys.A:
                case Keys.Left:
                    _engine.TryMovePlayer(-1, 0);
                    break;
                case Keys.D:
                case Keys.Right:
                    _engine.TryMovePlayer(1, 0);
                    break;
                case Keys.Space:
                    _engine.TryWait();
                    break;
                case Keys.R:
                    _engine.TryUseSpecial();
                    break;
                case Keys.Enter:
                    _engine.BeginPlayEquippedQueue();
                    break;
            }
        }

        private void SyncScreenWithRunState()
        {
            if (_currentScreen == ScreenMode.Arena
                && _engine.CurrentRun != null
                && (_engine.CurrentRun.IsGameOver || _engine.CurrentRun.IsVictory))
            {
                _renderState.CurrentAimPreview = null;
                _renderState.DraggingEquippedIndex = -1;
                _currentScreen = ScreenMode.GameOver;
            }
        }
    }
}