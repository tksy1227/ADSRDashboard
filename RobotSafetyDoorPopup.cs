using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ADSRDashboard
{
    /// <summary>
    /// Popup dialog for alarm "Problem: 111 – SHUT AND RESET ROBOT SAFETY DOOR".
    /// </summary>
    public class RobotSafetyDoorPopup : Form
    {
        public RobotSafetyDoorPopup()
        {
            this.Text            = "Problem 111 – Robot Safety Door";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition   = FormStartPosition.CenterParent;
            this.Size            = new Size(770, 600);
            this.BackColor       = Theme.PopupBody;
            this.MinimumSize     = new Size(600, 480);

            // Apply rounded corners
            this.Load += (s, e) => ApplyRoundedCorners();

            this.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.FromArgb(100, 105, 120), 2f);
                
                // Draw rounded border
                int borderRadius = 12;
                using var path = DrawingUtils.RoundedRect(new Rectangle(0, 0, this.Width - 1, this.Height - 1), borderRadius);
                g.DrawPath(pen, path);
            };
            Build();
        }

        void Build()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Theme.PopupHeader };
            var lblTitle = new Label {
                Text      = "Problem: 111 \u2013 SHUT AND RESET ROBOT SAFETY DOOR",
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0)
            };
            header.Controls.Add(lblTitle);

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.PopupBody, Padding = new Padding(12, 8, 12, 0) };
            btnBar.Paint += (s, e) => {
                using var pen = new Pen(Color.FromArgb(210, 214, 222), 1f);
                e.Graphics.DrawLine(pen, 0, btnBar.Height - 1, btnBar.Width, btnBar.Height - 1);
            };

            var btnContinue = MakeActionButton("Continue", "\u25BA", Color.FromArgb(240, 248, 240), Color.FromArgb(40, 160, 70));
            var btnIgnore   = MakeActionButton("Ignore",   "\u00D8", Theme.PopupBtnBg, Theme.TextMid);
            var btnAbort    = MakeActionButton("Abort",    "\u2715", Theme.PopupBtnBg, Theme.TextMid);
            var btnClose    = MakeActionButton("Close",    "\u25B6", Theme.PopupBtnBg, Theme.TextMid);

            btnClose.Text = "";
            btnClose.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                DrawRunningPerson(g, new Rectangle(8, 4, 22, 26), Theme.TextMid);
                using var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                using var tb = new SolidBrush(Theme.TextDark);
                g.DrawString("Close", new Font("Segoe UI", 9f), tb, new RectangleF(0, 0, btnClose.Width - 4, btnClose.Height), sf);
            };

            // Only the Close button works; others are disabled for the mockup
            btnClose.Click    += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            int bx = 12;
            foreach (var b in new[] { btnContinue, btnIgnore, btnAbort, btnClose }) {
                b.Location = new Point(bx, 8); btnBar.Controls.Add(b); bx += b.Width + 6;
            }

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.PopupBody, Padding = new Padding(20, 16, 20, 20) };
            int y = 0;
            int instructionW = 550;

            Action<string, int> AddInstruction = (text, top) => {
                var instructionLabel = new Label {
                    Text = text, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = Theme.TextMid,
                    Location = new Point(24, top), Width = instructionW, AutoSize = true, MaximumSize = new Size(instructionW, 0)
                };
                scroll.Controls.Add(instructionLabel);
            };

            var step1Label = MakeStepLabel(1, "Close the Robot safety door(s)");
            step1Label.Location = new Point(0, y); scroll.Controls.Add(step1Label);
            y += step1Label.PreferredHeight + 8;

            var diagram1 = new Panel { Location = new Point(20, y), Size = new Size(310, 200), BackColor = Theme.DiagramBg };
            diagram1.Paint += DrawDoorDiagram; scroll.Controls.Add(diagram1);
            AddInstruction("1. Ensure all three physical latches at the rear are engaged. The indicator on the main HMI panel should turn from flashing red to solid yellow once secured.", y + 205);
            y += diagram1.Height + 54;

            var step2Label = MakeStepLabel(2, "Press the RESET button to reset to normal safety condition");
            step2Label.Location = new Point(0, y); scroll.Controls.Add(step2Label);
            y += step2Label.PreferredHeight + 8;

            var diagram2 = new Panel { Location = new Point(20, y), Size = new Size(340, 90), BackColor = Theme.DiagramBg };
            diagram2.Paint += DrawButtonPanel; scroll.Controls.Add(diagram2);
            AddInstruction("2. The green RESET button will illuminate when the safety circuit is ready. Press and hold for 2 seconds until the machine status updates to 'Standby'.", y + 95);
            y += diagram2.Height + 20;

            scroll.AutoScrollMinSize = new Size(0, y + 30);
            this.Controls.Add(scroll); this.Controls.Add(btnBar); this.Controls.Add(header);
        }

        void ApplyRoundedCorners()
        {
            using var path = DrawingUtils.RoundedRect(new Rectangle(0, 0, this.Width, this.Height), 12);
            this.Region = new Region(path);
        }

        Button MakeActionButton(string text, string icon, Color bg, Color iconColor)
        {
            var btn = new Button {
                Text = $"  {icon}  {text}", Font = new Font("Segoe UI", 9f), ForeColor = Theme.TextDark,
                BackColor = bg, FlatStyle = FlatStyle.Flat, Size = new Size(110, 32), Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft
            };
            btn.FlatAppearance.BorderColor = Theme.Border;
            return btn;
        }

        Label MakeStepLabel(int stepNum, string text)
        {
            var stepLabel = new Label { AutoSize = true, MaximumSize = new Size(700, 0), Font = new Font("Segoe UI", 10f), ForeColor = Theme.TextDark };
            stepLabel.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bf = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var nf = new Font("Segoe UI", 10f);
                using var br = new SolidBrush(Theme.TextDark);
                string num = $"{stepNum}.   ";
                g.DrawString(num, bf, br, 0, 0);
                float w = g.MeasureString(num, bf).Width;
                g.DrawString(text, nf, br, w, 0);
            };
            stepLabel.Text = ""; // Text handled by Paint
            return stepLabel;
        }

        void DrawDoorDiagram(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel p) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var bgBr = new SolidBrush(Theme.DiagramBg); g.FillRectangle(bgBr, 0, 0, p.Width, p.Height);
            using var redPen = new Pen(Theme.Red, 1.5f); using var darkPen = new Pen(Color.FromArgb(60, 65, 80), 1.2f);
            using var thinPen = new Pen(Color.FromArgb(130, 135, 148), 0.8f);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var darkBrush = new SolidBrush(Theme.TextDark);

            int padX = 30, innerL = padX, innerR = p.Width - padX, innerW = innerR - innerL;
            int doorY = 50, doorH = 30;
            int[] dxs = { innerL + innerW / 6, innerL + innerW / 2, innerL + 5 * innerW / 6 };

            var lblR = new RectangleF(innerR - 85, 10, 80, 32);
            using var lblBr = new SolidBrush(Color.FromArgb(245, 246, 248)); g.FillRectangle(lblBr, lblR);
            using var lblP = new Pen(Theme.Red, 1f); g.DrawRectangle(lblP, lblR.X, lblR.Y, lblR.Width, lblR.Height);
            using var lblF = new Font("Segoe UI", 6f, FontStyle.Bold);
            using var redB = new SolidBrush(Theme.Red); g.DrawString("ROBOT SAFETY\nDOORS (X3)", lblF, redB, lblR, sf);

            g.DrawLine(redPen, innerL, doorY, innerR, doorY);
            foreach (int dx in dxs) {
                int sz = 14; g.DrawLine(redPen, dx - sz, doorY, dx + sz, doorY + doorH);
                g.DrawLine(redPen, dx + sz, doorY, dx - sz, doorY + doorH);
                if (dx == dxs[2]) g.DrawLine(thinPen, dx + sz, doorY + doorH / 2, (int)lblR.X, (int)(lblR.Y + lblR.Height / 2));
            }
            using var sF = new Font("Segoe UI", 7f);
            g.DrawString("REAR (ROBOT SIDE)", sF, darkBrush, new RectangleF(innerL, doorY + doorH + 2, innerW, 16), sf);

            var fR = new Rectangle(innerL + 20, doorY + doorH + 22, innerW - 40, p.Height - doorY - doorH - 72);
            using var wB = new SolidBrush(Color.White); g.FillRectangle(wB, fR); g.DrawRectangle(darkPen, fR);
            g.DrawString("FRONT (CARTRIDGE LOADING SIDE)", sF, darkBrush, new RectangleF(innerL, fR.Bottom + 4, innerW, 16), sf);
        }

        void DrawButtonPanel(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel p) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var pR = new Rectangle(4, 8, 200, 70);
            using var hB = new SolidBrush(Color.FromArgb(50, 52, 58));
            using var hP = new Pen(Color.FromArgb(30, 32, 38), 1.5f);
            g.FillRectangle(hB, pR); g.DrawRectangle(hP, pR);
            using var iB = new SolidBrush(Color.FromArgb(65, 68, 76));
            g.FillRectangle(iB, new Rectangle(pR.X + 6, pR.Y + 8, pR.Width - 12, pR.Height - 16));

            var btns = new[] {
                (24,  Color.FromArgb(220, 40, 40),  "EMO"),
                (58,  Color.FromArgb(220, 160, 30), "DOOR OPEN"),
                (88,  Color.FromArgb(60, 180, 60),  "RESET"),
                (118, Color.FromArgb(30, 80, 180),  "PAUSE"),
                (148, Color.FromArgb(60, 180, 60),  "START"),
            };
            using var lF = new Font("Segoe UI", 5f); using var wB = new SolidBrush(Color.White);
            foreach (var (bx, col, name) in btns) {
                int cx = pR.X + bx, cy = pR.Y + pR.Height / 2, btnRadius = name == "EMO" ? 14 : 10;
                if (name == "RESET") {
                    using var cP = new Pen(Theme.Red, 1.5f); g.DrawRectangle(cP, cx - btnRadius - 3, cy - btnRadius - 3, btnRadius * 2 + 6, btnRadius * 2 + 6);
                }
                using var bB = new SolidBrush(col); g.FillEllipse(bB, cx - btnRadius, cy - btnRadius, btnRadius * 2, btnRadius * 2);
                using var gl = new LinearGradientBrush(new PointF(cx - btnRadius, cy - btnRadius), new PointF(cx, cy + btnRadius / 2), Color.FromArgb(80, 255, 255, 255), Color.FromArgb(0, 255, 255, 255));
                g.FillEllipse(gl, cx - btnRadius, cy - btnRadius, btnRadius * 2, btnRadius);
                using var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(name, lF, wB, new RectangleF(cx - 22, pR.Bottom + 2, 44, 12), sf);
            }
            int rcx = pR.X + 88, rcy = pR.Y + pR.Height / 2, asx = rcx + 14, aex = pR.Right + 30;
            using var aP = new Pen(Theme.Red, 1.5f); g.DrawLine(aP, asx, rcy, aex, rcy);
            g.FillPolygon(new SolidBrush(Theme.Red), new PointF[] { new(aex, rcy), new(aex - 7, rcy - 4), new(aex - 7, rcy + 4) });
            var tB = new RectangleF(aex + 4, rcy - 14, 80, 28);
            using var tbB = new SolidBrush(Color.FromArgb(245, 246, 248)); g.FillRectangle(tbB, tB);
            using var tbP = new Pen(Theme.Red, 1f); g.DrawRectangle(tbP, tB.X, tB.Y, tB.Width, tB.Height);
            using var tbF = new Font("Segoe UI", 7.5f, FontStyle.Bold); using var tbT = new SolidBrush(Theme.TextDark);
            using var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("RESET\nBUTTON", tbF, tbT, tB, sfC);
        }

        static void DrawRunningPerson(Graphics g, Rectangle bounds, Color col)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(col, 1.5f); using var br = new SolidBrush(col);
            float cx = bounds.X + bounds.Width / 2f, top = bounds.Y;
            g.FillEllipse(br, cx - 4, top, 8, 8);
            g.DrawLine(pen, cx, top + 8, cx - 3, top + 17);
            g.DrawLine(pen, cx, top + 12, cx - 7, top + 9);
            g.DrawLine(pen, cx, top + 12, cx + 5, top + 15);
            g.DrawLine(pen, cx - 3, top + 17, cx + 4, top + 24);
            g.DrawLine(pen, cx - 3, top + 17, cx - 8, top + 24);
        }
    }
}