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
        static readonly Color C_BIN_EMPTY  = Color.FromArgb(230, 235, 240);
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
            var logo = new Panel { Size = new Size(60, 60), Location = new Point(14, 10), BackColor = Color.Transparent };
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
            // ── Fan/Light shared container ──────────────────────────────────
            var flGroup = new Panel { Size = new Size(204, 55), BackColor = Color.FromArgb(228, 231, 238) };
            flGroup.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, flGroup.Width - 1, flGroup.Height - 1), 8);
                using var pen = new Pen(C_BORDER, 1);
                g.DrawPath(pen, path);
                // Vertical separator line between Fan and Light buttons
                g.DrawLine(pen, 102, 4, 102, flGroup.Height - 4);
            };
            using (var path = RoundedRect(new Rectangle(0, 0, 204, 55), 8)) flGroup.Region = new Region(path);

            var btnFan   = MakeImgCtrlSlot("fan",   0,   flGroup);
            _btnFanCtrl  = btnFan;   // store ref so fan popup can update the icon
            var btnLight = MakeImgCtrlSlot("light", 102, flGroup);
            flGroup.Controls.AddRange(new Control[] { btnFan, btnLight });

            // ── Stop container (separate) ───────────────────────────────────
            var stopGroup = new Panel { Size = new Size(55, 55), BackColor = Color.FromArgb(228, 231, 238) };
            stopGroup.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, stopGroup.Width - 1, stopGroup.Height - 1), 8);
                using var pen = new Pen(C_BORDER, 1);
                g.DrawPath(pen, path);
            };
            using (var path = RoundedRect(new Rectangle(0, 0, 55, 55), 8)) stopGroup.Region = new Region(path);

            var btnStop = MakeImgCtrlSlot("stop", 0, stopGroup);
            stopGroup.Controls.Add(btnStop);

            // ── Divider between Stop and Avatar ──────────────────────────────
            var stopAvatarSep = new Panel { Size = new Size(1, 44), BackColor = C_BORDER };

            // ── Avatar + user block ───────────────────────────────────────────
            var avatar = new Panel { Size = new Size(52, 52), BackColor = Color.Transparent };
            avatar.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(Color.FromArgb(65, 125, 195));
                g.FillEllipse(br, 0, 0, 51, 51);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("GU", new Font("Segoe UI", 15f, FontStyle.Bold), Brushes.White, new RectangleF(0, 0, 52, 52), sf);
            };
            var lblWelcome = new Label { Text = "Welcome",                                      Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = C_TEXT_DARK, AutoSize = true };
            _lblClock      = new Label { Text = DateTime.Now.ToString("d MMM yyyy   HH:mm:ss"), Font = new Font("Segoe UI", 8.5f),                  ForeColor = C_TEXT_MID,  AutoSize = true };
            var lblVer     = new Label { Text = "ASRS (GUI v1.0.00)",                           Font = new Font("Segoe UI", 8f),                    ForeColor = C_TEXT_MID,  AutoSize = true };

            p.Controls.AddRange(new Control[] { logo, flGroup, stopGroup, stopAvatarSep, avatar, lblWelcome, _lblClock, lblVer });
            // Add background image LAST so it sits at the bottom of the Z-stack
            p.Controls.Add(headerBg);
            p.Resize += (s, e) =>
            {
                // Stretch background to fill header
                headerBg.SetBounds(0, 0, p.Width, p.Height);
                int right = p.Width - 10;
                int textX = right - 180;

                // Position user info and version with a 5px offset and clear spacing
                lblWelcome.Location = new Point(textX, 0);
                _lblClock.Location  = new Point(textX, 22);
                lblVer.Location     = new Point(textX, 40);
                avatar.Location     = new Point(right - 244, 4);
                stopAvatarSep.Location = new Point(right - 273, 8);
                flGroup.Location    = new Point(right - 572, 2);
                stopGroup.Location  = new Point(right - 572 + flGroup.Width + 12, 2);
            };
            return p;
        }

        // ── Fallback GHT hex logo drawing ──────────────────────────────────────
        void DrawGhtLogo(object? sender, PaintEventArgs e)
        {
            if (sender is not Control c) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = c.Width, h = c.Height, cx = w / 2f, cy = h / 2f, r = Math.Min(cx, cy) - 2;
            using var path = new GraphicsPath(); path.AddPolygon(HexPts(new PointF(cx, cy), r));
            using var lgb  = new LinearGradientBrush(new Point(0, 0), new Point((int)w, (int)h),
                Color.FromArgb(205, 50, 50), Color.FromArgb(235, 130, 30));
            g.FillPath(lgb, path);
            float s = w / 44f;
            using var wp = new Pen(Color.White, 2f * s);
            g.DrawLine(wp, 19 * s, 12 * s, 19 * s, 32 * s);
            for (int i = -1; i <= 1; i++) { float ny = 22 * s + i * 7 * s; g.DrawLine(wp, 19 * s, ny, 28 * s, ny); g.FillEllipse(Brushes.White, 26 * s, ny - 3 * s, 6 * s, 6 * s); }
        }

        // ── Image-based control slot (fan / light / stop) ──────────────────────
        Button MakeImgCtrlSlot(string type, int x, Panel parent)
        {
            var btn = new Button
            {
                Location = new Point(x + 1, 1), Size = new Size(type == "stop" ? 53 : 100, 53),
                BackColor = Color.FromArgb(228, 231, 238),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Tag = true
            };
            Image? swImg = LoadImg("switch.png");
            btn.FlatAppearance.BorderSize = 0;

            Image? imgOn = null, imgOff = null; string lbl = "";
            if (type == "fan")
            {
                // fan_on_l = both on; fan_off__f_l = front off; fan_off__b_l = back off; fan_off_l = both off
                imgOn  = LoadImg("fan_on_l.png");    // loaded for reference; actual shown image computed at paint time
                imgOff = LoadImg("fan_off_l.png");   // both off
                lbl    = "";
            }
            if (type == "light") { imgOn = LoadImg("light_on_l.png"); imgOff = LoadImg("light_off_l.png"); lbl = ""; }
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

                int sz = (type == "stop") ? 35 : 48;
                Image? img = null;
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
                
                int iy = (btn.Height - sz) / 2;
                if (ct == "stop")
                {
                    int ix = (btn.Width - sz) / 2;
                    if (img != null) g.DrawImage(img, ix, iy, sz, sz);
                    else { using var fb = new SolidBrush(C_GREEN); g.FillEllipse(fb, ix + 4, iy + 4, sz - 8, sz - 8); }
                }
                else
                {
                    // Rectangular switch logic (vertical: tall height, short width)
                    int swH = sz;
                    int swW = 20;
                    int swX = (ct == "fan") ? 8 : 12; // Spacing from left edge/divider
                    int swY = (btn.Height - swH) / 2;
                    if (swImg != null) g.DrawImage(swImg, swX, swY, swW, swH);

                    int ix = swX + swW + 10; // Spacing between switch and icon
                    if (img != null) g.DrawImage(img, ix, iy, sz, sz);
                    else { using var fb = new SolidBrush(C_GREEN); g.FillEllipse(fb, ix + 4, iy + 4, sz - 8, sz - 8); }
                }
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
            else
            {
                btn.MouseDown += (s, e) =>
                {
                    if (e.X > 42) return; // Trigger interaction if clicking on the vertical switch area
                    if (type == "fan") ShowFanPopup(btn);
                    else { btn.Tag = !(bool)btn.Tag; _lightOn = (bool)btn.Tag; btn.Invalidate(); }
                };
            }

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
            const int MACHINE_BAR_H = 50;
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
            _gearPanel = new Panel { Size = new Size(120, 32), Location = new Point(14, 14), BackColor = Color.Transparent };
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

            // System Health (right side)
            var lblSHTitle = new Label { Text = "System Health", Font = new Font("Segoe UI", 8f), ForeColor = C_TEXT_DARK, AutoSize = true };
            var chipMAP    = MakeHealthChip("map_down_l.png",  "map_up_l.png",  false);
            var chipIWEA   = MakeHealthChip("iwea_down_l.png", "iwea_up_l.png", true);
            var chipPLC    = MakeHealthChip("plc_down_l.png",  "plc_up_l.png",  true);

            frame.Controls.AddRange(new Control[] { lblMSTitle, _gearPanel, lblSHTitle, chipMAP, chipIWEA, chipPLC });
            frame.Resize += (s, e) =>
            {
                // Shift Machine State closer to the System Health cluster
                // Calculated to be 40px to the left of where the health section begins (frame.Width - 378)
                int gearX = frame.Width - 378 - 40 - 120;
                _gearPanel!.Location = new Point(gearX, 14);
                lblMSTitle.Location = new Point(gearX + (_gearPanel.Width / 2) - (lblMSTitle.PreferredWidth / 2), 2);
                // System Health on the RIGHT
                const int cW = 120, gap = 2;
                int totalW = (3 * cW) + (2 * gap);
                int sX = frame.Width - totalW - 14; // Right-align the chips cluster to the frame edge
                chipMAP.Location    = new Point(sX, 14);
                chipIWEA.Location   = new Point(sX + cW + gap, 14);
                chipPLC.Location    = new Point(sX + 2 * (cW + gap), 14);
                lblSHTitle.Location = new Point(sX + (totalW / 2) - (lblSHTitle.PreferredWidth / 2), 2);
            };

            strip.Controls.Add(frame);
            strip.Resize += (s, e) => frame.SetBounds(0, 4, strip.Width, strip.Height - 4);
            return strip;
        }

        PictureBox MakeHealthChip(string imgDown, string imgUp, bool ok)
        {
            var pic = new PictureBox { Size = new Size(120, 32), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
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
            // Re-parent subHdr to topPic to enable true transparency against the image
            topPic.Controls.Add(subHdr);

            // Replace the drawn red hexagon with ght_logo.png
            var robotPic = new PictureBox
            {
                Size = new Size(50, 50), Location = new Point(10, 5),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent
            };
            var ghtLogoImg = LoadImg("ght_logo.png");
            if (ghtLogoImg != null) robotPic.Image = ghtLogoImg;

            _cmbView = new ComboBox
            {
                Location = new Point(70, 18), Width = 210,
                Font = new Font("Segoe UI", 11f), DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Standard, BackColor = Color.FromArgb(155, 159, 161)
            };
            _cmbView.Items.AddRange(new object[] { "Front Physical Bin View", "Back Physical Bin View", "Virtual Bin View" });
            _cmbView.SelectedIndex = 0;
            _cmbView.SelectedIndexChanged += (s, e) => { RefreshBinGrid(); outer.PerformLayout(); };

            int dx = 0;
            var badge1 = MakeStatBadge("0", "In Pending",    C_BIN_EMPTY, ref dx);
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
            outer.Controls.Add(_pnlBinGrid);  // behind subHdr
            outer.Controls.Add(topPic);       // behind bin grid
            outer.Controls.Add(bgPic);        // bottom of stack (back)

            // Ensure the background PictureBoxes do NOT capture mouse events
            bgPic.Enabled  = false;
            topPic.Enabled = true; // Must be enabled for children (subHdr) to receive input

            // Cabinet geometry — measured from adm_bottom.png (872×453 px):
            //   Left rail  = 54px  → 6.2% of width
            //   Right rail = 57px  → 6.5% of width
            //   Dark area extends essentially to the bottom of adm_bottom.
            //   adm_top.png covers the header strip (HDR_H pixels tall).
            const int HDR_H       = 70;
            const double LEFT_PCT  = 0.062;
            const double RIGHT_PCT = 0.065;
            // BOT_P=50: increased spacing at bottom to prevent bin grid from bleeding into footer
            const int BOT_P       = 50;

            outer.Layout += (s, e) =>
            {
                int pw = outer.Width, ph = outer.Height;
                int lRail = Math.Max(18, (int)(pw * LEFT_PCT));
                int rRail = Math.Max(18, (int)(pw * RIGHT_PCT));

                // Background layers
                bgPic.SetBounds(0, 0, pw, ph);
                topPic.SetBounds(0, 0, pw, HDR_H);

                // Sub-header sits inside the adm_top strip
                subHdr.SetBounds(20, 0, topPic.Width - 40, topPic.Height);

                // Position badges to lean right
                bool isVirt = _cmbView?.SelectedIndex == 2;
                badge3[0].Visible = badge3[1].Visible = !isVirt;
                var currentBadges = isVirt ? new[] { badge1, badge2 } : badgeGroups;

                int totalBadgeW = 0;
                foreach (var g in currentBadges) 
                    totalBadgeW += 40 + ((Label)g[1]).PreferredWidth + 100;
                
                int curX = subHdr.Width - totalBadgeW;
                foreach (var g in currentBadges)
                {
                    g[0].Left = curX;
                    g[1].Left = curX + 40;
                    curX += 40 + ((Label)g[1]).PreferredWidth + 100;
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
            var cir = new Label { Text = count, Size = new Size(32, 32), Location = new Point(x, 15), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter };
            cir.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(col);
                g.FillEllipse(br, 0, 0, 31, 31);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                var txtBrush = (col == C_BIN_EMPTY) ? Brushes.Black : Brushes.White;
                g.DrawString(cir.Text, cir.Font, txtBrush, new RectangleF(0, 0, 32, 32), sf);
            };

            // Use transparent background on the text label so adm_top shows through
            var lbl = new Label { Text = label, AutoSize = true, Location = new Point(x + 40, 21), Font = new Font("Segoe UI", 9.5f), ForeColor = C_TEXT_DARK, BackColor = Color.Transparent };
            x += 40 + lbl.PreferredWidth + 100;
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
            Color bg = C_BIN_EMPTY;

            // "Easter Egg": Arrange green bins to spell "G H T" across physical bin grids
            if (label.Length >= 5 && (label.StartsWith("L") || label.StartsWith("R")))
            {
                int num = int.Parse(label.Substring(2));
                int c = (num - 1) / 9, r = 9 - ((num - 1) % 9 + 1);
                int gc = label.StartsWith("R") ? c + 10 : c;

                bool isG = (gc == 1 && r >= 1 && r <= 7) || (r == 1 && gc >= 1 && gc <= 5) || (r == 7 && gc >= 1 && gc <= 5) || (gc == 5 && r >= 5 && r <= 7) || (r == 5 && gc >= 3 && gc <= 5);
                bool isH = (gc == 8 && r >= 1 && r <= 7) || (gc == 11 && r >= 1 && r <= 7) || (r == 4 && gc >= 8 && gc <= 11);
                bool isT = (r == 1 && gc >= 14 && gc <= 18) || (gc == 16 && r >= 1 && r <= 7);

                if (isG || isH || isT) bg = C_GREEN;
                else bg = rng.Next(100) < 8 ? C_RED : C_BIN_EMPTY; // Reduced red noise for clarity
            }
            else if (label.StartsWith("VT"))
            {
                int pick = rng.Next(10);
                bg = pick < 6 ? C_GREEN : C_BIN_EMPTY; // No red for virtual bins
            }
            else
            {
                int pick = rng.Next(10);
                bg = pick < 2 ? C_RED : pick < 6 ? C_GREEN : C_BIN_EMPTY;
            }

            bool dk  = bg != C_BIN_EMPTY;
            var btn  = new Button
            {
                Text = label, Font = new Font("Segoe UI", w > 50 ? 7.5f : 6.5f),
                ForeColor = dk ? Color.White : C_TEXT_DARK, BackColor = bg,
                FlatStyle = FlatStyle.Flat, Size = new Size(w, h), Cursor = Cursors.Hand,
                Tag = (label, bg),   // store both label AND original colour
                TextAlign = ContentAlignment.MiddleCenter
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
                // Read original colour from tag tuple
                var (_, origColor) = ((string, Color))btn.Tag!;
                _clickedBinOriginalColor = origColor;                
                btn.BackColor = DarkenColor(btn.BackColor, 15); // Darken the new clicked bin by 15%
                btn.Invalidate(); // Force redraw

                ShowBinDetail(btn);
            }
        }

        void ShowBinDetail(Button btn)
        {
            // Always close any existing popup first
            if (_hoverPopup != null) DismissHoverPopup();
            var (binId, origColor) = btn.Tag is (string s, Color c) ? (s, c) : (btn.Tag?.ToString() ?? "", C_BIN_EMPTY);
            
            string infoText = "";
            int popW = 330, popH = 350;

            if (origColor == C_GREEN)
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
                popH = 100; // Smaller height for Pending Setup
            }

            // Use a borderless Form as the popup host so it can float above everything
            // Key fix: DON'T set ShowInTaskbar or wire Deactivate — instead use a timer-based
            // dismiss on mouse-leave so the X button always fires before dismiss.
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
                Text = binId + (_isHoverLock && _clickedBin == btn ? "  📌" : ""),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White, AutoSize = true, Location = new Point(14, 12)
            };
            var lblInfo = new Label
            {
                Text = infoText,
                Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(195, 200, 215),
                Location = new Point(14, 36), Size = new Size(300, popH - 50)
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
            using var popup = new RobotSafetyDoorPopup();
            popup.ShowDialog(this);
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

    /// <summary>
    /// Popup dialog for alarm "Problem: 111 – SHUT AND RESET ROBOT SAFETY DOOR".
    /// </summary>
    public class RobotSafetyDoorPopup : Form
    {
        static readonly Color C_HEADER_BG  = Color.FromArgb(55, 58, 68);
        static readonly Color C_BODY_BG    = Color.FromArgb(245, 246, 248);
        static readonly Color C_BTN_BG     = Color.FromArgb(230, 232, 237);
        static readonly Color C_BTN_BORDER = Color.FromArgb(190, 195, 205);
        static readonly Color C_TEXT_DARK  = Color.FromArgb(38, 42, 52);
        static readonly Color C_TEXT_MID   = Color.FromArgb(90, 98, 115);
        static readonly Color C_RED        = Color.FromArgb(200, 50, 50);
        static readonly Color C_STEP_NUM   = Color.FromArgb(38, 42, 52);
        static readonly Color C_DIAGRAM_BG = Color.FromArgb(250, 250, 252);

        public RobotSafetyDoorPopup()
        {
            this.Text            = "Problem 111 – Robot Safety Door";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition   = FormStartPosition.CenterParent;
            this.Size            = new Size(770, 600);
            this.BackColor       = C_BODY_BG;
            this.MinimumSize     = new Size(600, 480);

            // Apply rounded corners
            this.Load += (s, e) => ApplyRoundedCorners();

            this.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.FromArgb(100, 105, 120), 2f);
                
                // Draw rounded border
                int r = 12;
                using var path = new GraphicsPath();
                path.AddArc(0, 0, r, r, 180, 90);
                path.AddArc(this.Width - r - 1, 0, r, r, 270, 90);
                path.AddArc(this.Width - r - 1, this.Height - r - 1, r, r, 0, 90);
                path.AddArc(0, this.Height - r - 1, r, r, 90, 90);
                path.CloseFigure();
                g.DrawPath(pen, path);
            };
            Build();
        }

        void Build()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = C_HEADER_BG };
            var lblTitle = new Label {
                Text      = "Problem: 111 \u2013 SHUT AND RESET ROBOT SAFETY DOOR",
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0)
            };
            header.Controls.Add(lblTitle);

            var btnBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = C_BODY_BG, Padding = new Padding(12, 8, 12, 0) };
            btnBar.Paint += (s, e) => {
                using var pen = new Pen(Color.FromArgb(210, 214, 222), 1f);
                e.Graphics.DrawLine(pen, 0, btnBar.Height - 1, btnBar.Width, btnBar.Height - 1);
            };

            var btnContinue = MakeActionButton("Continue", "\u25BA", Color.FromArgb(240, 248, 240), Color.FromArgb(40, 160, 70));
            var btnIgnore   = MakeActionButton("Ignore",   "\u00D8", C_BTN_BG, C_TEXT_MID);
            var btnAbort    = MakeActionButton("Abort",    "\u2715", C_BTN_BG, C_TEXT_MID);
            var btnClose    = MakeActionButton("Close",    "\u25B6", C_BTN_BG, C_TEXT_MID);

            btnClose.Text = "";
            btnClose.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                DrawRunningPerson(g, new Rectangle(8, 4, 22, 26), C_TEXT_MID);
                using var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                using var tb = new SolidBrush(C_TEXT_DARK);
                g.DrawString("Close", new Font("Segoe UI", 9f), tb, new RectangleF(0, 0, btnClose.Width - 4, btnClose.Height), sf);
            };

            // Only the Close button works; others are disabled for the mockup
            btnClose.Click    += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            int bx = 12;
            foreach (var b in new[] { btnContinue, btnIgnore, btnAbort, btnClose }) {
                b.Location = new Point(bx, 8); btnBar.Controls.Add(b); bx += b.Width + 6;
            }

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = C_BODY_BG, Padding = new Padding(20, 16, 20, 20) };
            int y = 0;
            int instructionW = 550;

            // Helper to add instruction text
            Action<string, int> AddInstruction = (text, top) => {
                var lbl = new Label {
                    Text = text,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                    ForeColor = C_TEXT_MID,
                    Location = new Point(24, top),
                    Width = instructionW,
                    AutoSize = true,
                    MaximumSize = new Size(instructionW, 0)
                };
                scroll.Controls.Add(lbl);
            };

            var step1Label = MakeStepLabel(1, "Close the Robot safety door(s)");
            step1Label.Location = new Point(0, y); scroll.Controls.Add(step1Label);
            y += step1Label.PreferredHeight + 8;

            var diagram1 = new Panel { Location = new Point(20, y), Size = new Size(310, 200), BackColor = C_DIAGRAM_BG };
            diagram1.Paint += DrawDoorDiagram; scroll.Controls.Add(diagram1);
            
            AddInstruction("1. Ensure all three physical latches at the rear are engaged. The indicator on the main HMI panel should turn from flashing red to solid yellow once secured.", y + 205);
            
            y += diagram1.Height + 24;
            y += 30; // space for instruction

            var step2Label = MakeStepLabel(2, "Press the RESET button to reset to normal safety condition");
            step2Label.Location = new Point(0, y); scroll.Controls.Add(step2Label);
            y += step2Label.PreferredHeight + 8;

            var diagram2 = new Panel { Location = new Point(20, y), Size = new Size(340, 90), BackColor = C_DIAGRAM_BG };
            diagram2.Paint += DrawButtonPanel; scroll.Controls.Add(diagram2);
            
            AddInstruction("2. The green RESET button will illuminate when the safety circuit is ready. Press and hold for 2 seconds until the machine status updates to 'Standby'.", y + 95);

            y += diagram2.Height + 20;
            y += 30; // space for instruction

            scroll.AutoScrollMinSize = new Size(0, y + 30);
            this.Controls.Add(scroll); this.Controls.Add(btnBar); this.Controls.Add(header);
        }

        void ApplyRoundedCorners()
        {
            int r = 12;
            using var path = new GraphicsPath();
            path.AddArc(0, 0, r, r, 180, 90);
            path.AddArc(this.Width - r, 0, r, r, 270, 90);
            path.AddArc(this.Width - r, this.Height - r, r, r, 0, 90);
            path.AddArc(0, this.Height - r, r, r, 90, 90);
            path.CloseFigure();
            this.Region = new Region(path);
        }

        Button MakeActionButton(string text, string icon, Color bg, Color iconColor)
        {
            var btn = new Button {
                Text = $"  {icon}  {text}", Font = new Font("Segoe UI", 9f), ForeColor = C_TEXT_DARK,
                BackColor = bg, FlatStyle = FlatStyle.Flat, Size = new Size(110, 32), Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft
            };
            btn.FlatAppearance.BorderColor = C_BTN_BORDER;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Min(255, bg.R + 15), Math.Min(255, bg.G + 15), Math.Min(255, bg.B + 15));
            return btn;
        }

        Label MakeStepLabel(int stepNum, string text)
        {
            var lbl = new Label { AutoSize = true, MaximumSize = new Size(700, 0), Font = new Font("Segoe UI", 10f), ForeColor = C_TEXT_DARK };
            lbl.Paint += (s, e) => {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bf = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var nf = new Font("Segoe UI", 10f);
                using var br = new SolidBrush(C_STEP_NUM);
                string num = $"{stepNum}.   ";
                g.DrawString(num, bf, br, 0, 0);
                float w = g.MeasureString(num, bf).Width;
                g.DrawString(text, nf, br, w, 0);
            };
            using var g2 = lbl.CreateGraphics();
            using var bf2 = new Font("Segoe UI", 10f, FontStyle.Bold);
            using var nf2 = new Font("Segoe UI", 10f);
            string n = $"{stepNum}.   ";
            float nw = g2.MeasureString(n, bf2).Width;
            SizeF sz = g2.MeasureString(text, nf2, (int)(700 - nw));
            lbl.Size = new Size((int)(nw + sz.Width + 4), (int)sz.Height + 4); lbl.Text = "";
            return lbl;
        }

        void DrawDoorDiagram(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel p) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var bgBr = new SolidBrush(C_DIAGRAM_BG); g.FillRectangle(bgBr, 0, 0, p.Width, p.Height);
            using var redPen = new Pen(C_RED, 1.5f); using var darkPen = new Pen(Color.FromArgb(60, 65, 80), 1.2f);
            using var thinPen = new Pen(Color.FromArgb(130, 135, 148), 0.8f);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var darkBrush = new SolidBrush(C_TEXT_DARK);

            int padX = 30, innerL = padX, innerR = p.Width - padX, innerW = innerR - innerL;
            int doorY = 50, doorH = 30;
            int[] dxs = { innerL + innerW / 6, innerL + innerW / 2, innerL + 5 * innerW / 6 };

            var lblR = new RectangleF(innerR - 85, 10, 80, 32);
            using var lblBr = new SolidBrush(Color.FromArgb(245, 246, 248)); g.FillRectangle(lblBr, lblR);
            using var lblP = new Pen(C_RED, 1f); g.DrawRectangle(lblP, lblR.X, lblR.Y, lblR.Width, lblR.Height);
            using var lblF = new Font("Segoe UI", 6f, FontStyle.Bold);
            using var redB = new SolidBrush(C_RED); g.DrawString("ROBOT SAFETY\nDOORS (X3)", lblF, redB, lblR, sf);

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
                int cx = pR.X + bx, cy = pR.Y + pR.Height / 2, r = name == "EMO" ? 14 : 10;
                if (name == "RESET") {
                    using var cP = new Pen(C_RED, 1.5f); g.DrawRectangle(cP, cx - r - 3, cy - r - 3, r * 2 + 6, r * 2 + 6);
                }
                using var bB = new SolidBrush(col); g.FillEllipse(bB, cx - r, cy - r, r * 2, r * 2);
                using var gl = new LinearGradientBrush(new PointF(cx - r, cy - r), new PointF(cx, cy + r / 2), Color.FromArgb(80, 255, 255, 255), Color.FromArgb(0, 255, 255, 255));
                g.FillEllipse(gl, cx - r, cy - r, r * 2, r);
                using var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(name, lF, wB, new RectangleF(cx - 22, pR.Bottom + 2, 44, 12), sf);
            }
            int rcx = pR.X + 88, rcy = pR.Y + pR.Height / 2, asx = rcx + 14, aex = pR.Right + 30;
            using var aP = new Pen(C_RED, 1.5f); g.DrawLine(aP, asx, rcy, aex, rcy);
            g.FillPolygon(new SolidBrush(C_RED), new PointF[] { new(aex, rcy), new(aex - 7, rcy - 4), new(aex - 7, rcy + 4) });
            var tB = new RectangleF(aex + 4, rcy - 14, 80, 28);
            using var tbB = new SolidBrush(Color.FromArgb(245, 246, 248)); g.FillRectangle(tbB, tB);
            using var tbP = new Pen(C_RED, 1f); g.DrawRectangle(tbP, tB.X, tB.Y, tB.Width, tB.Height);
            using var tbF = new Font("Segoe UI", 7.5f, FontStyle.Bold); using var tbT = new SolidBrush(C_TEXT_DARK);
            using var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("RESET\nBUTTON", tbF, tbT, tB, sfC);
        }

        static void DrawRunningPerson(Graphics g, Rectangle r, Color col)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(col, 1.5f); using var br = new SolidBrush(col);
            float cx = r.X + r.Width / 2f, top = r.Y;
            g.FillEllipse(br, cx - 4, top, 8, 8);
            g.DrawLine(pen, cx, top + 8, cx - 3, top + 17);
            g.DrawLine(pen, cx, top + 12, cx - 7, top + 9);
            g.DrawLine(pen, cx, top + 12, cx + 5, top + 15);
            g.DrawLine(pen, cx - 3, top + 17, cx + 4, top + 24);
            g.DrawLine(pen, cx - 3, top + 17, cx - 8, top + 24);
        }
    }
}
