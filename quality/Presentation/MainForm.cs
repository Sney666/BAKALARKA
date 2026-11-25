using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using RogueCards.Domain;
namespace RogueCards.Presentation
{
    public sealed class MainForm : Form
    {
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;
        private ScreenMode _currentScreen = ScreenMode.Menu;
        private readonly Dictionary<ScreenMode, Rectangle> _navButtons = new Dictionary<ScreenMode, Rectangle>();

        public MainForm()
        {
            ConfigureWindow();
            BuildNavigationLayout();
        }

        private void ConfigureWindow()
        {
            Text = "Rogue Cards";
            ClientSize = new Size(WindowWidth, WindowHeight);
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

        private void BuildNavigationLayout()
        {
            int buttonWidth = 170;
            int buttonHeight = 36;
            int gap = 14;
            int startX = 40;
            int topY = 22;

            ScreenMode[] screens =
            {
            ScreenMode.Menu,
            ScreenMode.Arena,
            ScreenMode.Stats,
            ScreenMode.GameOver
        };

            for (int i = 0; i < screens.Length; i++)
            {
                int x = startX + (i * (buttonWidth + gap));
                _navButtons[screens[i]] = new Rectangle(x, topY, buttonWidth, buttonHeight);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            DrawBackground(g);
            DrawTopBar(g);
            DrawScreenContent(g);
        }

        private void DrawBackground(Graphics g)
        {
            Rectangle bounds = new Rectangle(0, 0, WindowWidth, WindowHeight);
            using LinearGradientBrush brush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(20, 24, 34),
                Color.FromArgb(12, 15, 22),
                LinearGradientMode.Vertical);

            g.FillRectangle(brush, bounds);
        }

        private void DrawTopBar(Graphics g)
        {
            using SolidBrush barBrush = new SolidBrush(Color.FromArgb(28, 34, 48));
            using Pen borderPen = new Pen(Color.FromArgb(58, 70, 94), 1f);
            g.FillRectangle(barBrush, 0, 0, WindowWidth, 80);
            g.DrawLine(borderPen, 0, 80, WindowWidth, 80);

            foreach (KeyValuePair<ScreenMode, Rectangle> pair in _navButtons)
            {
                DrawNavigationButton(g, pair.Key, pair.Value);
            }

            using Font titleFont = new Font("Segoe UI", 16f, FontStyle.Bold);
            using SolidBrush titleBrush = new SolidBrush(Color.FromArgb(236, 200, 112));
            StringFormat rightFormat = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString("ROGUE CARDS", titleFont, titleBrush, new RectangleF(840, 16, 400, 48), rightFormat);
        }

        private void DrawNavigationButton(Graphics g, ScreenMode mode, Rectangle rect)
        {
            bool isSelected = mode == _currentScreen;
            Color fillColor = isSelected ? Color.FromArgb(62, 102, 168) : Color.FromArgb(40, 48, 66);
            Color borderColor = isSelected ? Color.FromArgb(126, 178, 255) : Color.FromArgb(72, 86, 112);

            using SolidBrush fillBrush = new SolidBrush(fillColor);
            using Pen borderPen = new Pen(borderColor, 1.5f);
            using Font font = new Font("Segoe UI", 10f, FontStyle.Bold);
            using SolidBrush textBrush = new SolidBrush(Color.White);

            g.FillRectangle(fillBrush, rect);
            g.DrawRectangle(borderPen, rect);

            StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(GetScreenTitle(mode), font, textBrush, rect, format);
        }

        private void DrawScreenContent(Graphics g)
        {
            Rectangle panelRect = new Rectangle(140, 160, 1000, 440);
            using SolidBrush panelBrush = new SolidBrush(Color.FromArgb(30, 36, 50));
            using Pen panelPen = new Pen(Color.FromArgb(74, 88, 116), 2f);
            g.FillRectangle(panelBrush, panelRect);
            g.DrawRectangle(panelPen, panelRect);

            using Font headerFont = new Font("Segoe UI", 26f, FontStyle.Bold);
            using Font bodyFont = new Font("Segoe UI", 13f, FontStyle.Regular);
            using SolidBrush headerBrush = new SolidBrush(Color.FromArgb(240, 244, 250));
            using SolidBrush bodyBrush = new SolidBrush(Color.FromArgb(174, 186, 210));

            StringFormat centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(
                GetScreenTitle(_currentScreen),
                headerFont,
                headerBrush,
                new RectangleF(panelRect.X, panelRect.Y + 90, panelRect.Width, 60),
                centerFormat);

            g.DrawString(
                GetScreenDescription(_currentScreen),
                bodyFont,
                bodyBrush,
                new RectangleF(panelRect.X + 80, panelRect.Y + 180, panelRect.Width - 160, 140),
                centerFormat);
        }

        private static string GetScreenTitle(ScreenMode mode)
        {
            return mode switch
            {
                ScreenMode.Menu => "Main Menu",
                ScreenMode.Arena => "Battle Arena",
                ScreenMode.Stats => "Achievements & Stats",
                ScreenMode.GameOver => "Run Summary",
                _ => "Rogue Cards"
            };
        }

        private static string GetScreenDescription(ScreenMode mode)
        {
            return mode switch
            {
                ScreenMode.Menu => "Domain models and window host initialized (1280x720, double-buffered).\nUse the top bar or keys 1-4 to preview screen states.",
                ScreenMode.Arena => "5x5 tactical arena screen host ready for engine integration.",
                ScreenMode.Stats => "Lifetime statistics and 2x5 achievements grid screen host ready.",
                ScreenMode.GameOver => "Victory and defeat summary screen host ready.",
                _ => string.Empty
            };
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            foreach (KeyValuePair<ScreenMode, Rectangle> pair in _navButtons)
            {
                if (pair.Value.Contains(e.Location))
                {
                    SwitchScreen(pair.Key);
                    return;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.D1)
            {
                SwitchScreen(ScreenMode.Menu);
            }
            else if (e.KeyCode == Keys.D2)
            {
                SwitchScreen(ScreenMode.Arena);
            }
            else if (e.KeyCode == Keys.D3)
            {
                SwitchScreen(ScreenMode.Stats);
            }
            else if (e.KeyCode == Keys.D4)
            {
                SwitchScreen(ScreenMode.GameOver);
            }
        }

        private void SwitchScreen(ScreenMode nextScreen)
        {
            if (_currentScreen == nextScreen)
            {
                return;
            }

            _currentScreen = nextScreen;
            Invalidate();
        }
    }
}