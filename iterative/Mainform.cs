#nullable disable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RogueCards
{
    public class MainForm : Form
    {
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;

        private readonly DataRepository dataRepository;
        private readonly GameDatabase database;
        private readonly PlayerProfile profile;
        private readonly MenuRenderer menuRenderer;

        private GameScreen currentScreen;
        private Point mouseLocation;
        private string menuStatusMessage;
        private Rectangle backToMenuButtonRect;

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
            profile = dataRepository.CreateDefaultProfile(database);
            menuRenderer = new MenuRenderer();

            currentScreen = GameScreen.Menu;
            backToMenuButtonRect = new Rectangle(28, 18, 180, 38);

            if (dataRepository.ParseErrors.Count > 0)
            {
                menuStatusMessage = $"Warning: {dataRepository.ParseErrors.Count} malformed data line(s) logged to Logs/data_parser_errors.txt.";
            }
            else
            {
                menuStatusMessage = string.Empty;
            }

            MouseMove += OnFormMouseMove;
            MouseDown += OnFormMouseDown;
            KeyDown += OnFormKeyDown;
        }

        private void SetScreen(GameScreen nextScreen)
        {
            if (currentScreen != nextScreen)
            {
                currentScreen = nextScreen;
                Cursor = Cursors.Default;
                Invalidate();
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
            else
            {
                Cursor = backToMenuButtonRect.Contains(mouseLocation) ? Cursors.Hand : Cursors.Default;
            }

            Invalidate();
        }

        private void OnFormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (currentScreen == GameScreen.Menu)
            {
                HandleMenuClick(e.Location);
                return;
            }

            if (backToMenuButtonRect.Contains(e.Location))
            {
                SetScreen(GameScreen.Menu);
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
                    SetScreen(GameScreen.Arena);
                    break;

                case MenuActionType.ContinueRun:
                    if (hit.IsEnabled && dataRepository.HasActiveRunSave())
                    {
                        SetScreen(GameScreen.Arena);
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
            menuStatusMessage = $"Purchased permanent upgrade '{upgrade.Name}' for {upgrade.Cost} Coins!";
            Invalidate();
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && currentScreen != GameScreen.Menu)
            {
                SetScreen(GameScreen.Menu);
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
                    dataRepository.HasActiveRunSave(),
                    mouseLocation,
                    menuStatusMessage);
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