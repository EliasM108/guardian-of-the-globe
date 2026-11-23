using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Gotg
{
    public class HomeMenu : Control
    {
        private enum View { Menu, Settings, Build, Done, Choose, Confirm }

        private class Item
        {
            public string Id, Text, Hint, Value;
            public int Glyph = -1;
            public int Kind;
            public bool Big;
            public bool Enabled = true;
            public bool On;
            public int Index;
            public string[] Options;
            public RectangleF Rect;
            public float Hover, Press, Anim;
            public GraphicsPath Path;
        }
        private class Spark { public float X, Y, Speed, Size, Phase; }

        public event Action<string> ButtonClicked;
        public event Action<string, string> SettingChanged;

        private View view = View.Menu;
        private List<Item> items = new List<Item>();
        private List<string> games = new List<string>();
        private List<Spark> sparks = new List<Spark>();
        private Random rnd = new Random();
        private System.Windows.Forms.Timer timer;
        private DateTime lastTick = DateTime.Now;
        private float time, flash, gearAngle, noticeLeft;
        private PointF flashAt;
        private int sel = -1, down = -1;
        private float scale = 1f, offX, offY;
        private bool discKnown;
        private string discText = "";
        private string[] choices = new string[0];
        private bool setFull, setWide, setUpdate;
        private int setLang;
        private string setRegion = "auto";
        private string bStatus = "", bDetail = "", bElapsed = "", dName = "", dText = "", notice = "";
        private int bPercent = -1;
        private bool bFailed;
        private Bitmap cache, fogBmp;
        private Size cacheSize;
        private ImageAttributes[] fogAttr;
        private GraphicsPath titleAll;
        private RectangleF titleBounds, globeRect;
        private Font fBtn, fRow, fHead, fBody, fBig, fSmall, fName, fFoot, fOf;
        private StringFormat sfCenter, sfLeft, sfMid, sfWrap, sfPath, sfRight;

        public HomeMenu()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.Opaque, true);
            TabStop = true;
            BackColor = Color.FromArgb(3, 10, 7);
            fBtn = new Font("Segoe UI Semibold", 18f, FontStyle.Regular, GraphicsUnit.Pixel);
            fRow = new Font("Segoe UI Semibold", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
            fHead = new Font("Segoe UI Semibold", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            fBody = new Font("Segoe UI", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
            fBig = new Font("Segoe UI Semibold", 24f, FontStyle.Regular, GraphicsUnit.Pixel);
            fSmall = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            fName = new Font("Segoe UI Semibold", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
            fFoot = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel);
            fOf = new Font("Segoe UI Semibold", 16f, FontStyle.Regular, GraphicsUnit.Pixel);
            sfCenter = new StringFormat();
            sfCenter.Alignment = StringAlignment.Center;
            sfCenter.LineAlignment = StringAlignment.Center;
            sfLeft = new StringFormat();
            sfLeft.Alignment = StringAlignment.Near;
            sfLeft.LineAlignment = StringAlignment.Near;
            sfLeft.FormatFlags = StringFormatFlags.NoWrap;
            sfMid = new StringFormat();
            sfMid.Alignment = StringAlignment.Near;
            sfMid.LineAlignment = StringAlignment.Center;
            sfMid.FormatFlags = StringFormatFlags.NoWrap;
            sfRight = new StringFormat();
            sfRight.Alignment = StringAlignment.Far;
            sfRight.LineAlignment = StringAlignment.Center;
            sfRight.FormatFlags = StringFormatFlags.NoWrap;
            sfWrap = new StringFormat();
            sfWrap.Trimming = StringTrimming.EllipsisWord;
            sfPath = new StringFormat();
            sfPath.Alignment = StringAlignment.Far;
            sfPath.LineAlignment = StringAlignment.Center;
            sfPath.Trimming = StringTrimming.EllipsisPath;
            sfPath.FormatFlags = StringFormatFlags.NoWrap;

            for (int i = 0; i < 24; i++)
            {
                Spark s = new Spark();
                s.X = Rf(rnd) * 800f;
                s.Y = 100f + Rf(rnd) * 500f;
                s.Speed = 8f + Rf(rnd) * 22f;
                s.Size = 1.2f + Rf(rnd) * 1.8f;
                s.Phase = Rf(rnd) * 6.28f;
                sparks.Add(s);
            }
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 16;
            timer.Tick += new EventHandler(OnTick);
            BuildItems();
        }

        // ================= public API (called from app.ps1) =================
        public void LoadSettings(bool fullscreen, bool widescreen, int language, string region, bool autoUpdate)
        {
            setFull = fullscreen; setWide = widescreen; setLang = language; setUpdate = autoUpdate;
            setRegion = string.IsNullOrEmpty(region) ? "auto" : region;
            if (view == View.Settings) BuildItems();
        }

        // lines look like "EU version|READY|1"  (name | text | 1 = ok)
        public void SetState(bool known, string discPath, string[] lines)
        {
            discKnown = known;
            discText = discPath ?? "";
            games.Clear();
            if (lines != null) { foreach (string s in lines) games.Add(s); }
            if (view == View.Menu || view == View.Settings) BuildItems();
        }

        public void SetBuild(string status, string detail, int percent, string elapsed, bool failed)
        {
            bStatus = status ?? "";
            bDetail = detail ?? "";
            bPercent = percent;
            bElapsed = elapsed ?? "";
            bool changed = (failed != bFailed);
            bFailed = failed;
            if (changed && view == View.Build) BuildItems();
        }

        public void SetDone(string name, string text)
        {
            dName = name ?? "";
            dText = text ?? "";
        }

        public void ShowMode(string mode)
        {
            View v = view;
            if (mode == "build") v = View.Build;
            else if (mode == "done") v = View.Done;
            else if (view == View.Build || view == View.Done) v = View.Menu;
            if (v != view) { view = v; BuildItems(); }
        }

        public void ShowChoices(string[] names)
        {
            choices = names ?? new string[0];
            view = View.Choose;
            BuildItems();
        }

        public void ShowNotice(string text)
        {
            notice = text ?? "";
            noticeLeft = 4f;
            Invalidate();
        }

        // ================= items per view =================
        private Item Add(string id, string text, int glyph, float x, float y, float w, float h, bool big)
        {
            Item it = new Item();
            it.Id = id; it.Text = text; it.Glyph = glyph; it.Big = big;
            it.Rect = new RectangleF(x, y, w, h);
            it.Path = Round(new RectangleF(-w / 2f, -h / 2f, w, h), h * 0.42f);
            items.Add(it);
            return it;
        }

        private bool AnyReady()
        {
            foreach (string s in games)
            {
                string[] p = s.Split('|');
                if (p.Length > 2 && p[2] == "1") return true;
            }
            return false;
        }

        private void BuildItems()
        {
            foreach (Item o in items) { if (o.Path != null) o.Path.Dispose(); }
            items.Clear();
            sel = -1; down = -1;
            if (view == View.Menu) BuildMenu();
            else if (view == View.Settings) BuildSettings();
            else if (view == View.Build) BuildBuild();
            else if (view == View.Done) BuildDone();
            else if (view == View.Confirm) BuildConfirm(); else BuildChoose();
            Invalidate();
        }

        private void BuildMenu()
        {
            float x = 240f, w = 320f, h = 54f, y = 204f;
            if (!discKnown)
            {
                Add("locate", "LOCATE GAME FILES", 4, x, y, w, h, true);
                y += h + 10f;
            }
            Item p = Add("play", "PLAY", 0, x, y, w, h, true);
            p.Enabled = AnyReady();
            p.Hint = "Compile the game first.";
            y += h + 10f;
            Item c = Add("compile", "COMPILE GAME", 1, x, y, w, h, true);
            c.Enabled = discKnown;
            c.Hint = "Show where your game files are first.";
            y += h + 10f;
            Add("settings", "SETTINGS", 2, x, y, w, h, true);
            y += h + 10f;
            Add("link", "GITHUB PROJECT", 3, x, y, w, h, true);
        }

        private void BuildSettings()
        {
            float x = 200f, w = 400f, h = 42f, y = 212f, step = 46f;
            Item a = Add("fullscreen", "FULL SCREEN", -1, x, y, w, h, false);
            a.Kind = 1; a.On = setFull; a.Anim = a.On ? 1f : 0f; y += step;
            Item d = Add("region", "VIDEO MODE", -1, x, y, w, h, false);
            d.Kind = 2;
            d.Options = new string[] { "AUTOMATIC", "PAL 50 HZ", "NTSC 60 HZ" };
            d.Index = (setRegion == "pal") ? 1 : ((setRegion == "ntsc") ? 2 : 0); y += step;
            Item e = Add("autoupdate", "CHECK FOR UPDATES", -1, x, y, w, h, false);
            e.Kind = 1; e.On = setUpdate; e.Anim = e.On ? 1f : 0f; y += step;
            Item f = Add("locate", "GAME FILES", -1, x, y, w, h, false);
            f.Kind = 3; f.Value = discKnown ? discText : "NOT SET"; y += step;
            Add("sback", "BACK", 5, x, y, w, h, false);
        }

        private void BuildBuild()
        {
            if (bFailed)
            {
                Add("retry", "TRY AGAIN", 7, 190f, 444f, 200f, 48f, true);
                Add("back", "BACK", 5, 410f, 444f, 200f, 48f, true);
            }
            else
            {
                Add("cancel", "CANCEL", 6, 300f, 444f, 200f, 48f, true);
            }
        }

        private void BuildDone()
        {
            Add("doneplay", "PLAY", 0, 240f, 412f, 320f, 54f, true);
            Add("doneback", "BACK TO MENU", 5, 240f, 476f, 320f, 46f, true);
        }

                private string cFile = "", cSize = "", cSerial = "", cRegion = "";
        private bool cOk;

        private void PrepareConfirm()
        {
            cFile = ""; cSize = ""; cSerial = "Unknown"; cRegion = "Unknown"; cOk = false;
            try
            {
                cFile = System.IO.Path.GetFileName(discText);
                System.IO.FileInfo fi = new System.IO.FileInfo(discText);
                cSize = (fi.Length / 1048576L).ToString() + " MB";
                int n = (int)Math.Min(fi.Length, 25165824L);
                byte[] buf = new byte[n];
                using (System.IO.FileStream fs = new System.IO.FileStream(discText, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                {
                    int got = 0;
                    while (got < n) { int r = fs.Read(buf, got, n - got); if (r <= 0) break; got += r; }
                }
                string t = System.Text.Encoding.ASCII.GetString(buf);
                if (t.IndexOf("SLES_549.51") >= 0) { cSerial = "SLES_549.51"; cRegion = "Europe (PAL)"; cOk = true; }
                else if (t.IndexOf("SLUS_216.61") >= 0) { cSerial = "SLUS_216.61"; cRegion = "USA (NTSC)"; cOk = true; }
            }
            catch { }
        }

        private void BuildConfirm()
        {
            Item y = Add("cyes", "YES, COMPILE", 8, 240f, 384f, 320f, 54f, true);
            y.Enabled = cOk;
            y.Hint = "This is not a recognised Ben 10 disc.";
            Add("cother", "CHOOSE ANOTHER FILE", 4, 240f, 448f, 320f, 46f, true);
        }

        private void DrawConfirmPanel(Graphics g)
        {
            RectangleF pr = new RectangleF(160f, 192f, 480f, 180f);
            DrawGlass(g, pr, 16f, 1f);
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(150, 255, 130)))
                g.DrawString("IS THIS THE CORRECT GAME FILE?", fName, hb, new RectangleF(pr.X + 22f, pr.Y + 14f, 436f, 24f), sfLeft);
            string[] k = new string[] { "File", "Size", "Serial", "Region" };
            string[] v = new string[] { cFile, cSize, cSerial, cRegion };
            for (int i = 0; i < 4; i++)
            {
                float y = pr.Y + 52f + i * 28f;
                using (SolidBrush a = new SolidBrush(Color.FromArgb(140, 190, 150)))
                    g.DrawString(k[i], fBody, a, new RectangleF(pr.X + 22f, y, 90f, 22f), sfLeft);
                using (SolidBrush b = new SolidBrush((i == 2 && !cOk) ? Color.FromArgb(255, 150, 130) : Color.FromArgb(232, 255, 226)))
                    g.DrawString(v[i], fBody, b, new RectangleF(pr.X + 120f, y, 340f, 22f), sfLeft);
            }
        }

                private float devAmt, devAngle, devPhase, padTimer, mvx, devTarget; private int devPrev = -1;
        private bool padOn;
        private string padText = "Checking...";

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct XState { public uint Packet; public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }
        [System.Runtime.InteropServices.DllImport("xinput1_4.dll")]
        private static extern int XInputGetState(int idx, out XState st);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct JoyInfo { public uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, POV, R1, R2; }
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int joyGetPosEx(uint id, ref JoyInfo ji);

        private void PollPad()
        {
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    XState st;
                    if (XInputGetState(i, out st) == 0) { padOn = true; padText = "Xbox-compatible, player " + (i + 1); return; }
                }
            }
            catch { }
            try
            {
                for (uint i = 0; i < 4; i++)
                {
                    JoyInfo ji = new JoyInfo();
                    ji.Size = 52; ji.Flags = 255;
                    if (joyGetPosEx(i, ref ji) == 0) { padOn = true; padText = "Generic gamepad connected"; return; }
                }
            }
            catch { }
            padOn = false; padText = "None detected";
        }

        private void DrawPadInfo(Graphics g)
        {
            RectangleF pr = new RectangleF(18f, 536f, 214f, 42f);
            DrawGlass(g, pr, 14f, 1f);
            Color led = padOn ? Color.FromArgb(110, 255, 90) : Color.FromArgb(255, 190, 70);
            using (SolidBrush cb = new SolidBrush(led)) g.FillEllipse(cb, pr.X + 14f, pr.Y + 17f, 8f, 8f);
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(150, 255, 130)))
                g.DrawString("CONTROLLER", fHead, hb, new RectangleF(pr.X + 30f, pr.Y + 4f, 170f, 16f), sfLeft);
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(225, 250, 225)))
                g.DrawString(padText, fSmall, tb, new RectangleF(pr.X + 30f, pr.Y + 21f, 178f, 16f), sfLeft);
        }

        private void DrawLiquid(Graphics g, RectangleF b, float h, float am, float cx)
        {
            float bx = b.X + b.Width * (0.5f + 0.32f * (float)Math.Sin(time * 0.6));
            if (h > 0.02f)
            {
                float lx = mvx - cx;
                if (lx < b.X) lx = b.X;
                if (lx > b.Right) lx = b.Right;
                bx = bx + (lx - bx) * h;
            }
            float bw = b.Width * 0.55f;
            RectangleF br = new RectangleF(bx - bw / 2f, b.Y - b.Height * 0.2f, bw, b.Height * 1.4f);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(br);
                using (PathGradientBrush pg = new PathGradientBrush(gp))
                {
                    pg.CenterColor = Color.FromArgb(A((16f + 46f * h) * am), 190, 255, 215);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, 190, 255, 215) };
                    g.FillPath(pg, gp);
                }
            }
            using (Pen sp = new Pen(Color.FromArgb(A((70f + 60f * h) * am), 235, 255, 245), 1.2f))
                g.DrawLine(sp, b.X + b.Height * 0.45f, b.Y + 1.6f, b.Right - b.Height * 0.45f, b.Y + 1.6f);
            RectangleF lo = new RectangleF(b.X, b.Bottom - b.Height * 0.38f, b.Width, b.Height * 0.38f);
            using (LinearGradientBrush lb = new LinearGradientBrush(lo, Color.FromArgb(0, 120, 255, 170), Color.FromArgb(A((30f + 50f * h) * am), 120, 255, 170), 90f))
                g.FillRectangle(lb, lo);
        }

                private static readonly float[] siteX = new float[] { 462f, 492f, 522f, 552f, 580f, 602f, 622f, 655f, 672f, 694f, 716f, 744f, 776f };
        private static readonly float[] siteY = new float[] { 440f, 400f, 362f, 330f, 296f, 262f, 234f, 232f, 252f, 280f, 306f, 336f, 366f };
        private float blastT = -1f, blastX, blastY, blastNext = 6f;

        private void StepBlast(float dt)
        {
            if (blastT >= 0f) { blastT += dt; if (blastT > 1.2f) blastT = -1f; }
            blastNext -= dt;
            if (blastNext <= 0f && blastT < 0f)
            {
                int k = rnd.Next(siteX.Length);
                blastX = siteX[k] + (Rf(rnd) - 0.5f) * 10f;
                blastY = siteY[k] + 6f + Rf(rnd) * 30f;
                blastT = 0f;
                blastNext = 10f + Rf(rnd) * 20f;
            }
        }

        private void DrawBlasts(Graphics g)
        {
            if (blastT < 0f) return;
            float u = blastT / 1.2f;
            float rr = 4f + 20f * (float)Math.Sqrt(u);
            float al = 1f - u;
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(blastX - rr * 2f, blastY - rr * 2f, rr * 4f, rr * 4f);
                using (PathGradientBrush pg = new PathGradientBrush(gp))
                {
                    pg.CenterColor = Color.FromArgb(A(200f * al), 255, 230, 140);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, 255, 120, 40) };
                    g.FillPath(pg, gp);
                }
            }
            if (u < 0.25f)
            {
                float cr2 = 7f * (1f - u * 3f);
                using (SolidBrush wb = new SolidBrush(Color.FromArgb(A(255f * (1f - u * 3.5f)), 255, 250, 220)))
                    g.FillEllipse(wb, blastX - cr2, blastY - cr2, cr2 * 2f, cr2 * 2f);
            }
            for (int k = 0; k < 9; k++)
            {
                double an = k * 0.7 + 0.3;
                float d = 6f + 48f * u * (0.6f + 0.2f * (k % 3));
                float px = blastX + (float)Math.Cos(an) * d;
                float py = blastY + (float)Math.Sin(an) * d + 40f * u * u;
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(A(255f * al), 255, 190, 80)))
                    g.FillEllipse(sb, px - 1.2f, py - 1.2f, 2.4f, 2.4f);
            }
        }

        private void BuildChoose()
        {
            float y = 212f;
            for (int i = 0; i < choices.Length && i < 4; i++)
            {
                Add("play:" + choices[i], choices[i].ToUpper(), 0, 240f, y, 320f, 54f, true);
                y += 62f;
            }
            Add("cback", "BACK", 5, 240f, y, 320f, 46f, true);
        }

        // ================= timing =================
        private void OnTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            float dt = (float)(now - lastTick).TotalSeconds;
            lastTick = now;
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;
            Step(dt);
            Invalidate();
            Update();
        }

        private void SyncTimer()
        {
            if (timer == null) return;
            bool run = Visible && IsHandleCreated;
            if (run && !timer.Enabled) { lastTick = DateTime.Now; timer.Start(); }
            else if (!run && timer.Enabled) { timer.Stop(); }
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); SyncTimer(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SyncTimer(); }

        private void Step(float dt)
        {
            time += dt;
            flash = Math.Max(0f, flash - dt * 1.8f);
            if (noticeLeft > 0f) noticeLeft -= dt;
            gearAngle += dt * 40f; bool anyHover = (sel >= 0 && sel < items.Count && items[sel].Enabled); devAmt += ((anyHover ? 1f : 0f) - devAmt) * Math.Min(1f, dt * 5f); if (sel != devPrev) { devPrev = sel; if (anyHover) devTarget += 90f; } devAngle += (devTarget - devAngle) * Math.Min(1f, dt * 7f); devPhase += dt * 0.12f; padTimer -= dt; if (padTimer <= 0f) { padTimer = 2f; PollPad(); } StepBlast(dt);
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                float th = (i == sel && it.Enabled) ? 1f : 0f;
                it.Hover += (th - it.Hover) * Math.Min(1f, dt * 12f);
                float tp = (i == down && sel == down && it.Enabled) ? 1f : 0f;
                it.Press += (tp - it.Press) * Math.Min(1f, dt * 22f);
                float ta = it.On ? 1f : 0f;
                it.Anim += (ta - it.Anim) * Math.Min(1f, dt * 14f);
            }
            foreach (Spark s in sparks)
            {
                s.Y -= s.Speed * dt * 0.5f;
                s.X += (float)Math.Sin(time * 0.7 + s.Phase) * 6f * dt;
                if (s.Y < 90f) { s.Y = 592f; s.X = Rf(rnd) * 800f; }
            }
        }

        // ================= input =================
        private void UpdateScale()
        {
            scale = Math.Min(Width / 800f, Height / 600f);
            if (scale <= 0f) scale = 1f;
            offX = (Width - 800f * scale) / 2f;
            offY = (Height - 600f * scale) / 2f;
        }

        private int HitTest(Point p)
        {
            UpdateScale();
            float x = (p.X - offX) / scale;
            float y = (p.Y - offY) / scale;
            for (int i = 0; i < items.Count; i++)
            {
                RectangleF r = items[i].Rect;
                r.Inflate(6f, 4f);
                if (r.Contains(x, y)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = HitTest(e.Location);
            sel = i;
            Cursor = (i >= 0 && items[i].Enabled) ? Cursors.Hand : Cursors.Default; mvx = (e.X - offX) / scale;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (down < 0) sel = -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                down = HitTest(e.Location);
                if (down >= 0) sel = down;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                int hit = HitTest(e.Location);
                int d = down;
                down = -1;
                if (d >= 0 && hit == d) Activate(d);
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Return:
                case Keys.Space:
                case Keys.Escape:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                if (view == View.Settings || view == View.Choose || view == View.Confirm) { view = View.Menu; BuildItems(); }
                e.Handled = true;
                return;
            }
            if (items.Count == 0) return;
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Right) { sel = (sel + 1) % items.Count; e.Handled = true; }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Left) { sel = (sel <= 0) ? items.Count - 1 : sel - 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space)
            {
                int i = sel;
                if (i < 0) { for (int k = 0; k < items.Count; k++) { if (items[k].Enabled) { i = k; break; } } }
                if (i >= 0) Activate(i);
                e.Handled = true;
            }
        }

        private string ValueFor(Item it)
        {
            if (it.Id == "language") return it.Index.ToString();
            if (it.Id == "region") return it.Index == 1 ? "pal" : (it.Index == 2 ? "ntsc" : "auto");
            return it.Index.ToString();
        }

        private void Activate(int i)
        {
            Item it = items[i];
            if (!it.Enabled)
            {
                if (!string.IsNullOrEmpty(it.Hint)) ShowNotice(it.Hint);
                return;
            }
            flash = 1f;
            flashAt = new PointF(it.Rect.X + it.Rect.Width / 2f, it.Rect.Y + it.Rect.Height / 2f);
            string id = it.Id;
            if (it.Kind == 1)
            {
                it.On = !it.On;
                if (id == "fullscreen") setFull = it.On;
                else if (id == "widescreen") setWide = it.On;
                else if (id == "autoupdate") setUpdate = it.On;
                RaiseSetting(id, it.On ? "1" : "0");
                return;
            }
            if (it.Kind == 2)
            {
                it.Index = (it.Index + 1) % it.Options.Length;
                if (id == "language") setLang = it.Index;
                else if (id == "region") setRegion = ValueFor(it);
                RaiseSetting(id, ValueFor(it));
                return;
            }
            if (id == "compile") { PrepareConfirm(); view = View.Confirm; BuildItems(); return; } if (id == "cyes") { view = View.Menu; BuildItems(); RaiseClicked("compile"); return; } if (id == "cother") { view = View.Menu; BuildItems(); RaiseClicked("locate"); return; } if (id == "settings") { view = View.Settings; BuildItems(); return; }
            if (id == "sback" || id == "cback") { view = View.Menu; BuildItems(); return; }
            if (id.StartsWith("play:")) { view = View.Menu; BuildItems(); }
            RaiseClicked(id);
        }

        private void RaiseSetting(string key, string value)
        {
            Action<string, string> h = SettingChanged;
            if (h != null) h(key, value);
        }

        private void RaiseClicked(string id)
        {
            Action<string> h = ButtonClicked;
            if (h == null) return;
            if (IsHandleCreated) BeginInvoke(new MethodInvoker(delegate { h(id); }));
            else h(id);
        }

        // ================= helpers =================
        private static float Rf(Random r) { return (float)r.NextDouble(); }

        private static int A(float v)
        {
            if (v < 0f) return 0;
            if (v > 255f) return 255;
            return (int)v;
        }

        private static Color Mix(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        private static GraphicsPath Round(RectangleF r, float rad)
        {
            float d = rad * 2f;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        private static void Tree(Graphics g, float x, float baseY, float h, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
            {
                for (int i = 0; i < 4; i++)
                {
                    float yt = baseY - h + i * h * 0.22f;
                    float yb = yt + h * 0.38f;
                    float hw = h * (0.10f + 0.055f * i);
                    g.FillPolygon(b, new PointF[] { new PointF(x, yt), new PointF(x + hw, yb), new PointF(x - hw, yb) });
                }
                g.FillRectangle(b, x - h * 0.02f, baseY - h * 0.04f, h * 0.04f, h * 0.08f);
            }
        }

        private static PointF[] Scaled(float[] pts, RectangleF gr)
        {
            PointF[] r = new PointF[pts.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new PointF(gr.X + pts[i * 2] * gr.Width, gr.Y + pts[i * 2 + 1] * gr.Height);
            return r;
        }

        private static FontFamily TitleFamily(out bool regular)
        {
            regular = true;
            string[] names = new string[] { "Impact", "Arial Black", "Segoe UI Black" };
            foreach (string n in names)
            {
                try { return new FontFamily(n); } catch { }
            }
            regular = false;
            return FontFamily.GenericSansSerif;
        }

        private static GraphicsPath TextPath(string s, FontFamily fam, bool regular)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddString(s, fam, (int)(regular ? FontStyle.Regular : FontStyle.Bold), 100f, new PointF(0f, 0f), StringFormat.GenericTypographic);
            return p;
        }

        private static void PlaceBox(GraphicsPath p, float x, float y, float w, float h)
        {
            RectangleF b = p.GetBounds();
            using (Matrix m = new Matrix())
            {
                m.Translate(-b.X, -b.Y);
                m.Scale(w / b.Width, h / b.Height, MatrixOrder.Append);
                m.Translate(x, y, MatrixOrder.Append);
                p.Transform(m);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
                if (titleAll != null) { titleAll.Dispose(); titleAll = null; }
                if (cache != null) { cache.Dispose(); cache = null; }
                if (fogBmp != null) { fogBmp.Dispose(); fogBmp = null; }
                foreach (Item it in items) { if (it.Path != null) it.Path.Dispose(); }
                Font[] fs = new Font[] { fBtn, fRow, fHead, fBody, fBig, fSmall, fName, fFoot, fOf };
                foreach (Font f in fs) { if (f != null) f.Dispose(); }
                StringFormat[] sf = new StringFormat[] { sfCenter, sfLeft, sfMid, sfWrap, sfPath, sfRight };
                foreach (StringFormat s in sf) { if (s != null) s.Dispose(); }
            }
            base.Dispose(disposing);
        }

        // ================= static layer (drawn once per window size) =================
        private void BuildFog()
        {
            fogBmp = new Bitmap(512, 96, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(fogBmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(0f, 0f, 511f, 95f);
                    using (PathGradientBrush pg = new PathGradientBrush(p))
                    {
                        pg.CenterColor = Color.FromArgb(120, 150, 205, 175);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 150, 205, 175) };
                        g.FillPath(pg, p);
                    }
                }
            }
            fogAttr = new ImageAttributes[3];
            float[] al = new float[] { 0.34f, 0.24f, 0.30f };
            for (int i = 0; i < 3; i++)
            {
                ColorMatrix cm = new ColorMatrix();
                cm.Matrix33 = al[i];
                fogAttr[i] = new ImageAttributes();
                fogAttr[i].SetColorMatrix(cm);
            }
        }

        private void EnsureCache()
        {
            if (fogBmp == null) BuildFog();
            if (cache != null && cacheSize.Width == Width && cacheSize.Height == Height) return;
            if (cache != null) { cache.Dispose(); cache = null; }
            cacheSize = new Size(Width, Height);
            cache = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(cache))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                Rectangle full = new Rectangle(0, 0, Width, Height);
                using (LinearGradientBrush sky = new LinearGradientBrush(full, Color.FromArgb(2, 6, 6), Color.FromArgb(7, 26, 17), 90f))
                    g.FillRectangle(sky, full);
                GraphicsState st = g.Save();
                g.TranslateTransform(offX, offY);
                g.ScaleTransform(scale, scale);
                DrawStars(g);
                DrawClouds(g);
                DrawMoon(g);
                DrawMountains(g);
                DrawWater(g);
                DrawShore(g);
                DrawTrees(g);
                DrawTitleStatic(g);
                g.Restore(st);
                DrawVignette(g);
            }
        }

        private void DrawVignette(Graphics g)
        {
            Rectangle r = new Rectangle(0, 0, Width, Height);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(-Width * 0.2f, -Height * 0.2f, Width * 1.4f, Height * 1.4f);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    pg.SurroundColors = new Color[] { Color.FromArgb(190, 0, 0, 0) };
                    g.FillRectangle(pg, r);
                }
            }
        }

        private void DrawStars(Graphics g)
        {
            Random r = new Random(7);
            for (int i = 0; i < 120; i++)
            {
                float x = Rf(r) * 800f, y = Rf(r) * 360f, s = 0.7f + Rf(r) * 1.3f, a = 40f + Rf(r) * 150f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(A(a), 200, 255, 215))) g.FillEllipse(b, x, y, s, s);
            }
        }

        private void DrawClouds(Graphics g)
        {
            Random r = new Random(21);
            for (int i = 0; i < 10; i++)
            {
                float w = 240f + Rf(r) * 260f, h = 36f + Rf(r) * 50f;
                float cx = Rf(r) * 900f - 50f, cy = 30f + Rf(r) * 210f;
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(cx - w / 2f, cy - h / 2f, w, h);
                    using (PathGradientBrush pg = new PathGradientBrush(p))
                    {
                        pg.CenterColor = Color.FromArgb(50, 22, 56, 38);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 22, 56, 38) };
                        g.FillPath(pg, p);
                    }
                }
            }
        }

        private void DrawMoon(Graphics g)
        {
            Random r = new Random(5);
            float mx = 792f, my = 130f, mr = 56f;
            using (GraphicsPath hp = new GraphicsPath())
            {
                hp.AddEllipse(mx - mr * 2.4f, my - mr * 2.4f, mr * 4.8f, mr * 4.8f);
                using (PathGradientBrush pg = new PathGradientBrush(hp))
                {
                    pg.CenterColor = Color.FromArgb(70, 120, 200, 150);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, 120, 200, 150) };
                    g.FillPath(pg, hp);
                }
            }
            using (GraphicsPath mp = new GraphicsPath())
            {
                mp.AddEllipse(mx - mr, my - mr, mr * 2f, mr * 2f);
                using (LinearGradientBrush mb = new LinearGradientBrush(new RectangleF(mx - mr, my - mr, mr * 2f, mr * 2f), Color.FromArgb(150, 190, 168), Color.FromArgb(48, 78, 64), 45f))
                    g.FillPath(mb, mp);
                GraphicsState ms = g.Save();
                g.SetClip(mp, CombineMode.Intersect);
                for (int i = 0; i < 9; i++)
                {
                    float cx = mx - mr + Rf(r) * mr * 2f, cy = my - mr + Rf(r) * mr * 2f, cr = 4f + Rf(r) * 12f;
                    using (SolidBrush cb = new SolidBrush(Color.FromArgb(46, 20, 44, 34))) g.FillEllipse(cb, cx - cr, cy - cr, cr * 2f, cr * 2f);
                }
                g.Restore(ms);
            }
        }

        private void DrawMountains(Graphics g)
        {
            Random r = new Random(33);
            PointF[] back = new PointF[] { new PointF(250f, 500f), new PointF(300f, 452f), new PointF(345f, 420f), new PointF(385f, 396f), new PointF(420f, 384f), new PointF(455f, 410f), new PointF(490f, 450f), new PointF(540f, 500f) };
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(8, 24, 18))) g.FillPolygon(bb, back);
            PointF[] m = new PointF[] {
                new PointF(430f, 500f), new PointF(462f, 440f), new PointF(492f, 400f), new PointF(522f, 362f), new PointF(552f, 330f),
                new PointF(580f, 296f), new PointF(602f, 262f), new PointF(622f, 234f), new PointF(640f, 208f), new PointF(655f, 232f),
                new PointF(672f, 252f), new PointF(694f, 280f), new PointF(716f, 306f), new PointF(744f, 336f), new PointF(776f, 366f),
                new PointF(820f, 396f), new PointF(1100f, 470f), new PointF(1100f, 520f) };
            for (int i = 1; i < m.Length - 3; i++)
            {
                if (i == 8) continue;
                m[i].X += (Rf(r) - 0.5f) * 6f;
                m[i].Y += (Rf(r) - 0.5f) * 6f;
            }
            using (LinearGradientBrush mb = new LinearGradientBrush(new RectangleF(430f, 200f, 670f, 320f), Color.FromArgb(18, 44, 32), Color.FromArgb(3, 10, 8), 90f))
                g.FillPolygon(mb, m);
            PointF[] rim = new PointF[9];
            Array.Copy(m, rim, 9);
            using (Pen rp = new Pen(Color.FromArgb(110, 130, 255, 150), 1.6f)) g.DrawLines(rp, rim);
            using (Pen sp = new Pen(Color.FromArgb(22, 140, 255, 160), 1f))
            {
                for (int k = 0; k < 12; k++)
                {
                    float x0 = 640f + (Rf(r) - 0.5f) * 20f;
                    float x1 = 470f + Rf(r) * 340f;
                    g.DrawLine(sp, x0, 214f, x1, 470f);
                }
            }
        }

        private void DrawWater(Graphics g)
        {
            RectangleF wr = new RectangleF(-1200f, 468f, 3200f, 900f);
            using (LinearGradientBrush b = new LinearGradientBrush(wr, Color.FromArgb(8, 28, 20), Color.FromArgb(2, 9, 7), 90f))
                g.FillRectangle(b, wr);
        }

        private void DrawShore(Graphics g)
        {
            Random r = new Random(55);
            PointF[] land2 = new PointF[] { new PointF(590f, 474f), new PointF(640f, 458f), new PointF(700f, 453f), new PointF(790f, 455f), new PointF(1200f, 460f), new PointF(1200f, 486f), new PointF(590f, 486f) };
            using (SolidBrush lb = new SolidBrush(Color.FromArgb(4, 14, 10))) g.FillPolygon(lb, land2);
            for (int i = 0; i < 34; i++)
                Tree(g, 392f + i * 11f + Rf(r) * 5f, 466f, 26f + Rf(r) * 20f, Color.FromArgb(4, 16, 11));
            for (int i = 0; i < 9; i++)
            {
                float bx = 668f + i * 13f, bw = 9f + Rf(r) * 7f, bh = 14f + Rf(r) * 30f;
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(5, 14, 11)))
                {
                    g.FillRectangle(bb, bx, 454f - bh, bw, bh);
                    if (i % 3 == 1)
                        g.FillPolygon(bb, new PointF[] { new PointF(bx - 1f, 454f - bh), new PointF(bx + bw / 2f, 454f - bh - 9f), new PointF(bx + bw + 1f, 454f - bh) });
                }
                for (int k = 0; k < 3; k++)
                {
                    if (Rf(r) < 0.6f)
                    {
                        using (SolidBrush wb = new SolidBrush(Color.FromArgb(170, 210, 255, 150)))
                            g.FillRectangle(wb, bx + 1.5f + Rf(r) * (bw - 4f), 454f - bh + 4f + k * 8f, 1.6f, 2.2f);
                    }
                }
            }
        }

        private void DrawTrees(Graphics g)
        {
            Random r = new Random(77);
            PointF[] land = new PointF[] { new PointF(-1200f, 1400f), new PointF(-1200f, 470f), new PointF(0f, 452f), new PointF(160f, 462f), new PointF(300f, 480f), new PointF(380f, 520f), new PointF(470f, 570f), new PointF(560f, 612f), new PointF(560f, 1400f) };
            using (SolidBrush lb = new SolidBrush(Color.FromArgb(3, 11, 8))) g.FillPolygon(lb, land);
            for (int i = 0; i < 14; i++)
            {
                float x = -16f + i * 25f + Rf(r) * 12f;
                float baseY = 486f + (i % 3) * 16f;
                float h = 120f + Rf(r) * 120f;
                Tree(g, x, baseY, h, Color.FromArgb(2, 8, 6));
            }
            for (int i = 0; i < 4; i++)
                Tree(g, 770f + i * 22f, 540f, 150f + Rf(r) * 60f, Color.FromArgb(2, 8, 6));
            PointF[] rocks = new PointF[] { new PointF(-40f, 640f), new PointF(-40f, 520f), new PointF(40f, 500f), new PointF(95f, 506f), new PointF(150f, 532f), new PointF(190f, 562f), new PointF(250f, 640f) };
            using (SolidBrush rb = new SolidBrush(Color.FromArgb(4, 13, 10))) g.FillPolygon(rb, rocks);
            using (Pen ep = new Pen(Color.FromArgb(60, 120, 230, 140), 1.5f)) g.DrawLines(ep, new PointF[] { rocks[1], rocks[2], rocks[3], rocks[4], rocks[5] });
        }

        private void BuildTitle()
        {
            if (titleAll != null) { titleAll.Dispose(); titleAll = null; }
            bool reg;
            FontFamily fam = TitleFamily(out reg);
            GraphicsPath a = TextPath("GUARDIAN", fam, reg);
            PlaceBox(a, 215f, 22f, 370f, 56f);
            GraphicsPath l = TextPath("GL", fam, reg);
            GraphicsPath be = TextPath("BE", fam, reg);
            RectangleF lb = l.GetBounds(), eb = be.GetBounds();
            float rowH = 62f, d = 62f, gap = 7f;
            float w1 = lb.Width * rowH / lb.Height * 1.25f;
            float w2 = eb.Width * rowH / eb.Height * 1.25f;
            float total = w1 + gap + d + gap + w2;
            float x0 = 400f - total / 2f, y0 = 122f;
            PlaceBox(l, x0, y0, w1, rowH);
            globeRect = new RectangleF(x0 + w1 + gap, y0, d, d);
            PlaceBox(be, x0 + w1 + gap + d + gap, y0, w2, rowH);
            titleAll = new GraphicsPath();
            titleAll.AddPath(a, false);
            titleAll.AddPath(l, false);
            titleAll.AddPath(be, false);
            a.Dispose(); l.Dispose(); be.Dispose();
            titleBounds = titleAll.GetBounds();
            fam.Dispose();
        }

        private void DrawTitleStatic(Graphics g)
        {
            if (titleAll == null) BuildTitle();
            using (Pen glow = new Pen(Color.FromArgb(40, 120, 255, 90), 14f))
            {
                glow.LineJoin = LineJoin.Round;
                g.DrawPath(glow, titleAll);
            }
            using (Pen ol = new Pen(Color.FromArgb(255, 10, 20, 8), 6f))
            {
                ol.LineJoin = LineJoin.Round;
                g.DrawPath(ol, titleAll);
            }
            using (LinearGradientBrush fb = new LinearGradientBrush(new RectangleF(0f, 20f, 800f, 170f), Color.FromArgb(188, 206, 128), Color.FromArgb(70, 104, 40), 90f))
                g.FillPath(fb, titleAll);
            GraphicsState gs = g.Save();
            g.SetClip(titleAll, CombineMode.Intersect);
            Random r = new Random(11);
            for (int i = 0; i < 900; i++)
            {
                float x = 190f + Rf(r) * 420f, y = 20f + Rf(r) * 166f, sz = 0.8f + Rf(r) * 2.4f;
                bool dark = Rf(r) < 0.6f;
                Color c = dark ? Color.FromArgb(A(30f + 60f * Rf(r)), 20, 34, 14) : Color.FromArgb(A(20f + 50f * Rf(r)), 230, 245, 170);
                using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x, y, sz, sz);
            }
            g.TranslateTransform(-1.2f, -1.2f);
            using (Pen hl = new Pen(Color.FromArgb(90, 240, 255, 190), 1.5f)) g.DrawPath(hl, titleAll);
            g.Restore(gs);
            DrawGlobe(g, globeRect);
            using (Pen rl = new Pen(Color.FromArgb(150, 170, 210, 110), 2f))
            {
                g.DrawLine(rl, 232f, 98f, 322f, 98f);
                g.DrawLine(rl, 478f, 98f, 568f, 98f);
            }
            using (SolidBrush ob = new SolidBrush(Color.FromArgb(170, 205, 120)))
                g.DrawString("O F   T H E", fOf, ob, new RectangleF(320f, 86f, 160f, 24f), sfCenter);
            using (SolidBrush ft = new SolidBrush(Color.FromArgb(120, 160, 120)))
                g.DrawString("Unofficial fan project  -  not affiliated with the owners of Ben 10  -  open-source recompilation tooling (MIT)", fFoot, ft, new RectangleF(0f, 584f, 800f, 14f), sfCenter);
        }

        private void DrawGlobe(Graphics g, RectangleF gr)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(gr);
                using (Pen hp = new Pen(Color.FromArgb(46, 120, 255, 90), 12f)) g.DrawPath(hp, p);
                using (Pen op = new Pen(Color.FromArgb(255, 10, 20, 8), 5f)) g.DrawPath(op, p);
                using (LinearGradientBrush b = new LinearGradientBrush(gr, Color.FromArgb(150, 205, 90), Color.FromArgb(34, 80, 24), 60f)) g.FillPath(b, p);
                GraphicsState cs = g.Save();
                g.SetClip(p, CombineMode.Intersect);
                PointF[] na = Scaled(new float[] { 0.14f, 0.26f, 0.30f, 0.14f, 0.50f, 0.16f, 0.56f, 0.30f, 0.46f, 0.44f, 0.38f, 0.50f, 0.28f, 0.44f, 0.18f, 0.36f }, gr);
                PointF[] sa = Scaled(new float[] { 0.40f, 0.54f, 0.52f, 0.52f, 0.60f, 0.64f, 0.54f, 0.80f, 0.45f, 0.92f, 0.38f, 0.76f, 0.34f, 0.62f }, gr);
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(200, 28, 72, 26)))
                {
                    g.FillClosedCurve(cb, na, FillMode.Alternate, 0.4f);
                    g.FillClosedCurve(cb, sa, FillMode.Alternate, 0.4f);
                }
                using (Pen lp = new Pen(Color.FromArgb(46, 220, 255, 170), 1f))
                {
                    g.DrawEllipse(lp, gr.X + gr.Width * 0.18f, gr.Y, gr.Width * 0.64f, gr.Height);
                    g.DrawLine(lp, gr.X, gr.Y + gr.Height / 2f, gr.Right, gr.Y + gr.Height / 2f);
                    g.DrawLine(lp, gr.X + gr.Width / 2f, gr.Y, gr.X + gr.Width / 2f, gr.Bottom);
                }
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 10, 0)))
                    g.FillEllipse(sh, gr.X + gr.Width * 0.25f, gr.Y + gr.Height * 0.35f, gr.Width, gr.Height);
                g.Restore(cs);
                using (Pen rp = new Pen(Color.FromArgb(200, 190, 235, 130), 1.5f)) g.DrawPath(rp, p);
            }
        }

        // ================= live layer =================
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            try { PaintAll(e.Graphics); }
            catch (Exception ex)
            {
                try
                {
                    e.Graphics.ResetTransform();
                    e.Graphics.Clear(Color.Black);
                    using (SolidBrush b = new SolidBrush(Color.OrangeRed))
                        e.Graphics.DrawString("Menu paint error: " + ex.GetType().Name + ": " + ex.Message, SystemFonts.DefaultFont, b, new RectangleF(10f, 10f, Width - 20f, Height - 20f));
                }
                catch { }
            }
        }

        private void PaintAll(Graphics g)
        {
            if (Width < 40 || Height < 40) return;
            UpdateScale();
            EnsureCache();
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(cache, new Rectangle(0, 0, cache.Width, cache.Height), 0, 0, cache.Width, cache.Height, GraphicsUnit.Pixel);
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            GraphicsState st = g.Save();
            g.TranslateTransform(offX, offY);
            g.ScaleTransform(scale, scale);
            DrawDevice(g); DrawBlasts(g);
            DrawFog(g);
            DrawFireflies(g);
            if (view == View.Menu) { DrawStatusPanel(g); DrawPadInfo(g); }
            else if (view == View.Settings) DrawHeader(g, "SETTINGS");
            else if (view == View.Choose) DrawHeader(g, "SELECT GAME"); else if (view == View.Confirm) DrawConfirmPanel(g);
            else if (view == View.Build) DrawBuildPanel(g);
            else DrawDonePanel(g);
            for (int i = 0; i < items.Count; i++) DrawHolo(g, items[i], i);
            DrawFlash(g);
            DrawNotice(g);
            g.Restore(st);
        }

                private void DrawDevice(Graphics g)
        {
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 1.9);
            float cx = 400f, cy = 556f, top = 150f;
            float boost = 0.55f + 0.45f * devAmt;
            for (int L = 0; L < 6; L++)
            {
                float f = 1.0f - L * 0.14f;
                PointF[] lc = new PointF[] { new PointF(cx - 50f * f, cy), new PointF(cx + 50f * f, cy), new PointF(cx + 215f * f, 470f), new PointF(cx + 250f * f, top), new PointF(cx - 250f * f, top), new PointF(cx - 215f * f, 470f) };
                RectangleF lr2 = new RectangleF(cx - 260f, top, 520f, cy - top);
                using (LinearGradientBrush lbb = new LinearGradientBrush(lr2, Color.FromArgb(0, 120, 255, 150), Color.FromArgb(A((10f + 6f * pulse) * boost), 120, 255, 150), 90f))
                    g.FillPolygon(lbb, lc);
            }
            PointF[] cone = new PointF[] { new PointF(cx - 18f, cy), new PointF(cx + 18f, cy), new PointF(cx + 75f, 470f), new PointF(cx + 87f, top), new PointF(cx - 87f, top), new PointF(cx - 75f, 470f) };
            RectangleF cr = new RectangleF(cx - 260f, top, 520f, cy - top);
            using (LinearGradientBrush b = new LinearGradientBrush(cr, Color.FromArgb(0, 120, 255, 150), Color.FromArgb(A((22f + 14f * pulse) * boost), 120, 255, 150), 90f))
                g.FillPolygon(b, cone);
            PointF[] core = new PointF[] { new PointF(cx - 18f, cy), new PointF(cx + 18f, cy), new PointF(cx + 95f, 470f), new PointF(cx + 110f, top), new PointF(cx - 110f, top), new PointF(cx - 95f, 470f) };
            using (LinearGradientBrush b2 = new LinearGradientBrush(cr, Color.FromArgb(0, 200, 255, 200), Color.FromArgb(A((26f + 18f * pulse) * boost), 200, 255, 200), 90f))
                g.FillPolygon(b2, core);
            using (Pen ep = new Pen(Color.FromArgb(40, 150, 255, 170), 1.2f))
            {
                g.DrawLines(ep, new PointF[] { new PointF(cx - 50f, cy), new PointF(cx - 215f, 470f), new PointF(cx - 250f, top) });
                g.DrawLines(ep, new PointF[] { new PointF(cx + 50f, cy), new PointF(cx + 215f, 470f), new PointF(cx + 250f, top) });
            }
            for (int k = 0; k < 9; k++)
            {
                float t = (devPhase + k / 9f) % 1f;
                float y = cy - t * (cy - top - 10f);
                float hw = 50f + 215f * (float)Math.Pow(t, 0.45);
                using (Pen sp = new Pen(Color.FromArgb(A(90f * (1f - t)), 170, 255, 190), 1.3f))
                    g.DrawEllipse(sp, cx - hw, y - hw * 0.07f, hw * 2f, hw * 0.14f);
            }
            RectangleF bs = new RectangleF(cx - 70f, cy - 12f, 140f, 36f);
            using (LinearGradientBrush bb = new LinearGradientBrush(bs, Color.FromArgb(34, 58, 46), Color.FromArgb(6, 14, 10), 90f))
                g.FillEllipse(bb, bs);
            using (Pen rp = new Pen(Color.FromArgb(170, 130, 240, 160), 1.6f)) g.DrawEllipse(rp, bs);
            RectangleF rg = new RectangleF(cx - 52f, cy - 7f, 104f, 26f);
            using (Pen rp2 = new Pen(Color.FromArgb(90, 120, 220, 150), 1.2f)) g.DrawEllipse(rp2, rg);
            for (int k = 0; k < 14; k++)
            {
                double a = (devAngle + k * (360.0 / 14.0)) * Math.PI / 180.0;
                float px = cx + (float)Math.Cos(a) * 58f, py = cy + 6f + (float)Math.Sin(a) * 15f;
                float fr = 0.5f + 0.5f * (float)Math.Sin(a);
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(A(60f + 190f * fr), 170, 255, 180)))
                    g.FillEllipse(tb, px - 2.2f, py - 1.6f, 4.4f, 3.2f);
            }
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(cx - 36f, cy - 6f, 72f, 24f);
                using (PathGradientBrush pg = new PathGradientBrush(gp))
                {
                    pg.CenterColor = Color.FromArgb(A(210f + 45f * pulse), 200, 255, 200);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, 90, 255, 120) };
                    g.FillPath(pg, gp);
                }
            }
        }

        private void DrawBeamOld(Graphics g)
        {
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 1.9);
            float fl = Math.Sin(time * 23.0) > 0.975 ? 0.65f : 1f;
            float px = 640f, py = 208f;
            for (int i = 3; i >= 1; i--)
            {
                float w = 6f + 12f * i;
                RectangleF br = new RectangleF(px - w / 2f, -10f, w, py + 14f);
                using (LinearGradientBrush b = new LinearGradientBrush(br, Color.FromArgb(0, 130, 255, 110), Color.FromArgb(A((14f + 22f * pulse) * fl), 130, 255, 110), 90f))
                    g.FillRectangle(b, br);
            }
            RectangleF core = new RectangleF(px - 1.6f, -10f, 3.2f, py + 14f);
            using (LinearGradientBrush cb = new LinearGradientBrush(core, Color.FromArgb(0, 210, 255, 190), Color.FromArgb(A((200f + 55f * pulse) * fl), 210, 255, 190), 90f))
                g.FillRectangle(cb, core);
            using (GraphicsPath fp = new GraphicsPath())
            {
                fp.AddEllipse(px - 60f, py - 34f, 120f, 68f);
                using (PathGradientBrush pg = new PathGradientBrush(fp))
                {
                    pg.CenterColor = Color.FromArgb(A((110f + 60f * pulse) * fl), 170, 255, 150);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, 120, 255, 100) };
                    g.FillPath(pg, fp);
                }
            }
            RectangleF rr = new RectangleF(px - 3f, 474f, 6f, 120f);
            using (LinearGradientBrush rb = new LinearGradientBrush(rr, Color.FromArgb(A(90f * fl), 170, 255, 170), Color.FromArgb(0, 170, 255, 170), 90f))
                g.FillRectangle(rb, rr);
            using (Pen sp = new Pen(Color.FromArgb(70, 170, 255, 170), 1.5f))
            {
                for (int k = 0; k < 11; k++)
                {
                    float yy = 480f + k * 10.5f;
                    float ww = 3f + 20f * (0.5f + 0.5f * (float)Math.Sin(time * 2.0 + k * 0.9));
                    g.DrawLine(sp, px - ww, yy, px + ww, yy);
                }
            }
        }

        private void DrawFog(Graphics g)
        {
            if (fogBmp == null) return;
            for (int i = 0; i < 3; i++)
            {
                float x = ((time * (6f + 4f * i) + i * 330f) % 1500f) - 450f;
                g.DrawImage(fogBmp, new Rectangle((int)x, 392 + i * 52, 760 + i * 90, 96), 0, 0, fogBmp.Width, fogBmp.Height, GraphicsUnit.Pixel, fogAttr[i]);
            }
        }

        private void DrawFireflies(Graphics g)
        {
            foreach (Spark s in sparks)
            {
                float tw = 0.5f + 0.5f * (float)Math.Sin(time * 1.7 + s.Phase);
                using (SolidBrush gb = new SolidBrush(Color.FromArgb(A(26f * tw), 120, 255, 100)))
                    g.FillEllipse(gb, s.X - s.Size * 2.4f, s.Y - s.Size * 2.4f, s.Size * 4.8f, s.Size * 4.8f);
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(A(80f + 160f * tw), 190, 255, 150)))
                    g.FillEllipse(cb, s.X - s.Size * 0.5f, s.Y - s.Size * 0.5f, s.Size, s.Size);
            }
        }

        private void DrawGlass(Graphics g, RectangleF r, float rad, float am)
        {
            using (GraphicsPath p = Round(r, rad))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(A(150f * am), 14, 40, 28), Color.FromArgb(A(175f * am), 4, 14, 10), 90f))
                    g.FillPath(b, p);
                GraphicsState cs = g.Save();
                g.SetClip(p, CombineMode.Intersect);
                using (Pen sl = new Pen(Color.FromArgb(10, 120, 255, 150), 1f))
                {
                    for (float y = r.Y + 0f; y < r.Bottom; y += 4f) g.DrawLine(sl, r.X, y, r.Right, y);
                }
                RectangleF gl = new RectangleF(r.X, r.Y, r.Width, r.Height * 0.35f);
                using (LinearGradientBrush glb = new LinearGradientBrush(gl, Color.FromArgb(26, 210, 255, 220), Color.FromArgb(0, 210, 255, 220), 90f))
                    g.FillRectangle(glb, gl);
                g.Restore(cs);
                using (Pen bp = new Pen(Color.FromArgb(A(120f * am), 140, 240, 150), 1.4f)) g.DrawPath(bp, p);
            }
        }

        private void DrawHeader(Graphics g, string text)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 130)))
                g.DrawString(text, fHead, b, new RectangleF(300f, 186f, 200f, 20f), sfCenter);
            using (Pen p = new Pen(Color.FromArgb(110, 150, 255, 130), 1.5f))
            {
                g.DrawLine(p, 200f, 196f, 292f, 196f);
                g.DrawLine(p, 508f, 196f, 600f, 196f);
            }
        }

        private void DrawInfoPanel(Graphics g)
        {
            RectangleF pr = new RectangleF(18f, 30f, 170f, 276f);
            DrawGlass(g, pr, 16f, 1f);
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(160, 255, 130)))
                g.DrawString("RECOMPILATION", fHead, hb, new RectangleF(pr.X + 14f, pr.Y + 12f, 150f, 16f), sfLeft);
            using (SolidBrush vb = new SolidBrush(Color.FromArgb(140, 190, 150)))
                g.DrawString("v1.0.0", fSmall, vb, new RectangleF(pr.X + 14f, pr.Y + 30f, 150f, 16f), sfLeft);
            using (Pen rp = new Pen(Color.FromArgb(70, 120, 240, 130), 1f))
            {
                g.DrawLine(rp, pr.X + 14f, pr.Y + 54f, pr.Right - 14f, pr.Y + 54f);
                g.DrawLine(rp, pr.X + 14f, pr.Y + 250f, pr.Right - 14f, pr.Y + 250f);
            }
            string[] heads = new string[] { "Native", "Widescreen", "EU + USA" };
            string[] descs = new string[] { "Runs as a real PC program.", "Modern resolutions supported.", "Both regions run, EU fix built in." };
            int[] icons = new int[] { 1, 2, 9 };
            Color ic = Color.FromArgb(200, 150, 255, 165);
            for (int i = 0; i < 3; i++)
            {
                float y = pr.Y + 64f + i * 62f;
                DrawGlyph(g, icons[i], pr.X + 28f, y + 14f, 1.2f, ic, 0f);
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(220, 255, 215)))
                    g.DrawString(heads[i], fName, nb, new RectangleF(pr.X + 52f, y, 112f, 18f), sfLeft);
                using (SolidBrush db = new SolidBrush(Color.FromArgb(140, 190, 150)))
                    g.DrawString(descs[i], fSmall, db, new RectangleF(pr.X + 52f, y + 19f, 108f, 36f), sfWrap);
            }
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(170, 235, 165)))
                g.DrawString("Thank you for playing!", fSmall, tb, new RectangleF(pr.X, pr.Y + 254f, pr.Width, 18f), sfCenter);
        }

        private void StatusRow(Graphics g, float px, float y, string name, string text, bool ok)
        {
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 3.0 + y);
            Color led = ok ? Color.FromArgb(110, 255, 90) : Color.FromArgb(255, 190, 70);
            using (SolidBrush gb = new SolidBrush(Color.FromArgb(ok ? A(40f + 80f * pulse) : 40, led)))
                g.FillEllipse(gb, px + 8f, y + 1f, 16f, 16f);
            using (SolidBrush cb = new SolidBrush(ok ? led : Color.FromArgb(170, led)))
                g.FillEllipse(cb, px + 12f, y + 5f, 8f, 8f);
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(225, 250, 225)))
                g.DrawString(name, fSmall, nb, new RectangleF(px + 30f, y, 80f, 18f), sfMid);
            using (SolidBrush tb = new SolidBrush(ok ? Color.FromArgb(150, 215, 150) : Color.FromArgb(230, 190, 110)))
                g.DrawString(text, fSmall, tb, new RectangleF(px + 100f, y, 76f, 18f), sfRight);
        }

        private void DrawStatusPanel(Graphics g)
        {
            int n = Math.Min(3, games.Count);
            float ph = 38f + 22f * (1 + n);
            RectangleF pr = new RectangleF(598f, 578f - ph, 186f, ph);
            DrawGlass(g, pr, 14f, 1f);
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(150, 255, 130)))
                g.DrawString("GAME FILES", fHead, hb, new RectangleF(pr.X + 14f, pr.Y + 8f, 160f, 16f), sfLeft);
            float y = pr.Y + 30f;
            StatusRow(g, pr.X, y, "Files", discKnown ? "FOUND" : "NOT SET", discKnown);
            y += 22f;
            for (int i = 0; i < n; i++)
            {
                string[] p = games[i].Split('|');
                StatusRow(g, pr.X, y, p.Length > 0 ? p[0] : "", p.Length > 1 ? p[1] : "", p.Length > 2 && p[2] == "1");
                y += 22f;
            }
        }

        private void DrawBuildPanel(Graphics g)
        {
            RectangleF pr = new RectangleF(160f, 196f, 480f, 232f);
            DrawGlass(g, pr, 16f, 1f);
            Color hc = bFailed ? Color.FromArgb(255, 150, 130) : Color.FromArgb(150, 255, 120);
            using (SolidBrush hb = new SolidBrush(hc))
                g.DrawString(bFailed ? "BUILD STOPPED" : "COMPILING", fBig, hb, new RectangleF(pr.X + 22f, pr.Y + 14f, 436f, 32f), sfLeft);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(232, 255, 226)))
                g.DrawString(bStatus, fBody, sb, new RectangleF(pr.X + 22f, pr.Y + 56f, 436f, 44f), sfWrap);
            using (SolidBrush db = new SolidBrush(Color.FromArgb(150, 200, 160)))
                g.DrawString(bDetail, fSmall, db, new RectangleF(pr.X + 22f, pr.Y + 104f, 436f, 50f), sfWrap);
            RectangleF bar = new RectangleF(pr.X + 22f, pr.Y + 168f, 436f, 14f);
            using (GraphicsPath bp = Round(bar, 7f))
            {
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(150, 3, 12, 8))) g.FillPath(bb, bp);
                GraphicsState cs = g.Save();
                g.SetClip(bp, CombineMode.Intersect);
                RectangleF fr;
                if (bPercent >= 0)
                {
                    float fw = bar.Width * Math.Min(100, bPercent) / 100f;
                    fr = new RectangleF(bar.X, bar.Y, Math.Max(4f, fw), bar.Height);
                }
                else
                {
                    float sx = bar.X + ((time * 0.6f) % 1f) * (bar.Width + 140f) - 140f;
                    fr = new RectangleF(sx, bar.Y, 140f, bar.Height);
                }
                Color fc = bFailed ? Color.FromArgb(230, 110, 90) : Color.FromArgb(90, 235, 100);
                using (LinearGradientBrush fb = new LinearGradientBrush(fr, fc, Mix(fc, Color.FromArgb(20, 90, 40), 0.6f), 90f))
                    g.FillRectangle(fb, fr);
                using (Pen st = new Pen(Color.FromArgb(40, 255, 255, 255), 2f))
                {
                    for (float x = fr.X - 14f + ((time * 24f) % 14f); x < fr.Right; x += 14f) g.DrawLine(st, x, fr.Bottom, x + 8f, fr.Y);
                }
                g.Restore(cs);
                using (Pen ep = new Pen(Color.FromArgb(150, 140, 240, 150), 1.4f)) g.DrawPath(ep, bp);
            }
            using (SolidBrush eb = new SolidBrush(Color.FromArgb(150, 200, 160)))
                g.DrawString(bElapsed, fSmall, eb, new RectangleF(pr.X + 22f, pr.Y + 190f, 300f, 18f), sfLeft);
            if (bPercent >= 0)
            {
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(225, 255, 220)))
                    g.DrawString(bPercent.ToString() + "%", fName, pb, new RectangleF(pr.Right - 122f, pr.Y + 186f, 100f, 22f), sfRight);
            }
        }

        private void DrawDonePanel(Graphics g)
        {
            RectangleF pr = new RectangleF(160f, 196f, 480f, 200f);
            DrawGlass(g, pr, 16f, 1f);
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(150, 255, 120)))
                g.DrawString("READY TO PLAY", fBig, hb, new RectangleF(pr.X + 22f, pr.Y + 14f, 436f, 32f), sfLeft);
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(235, 255, 230)))
                g.DrawString(dName, fBig, nb, new RectangleF(pr.X + 22f, pr.Y + 54f, 436f, 32f), sfLeft);
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(150, 200, 160)))
                g.DrawString(dText, fBody, tb, new RectangleF(pr.X + 22f, pr.Y + 96f, 436f, 56f), sfWrap);
            using (SolidBrush ib = new SolidBrush(Color.FromArgb(170, 235, 165)))
                g.DrawString("In the game: F1 opens the menu, F11 switches full screen.", fSmall, ib, new RectangleF(pr.X + 22f, pr.Y + 164f, 436f, 18f), sfLeft);
        }

        private void DrawFlash(Graphics g)
        {
            if (flash <= 0.01f) return;
            float rr = 12f + (1f - flash) * 190f;
            using (Pen p = new Pen(Color.FromArgb(A(flash * 200f), 170, 255, 140), 5f * flash + 1f))
                g.DrawEllipse(p, flashAt.X - rr, flashAt.Y - rr * 0.45f, rr * 2f, rr * 0.9f);
        }

        private void DrawNotice(Graphics g)
        {
            if (noticeLeft <= 0f || notice.Length == 0) return;
            float a = Math.Min(1f, noticeLeft / 0.6f);
            SizeF sz = g.MeasureString(notice, fBody);
            float w = Math.Min(560f, sz.Width + 44f);
            RectangleF r = new RectangleF(400f - w / 2f, 526f, w, 34f);
            using (GraphicsPath p = Round(r, 17f))
            {
                using (SolidBrush fb = new SolidBrush(Color.FromArgb(A(225f * a), 6, 20, 12))) g.FillPath(fb, p);
                using (Pen bp = new Pen(Color.FromArgb(A(220f * a), 150, 255, 150), 1.5f)) g.DrawPath(bp, p);
            }
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(A(255f * a), 232, 255, 226)))
                g.DrawString(notice, fBody, tb, r, sfCenter);
        }

        private void DrawGlyph(Graphics g, int id, float cx, float cy, float s, Color col, float h)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(s, s);
            using (Pen pen = new Pen(col, 2.2f))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(Math.Min(255, col.A / 3), col)))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (id == 0)
                {
                    PointF[] tri = new PointF[] { new PointF(-7f, -10f), new PointF(-7f, 10f), new PointF(10f, 0f) };
                    g.FillPolygon(fill, tri);
                    g.DrawPolygon(pen, tri);
                }
                else if (id == 1)
                {
                    PointF[] hx = new PointF[6];
                    for (int k = 0; k < 6; k++)
                    {
                        double a = (-90.0 + 60.0 * k) * Math.PI / 180.0;
                        hx[k] = new PointF((float)Math.Cos(a) * 11f, (float)Math.Sin(a) * 11f);
                    }
                    g.DrawPolygon(pen, hx);
                    g.DrawLine(pen, 0f, 0f, hx[1].X, hx[1].Y);
                    g.DrawLine(pen, 0f, 0f, hx[3].X, hx[3].Y);
                    g.DrawLine(pen, 0f, 0f, hx[5].X, hx[5].Y);
                }
                else if (id == 2)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        double a = (gearAngle + k * 45.0) * Math.PI / 180.0;
                        g.DrawLine(pen, (float)Math.Cos(a) * 8f, (float)Math.Sin(a) * 8f, (float)Math.Cos(a) * 12f, (float)Math.Sin(a) * 12f);
                    }
                    g.DrawEllipse(pen, -7f, -7f, 14f, 14f);
                    g.DrawEllipse(pen, -2.5f, -2.5f, 5f, 5f);
                }
                else if (id == 3)
                {
                    g.DrawLines(pen, new PointF[] { new PointF(-3f, -9f), new PointF(-10f, -9f), new PointF(-10f, 10f), new PointF(9f, 10f), new PointF(9f, 3f) });
                    g.DrawLine(pen, -2f, 2f, 10f, -10f);
                    g.DrawLines(pen, new PointF[] { new PointF(3f, -10f), new PointF(10f, -10f), new PointF(10f, -3f) });
                }
                else if (id == 4)
                {
                    g.DrawPolygon(pen, new PointF[] { new PointF(-11f, -7f), new PointF(-3f, -7f), new PointF(0f, -4f), new PointF(11f, -4f), new PointF(11f, 8f), new PointF(-11f, 8f) });
                }
                else if (id == 5)
                {
                    g.DrawLine(pen, 10f, 0f, -10f, 0f);
                    g.DrawLines(pen, new PointF[] { new PointF(-3f, -7f), new PointF(-10f, 0f), new PointF(-3f, 7f) });
                }
                else if (id == 6)
                {
                    g.DrawLine(pen, -8f, -8f, 8f, 8f);
                    g.DrawLine(pen, -8f, 8f, 8f, -8f);
                }
                else if (id == 7)
                {
                    g.DrawArc(pen, -9f, -9f, 18f, 18f, -60f, 280f);
                    g.DrawLine(pen, 4.5f, -7.8f, 11f, -6f);
                    g.DrawLine(pen, 4.5f, -7.8f, 5f, -14f);
                }
                else if (id == 8)
                {
                    g.DrawLines(pen, new PointF[] { new PointF(-9f, 0f), new PointF(-3f, 7f), new PointF(10f, -8f) });
                }
                else
                {
                    g.DrawLine(pen, -10f, -7f, 10f, -7f);
                    g.DrawLine(pen, -10f, 0f, 10f, 0f);
                    g.DrawLine(pen, -10f, 7f, 10f, 7f);
                    g.DrawEllipse(pen, -6f, -10f, 6f, 6f);
                    g.DrawEllipse(pen, 3f, -3f, 6f, 6f);
                    g.DrawEllipse(pen, -4f, 4f, 6f, 6f);
                }
            }
            g.Restore(st);
        }

        private void DrawLabel(Graphics g, string text, Font f, RectangleF r, StringFormat fmt, Color col, bool en, float h)
        {
            if (en)
            {
                RectangleF r2 = r;
                r2.Offset(1.3f, 0f);
                using (SolidBrush gb = new SolidBrush(Color.FromArgb(A(55f + 70f * h), 80, 255, 170))) g.DrawString(text, f, gb, r2, fmt);
            }
            using (SolidBrush tb = new SolidBrush(col)) g.DrawString(text, f, tb, r, fmt);
        }

        private void DrawHolo(Graphics g, Item it, int index)
        {
            float h = it.Hover, press = it.Press;
            bool en = it.Enabled;
            RectangleF r = it.Rect;
            float cx = r.X + r.Width / 2f + (en ? 8f * h : 0f);
            float cy = r.Y + r.Height / 2f;
            float sc = 1f + (en ? 0.02f * h : 0f) - 0.03f * press;
            float am = en ? 1f : 0.5f;
            

            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(sc, sc);
            RectangleF b = new RectangleF(-r.Width / 2f, -r.Height / 2f, r.Width, r.Height);
            GraphicsPath p = it.Path;

            if (en && h > 0.02f)
            {
                for (int i = 3; i >= 1; i--)
                {
                    using (Pen gp = new Pen(Color.FromArgb(A(15f * h * (4 - i)), 90, 255, 120), 3f * i))
                    {
                        gp.LineJoin = LineJoin.Round;
                        g.DrawPath(gp, p);
                    }
                }
            }
            Color c1 = Mix(Color.FromArgb(14, 42, 28), Color.FromArgb(26, 90, 50), h);
            Color c2 = Mix(Color.FromArgb(4, 16, 10), Color.FromArgb(9, 38, 22), h);
            using (LinearGradientBrush fb = new LinearGradientBrush(b, Color.FromArgb(A(125f * am), c1), Color.FromArgb(A(150f * am), c2), 90f))
                g.FillPath(fb, p);

            GraphicsState cs = g.Save();
            g.SetClip(p, CombineMode.Intersect);
            float lit = (float)Math.Pow(Math.Max(0f, 1f - (r.Y + r.Height / 2f - 200f) / 380f), 1.6) * (0.55f + 0.45f * devAmt);
            RectangleF lr = new RectangleF(b.X, b.Y, b.Width, b.Height);
            using (LinearGradientBrush lbr = new LinearGradientBrush(lr, Color.FromArgb(0, 130, 255, 160), Color.FromArgb(A((26f + 70f * lit + 50f * h) * am), 130, 255, 160), 90f))
                g.FillRectangle(lbr, lr);
            DrawLiquid(g, b, h, am, cx); float so = 0f;
            using (Pen sl = new Pen(Color.FromArgb(A((en ? 5f : 3f) + 5f * h), 120, 255, 150), 1f))
            {
                for (float y = b.Y + so; y < b.Bottom; y += 4f) g.DrawLine(sl, b.X, y, b.Right, y);
            }
            RectangleF gl = new RectangleF(b.X, b.Y, b.Width, b.Height * 0.5f);
            using (LinearGradientBrush glb = new LinearGradientBrush(gl, Color.FromArgb(A(64f * am), 225, 255, 235), Color.FromArgb(0, 210, 255, 220), 90f))
                g.FillRectangle(glb, gl);
            if (en && h > 0.02f)
            {
                float ph = (time * 0.9f + index * 0.21f) % 1.6f;
                if (ph < 1f)
                {
                    float sx = b.X - 30f + ph * (b.Width + 60f);
                    RectangleF sr = new RectangleF(sx - 44f, b.Y, 44f, b.Height);
                    using (LinearGradientBrush sb = new LinearGradientBrush(sr, Color.FromArgb(0, 160, 255, 170), Color.FromArgb(A(80f * h), 160, 255, 170), 0f))
                        g.FillRectangle(sb, sr);
                    using (Pen lp = new Pen(Color.FromArgb(A(40f * h), 200, 255, 200), 1.4f))
                        g.DrawLine(lp, sx, b.Y, sx, b.Bottom);
                }
            }
            g.Restore(cs);

            using (LinearGradientBrush bb = new LinearGradientBrush(b, Color.FromArgb(200, 170, 255, 170), Color.FromArgb(200, 90, 210, 120), 0f))
            {
                ColorBlend cbl = new ColorBlend(3);
                cbl.Positions = new float[] { 0f, 0.5f, 1f };
                cbl.Colors = new Color[] { Color.FromArgb(A((120f + 120f * h) * am), 170, 255, 170), Color.FromArgb(A((50f + 90f * h) * am), 90, 210, 120), Color.FromArgb(A((120f + 120f * h) * am), 170, 255, 170) };
                bb.InterpolationColors = cbl;
                using (Pen bp = new Pen(bb, 1.6f)) g.DrawPath(bp, p);
            }
            float bl = 7f, bo = 4f;
            using (Pen kp = new Pen(Color.FromArgb(A((70f + 170f * h) * am), 160, 255, 170), 1.6f))
            {
                g.DrawLines(kp, new PointF[] { new PointF(b.X - bo, b.Y + bl), new PointF(b.X - bo, b.Y - bo), new PointF(b.X + bl, b.Y - bo) });
                g.DrawLines(kp, new PointF[] { new PointF(b.Right + bo, b.Y + bl), new PointF(b.Right + bo, b.Y - bo), new PointF(b.Right - bl, b.Y - bo) });
                g.DrawLines(kp, new PointF[] { new PointF(b.X - bo, b.Bottom - bl), new PointF(b.X - bo, b.Bottom + bo), new PointF(b.X + bl, b.Bottom + bo) });
                g.DrawLines(kp, new PointF[] { new PointF(b.Right + bo, b.Bottom - bl), new PointF(b.Right + bo, b.Bottom + bo), new PointF(b.Right - bl, b.Bottom + bo) });
            }

            Color gc = Color.FromArgb(en ? 235 : 130, 150, 255, 165);
            Color tc = en ? Color.FromArgb(228, 255, 224) : Color.FromArgb(104, 136, 110);
            Font f = it.Big ? fBtn : fRow;
            if (it.Kind == 0)
            {
                float tx = it.Glyph >= 0 ? (it.Big ? b.X + 64f : b.X + 50f) : b.X + 10f;
                if (it.Glyph >= 0) DrawGlyph(g, it.Glyph, b.X + (it.Big ? 34f : 28f), 0f, it.Big ? 1.2f : 1f, gc, h);
                DrawLabel(g, it.Text, f, new RectangleF(tx, b.Y, b.Right - tx - 12f, b.Height), sfCenter, tc, en, h);
            }
            else
            {
                DrawLabel(g, it.Text, f, new RectangleF(b.X + 20f, b.Y, 180f, b.Height), sfMid, tc, en, h);
                if (it.Kind == 1)
                {
                    float t = it.Anim;
                    RectangleF tk = new RectangleF(b.Right - 66f, -10f, 44f, 20f);
                    using (GraphicsPath tp = Round(tk, 10f))
                    {
                        using (SolidBrush tf = new SolidBrush(Color.FromArgb(220, Mix(Color.FromArgb(10, 30, 20), Color.FromArgb(40, 150, 70), t))))
                            g.FillPath(tf, tp);
                        using (Pen tpn = new Pen(Mix(Color.FromArgb(90, 140, 100), Color.FromArgb(150, 255, 160), t), 1.5f))
                            g.DrawPath(tpn, tp);
                    }
                    using (SolidBrush kb = new SolidBrush(Mix(Color.FromArgb(120, 150, 125), Color.FromArgb(205, 255, 205), t)))
                        g.FillEllipse(kb, b.Right - 64f + 24f * t, -8f, 16f, 16f);
                }
                else if (it.Kind == 2)
                {
                    float vx = b.Right - 210f;
                    using (SolidBrush vb = new SolidBrush(Color.FromArgb(180, 255, 170)))
                        g.DrawString(it.Options[it.Index], fHead, vb, new RectangleF(vx + 16f, b.Y, 168f, b.Height), sfCenter);
                    using (Pen cp = new Pen(Color.FromArgb(A(190f * am), 150, 255, 165), 1.8f))
                    {
                        g.DrawLines(cp, new PointF[] { new PointF(vx + 8f, -5f), new PointF(vx + 2f, 0f), new PointF(vx + 8f, 5f) });
                        g.DrawLines(cp, new PointF[] { new PointF(b.Right - 22f, -5f), new PointF(b.Right - 16f, 0f), new PointF(b.Right - 22f, 5f) });
                    }
                }
                else
                {
                    using (SolidBrush vb = new SolidBrush(Color.FromArgb(160, 215, 165)))
                        g.DrawString(it.Value ?? "", fSmall, vb, new RectangleF(b.X + 130f, b.Y, b.Width - 130f - 16f, b.Height), sfPath);
                }
            }
            g.Restore(st);
        }
    }
}
