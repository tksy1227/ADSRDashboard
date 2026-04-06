using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ADSRDashboard
{
    public partial class MainDashboard : Form
    {
        // ── Palette ────────────────────────────────────────────────────────────
        static readonly Color C_PAGE_BG    = Color.FromArgb(236, 238, 242);
        static readonly Color C_WHITE      = Color.White;
        static readonly Color C_BORDER     = Color.FromArgb(205, 210, 220);
        static readonly Color C_ACCENT     = Color.FromArgb(185, 40, 40);
        static readonly Color C_TEXT_DARK  = Color.FromArgb(38, 42, 52);
        static readonly Color C_TEXT_MID   = Color.FromArgb(110, 118, 135);
        static readonly Color C_RED        = Color.FromArgb(205, 50, 50);
        static readonly Color C_GREEN      = Color.FromArgb(62, 175, 80);
        static readonly Color C_BIN_DARK   = Color.FromArgb(50, 54, 64);
        static readonly Color C_BIN_EMPTY  = Color.FromArgb(185, 190, 200);
        static readonly Color C_BTN_DETAIL = Color.FromArgb(220, 223, 230);
        static readonly Color C_BTN_GUIDE  = Color.FromArgb(195, 48, 52);
        static readonly Color C_ALARM_ICON = Color.FromArgb(205, 50, 50);
        static readonly Color C_MENU_ACT   = Color.FromArgb(185, 40, 40);
        static readonly Color C_MENU_HOVER = Color.FromArgb(245, 246, 248);

        // ── State ─────────────────────────────────────────────────────────────
        bool _fanFrontOn = true, _fanBackOn = true, _lightOn = true, _machineOn = true;
        Form? _fanPopupHost = null;

        // ── UI references ─────────────────────────────────────────────────────
        Label?    _lblClock;
        ComboBox? _cmbView;
        Panel?    _pnlBinGrid;
        Panel?    _gearPanel;
        Label?    _lblCountPending;
        Label?    _lblCountInOp;
        Label?    _lblCountDisabled;
        int       _cntPending, _cntInOp, _cntDisabled;
        Panel?    _alertScroll;
        Panel?    _warnScroll;

        // ── Menu items ─────────────────────────────────────────────────────

        // ── Stop-screen overlay (bee_stop) ────────────────────────────────────
        Panel?  _stopOverlay = null;

        // ── Fan button reference — updated when fan state changes ─────────────
        Button? _btnFanCtrl  = null;

        // ── Bin hover / click ─────────────────────────────────────────────────
        Form?   _hoverPopup  = null;
        Button? _clickedBin  = null;
        Color   _clickedBinOriginalColor;
        bool    _isHoverLock = false;

        // ── Layout reference: binPanel is needed by AlertCenter resize ─────────
        Panel? _binPanel   = null;
        Panel? _machineBar = null;   // frame2 strip, needed for width matching

        readonly string ImgDir;
        readonly string[] _menuItems = { "Dashboard", "Reports", "Settings", "Maintenance", "About" };

        public MainDashboard()
        {
            InitializeComponent();
            ImgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");

            this.Text          = "ADSR - Dashboard";
            this.Size          = new Size(1600, 900);
            this.MinimumSize   = new Size(1600, 900);
            this.BackColor     = C_PAGE_BG;
            this.Font          = new Font("Segoe UI", 9f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState   = FormWindowState.Normal;

            Build();
            StartClock();
        }

        // ── Safe image loader ─────────────────────────────────────────────────
        Image? LoadImg(string name)
        {
            try { var p = Path.Combine(ImgDir, name); return File.Exists(p) ? Image.FromFile(p) : null; }
            catch { return null; }
        }

        // ── Sample color from background image ──────────────────────────────
        Color SampleBackgroundColor()
        {
            var img = LoadImg("adm_top.png");
            if (img == null) return Color.FromArgb(70, 75, 85); // fallback

            try
            {
                using var bmp = new Bitmap(img);
                // Try multiple sample points to find the dark grey
                Color[] samples = new Color[5];
                samples[0] = bmp.GetPixel(10, 10);      // top-left
                samples[1] = bmp.GetPixel(bmp.Width / 2, 10);  // top-center
                samples[2] = bmp.GetPixel(bmp.Width - 10, 10); // top-right
                samples[3] = bmp.GetPixel(10, bmp.Height / 2); // middle-left
                samples[4] = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2); // center

                // Find the darkest non-white color
                Color darkest = Color.White;
                foreach (var color in samples)
                {
                    Console.WriteLine($"Sample: R={color.R}, G={color.G}, B={color.B}");
                    if (color.R < darkest.R && color.G < darkest.G && color.B < darkest.B)
                        darkest = color;
                }

                Console.WriteLine($"Selected darkest color: R={darkest.R}, G={darkest.G}, B={darkest.B}");
                return darkest == Color.White ? Color.FromArgb(70, 75, 85) : darkest;
            }
            catch
            {
                return Color.FromArgb(70, 75, 85); // fallback
            }
        }

        void StartClock()
        {
            var t = new System.Windows.Forms.Timer { Interval = 1000 };
            t.Tick += (s, e) => { if (_lblClock != null) _lblClock.Text = DateTime.Now.ToString("d MMM yyyy   HH:mm:ss"); };
            t.Start();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MASTER BUILD
        // ═════════════════════════════════════════════════════════════════════
        void Build()
        {
            var frame1 = BuildFrame1_Header();
            var body   = new Panel { Dock = DockStyle.Fill, BackColor = C_PAGE_BG };

            this.Controls.Add(body);
            this.Controls.Add(frame1);

            BuildBody(body);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FRAME 1 — Header  (logo | nav | controls | user)
        //  top_border_l.png (vertically-flipped bottom_border_l) is the
        //  background at the lowest Z-layer. Panel BackColor matches page.
        // ═════════════════════════════════════════════════════════════════════
        Panel BuildFrame1_Header()
        {
            // Use page background colour — no white box, blends with the page
            var p = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = C_PAGE_BG };
            // No painted border line

            // ── top_border_l.png as lowest background layer ───────────────────
            // This is bottom_border_l.png flipped vertically, giving a decorative
            // diagonal line running across the top of the page.
            var topBorderImg = LoadImg("top_border_l.png");
            var headerBg = new PictureBox
            {
                SizeMode  = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
                Enabled   = false   // don't capture mouse events
            };
            if (topBorderImg != null) headerBg.Image = topBorderImg;

            // ── Logo: dashboard_m_l.png (falls back to drawn GHT hex) ──────────
            var logo = new Panel { Size = new Size(44, 44), Location = new Point(14, 18), BackColor = Color.Transparent };
            var dashLogoImg = LoadImg("dashboard_m_l.png");
            if (dashLogoImg != null)
            {
                var logoPic = new PictureBox
                {
                    Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent, Image = dashLogoImg
                };
                logo.Controls.Add(logoPic);
            }
            else
                logo.Paint += DrawGhtLogo;

            // ── Horizontal nav menu ───────────────────────────────────────────
            // Removed as per supervisor's reference

            // ── Control group: [Fan] [Light] [Stop] ──────────────────────────
            var ctrlGroup = new Panel { Size = new Size(232, 60), BackColor = Color.FromArgb(228, 231, 238) };
            ctrlGroup.Paint += (s, e) =>
            {
                using var pen = new Pen(C_BORDER, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, ctrlGroup.Width - 1, ctrlGroup.Height - 1);
                e.Graphics.DrawLine(pen, 76,  4, 76,  ctrlGroup.Height - 4);
                e.Graphics.DrawLine(pen, 152, 4, 152, ctrlGroup.Height - 4);
            };
            var btnFan   = MakeImgCtrlSlot("fan",   0,   ctrlGroup);
            _btnFanCtrl  = btnFan;   // store ref so fan popup can update the icon
            var btnLight = MakeImgCtrlSlot("light", 76,  ctrlGroup);
            var btnStop  = MakeImgCtrlSlot("stop",  152, ctrlGroup);
            ctrlGroup.Controls.AddRange(new Control[] { btnFan, btnLight, btnStop });

            // ── Avatar + user block ───────────────────────────────────────────
            var avatar = new Panel { Size = new Size(44, 44), BackColor = Color.Transparent };
            avatar.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(Color.FromArgb(65, 125, 195));
                g.FillEllipse(br, 0, 0, 43, 43);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("GU", new Font("Segoe UI", 13f, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 44, 44), sf);
            };
            var lblWelcome = new Label { Text = "Welcome",                                      Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = C_TEXT_DARK, AutoSize = true };
            _lblClock      = new Label { Text = DateTime.Now.ToString("d MMM yyyy   HH:mm:ss"), Font = new Font("Segoe UI", 8.5f),                  ForeColor = C_TEXT_MID,  AutoSize = true };
            var lblVer     = new Label { Text = "ASRS (GUI v1.0.00)",                           Font = new Font("Segoe UI", 8f),                    ForeColor = C_TEXT_MID,  AutoSize = true };

            p.Controls.AddRange(new Control[] { logo, ctrlGroup, avatar, lblWelcome, _lblClock, lblVer });
            // Add background image LAST so it sits at the bottom of the Z-stack
            p.Controls.Add(headerBg);
            p.Resize += (s, e) =>
            {
                // Stretch background to fill header
                headerBg.SetBounds(0, 0, p.Width, p.Height);
                int right = p.Width - 16;
                int textX = right - 220;

                // Position user info and version with a 5px offset and clear spacing
                lblWelcome.Location = new Point(textX, 5);
                _lblClock.Location  = new Point(textX, 28);
                lblVer.Location     = new Point(textX, 48);
                avatar.Location     = new Point(right - 276, 10);
                ctrlGroup.Location  = new Point(right - 520, 0);
            };
            return p;
        }

        // ── Fallback GHT hex logo drawing ──────────────────────────────────────
        void DrawGhtLogo(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath(); path.AddPolygon(HexPts(new PointF(22, 22), 20));
            using var lgb  = new LinearGradientBrush(new Point(0, 0), new Point(44, 44),
                Color.FromArgb(205, 50, 50), Color.FromArgb(235, 130, 30));
            g.FillPath(lgb, path);
            using var wp = new Pen(Color.White, 2f);
            g.DrawLine(wp, 19, 12, 19, 32);
            for (int i = -1; i <= 1; i++) { int ny = 22 + i * 7; g.DrawLine(wp, 19, ny, 28, ny); g.FillEllipse(Brushes.White, 26, ny - 3, 6, 6); }
        }

        // ── Image-based control slot (fan / light / stop) ──────────────────────
        Button MakeImgCtrlSlot(string type, int x, Panel parent)
        {
            var btn = new Button
            {
                Location = new Point(x + 1, 1), Size = new Size(74, 58),
                BackColor = Color.FromArgb(228, 231, 238),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Tag = true
            };
            btn.FlatAppearance.BorderSize = 0;

            Image? imgOn = null, imgOff = null; string lbl = "";
            if (type == "fan")
            {
                // fan_on_l = both on; fan_off__f_l = front off; fan_off__b_l = back off; fan_off_l = both off
                imgOn  = LoadImg("fan_on_l.png");    // loaded for reference; actual shown image computed at paint time
                imgOff = LoadImg("fan_off_l.png");   // both off
                lbl    = "Fan";
            }
            if (type == "light") { imgOn = LoadImg("light_on_l.png"); imgOff = LoadImg("light_off_l.png"); lbl = "Light"; }
            // stop: imgOn = red stop square (machine running), imgOff = green play arrow (machine stopped)
            if (type == "stop")  { imgOn = LoadImg("adm_stop_l.png"); imgOff = LoadImg("adm_start_l.png"); lbl = ""; }

            // Pre-load all fan state images for dynamic switching
            Image? fanBothOn   = (type == "fan") ? LoadImg("fan_on_l.png")    : null;
            Image? fanBothOff  = (type == "fan") ? LoadImg("fan_off_l.png")   : null;
            Image? fanFrontOff = (type == "fan") ? LoadImg("fan_off__f_l.png"): null;
            Image? fanBackOff  = (type == "fan") ? LoadImg("fan_off__b_l.png"): null;

            string ct = type, cl = lbl; Image? con = imgOn, coff = imgOff;
            btn.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(btn.BackColor); g.FillRectangle(bg, 0, 0, btn.Width, btn.Height);

                Image? img;
                if (ct == "fan")
                {
                    // Choose fan image based on front/back state
                    if (_fanFrontOn && _fanBackOn)           img = fanBothOn;
                    else if (!_fanFrontOn && !_fanBackOn)    img = fanBothOff;
                    else if (!_fanFrontOn)                   img = fanFrontOff;
                    else                                     img = fanBackOff;
                }
                else
                {
                    bool isOn = ct == "stop" ? _machineOn : (bool)btn.Tag;
                    img = isOn ? con : coff;
                }

                int sz = 34;
                int ix = (btn.Width - sz) / 2;
                // Center vertically if there is no text label (like the Stop button)
                int iy = string.IsNullOrEmpty(cl) ? (btn.Height - sz) / 2 : 4;

                if (img != null) g.DrawImage(img, ix, iy, sz, sz);
                else { using var fb = new SolidBrush(C_GREEN); g.FillEllipse(fb, ix + 4, iy + 2, sz - 8, sz - 8); }
                using var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(cl, new Font("Segoe UI", 7f), new SolidBrush(C_TEXT_DARK), new RectangleF(0, 45, btn.Width, 14), sf);
            };

            if (type == "stop")
            {
                btn.Click += (s, e) =>
                {
                    _machineOn = !_machineOn;
                    btn.Invalidate();
                    _gearPanel?.Invalidate();

                    if (!_machineOn)
                        ShowStopOverlay();
                    else
                    {
                        HideStopOverlay();
                        // Refresh all controls after hide
                        foreach (Control c in this.Controls) c.Invalidate(true);
                    }
                };
            }
            else if (type == "fan")
                btn.Click += (s, e) => ShowFanPopup(btn);
            else
                btn.Click += (s, e) => { btn.Tag = !(bool)btn.Tag; _lightOn = (bool)btn.Tag; btn.Invalidate(); };

            return btn;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  BEE STOP OVERLAY  — covers everything below the header
        //  No Resume button on overlay — user presses the Stop/Play button
        //  in the header to resume (it becomes a green Play icon when stopped).
        // ═════════════════════════════════════════════════════════════════════
        void ShowStopOverlay()
        {
            if (_stopOverlay != null) return;

            // Find the header panel height so overlay starts below it
            int headerBottom = 0;
            foreach (Control c in this.Controls)
                if (c.Dock == DockStyle.Top)
                    headerBottom = Math.Max(headerBottom, c.Bottom);

            _stopOverlay = new Panel
            {
                Location  = new Point(0, headerBottom),
                Size      = new Size(this.ClientSize.Width, this.ClientSize.Height - headerBottom),
                BackColor = Color.FromArgb(30, 32, 38)
            };

            // Try bee_stop.png
            Image? beeImg = LoadImg("bee_stop.png");

            if (beeImg != null)
            {
                int bw = _stopOverlay.Width / 2;
                int bh = _stopOverlay.Height / 2;
                
                var beePic = new PictureBox
                {
                    SizeMode  = PictureBoxSizeMode.Zoom,
                    Image     = beeImg,
                    BackColor = Color.Transparent,
                    Size      = new Size(bw, bh),
                    Location  = new Point(_stopOverlay.Width, -bh), // Start off-screen top-right
                    Enabled   = false
                };
                _stopOverlay.Controls.Add(beePic);

                // Floating animation timer
                var animTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
                int step = 0;
                const int totalSteps = 45; // ~0.75 seconds
                Point startPos = beePic.Location;

                animTimer.Tick += (s, e) =>
                {
                    if (_stopOverlay == null || beePic.IsDisposed) { animTimer.Stop(); animTimer.Dispose(); return; }

                    step++;
                    float t = (float)step / totalSteps;
                    int targetX = (_stopOverlay.Width - bw) / 2;
                    int targetY = (_stopOverlay.Height - bh) / 2;

                    if (t >= 1.0f) {
                        beePic.Location = new Point(targetX, targetY);
                        animTimer.Stop(); animTimer.Dispose();
                    } else {
                        float ease = 1f - (float)Math.Pow(1f - t, 3); // Cubic ease-out for smooth landing
                        beePic.Left = (int)(startPos.X + (targetX - startPos.X) * ease);
                        beePic.Top  = (int)(startPos.Y + (targetY - startPos.Y) * ease + (Math.Sin(t * 12) * 25 * (1 - t)));
                    }
                };
                animTimer.Start();
            }
            else
            {
                // Fallback — draw stop sign
                _stopOverlay.Paint += DrawStopOverlayContent;
            }

            this.Controls.Add(_stopOverlay);
            _stopOverlay.BringToFront();

            // Keep overlay below the header on form resize
            this.SizeChanged += StopOverlay_FollowHeader;
        }

        void StopOverlay_FollowHeader(object? sender, EventArgs e)
        {
            if (_stopOverlay == null) { this.SizeChanged -= StopOverlay_FollowHeader; return; }
            int hdrBottom = 0;
            foreach (Control c in this.Controls)
                if (c.Dock == DockStyle.Top) hdrBottom = Math.Max(hdrBottom, c.Bottom);
            _stopOverlay.SetBounds(0, hdrBottom, this.ClientSize.Width, this.ClientSize.Height - hdrBottom);

            // Maintain the 50% size and centered position for the stop image on resize
            foreach (Control c in _stopOverlay.Controls)
            {
                if (c is PictureBox pb)
                {
                    int bw = _stopOverlay.Width / 2, bh = _stopOverlay.Height / 2;
                    pb.SetBounds((_stopOverlay.Width - bw) / 2, (_stopOverlay.Height - bh) / 2, bw, bh);
                }
            }
        }

        void HideStopOverlay()
        {
            this.SizeChanged -= StopOverlay_FollowHeader;
            if (_stopOverlay == null) return;
            this.Controls.Remove(_stopOverlay);
            _stopOverlay.Dispose();
            _stopOverlay = null;
        }

        void DrawStopOverlayContent(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel p) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;

            // Dark translucent fill
            using var bgBr = new SolidBrush(Color.FromArgb(200, 20, 20, 20));
            g.FillRectangle(bgBr, 0, 0, p.Width, p.Height);

            int cx = p.Width / 2, cy = p.Height / 2;

            // Large STOP octagon
            float r = 120f;
            var octPts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double ang = Math.PI / 180.0 * (45 * i - 22.5);
                octPts[i] = new PointF(cx + r * (float)Math.Cos(ang), cy + r * (float)Math.Sin(ang));
            }
            using var stopBr  = new SolidBrush(Color.FromArgb(220, 50, 50)); g.FillPolygon(stopBr, octPts);
            using var stopPen = new Pen(Color.White, 6f); g.DrawPolygon(stopPen, octPts);

            // "STOP" text
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("STOP", new Font("Segoe UI", 38f, FontStyle.Bold), Brushes.White,
                new RectangleF(cx - 130, cy - 50, 260, 100), sf);

            // Sub-message
            g.DrawString("Machine has been stopped.\nPress the Play button above to restart.",
                new Font("Segoe UI", 13f), new SolidBrush(Color.FromArgb(210, 210, 210)),
                new RectangleF(cx - 240, cy + 140, 480, 80), sf);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FAN POPUP  — On/Off toggles for Front & Back fans
        // ═════════════════════════════════════════════════════════════════════
        void ShowFanPopup(Button sourceBtn)
        {
            if (_fanPopupHost != null)
            {
                _fanPopupHost.Close(); _fanPopupHost = null; return;
            }

            int popW = 260, popH = 190;
            var pop = new Panel { Size = new Size(popW, popH), BackColor = Color.FromArgb(40, 46, 60) };
            pop.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(85, 90, 110), 1.5f);
                e.Graphics.DrawRectangle(pen, 0, 0, pop.Width - 1, pop.Height - 1);
            };

            var hdrLbl = new Label { Text = "Fan Control", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(14, 12), AutoSize = true };
            pop.Controls.Add(hdrLbl);

            int slotY = 46;
            // Front fan row
            BuildFanToggleRow(pop, "Front", "fan_off__f_l.png", _fanFrontOn, slotY,
                newVal => { _fanFrontOn = newVal; });
            slotY += 76;
            // Back fan row
            BuildFanToggleRow(pop, "Back", "fan_off__b_l.png", _fanBackOn, slotY,
                newVal => { _fanBackOn = newVal; });

            var btnX = new Button { Text = "×", Font = new Font("Segoe UI", 12f), ForeColor = Color.White, BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat, Size = new Size(28, 26), Location = new Point(popW - 32, 4), Cursor = Cursors.Hand };
            btnX.FlatAppearance.BorderSize = 0;
            pop.Controls.Add(btnX);

            var win = new Form
            {
                FormBorderStyle = FormBorderStyle.None, Size = new Size(popW, popH),
                StartPosition = FormStartPosition.Manual, TopMost = true, ShowInTaskbar = false,
                BackColor = Color.FromArgb(40, 46, 60)
            };
            win.Controls.Add(pop); pop.Dock = DockStyle.Fill;
            Point sc = sourceBtn.PointToScreen(new Point(0, sourceBtn.Height + 4));
            win.Location = sc;
            _fanPopupHost = win;
            btnX.Click    += (s, e) => { win.Close(); _fanPopupHost = null; };
            win.Deactivate += (s, e) => { win.Close(); _fanPopupHost = null; };
            win.Show(this);
        }

        void BuildFanToggleRow(Panel container, string fanName, string imgFile, bool currentOn, int y, Action<bool> onChange)
        {
            var lblName = new Label { Text = fanName + " Fan", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(14, y + 4) };

            // ON / OFF labels
            var lblOn  = new Label { Text = "ON",  Font = new Font("Segoe UI", 8.5f), ForeColor = currentOn ? C_GREEN    : C_TEXT_MID, AutoSize = true, Location = new Point(14,  y + 30) };
            var lblOff = new Label { Text = "OFF", Font = new Font("Segoe UI", 8.5f), ForeColor = currentOn ? C_TEXT_MID : C_RED,      AutoSize = true, Location = new Point(54,  y + 30) };

            // Toggle button (drawn as pill switch)
            bool state = currentOn;
            var toggle = new Button
            {
                Size = new Size(60, 26), Location = new Point(100, y + 26),
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, Cursor = Cursors.Hand, Tag = state
            };
            toggle.FlatAppearance.BorderSize = 0;
            toggle.Paint += (s, e) => DrawToggleSwitch(e.Graphics, toggle.ClientRectangle, (bool)toggle.Tag);
            toggle.Click += (s, e) =>
            {
                state = !state; toggle.Tag = state;
                toggle.Invalidate();
                lblOn.ForeColor  = state ? C_GREEN    : C_TEXT_MID;
                lblOff.ForeColor = state ? C_TEXT_MID : C_RED;
                onChange(state);
                // Update the main fan button icon in the header control group
                _btnFanCtrl?.Invalidate();
            };

            container.Controls.AddRange(new Control[] { lblName, lblOn, lblOff, toggle });
        }

        // Draws an iOS-style pill toggle switch
        void DrawToggleSwitch(Graphics g, Rectangle rect, bool isOn)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int r = rect.Height / 2;

            // Track
            using var trackPath = new GraphicsPath();
            trackPath.AddArc(rect.X, rect.Y, rect.Height, rect.Height, 90, 180);
            trackPath.AddArc(rect.Right - rect.Height, rect.Y, rect.Height, rect.Height, 270, 180);
            trackPath.CloseFigure();
            using var trackBr = new SolidBrush(isOn ? Color.FromArgb(62, 175, 80) : Color.FromArgb(100, 105, 120));
            g.FillPath(trackBr, trackPath);

            // Thumb
            int td = rect.Height - 4;
            int tx = isOn ? rect.Right - td - 2 : rect.X + 2;
            using var thumbBr = new SolidBrush(Color.White);
            g.FillEllipse(thumbBr, tx, rect.Y + 2, td, td);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  BODY  — lays out the body panel with footer + main row
        //  KEY LAYOUT CHANGE:
        //    ┌──────────────────────────────────┬────────────────┐
        //    │  Machine State / System Health   │                │
        //    │  (narrow, same width as binPanel)│  Alert Center  │
        //    ├──────────────────────────────────┤  (full height) │
        //    │  Bin Area (cabinet)              │                │
        //    └──────────────────────────────────┴────────────────┘
        // ═════════════════════════════════════════════════════════════════════
        void BuildBody(Panel body)
        {
            body.Padding = new Padding(8, 4, 8, 0);   // reduced padding, no white margins

            var footer = BuildFrame5_Footer();
            body.Controls.Add(footer);

            // Main content row (Fill)
            var row = new Panel { Dock = DockStyle.Fill, BackColor = C_PAGE_BG };
            body.Controls.Add(row);

            // Build all three panels
            var alertPanel = BuildFrame4_AlertCenter();
            var binPanel   = BuildFrame3_BinArea();
            var machBar    = BuildFrame2_MachineBar();   // no longer Dock=Top; lives in left column

            _binPanel   = binPanel;
            _machineBar = machBar;

            // Left column panel  (machine bar stacked above bin area)
            var leftCol = new Panel { BackColor = C_PAGE_BG };

            leftCol.Controls.Add(binPanel);
            leftCol.Controls.Add(machBar);

            // Machine bar sits at top, fixed height
            const int MACHINE_BAR_H = 64;
            leftCol.Resize += (s, e) =>
            {
                int w = leftCol.ClientSize.Width;
                int h = leftCol.ClientSize.Height;
                machBar.SetBounds(0, 0, w, MACHINE_BAR_H);
                binPanel.SetBounds(0, MACHINE_BAR_H + 6, w, h - MACHINE_BAR_H - 6);
            };

            row.Controls.Add(leftCol);
            row.Controls.Add(alertPanel);

            const int ALERT_W = 420;
            const int GAP     = 12;

            row.Resize += (s, e) =>
            {
                int w = row.ClientSize.Width;
                int h = row.ClientSize.Height - 3;
                int binW = w - ALERT_W - GAP;
                leftCol.SetBounds(0,            0, binW,   h);
                alertPanel.SetBounds(binW + GAP, 0, ALERT_W, h);
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FRAME 2 — Machine State / System Health  (inside left column)
        //  No white background box — transparent strip blends with page.
        // ═════════════════════════════════════════════════════════════════════
        Panel BuildFrame2_MachineBar()
        {
            // Outer strip — page background, no border box
            var strip = new Panel { BackColor = C_PAGE_BG };
            // Inner frame — page background (no white rounded box)
            var frame = new Panel { BackColor = C_PAGE_BG };
            // No Paint override — no white rounded rectangle drawn

            // Machine State (left side of frame)
            var lblMSTitle = new Label { Text = "Machine State", Font = new Font("Segoe UI", 8f), ForeColor = C_TEXT_DARK, AutoSize = true, Location = new Point(14, 4) };

            // Static gear image (no rotation per feedback)
            _gearPanel = new Panel { Size = new Size(70, 28), Location = new Point(14, 20), BackColor = Color.Transparent };
            Image? gearRun  = LoadImg("adm_start_status_l.png");
            Image? gearStop = LoadImg("adm_stop_status_l.png");
            _gearPanel.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                Image? img = _machineOn ? gearRun : gearStop;
                if (img == null) { using var pen = new Pen(_machineOn ? C_GREEN : C_RED, 2.5f); g.DrawArc(pen, 4, 4, 22, 22, 0, 180); return; }
                // Draw static (no rotation)
                float sx = (float)_gearPanel!.Width / img.Width;
                float sy = (float)_gearPanel.Height / img.Height;
                float sc = Math.Min(sx, sy);
                int dw = (int)(img.Width * sc), dh = (int)(img.Height * sc);
                int ox = (_gearPanel.Width  - dw) / 2;
                int oy = (_gearPanel.Height - dh) / 2;
                g.DrawImage(img, ox, oy, dw, dh);
            };

            // Vertical divider — subtle on grey background
            var divider = new Panel { Width = 1, BackColor = C_BORDER };

            // System Health (right side)
            var lblSHTitle = new Label { Text = "System Health", Font = new Font("Segoe UI", 8f), ForeColor = C_TEXT_DARK, AutoSize = true };
            var chipMAP    = MakeHealthChip("map_down_l.png",  "map_up_l.png",  false);
            var chipIWEA   = MakeHealthChip("iwea_down_l.png", "iwea_up_l.png", true);
            var chipPLC    = MakeHealthChip("plc_down_l.png",  "plc_up_l.png",  true);

            frame.Controls.AddRange(new Control[] { lblMSTitle, _gearPanel, divider, lblSHTitle, chipMAP, chipIWEA, chipPLC });
            frame.Resize += (s, e) =>
            {
                int mid = frame.Width / 2;
                // Machine State on the LEFT — gear right-aligned within its half
                // Title right-aligned, gear to its left
                int msLabelX = mid - 14 - lblMSTitle.PreferredWidth;
                lblMSTitle.Location         = new Point(Math.Max(14, msLabelX), 4);
                _gearPanel!.Location        = new Point(mid - 14 - 70, 20);
                // Divider in centre
                divider.SetBounds(mid - 1, 6, 2, frame.Height - 12);
                // System Health on the RIGHT
                lblSHTitle.Location = new Point(mid + 14, 4);
                chipMAP.Location    = new Point(mid + 14,  20);
                chipIWEA.Location   = new Point(mid + 140, 20);
                chipPLC.Location    = new Point(mid + 266, 20);
            };

            strip.Controls.Add(frame);
            strip.Resize += (s, e) => frame.SetBounds(0, 4, strip.Width, strip.Height - 4);
            return strip;
        }

        PictureBox MakeHealthChip(string imgDown, string imgUp, bool ok)
        {
            var pic = new PictureBox { Size = new Size(159, 30), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            var img = LoadImg(ok ? imgUp : imgDown); if (img != null) pic.Image = img;
            return pic;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FRAME 5 — Footer  (bottom_border_l.png + ght_logo.png)
        // ═════════════════════════════════════════════════════════════════════
        Panel BuildFrame5_Footer()
        {
            var wrapper = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = C_PAGE_BG };
            Image? borderImg = LoadImg("bottom_border_l.png");
            Image? ghtImg    = LoadImg("ght_logo.png");

            var frame = new Panel { BackColor = C_PAGE_BG };
            frame.Paint += (s, e) =>
            {
                if (borderImg != null) e.Graphics.DrawImage(borderImg, 0, 0, frame.Width, frame.Height);
                // No fallback border line needed — page bg is fine
            };

            var ghtPic = new PictureBox { Size = new Size(72, 38), Location = new Point(10, 5), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            if (ghtImg != null) ghtPic.Image = ghtImg;

            // Version label: LEFT-aligned, just after logo
            var lblVer  = new Label { Text = "ASRS GUI v1.0.00", Font = new Font("Segoe UI", 8f), ForeColor = Color.FromArgb(160, 165, 175), AutoSize = true };
            // Copyright: RIGHT-aligned
            var lblCopy = new Label { Text = "Copyright © 2026 Great Health Technologies Pte Ltd.  All Rights Reserved.", Font = new Font("Segoe UI", 8.5f), ForeColor = C_TEXT_MID, AutoSize = true };

            frame.Controls.AddRange(new Control[] { ghtPic, lblVer, lblCopy });
            frame.Resize += (s, e) =>
            {
                int cy = (frame.Height - lblCopy.PreferredHeight) / 2 + 2;
                lblVer.Location  = new Point(90, cy + 2);                                              // LEFT — next to logo
                lblCopy.Location = new Point(frame.Width - lblCopy.PreferredWidth - 16, cy);           // RIGHT
            };
            wrapper.Controls.Add(frame);
            wrapper.Resize += (s, e) => frame.SetBounds(0, 4, wrapper.Width, wrapper.Height - 4);
            return wrapper;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FRAME 3 — Bin Area  (cabinet chrome using adm_top / adm_bottom)
        //
        //  Z-ORDER FIX:  adm_bottom PictureBox is placed at the very back by
        //  calling Controls.SetChildIndex(..., last) after all adds, so it
        //  never obscures the bin buttons above it.
        // ═════════════════════════════════════════════════════════════════════
        Panel BuildFrame3_BinArea()
        {
            var outer = new Panel { BackColor = C_PAGE_BG };

            // ── Background image layers (drawn behind everything else) ──────────
            // adm_bottom.png: cabinet outer frame + side rails + dark inner area
            var bgPic = new PictureBox { SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.Transparent };
            var admBot = LoadImg("adm_bottom.png"); if (admBot != null) bgPic.Image = admBot;

            // adm_top.png: glossy grey gradient header strip with yellow line
            var topPic = new PictureBox { SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.Transparent };
            var admTop = LoadImg("adm_top.png"); if (admTop != null) topPic.Image = admTop;

            // ── Sub-header: ght_logo + dropdown + stat badges ─────────────────
            // Create gradient background with specified colors
            var subHdr = new Panel { BackColor = Color.Transparent };
            subHdr.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using var brush = new LinearGradientBrush(
                    new Point(0, 0),
                    new Point(0, subHdr.Height),
                    Color.FromArgb(155, 159, 161),  // #9b9fa1 - top color
                    Color.FromArgb(200, 206, 209)  // #c8ced1 - bottom color
                );
                g.FillRectangle(brush, 0, 0, subHdr.Width, subHdr.Height);

                // Add subtle yellow accent line at bottom (matching adm_top.png)
                using var yellowPen = new Pen(Color.FromArgb(100, 220, 180, 50), 1);
                g.DrawLine(yellowPen, 0, subHdr.Height - 1, subHdr.Width, subHdr.Height - 1);
            };

            // Replace the drawn red hexagon with ght_logo.png
            var robotPic = new PictureBox
            {
                Size = new Size(40, 40), Location = new Point(10, 2),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent
            };
            var ghtLogoImg = LoadImg("ght_logo.png");
            if (ghtLogoImg != null) robotPic.Image = ghtLogoImg;

            _cmbView = new ComboBox
            {
                Location = new Point(56, 7), Width = 220,
                Font = new Font("Segoe UI", 8f), DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Standard, BackColor = Color.FromArgb(155, 159, 161)
            };
            _cmbView.Items.AddRange(new object[] { "Front Physical Bin View", "Back Physical Bin View", "Virtual Bin View" });
            _cmbView.SelectedIndex = 0;
            _cmbView.SelectedIndexChanged += (s, e) => RefreshBinGrid();

            int dx = 0;
            var badge1 = MakeStatBadge("0", "Pending Setup", C_BIN_EMPTY, ref dx);
            _lblCountPending = (Label)badge1[0];
            var badge2 = MakeStatBadge("0", "In Operation",  C_GREEN,                       ref dx);
            _lblCountInOp = (Label)badge2[0];
            var badge3 = MakeStatBadge("0", "Disabled",      C_RED,                         ref dx);
            _lblCountDisabled = (Label)badge3[0];

            var badgeGroups = new[] { badge1, badge2, badge3 };
            subHdr.Controls.Add(robotPic); subHdr.Controls.Add(_cmbView);
            foreach (var group in badgeGroups)
                foreach (var c in group) subHdr.Controls.Add(c);

            // ── Dark bin grid panel ────────────────────────────────────────────
            _pnlBinGrid = new Panel { BackColor = C_BIN_DARK, AutoScroll = false, Padding = new Padding(12, 6, 12, 6) };
            _pnlBinGrid.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(75, 79, 92), 1.5f);
                e.Graphics.DrawRectangle(pen, 1, 1, _pnlBinGrid!.Width - 3, _pnlBinGrid.Height - 3);
            };

            // ── Add controls — interactive elements FIRST (higher Z-order) ──────
            // WinForms Z-order: first added = highest; last added = lowest.
            // We add the bin grid and sub-header BEFORE the background images,
            // then call SetChildIndex to push the images to the back.
            outer.Controls.Add(subHdr);       // top of stack (front)
            outer.Controls.Add(_pnlBinGrid);  // behind subHdr
            outer.Controls.Add(topPic);       // behind bin grid
            outer.Controls.Add(bgPic);        // bottom of stack (back)

            // Ensure the background PictureBoxes do NOT capture mouse events
            bgPic.Enabled  = false;
            topPic.Enabled = false;

            // Cabinet geometry — measured from adm_bottom.png (872×453 px):
            //   Left rail  = 54px  → 6.2% of width
            //   Right rail = 57px  → 6.5% of width
            //   Dark area extends essentially to the bottom of adm_bottom.
            //   adm_top.png covers the header strip (HDR_H pixels tall).
            const int HDR_H       = 42;
            const double LEFT_PCT  = 0.062;
            const double RIGHT_PCT = 0.065;
            // BOT_P=50: increased spacing at bottom to prevent bin grid from bleeding into footer
            const int BOT_P       = 50;

            outer.Resize += (s, e) =>
            {
                int pw = outer.Width, ph = outer.Height;
                int lRail = Math.Max(18, (int)(pw * LEFT_PCT));
                int rRail = Math.Max(18, (int)(pw * RIGHT_PCT));

                // Background layers
                bgPic.SetBounds(0, 0, pw, ph);
                topPic.SetBounds(0, 0, pw, HDR_H);

                // Sub-header sits inside the adm_top strip
                subHdr.SetBounds(lRail, 2, pw - lRail - rRail, HDR_H - 2);

                // Position badges to lean right
                int totalBadgeW = 0;
                foreach (var g in badgeGroups) 
                    totalBadgeW += 44 + ((Label)g[1]).PreferredWidth + 20;
                
                int curX = subHdr.Width - totalBadgeW;
                foreach (var g in badgeGroups)
                {
                    g[0].Left = curX;
                    g[1].Left = curX + 44;
                    curX += 44 + ((Label)g[1]).PreferredWidth + 20;
                }

                // Bin grid fills the dark inner area — constrained to adm_bottom's grey box
                _pnlBinGrid!.SetBounds(lRail, HDR_H, pw - lRail - rRail, ph - HDR_H - BOT_P);

                RefreshBinGrid();
            };

            RefreshBinGrid();
            return outer;
        }

        Control[] MakeStatBadge(string count, string label, Color col, ref int x)
        {
            var cir = new Label { Text = count, Size = new Size(40, 22), Location = new Point(x, 9), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.White, BackColor = col, TextAlign = ContentAlignment.MiddleCenter };
            // Use transparent background on the text label so adm_top shows through
            var lbl = new Label { Text = label, AutoSize = true, Location = new Point(x + 44, 13), Font = new Font("Segoe UI", 9f), ForeColor = C_TEXT_DARK, BackColor = Color.Transparent };
            x += 44 + lbl.PreferredWidth + 20;
            return new Control[] { cir, lbl };
        }

        // ── Bin grid rendering ─────────────────────────────────────────────────
        void RefreshBinGrid()
        {
            if (_pnlBinGrid == null) return;
            _pnlBinGrid.SuspendLayout();
            _pnlBinGrid.Controls.Clear();
            DismissHoverPopup(); _clickedBin = null; _isHoverLock = false;
            _cntPending = 0; _cntInOp = 0; _cntDisabled = 0;

            string sel = _cmbView?.SelectedItem?.ToString() ?? "Front Physical Bin View";
            if (sel.StartsWith("Virtual")) RenderVirtualBins();
            else RenderPhysicalBins(sel.StartsWith("Back") ? "B" : "F");

            if (_lblCountPending != null)  _lblCountPending.Text  = _cntPending.ToString();
            if (_lblCountInOp != null)     _lblCountInOp.Text     = _cntInOp.ToString();
            if (_lblCountDisabled != null) _lblCountDisabled.Text = _cntDisabled.ToString();

            _pnlBinGrid.ResumeLayout(true);
        }

        void RenderPhysicalBins(string side)
        {
            var panel = _pnlBinGrid; if (panel == null) return;
            int cols = 10, rows = 9;
            int availW = panel.ClientSize.Width  - panel.Padding.Horizontal;
            int availH = panel.ClientSize.Height - panel.Padding.Vertical;
            int bW = ((availW - 16) / (cols * 2)) - 3;
            int bH = (availH / rows) - 3;
            bW = Math.Max(bW, 38); bH = Math.Max(bH, 24);
            int gX = 3, gY = 3, sX = panel.Padding.Left, sY = panel.Padding.Top;
            string lPfx = "L" + side, rPfx = "R" + side;

            for (int col = 0; col < cols; col++)
                for (int row = 0; row < rows; row++)
                {
                    int num = col * rows + (rows - row);
                    var b = MakeBinBtn(lPfx + num.ToString("D3"), bW, bH);
                    b.Location = new Point(sX + col * (bW + gX), sY + row * (bH + gY));
                    panel.Controls.Add(b);
                }

            int rightX = sX + cols * (bW + gX) + 16;
            for (int col = 0; col < cols; col++)
                for (int row = 0; row < rows; row++)
                {
                    int num = col * rows + (rows - row);
                    var b = MakeBinBtn(rPfx + num.ToString("D3"), bW, bH);
                    b.Location = new Point(rightX + col * (bW + gX), sY + row * (bH + gY));
                    panel.Controls.Add(b);
                }
        }

        void RenderVirtualBins()
        {
            var panel = _pnlBinGrid; if (panel == null) return;
            int cols = 10, rows = 3;
            int availW = panel.ClientSize.Width  - panel.Padding.Horizontal;
            int availH = panel.ClientSize.Height - panel.Padding.Vertical;

            int gX = 8, gY = 10;
            int bW = (availW - (cols - 1) * gX) / cols;
            int bH = (availH - (rows - 1) * gY) / rows;

            int sX = panel.Padding.Left + (availW - (cols * bW + (cols - 1) * gX)) / 2;
            int sY = panel.Padding.Top + (availH - (rows * bH + (rows - 1) * gY)) / 2;

            for (int col = 0; col < cols; col++)
                for (int row = 0; row < rows; row++)
                {
                    int num = col * rows + row + 1; if (num > 30) continue;
                    var b = MakeBinBtn("VT" + num.ToString("D3"), bW, bH);
                    b.Location = new Point(sX + col * (bW + gX), sY + row * (bH + gY));
                    panel.Controls.Add(b);
                }
        }

        Button MakeBinBtn(string label, int w, int h)
        {
            var rng  = new Random(label.GetHashCode() & 0x7fffffff);
            int pick = rng.Next(10);
            Color bg = pick < 4 ? C_RED : pick < 7 ? C_GREEN : C_BIN_EMPTY;
            bool dk  = bg != C_BIN_EMPTY;
            var btn  = new Button
            {
                Text = label, Font = new Font("Segoe UI", w > 50 ? 7.5f : 6.5f),
                ForeColor = dk ? Color.White : C_TEXT_DARK, BackColor = bg,
                FlatStyle = FlatStyle.Flat, Size = new Size(w, h), Cursor = Cursors.Hand,
                Tag = label, TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize  = 1;

            if (bg == C_RED) _cntDisabled++;
            else if (bg == C_GREEN) _cntInOp++;
            else _cntPending++; // Counts light grey (C_BIN_EMPTY) bins

            btn.FlatAppearance.BorderColor = Color.FromArgb(65, 69, 82);
            btn.MouseEnter += (s, e) => BinMouseEnter(btn);
            btn.MouseLeave += (s, e) => BinMouseLeave(btn);
            btn.Click      += (s, e) => BinClick(btn);
            return btn;
        }

        // ── Bin hover / click ──────────────────────────────────────────────────
        void BinMouseEnter(Button btn) 
        { 
            if (btn.BackColor == C_RED) 
            { 
                if (!_isHoverLock) DismissHoverPopup();
                return; 
            }
            if (_isHoverLock && _clickedBin != btn) return; 
            ShowBinDetail(btn); 
        }

        void BinMouseLeave(Button btn)
        {
            if (_isHoverLock && _clickedBin == btn) return;
            var t = new System.Windows.Forms.Timer { Interval = 80 };
            t.Tick += (s, e) =>
            {
                t.Stop(); t.Dispose();
                if (_hoverPopup == null) return;
                if (!_hoverPopup.ClientRectangle.Contains(_hoverPopup.PointToClient(Cursor.Position)) && !_isHoverLock)
                    DismissHoverPopup();
            };
            t.Start();
        }

        void BinClick(Button btn)
        {
            if (btn.BackColor == C_RED) return; // Red bins are non-interactive

            if (_isHoverLock && _clickedBin == btn)
            {
                // Case 1: User clicks the *same* bin that is currently locked/clicked.
                // This means "un-click" it.
                _isHoverLock = false;
                if (_clickedBin != null)
                {
                    _clickedBin.BackColor = _clickedBinOriginalColor; // Restore original color
                    _clickedBin.Invalidate(); // Force redraw
                }
                _clickedBin = null;
                DismissHoverPopup();
            }
            else
            {
                // Case 2: User clicks a *different* bin, or clicks an un-locked bin.
                // First, if there was a previously clicked bin, restore its color.
                if (_clickedBin != null)
                {
                    _clickedBin.BackColor = _clickedBinOriginalColor;
                    _clickedBin.Invalidate(); // Force redraw
                }

                // Now, set the new bin as clicked.
                _isHoverLock = true;
                _clickedBin = btn;
                _clickedBinOriginalColor = btn.BackColor; // Store the original color of the *newly* clicked bin
                btn.BackColor = DarkenColor(btn.BackColor, 15); // Darken the new clicked bin by 15%
                btn.Invalidate(); // Force redraw

                ShowBinDetail(btn);
            }
        }

        void ShowBinDetail(Button btn)
        {
            // Always close any existing popup first
            if (_hoverPopup != null) DismissHoverPopup();
            string id = btn.Tag?.ToString() ?? "";
            string titleText = id;
            string infoText = "";

            if (btn.BackColor == C_GREEN)
            {
                infoText = "[ 60 ] PARACETAMOL 500MG TABLET 10s\r\n\r\n" +
                           "Article ID / Item ID\r\n0004-28-038-G001000A-S\r\n0004-28-038-G\r\n\r\n" +
                           "Batch No / Expiry Date\r\n00BNO49494 (05/11/2026)\r\n\r\n\r\n" +
                           "[ 180 ] PARACETAMOL 500MG TABLET 10s\r\n\r\n" +
                           "Article ID / Item ID\r\n0004-28-038-G001000A-S\r\n0004-28-038-G\r\n\r\n" +
                           "Batch No / Expiry Date\r\n00BNO49499 (25/12/2026)";
            }
            else
            {
                infoText = "No item is setup for this bin.";
            }

            // Use a borderless Form as the popup host so it can float above everything
            // Key fix: DON'T set ShowInTaskbar or wire Deactivate — instead use a timer-based
            // dismiss on mouse-leave so the X button always fires before dismiss.
            int popW = 330, popH = 420;

            var popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                BackColor       = Color.FromArgb(42, 48, 62),
                Size            = new Size(popW, popH),
                StartPosition   = FormStartPosition.Manual,
                TopMost         = true,
                ShowInTaskbar   = false
            };

            // Rounded corners via Region
            using var rp = new GraphicsPath();
            rp.AddArc(0, 0, 12, 12, 180, 90);
            rp.AddArc(popW - 12, 0, 12, 12, 270, 90);
            rp.AddArc(popW - 12, popH - 12, 12, 12, 0, 90);
            rp.AddArc(0, popH - 12, 12, 12, 90, 90);
            rp.CloseFigure();
            popup.Region = new Region(rp);

            bool isLocked = _isHoverLock && _clickedBin == btn;

            popup.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                if (_isHoverLock && _clickedBin == btn)
                    g.FillRectangle(new SolidBrush(C_ACCENT), 0, 0, 5, popH);
                g.DrawRectangle(new Pen(Color.FromArgb(85, 90, 110), 1.5f), 0, 0, popW - 1, popH - 1);
            };

            var lblId   = new Label
            {
                Text = titleText + (_isHoverLock && _clickedBin == btn ? "  📌" : ""),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White, AutoSize = true, Location = new Point(14, 12)
            };
            var lblInfo = new Label
            {
                Text = infoText,
                Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(195, 200, 215),
                Location = new Point(14, 36), Size = new Size(300, 370)
            };

            // X button — explicitly close and clear state, no race with Deactivate
            var btnX = new Button
            {
                Text = "×", Font = new Font("Segoe UI", 13f), ForeColor = Color.White,
                BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 28), Location = new Point(popW - 34, 4),
                Cursor = Cursors.Hand
            };
            btnX.FlatAppearance.BorderSize = 0;
            btnX.Click += (s, e) =>
            {
                _isHoverLock = false;
                _clickedBin  = null;
                DismissHoverPopup();
            };

            popup.Controls.AddRange(new Control[] { lblId, lblInfo, btnX });

            // Mouse-leave dismiss (only when not locked)
            popup.MouseLeave += (s, e) =>
            {
                if (_isHoverLock) return;
                var t = new System.Windows.Forms.Timer { Interval = 80 };
                t.Tick += (ts, te) =>
                {
                    t.Stop(); t.Dispose();
                    if (_hoverPopup == null || _isHoverLock) return;
                    if (!_hoverPopup.ClientRectangle.Contains(_hoverPopup.PointToClient(Cursor.Position)))
                        DismissHoverPopup();
                };
                t.Start();
            };

            // Position below the clicked bin, clamped to screen
            Point sc = btn.PointToScreen(new Point(btn.Width / 2 - popW / 2, btn.Height + 4));
            var   sr = Screen.FromControl(btn).WorkingArea;
            sc.X = Math.Max(sr.Left, Math.Min(sc.X, sr.Right  - popW));
            sc.Y = Math.Max(sr.Top,  Math.Min(sc.Y, sr.Bottom - popH));
            popup.Location = sc;

            _hoverPopup = popup;
            popup.FormClosed += (s, e) => { if (_hoverPopup == popup) _hoverPopup = null; };
            popup.Show(this);  // pass owner so it stays on top of this form
        }

        void DismissHoverPopup()
        {
            var p = _hoverPopup; _hoverPopup = null;
            if (p != null && !p.IsDisposed) try { p.Close(); } catch { }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FRAME 4 — Alert Center  (Alarm + Warning tabs; full height)
        // ═════════════════════════════════════════════════════════════════════
        Panel BuildFrame4_AlertCenter()
        {
            var outer = new Panel { BackColor = C_PAGE_BG };
            outer.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, outer.Width - 1, outer.Height - 1), 8);
                using var br   = new SolidBrush(C_WHITE); g.FillPath(br, path);
                using var pen  = new Pen(C_BORDER, 1f);   g.DrawPath(pen, path);
                using var rp2  = new Pen(C_ACCENT, 3f);   g.DrawLine(rp2, 2, 1, outer.Width - 3, 1);
            };

            var lblTitle = new Label { Text = "Alert Center", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = C_TEXT_DARK, AutoSize = true, Location = new Point(18, 16) };

            var pnlTabs = new Panel { Location = new Point(0, 50), Height = 36, BackColor = C_WHITE };
            pnlTabs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _alertScroll = new Panel { BackColor = C_WHITE, AutoScroll = true };
            _alertScroll.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _warnScroll  = new Panel { BackColor = C_WHITE, AutoScroll = true, Visible = false };
            _warnScroll.Anchor  = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            PopulateAlarms(_alertScroll);
            PopulateWarnings(_warnScroll);

            bool alarmActive = true;
            var btnAlarm = new Button { Text = "Alarm",   Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = C_RED,      BackColor = C_WHITE, FlatStyle = FlatStyle.Flat, Size = new Size(108, 36), Location = new Point(0, 0),   Cursor = Cursors.Hand };
            var btnWarn  = new Button { Text = "Warning", Font = new Font("Segoe UI", 10f),                 ForeColor = C_TEXT_MID, BackColor = C_WHITE, FlatStyle = FlatStyle.Flat, Size = new Size(108, 36), Location = new Point(108, 0), Cursor = Cursors.Hand };
            btnAlarm.FlatAppearance.BorderSize = 0; btnWarn.FlatAppearance.BorderSize = 0;

            btnAlarm.Paint += (s, e) => { if (alarmActive) { using var pen = new Pen(C_RED, 2f); e.Graphics.DrawLine(pen, 6, btnAlarm.Height - 2, btnAlarm.Width - 6, btnAlarm.Height - 2); } };
            btnWarn.Paint  += (s, e) => { if (!alarmActive) { using var pen = new Pen(Color.FromArgb(200, 155, 20), 2f); e.Graphics.DrawLine(pen, 6, btnWarn.Height - 2, btnWarn.Width - 6, btnWarn.Height - 2); } };

            btnAlarm.Click += (s, e) => { alarmActive = true;  _alertScroll!.Visible = true; _warnScroll!.Visible = false; btnAlarm.Font = new Font("Segoe UI", 10f, FontStyle.Bold); btnAlarm.ForeColor = C_RED; btnWarn.Font = new Font("Segoe UI", 10f); btnWarn.ForeColor = C_TEXT_MID; btnAlarm.Invalidate(); btnWarn.Invalidate(); };
            btnWarn.Click  += (s, e) => { alarmActive = false; _alertScroll!.Visible = false; _warnScroll!.Visible = true; btnAlarm.Font = new Font("Segoe UI", 10f); btnAlarm.ForeColor = C_TEXT_MID; btnWarn.Font = new Font("Segoe UI", 10f, FontStyle.Bold); btnWarn.ForeColor = Color.FromArgb(200, 155, 20); btnAlarm.Invalidate(); btnWarn.Invalidate(); };

            var tabSep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = C_BORDER };
            pnlTabs.Controls.AddRange(new Control[] { btnAlarm, btnWarn, tabSep });

            outer.Controls.AddRange(new Control[] { lblTitle, pnlTabs, _alertScroll, _warnScroll });
            outer.Resize += (s, e) =>
            {
                pnlTabs.Width = outer.ClientSize.Width;
                int st = 86, sh = Math.Max(0, outer.ClientSize.Height - st);
                _alertScroll!.SetBounds(0, st, outer.ClientSize.Width, sh);
                _warnScroll!.SetBounds(0,  st, outer.ClientSize.Width, sh);
            };
            return outer;
        }

        void PopulateAlarms(Panel c)
        {
            c.Controls.Clear(); int y = 12;
            y = AddAlarmRow(c, y, "[142] LABEL VACUUM PICK FAIL", "2 time(s). Last known: 04/02/2026 08:21:59",
                new string[,] { { "04/02/2026 08:21:59", "1", "System" }, { "04/02/2026 08:20:44", "1", "System" } },
                "Show Details <<", "Guide to Fix");
            y = AddAlarmRow(c, y, "[932] LABELER EXTEND FAIL", "7 time(s). Last known: 04/02/2026 07:21:59",
                null, "Show Details >>", "Contact Service Engineer");
            y = AddAlarmRow(c, y, "………  ………", "", null, "Show Details >>", "Guide to Fix");
            y = AddAlarmRow(c, y, "………  ………", "", null, "Show Details >>", "Contact Service Engineer");
            c.AutoScrollMinSize = new Size(0, y + 16);
        }

        void PopulateWarnings(Panel c)
        {
            c.Controls.Clear(); int y = 12;
            y = AddWarningRow(c, y, "[W201] LOW STOCK — BIN LF035",   "Remaining stock: 12 units. Threshold: 20 units.",             "04/02/2026 09:15:00");
            y = AddWarningRow(c, y, "[W305] TEMPERATURE ALERT",       "Current: 28.4 °C. Limit: 25 °C. Monitor closely.",            "04/02/2026 08:50:00");
            y = AddWarningRow(c, y, "[W410] EXPIRY APPROACHING",      "AMOXICILLIN 250MG in RF053 — expires 15/03/2026 (43 days).",  "04/02/2026 08:00:00");
            y = AddWarningRow(c, y, "[W512] CONNECTIVITY DEGRADED",   "iWEA packet loss: 4.2%. Signal: -74 dBm.",                    "04/02/2026 07:30:00");
            c.AutoScrollMinSize = new Size(0, y + 16);
        }

        int AddAlarmRow(Panel c, int y, string title, string detail, string[,]? logRows, string detailTxt, string guideTxt)
        {
            int lx = 16;
            var icon = new Panel { Size = new Size(28, 28), Location = new Point(lx, y), BackColor = Color.Transparent };
            icon.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(C_ALARM_ICON); g.FillEllipse(br, 0, 0, 27, 27);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("!", new Font("Segoe UI", 13f, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 28, 28), sf);
            };
            int tx = lx + 36;
            c.Controls.Add(icon);
            c.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = C_TEXT_DARK, AutoSize = true, Location = new Point(tx, y + 2) });
            int ny = y + 24;
            if (!string.IsNullOrEmpty(detail)) { c.Controls.Add(new Label { Text = detail, Font = new Font("Segoe UI", 8.5f), ForeColor = C_TEXT_MID, AutoSize = true, Location = new Point(tx, ny) }); ny += 20; }
            if (logRows != null)
            {
                for (int i = 0; i < logRows.GetLength(0); i++)
                {
                    var row = new Panel { Size = new Size(350, 18), Location = new Point(tx, ny), BackColor = C_WHITE };
                    row.Controls.Add(new Label { Text = logRows[i, 0], Font = new Font("Segoe UI", 8f), ForeColor = C_TEXT_MID, Size = new Size(160, 18), Location = new Point(0, 0) });
                    row.Controls.Add(new Label { Text = logRows[i, 1], Font = new Font("Segoe UI", 8f), ForeColor = C_TEXT_MID, Size = new Size(30, 18),  Location = new Point(162, 0) });
                    row.Controls.Add(new Label { Text = logRows[i, 2], Font = new Font("Segoe UI", 8f), ForeColor = C_TEXT_MID, Size = new Size(70, 18),  Location = new Point(196, 0) });
                    c.Controls.Add(row); ny += 20;
                }
                ny += 4;
            }
            bool red = (guideTxt == "Guide to Fix"); string ct = title;
            var bD = MakeBtn(detailTxt, C_BTN_DETAIL, C_TEXT_DARK, tx, ny + 2, 148, 28);
            var bG = MakeBtn(guideTxt,  red ? C_BTN_GUIDE : C_BTN_DETAIL, red ? Color.White : C_TEXT_DARK, tx + 154, ny + 2, 178, 28);
            bD.Click += (s, e) => MessageBox.Show("Details: " + ct);
            if (red) bG.Click += (s, e) => ShowGuideToFixWindow(ct);
            else     bG.Click += (s, e) => MessageBox.Show("Contact engineer for:\n" + ct, "Contact Service Engineer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            c.Controls.AddRange(new Control[] { bD, bG });
            ny += 36;
            c.Controls.Add(new Panel { Size = new Size(c.Width - lx * 2, 1), Location = new Point(lx, ny + 4), BackColor = C_BORDER });
            return ny + 14;
        }

        int AddWarningRow(Panel c, int y, string title, string detail, string ts)
        {
            int lx = 16;
            var icon = new Panel { Size = new Size(28, 28), Location = new Point(lx, y), BackColor = Color.Transparent };
            icon.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(Color.FromArgb(210, 155, 20)); g.FillEllipse(br, 0, 0, 27, 27);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("!", new Font("Segoe UI", 13f, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 28, 28), sf);
            };
            int tx = lx + 36; string ct = title;
            c.Controls.AddRange(new Control[] { icon,
                new Label { Text = title,  Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = C_TEXT_DARK, AutoSize = true, Location = new Point(tx, y + 2) },
                new Label { Text = detail, Font = new Font("Segoe UI", 8.5f),                  ForeColor = C_TEXT_MID,  AutoSize = true, Location = new Point(tx, y + 24) },
                new Label { Text = ts,     Font = new Font("Segoe UI", 7.5f),                  ForeColor = Color.FromArgb(160, 165, 175), AutoSize = true, Location = new Point(tx, y + 44) }
            });
            int ny = y + 64;
            var bDis = MakeBtn("Dismiss",         C_BTN_DETAIL, C_TEXT_DARK, tx,       ny, 100, 26);
            var bDet = MakeBtn("Show Details >>", C_BTN_DETAIL, C_TEXT_DARK, tx + 106, ny, 140, 26);
            bDis.Click += (s, e) => MessageBox.Show("Warning dismissed:\n" + ct);
            bDet.Click += (s, e) => MessageBox.Show("Details:\n" + ct);
            c.Controls.AddRange(new Control[] { bDis, bDet });
            ny += 34;
            c.Controls.Add(new Panel { Size = new Size(c.Width - lx * 2, 1), Location = new Point(lx, ny + 2), BackColor = C_BORDER });
            return ny + 12;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Guide to Fix popup window
        // ═════════════════════════════════════════════════════════════════════
        void ShowGuideToFixWindow(string alarmTitle)
        {
            var win = new Form { Text = "Guide to Fix", FormBorderStyle = FormBorderStyle.Sizable, StartPosition = FormStartPosition.CenterParent, Size = new Size(720, 620), MinimumSize = new Size(560, 460), BackColor = Color.FromArgb(42, 48, 62), MaximizeBox = true };
            var hdr = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(32, 36, 48) };
            hdr.Paint += (s, e) => { using var pen = new Pen(C_ACCENT, 2.5f); e.Graphics.DrawLine(pen, 0, hdr.Height - 1, hdr.Width, hdr.Height - 1); };
            var hdrIcon = new Panel { Size = new Size(28, 28), Location = new Point(14, 14), BackColor = Color.Transparent };
            hdrIcon.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(C_ACCENT); g.FillEllipse(br, 0, 0, 27, 27);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("?", new Font("Segoe UI", 14f, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 28, 28), sf);
            };
            hdr.Controls.AddRange(new Control[] { hdrIcon,
                new Label { Text = "Guide to Fix", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(50, 10) },
                new Label { Text = alarmTitle, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(160, 168, 185), AutoSize = true, Location = new Point(50, 34) }
            });
            var scroll = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(42, 48, 62), AutoScroll = true, Padding = new Padding(20, 16, 20, 20) };
            int cy = 0, cw = 650;
            var steps = new (string T, string D, bool M, bool V, string ML)[]
            {
                ("Step 1 — Identify the Issue",    "Check the label feeder unit. Ensure the vacuum pick sensor LED is lit green. If red or off, proceed to Step 2.", true, false, "📷  Photo: Label Feeder Unit"),
                ("Step 2 — Inspect the Vacuum Line","Locate the vacuum tube. Inspect for kinks, cracks, or disconnections. Reconnect or replace as needed.",          true, true,  "🎬  Video: Vacuum Line Inspection (2m 14s)"),
                ("Step 3 — Clean the Pick Head",    "Using IPA on a lint-free cloth, wipe the nozzle opening gently. Ensure no debris is blocking it.",               true, false, "📷  Photo: Pick Head Cleaning"),
                ("Step 4 — Test the Sensor",        "Run a vacuum test via Settings → Diagnostics → Vacuum Test. Replace sensor if it fails (Part No. VS-4492-A).",  false, false, ""),
                ("Step 5 — Reset and Verify",       "Reset the alarm, restart the labeler module, and observe 3–5 cycles to confirm resolution.",                     false, false, ""),
            };
            foreach (var (t, d, m, v, ml) in steps)
            {
                scroll.Controls.Add(new Label { Text = t, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.White, Size = new Size(cw, 24), Location = new Point(0, cy) }); cy += 28;
                var ld = new Label { Text = d, Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(195, 200, 215), Size = new Size(cw, 0), Location = new Point(0, cy), AutoSize = false };
                ld.Height = ld.GetPreferredSize(new Size(cw, 0)).Height + 4; scroll.Controls.Add(ld); cy += ld.Height + 8;
                if (m)
                {
                    bool cv = v; string cml = ml;
                    var mb = new Panel { Size = new Size(cw, 160), Location = new Point(0, cy), BackColor = Color.FromArgb(32, 36, 48) };
                    mb.Paint += (s, e) =>
                    {
                        var g2 = e.Graphics; g2.SmoothingMode = SmoothingMode.AntiAlias;
                        using var p2 = new Pen(Color.FromArgb(75, 80, 100), 1.5f); g2.DrawRectangle(p2, 0, 0, mb.Width - 1, mb.Height - 1);
                        using var ib = new SolidBrush(Color.FromArgb(80, 90, 115));
                        if (cv) { PointF[] tri = { new(mb.Width/2f-22,mb.Height/2f-24), new(mb.Width/2f-22,mb.Height/2f+24), new(mb.Width/2f+28,mb.Height/2f) }; g2.FillPolygon(ib, tri); }
                        else g2.FillEllipse(ib, mb.Width/2-28, mb.Height/2-28, 56, 56);
                        using var sf2 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far };
                        g2.DrawString(cml, new Font("Segoe UI", 9f), new SolidBrush(Color.FromArgb(140, 148, 165)), new RectangleF(0, 0, mb.Width, mb.Height - 10), sf2);
                    };
                    scroll.Controls.Add(mb); cy += 168;
                }
                scroll.Controls.Add(new Panel { Size = new Size(cw, 1), Location = new Point(0, cy + 4), BackColor = Color.FromArgb(60, 65, 80) }); cy += 18;
            }
            scroll.AutoScrollMinSize = new Size(0, cy + 40);
            var foot = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.FromArgb(32, 36, 48) };
            foot.Paint += (s, e) => { using var pen = new Pen(Color.FromArgb(60, 65, 80), 1f); e.Graphics.DrawLine(pen, 0, 0, foot.Width, 0); };
            var bCl = MakeBtn("Close",            C_BTN_DETAIL, C_TEXT_DARK, 0, 11, 100, 32);
            var bEn = MakeBtn("Contact Engineer", C_ACCENT,     Color.White, 0, 11, 148, 32);
            bCl.Click += (s, e) => win.Close();
            bEn.Click += (s, e) => MessageBox.Show("Contacting service engineer…", "Service", MessageBoxButtons.OK, MessageBoxIcon.Information);
            foot.Controls.AddRange(new Control[] { bCl, bEn });
            foot.Resize += (s, e) => { int r = foot.Width - 16; bCl.Location = new Point(r - bCl.Width, 11); bEn.Location = new Point(r - bCl.Width - bEn.Width - 8, 11); };
            win.Controls.AddRange(new Control[] { foot, scroll, hdr });
            win.Show(this);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════════
        Button MakeBtn(string text, Color bg, Color fg, int x, int y, int w, int h)
        {
            var b = new Button { Text = text, Font = new Font("Segoe UI", 8.5f), BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Size = new Size(w, h), Location = new Point(x, y), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = C_BORDER; b.FlatAppearance.BorderSize = 1;
            return b;
        }

        static PointF[] HexPts(PointF c, float r)
        {
            var pts = new PointF[6];
            for (int i = 0; i < 6; i++) { double a = Math.PI / 180.0 * (60 * i - 30); pts[i] = new PointF(c.X + r * (float)Math.Cos(a), c.Y + r * (float)Math.Sin(a)); }
            return pts;
        }

        /// <summary>
        /// Returns a darker version of the specified color.
        /// </summary>
        /// <param name="color">The original color.</param>
        /// <param name="percent">The percentage to darken (e.g., 15 for 15%).</param>
        /// <returns>A new Color instance that is darker than the original.</returns>
        Color DarkenColor(Color color, int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent)); // Clamp percent between 0 and 100
            float factor = 1f - (float)percent / 100f;
            return Color.FromArgb(color.A, (int)(color.R * factor), (int)(color.G * factor), (int)(color.B * factor));
        }


        static GraphicsPath RoundedRect(Rectangle r, int rad)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            path.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            path.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            path.CloseFigure(); return path;
        }
    }
}
