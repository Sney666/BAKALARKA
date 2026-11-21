// File: MainForm.cs
#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RogueCards
{
    public class MainForm : Form
    {
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;

        private GameScreen currentScreen;
        private readonly List<ScreenButton> navigationButtons;
        private Point mouseLocation;

        private readonly Color colorBackground = Color.FromArgb(18, 22, 29);
        private readonly Color colorPanel = Color.FromArgb(28, 34, 45);
        private readonly Color colorPanelBorder = Color.FromArgb(58, 69, 88);
        private readonly Color colorAccent = Color.FromArgb(217, 155, 48);
        private readonly Color colorButtonIdle = Color.FromArgb(37, 45, 59);
        private readonly Color colorButtonHover = Color.FromArgb(52, 63, 82);
        private readonly Color colorButtonActive = Color.FromArgb(181, 125, 34);
        private readonly Color colorTextPrimary = Color.FromArgb(236, 240, 246);
        private readonly Color colorTextSecondary = Color.FromArgb(156, 169, 189);

        private sealed class ScreenButton
        {
            public string Label { get; set; }
            public GameScreen TargetScreen { get; set; }
            public Rectangle Bounds { get; set; }
        }

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

            currentScreen = GameScreen.Menu;
            navigationButtons = new List<ScreenButton>();
            BuildNavigationButtons();

            MouseMove += OnFormMouseMove;
            MouseDown += OnFormMouseDown;
            KeyDown += OnFormKeyDown;
        }

        private void BuildNavigationButtons()
        {
            navigationButtons.Clear();

            GameScreen[] screens = new GameScreen[]
            {
                GameScreen.Menu,
                GameScreen.Arena,
                GameScreen.Aiming,
                GameScreen.RewardCard,
                GameScreen.RewardUpgrade,
                GameScreen.Stats,
                GameScreen.GameOver
            };

            string[] labels = new string[]
            {
                "Menu",
                "Arena",
                "Aiming",
                "Reward: Card",
                "Reward: Upgrade",
                "Stats & Achievements",
                "Game Over"
            };

            int startX = 28;
            int topY = 18;
            int buttonHeight = 36;
            int gap = 10;
            int totalGap = gap * (screens.Length - 1);
            int buttonWidth = (WindowWidth - (startX * 2) - totalGap) / screens.Length;

            for (int i = 0; i < screens.Length; i++)
            {
                int x = startX + (i * (buttonWidth + gap));
                navigationButtons.Add(new ScreenButton
                {
                    Label = labels[i],
                    TargetScreen = screens[i],
                    Bounds = new Rectangle(x, topY, buttonWidth, buttonHeight)
                });
            }
        }

        private void SetScreen(GameScreen nextScreen)
        {
            if (currentScreen != nextScreen)
            {
                currentScreen = nextScreen;
                Invalidate();
            }
        }

        private void OnFormMouseMove(object sender, MouseEventArgs e)
        {
            mouseLocation = e.Location;
            bool hoveringAny = false;

            foreach (ScreenButton btn in navigationButtons)
            {
                if (btn.Bounds.Contains(mouseLocation))
                {
                    hoveringAny = true;
                    break;
                }
            }

            Cursor = hoveringAny ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        private void OnFormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            foreach (ScreenButton btn in navigationButtons)
            {
                if (btn.Bounds.Contains(e.Location))
                {
                    SetScreen(btn.TargetScreen);
                    return;
                }
            }
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.D1)
            {
                SetScreen(GameScreen.Menu);
            }
            else if (e.KeyCode == Keys.D2)
            {
                SetScreen(GameScreen.Arena);
            }
            else if (e.KeyCode == Keys.D3)
            {
                SetScreen(GameScreen.Aiming);
            }
            else if (e.KeyCode == Keys.D4)
            {
                SetScreen(GameScreen.RewardCard);
            }
            else if (e.KeyCode == Keys.D5)
            {
                SetScreen(GameScreen.RewardUpgrade);
            }
            else if (e.KeyCode == Keys.D6)
            {
                SetScreen(GameScreen.Stats);
            }
            else if (e.KeyCode == Keys.D7)
            {
                SetScreen(GameScreen.GameOver);
            }
            else if (e.KeyCode == Keys.Escape)
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

            DrawBackground(g);
            DrawTopBar(g);
            DrawScreenContent(g);
            DrawFooterBar(g);
        }

        private void DrawBackground(Graphics g)
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
        }

        private void DrawTopBar(Graphics g)
        {
            Rectangle headerBar = new Rectangle(0, 0, WindowWidth, 72);
            using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(22, 27, 36)))
            {
                g.FillRectangle(barBrush, headerBar);
            }

            using (Pen borderPen = new Pen(colorPanelBorder, 1f))
            {
                g.DrawLine(borderPen, 0, 72, WindowWidth, 72);
            }

            using (Font btnFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                foreach (ScreenButton btn in navigationButtons)
                {
                    bool isActive = btn.TargetScreen == currentScreen;
                    bool isHovered = btn.Bounds.Contains(mouseLocation);

                    Color fillColor = colorButtonIdle;
                    if (isActive)
                    {
                        fillColor = colorButtonActive;
                    }
                    else if (isHovered)
                    {
                        fillColor = colorButtonHover;
                    }

                    DrawRoundedBox(g, btn.Bounds, 6, fillColor, isActive ? colorAccent : colorPanelBorder);

                    using (SolidBrush textBrush = new SolidBrush(colorTextPrimary))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(btn.Label, btnFont, textBrush, btn.Bounds, sf);
                    }
                }
            }
        }

        private void DrawScreenContent(Graphics g)
        {
            Rectangle stageRect = new Rectangle(28, 92, WindowWidth - 56, WindowHeight - 152);
            DrawRoundedBox(g, stageRect, 12, colorPanel, colorPanelBorder);

            string title = GetScreenTitle(currentScreen);
            string subtitle = GetScreenDescription(currentScreen);

            using (Font titleFont = new Font("Segoe UI", 28f, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 12.5f, FontStyle.Regular))
            using (Font badgeFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(colorAccent))
            using (SolidBrush textBrush = new SolidBrush(colorTextPrimary))
            using (SolidBrush mutedBrush = new SolidBrush(colorTextSecondary))
            using (StringFormat centerFormat = new StringFormat())
            {
                centerFormat.Alignment = StringAlignment.Center;
                centerFormat.LineAlignment = StringAlignment.Center;

                Rectangle badgeRect = new Rectangle((WindowWidth / 2) - 110, 140, 220, 30);
                DrawRoundedBox(g, badgeRect, 14, Color.FromArgb(42, 51, 67), colorAccent);
                g.DrawString($"STATE: {currentScreen.ToString().ToUpperInvariant()}", badgeFont, textBrush, badgeRect, centerFormat);

                Rectangle titleRect = new Rectangle(80, 190, WindowWidth - 160, 56);
                g.DrawString(title, titleFont, titleBrush, titleRect, centerFormat);

                Rectangle subRect = new Rectangle(160, 255, WindowWidth - 320, 70);
                g.DrawString(subtitle, subFont, mutedBrush, subRect, centerFormat);

                DrawStageWireframe(g, stageRect);
            }
        }

        private void DrawStageWireframe(Graphics g, Rectangle stageRect)
        {
            int centerX = stageRect.X + (stageRect.Width / 2);
            int previewTop = 340;

            if (currentScreen == GameScreen.Arena || currentScreen == GameScreen.Aiming)
            {
                int cellSize = 42;
                int gridSize = cellSize * 5;
                int gridLeft = centerX - (gridSize / 2);

                using (Pen gridPen = new Pen(colorPanelBorder, 1.5f))
                using (SolidBrush cellBrush = new SolidBrush(Color.FromArgb(35, 43, 56)))
                using (SolidBrush aimBrush = new SolidBrush(Color.FromArgb(120, 217, 155, 48)))
                {
                    for (int row = 0; row < 5; row++)
                    {
                        for (int col = 0; col < 5; col++)
                        {
                            Rectangle cell = new Rectangle(
                                gridLeft + (col * cellSize),
                                previewTop + (row * cellSize),
                                cellSize - 2,
                                cellSize - 2);

                            g.FillRectangle(cellBrush, cell);

                            if (currentScreen == GameScreen.Aiming && row == 2 && col >= 2)
                            {
                                g.FillRectangle(aimBrush, cell);
                            }

                            g.DrawRectangle(gridPen, cell);
                        }
                    }
                }
            }
            else if (currentScreen == GameScreen.RewardCard || currentScreen == GameScreen.RewardUpgrade)
            {
                int cardW = 190;
                int cardH = 210;
                int gap = 28;
                int count = currentScreen == GameScreen.RewardCard ? 2 : 3;
                int totalW = (cardW * count) + (gap * (count - 1));
                int startX = centerX - (totalW / 2);

                using (Font cardFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(colorTextSecondary))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;

                    for (int i = 0; i < count; i++)
                    {
                        Rectangle cardRect = new Rectangle(startX + (i * (cardW + gap)), previewTop, cardW, cardH);
                        DrawRoundedBox(g, cardRect, 10, Color.FromArgb(34, 42, 55), colorAccent);
                        string label = currentScreen == GameScreen.RewardCard
                            ? $"Reward Card #{i + 1}"
                            : $"Upgrade Option #{i + 1}";
                        g.DrawString(label, cardFont, textBrush, cardRect, sf);
                    }
                }
            }
            else if (currentScreen == GameScreen.Stats)
            {
                int cellW = 190;
                int cellH = 88;
                int gap = 14;
                int totalW = (cellW * 5) + (gap * 4);
                int startX = centerX - (totalW / 2);

                using (Font badgeFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(colorTextSecondary))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;

                    for (int row = 0; row < 2; row++)
                    {
                        for (int col = 0; col < 5; col++)
                        {
                            int index = (row * 5) + col + 1;
                            Rectangle achRect = new Rectangle(
                                startX + (col * (cellW + gap)),
                                previewTop + (row * (cellH + gap)),
                                cellW,
                                cellH);

                            DrawRoundedBox(g, achRect, 8, Color.FromArgb(34, 42, 55), colorPanelBorder);
                            g.DrawString($"Achievement #{index}", badgeFont, textBrush, achRect, sf);
                        }
                    }
                }
            }
            else
            {
                Rectangle summaryBox = new Rectangle(centerX - 280, previewTop + 20, 560, 140);
                DrawRoundedBox(g, summaryBox, 10, Color.FromArgb(34, 42, 55), colorPanelBorder);

                using (Font infoFont = new Font("Segoe UI", 11f, FontStyle.Regular))
                using (SolidBrush textBrush = new SolidBrush(colorTextSecondary))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    string text = currentScreen == GameScreen.Menu
                        ? "Rogue Cards — Fixed 1280x720 Single-Screen Tactical Engine Ready"
                        : "Run Summary & Return to Main Menu Screen Ready";
                    g.DrawString(text, infoFont, textBrush, summaryBox, sf);
                }
            }
        }

        private void DrawFooterBar(Graphics g)
        {
            Rectangle footerRect = new Rectangle(0, WindowHeight - 44, WindowWidth, 44);
            using (SolidBrush footerBrush = new SolidBrush(Color.FromArgb(20, 25, 34)))
            {
                g.FillRectangle(footerBrush, footerRect);
            }

            using (Pen borderPen = new Pen(colorPanelBorder, 1f))
            {
                g.DrawLine(borderPen, 0, WindowHeight - 44, WindowWidth, WindowHeight - 44);
            }

            using (Font footerFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(colorTextSecondary))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                string hint = "Click top state tabs or press keys [1-7] to switch screens | ESC: Menu | Single-Screen Mode (1280x720, AutoScroll = false)";
                g.DrawString(hint, footerFont, textBrush, footerRect, sf);
            }
        }

        private static string GetScreenTitle(GameScreen screen)
        {
            switch (screen)
            {
                case GameScreen.Menu:
                    return "ROGUE CARDS — MAIN MENU";
                case GameScreen.StartingDraft:
                    return "STARTING HAND DRAFT";
                case GameScreen.Arena:
                    return "TACTICAL ARENA (5x5 GRID)";
                case GameScreen.Aiming:
                    return "SEQUENTIAL CARD AIMING PREVIEW";
                case GameScreen.RewardCard:
                    return "WAVE CLEARED — PICK A CARD";
                case GameScreen.RewardUpgrade:
                    return "WAVE CLEARED — UPGRADE A CARD";
                case GameScreen.Stats:
                    return "ACHIEVEMENTS & LIFETIME STATISTICS";
                case GameScreen.GameOver:
                    return "RUN FINISHED — GAME OVER";
                default:
                    return "ROGUE CARDS";
            }
        }

        private static string GetScreenDescription(GameScreen screen)
        {
            switch (screen)
            {
                case GameScreen.Menu:
                    return "Select character, start or continue a run, unlock weapons and permanent upgrades, or view statistics.";
                case GameScreen.StartingDraft:
                    return "Choose 2 cards from 4 unlocked options to build your starting tactical hand.";
                case GameScreen.Arena:
                    return "Move on the 5x5 grid, equip cards, reorder your queue, trigger your Special ability, or wait.";
                case GameScreen.Aiming:
                    return "Hover or use directional keys to preview affected tiles and confirm execution for the active queued card.";
                case GameScreen.RewardCard:
                    return "Select 1 new weapon or utility card, skip the reward, or spend coins to reroll choices.";
                case GameScreen.RewardUpgrade:
                    return "Select a modifier (+Damage, -Cooldown, or Poison) and attach it to one of your cards.";
                case GameScreen.Stats:
                    return "Compact 2x5 Achievement grid paired with lifetime combat statistics in a single non-scrolling view.";
                case GameScreen.GameOver:
                    return "Displays victory or defeat status, wave reached, coins earned, and option to return to Main Menu.";
                default:
                    return string.Empty;
            }
        }

        private static void DrawRoundedBox(Graphics g, Rectangle bounds, int radius, Color fillColor, Color borderColor)
        {
            using (GraphicsPath path = CreateRoundedRectPath(bounds, radius))
            using (SolidBrush fillBrush = new SolidBrush(fillColor))
            using (Pen borderPen = new Pen(borderColor, 1.5f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}