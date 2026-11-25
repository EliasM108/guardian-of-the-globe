#pragma warning disable 0414, 0169, 0219, 0649, 0168
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
            public bool Active;
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
        private float topAng, botAng, glitchT, glitchAmt, glitchJit, emaMs = 8f;
        private bool glitchOn;
        private volatile bool padBusy;
        private int quality, frameN;
        private string saveDir = "";
        private Bitmap beamTex, beamTex2, blobBmp, blobHi, bloomBmp, bloomA, bloomB;
        private System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        private int sel = -1, down = -1;
        private float scale = 1f, offX, offY;
        private bool discKnown;
        private string discText = "";
        private string[] choices = new string[0];
        private bool setFull, setWide, setUpdate, setKeep = true;
        private float hdrY = 217f;   // where a screen header is drawn (set by BuildSettings / BuildChoose)
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
            timer.Interval = 8;
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

        public void SetSaveDir(string p) { saveDir = p ?? ""; if (view == View.Settings) BuildItems(); }
        public void SetKeepOpen(bool v) { setKeep = v; } public void SetDone(string name, string text)
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
            if (v != view) { view = v; BuildItems(); if (v == View.Menu) RaiseClicked("refresh"); }
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
            float x = 260f, w = 280f, h = 48f, gap = 10f;
            int nb = discKnown ? 4 : 5;   // buttons shown
            float y = 358f - (nb * h + (nb - 1) * gap) / 2f;   // 358 = middle between the portals (PTOP 176, PBOT 540)
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
            if (spage != 0) { BuildSub(); return; }
            float x = 200f, w = 400f, h = 30f, step = 34f;
            hdrY = 205f;
            float ty = hdrY + 30f;
            string[] tn = new string[] { "GENERAL", "GRAPHICS", "SOUND", "CONTROLS" };
            string[] ti = new string[] { "t_gen", "t_gfx", "t_snd", "t_ctl" };
            for (int i = 0; i < 4; i++)
            {
                Item tb = Add(ti[i], tn[i], -1, x + i * (90f + 40f / 3f), ty, 90f, 28f, false);
                tb.Active = (i == stab);
                if (i == 1) { tb.Enabled = false; tb.Hint = "Not yet fully implemented"; }
            }
            float y = ty + 28f + 14f;
            if (stab == 0)
            {
                Item kp = Add("keepopen", "KEEP MENU OPEN", -1, x, y, w, h, false);
                kp.Kind = 1; kp.On = setKeep; kp.Anim = kp.On ? 1f : 0f; y += step;
                Item e = Add("savedir", "SAVE FOLDER", -1, x, y, w, h, false);
                e.Kind = 3; e.Value = string.IsNullOrEmpty(saveDir) ? "DEFAULT" : saveDir; y += step;
                Item f = Add("locate", "GAME FILES", -1, x, y, w, h, false);
                f.Kind = 3; f.Value = discKnown ? discText : "NOT SET"; y += step;
            }
            else if (stab == 1)
            {
                ReadGfx();
                Item a1 = Add("fullscreen", "FULL SCREEN", -1, x, y, w, h, false);
                a1.Kind = 1; a1.On = setFull; a1.Anim = a1.On ? 1f : 0f; y += step;
                Item a2 = Add("widescreen", "WIDESCREEN 16:9", -1, x, y, w, h, false);
                a2.Kind = 1; a2.On = setWide; a2.Anim = a2.On ? 1f : 0f; y += step;
                Item d = Add("region", "VIDEO MODE", -1, x, y, w, h, false);
                d.Kind = 2;
                d.Options = new string[] { "AUTOMATIC", "PAL 50 HZ", "NTSC 60 HZ" };
                d.Index = (setRegion == "pal") ? 1 : ((setRegion == "ntsc") ? 2 : 0); y += step;
                Item lk = Add("g_look", "PICTURE LOOK", -1, x, y, w, h, false);
                lk.Kind = 2; lk.Options = LookLabels; lk.Index = Math.Max(0, Array.IndexOf(LookWords, gLook)); y += step;
                Item sc = Add("g_scale", "RESOLUTION", -1, x, y, w, h, false);
                sc.Kind = 2; sc.Options = ScaleLabels; sc.Index = Math.Max(0, Math.Min(3, gScale - 1)); y += step;
                Item sh = Add("g_sharp", "TEXT AND MENUS", -1, x, y, w, h, false);
                sh.Kind = 2; sh.Options = new string[] { "SOFT", "SHARP", "SHARPEST" }; sh.Index = Math.Max(0, Array.IndexOf(SharpWords, gSharp)); y += step;
            }
            else if (stab == 2)
            {
                ReadAudio();
                Item v1 = Add("c_vol", "VOLUME", -1, x, y, w, h, false);
                v1.Kind = 2; v1.Options = VolOptions();
                v1.Index = (aVol + 5) / 10; if (v1.Index > 10) v1.Index = 10; y += step;
                Item m1 = Add("c_mute", "MUTE", -1, x, y, w, h, false);
                m1.Kind = 1; m1.On = aMute; m1.Anim = m1.On ? 1f : 0f; y += step;
            }
            else
            {
                Item k1 = Add("c_kb", "KEYBOARD AND MOUSE", -1, x, y, w, h, false); k1.Kind = 3; k1.Value = "EDIT"; y += step;
                Item p1 = Add("c_pad", "CONTROLLER", -1, x, y, w, h, false); p1.Kind = 3; p1.Value = "EDIT"; y += step;
                Item r1 = Add("c_reset", "RESET KEYBOARD", -1, x, y, w, h, false); r1.Kind = 3; r1.Value = "DEFAULT"; y += step;
            }
            Add("sback", "BACK", 5, x, y, w, h, false);
        }

        // picture look, inner resolution and 2D sharpness live in keys.ini (the runner reads them, and its F2 key writes the look there too)
        private void ReadGfx()
        {
            gLook = "sharp_edges"; gSharp = "sharp"; gScale = 1;
            string path = CtlFile("keys.ini");
            if (path == null || !System.IO.File.Exists(path)) return;
            try
            {
                foreach (string raw in System.IO.File.ReadAllLines(path))
                {
                    string n, v;
                    if (!SplitLine(raw, out n, out v)) continue;
                    string vv = Squash(v);
                    if (n == "look") { for (int i = 0; i < LookWords.Length; i++) { if (Squash(LookWords[i]) == vv) gLook = LookWords[i]; } }
                    else if (n == "scale") { int s; if (int.TryParse(v.Trim(), out s) && s >= 1 && s <= 4) gScale = s; }
                    else if (n == "sharp_2d" || n == "sharp2d") { for (int i = 0; i < SharpWords.Length; i++) { if (SharpWords[i] == vv) gSharp = SharpWords[i]; } }
                }
            }
            catch { }
        }

        private void KeysSet(string name, string value)
        {
            try
            {
                string path = CtlFile("keys.ini");
                if (path == null) return;
                List<string> lines = new List<string>();
                if (System.IO.File.Exists(path)) lines.AddRange(System.IO.File.ReadAllLines(path));
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    string n, v;
                    if (SplitLine(lines[i], out n, out v) && n == name) lines.RemoveAt(i);
                }
                lines.Add(name + " = " + value);
                EnsureDirOf(path);
                System.IO.File.WriteAllLines(path, lines.ToArray(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex) { ShowNotice("Could not save: " + ex.Message); }
        }

        // one step of a selector row (left arrow = -1, right arrow = +1); saves at once
        private void CycleItem(Item it, int dir)
        {
            int n = it.Options.Length;
            it.Index = ((it.Index + dir) % n + n) % n;
            string id = it.Id;
            if (id == "c_vol") { aVol = it.Index * 10; WriteAudio(); }
            else if (id == "c_tpl")
            {
                if (it.Index < 2)
                {
                    padMap = (string[])PadTemplates[it.Index].Clone();
                    WritePad();
                    for (int i = 0; i < 6; i++) { Item r = FindItem("c_pb" + i.ToString()); if (r != null) r.Index = PadTargetIndex(padMap[i]); }
                }
            }
            else if (id.StartsWith("c_pb"))
            {
                int pi = int.Parse(id.Substring(4));
                padMap[pi] = PadTargets[it.Index];
                WritePad();
                Item tp = FindItem("c_tpl");
                if (tp != null) tp.Index = TemplateIndex();
            }
            else if (id == "g_look") { gLook = LookWords[it.Index]; KeysSet("look", gLook); }
            else if (id == "g_scale") { gScale = it.Index + 1; KeysSet("scale", gScale.ToString()); ShowNotice("Resolution is used when the game starts."); }
            else if (id == "g_sharp") { gSharp = SharpWords[it.Index]; KeysSet("sharp_2d", gSharp); ShowNotice("Used when the game starts."); }
            else
            {
                if (id == "language") setLang = it.Index;
                else if (id == "region") setRegion = ValueFor(it);
                RaiseSetting(id, ValueFor(it));
            }
            Invalidate();
        }

        private void SwitchTab(string id)
        {
            int ni = id == "t_gfx" ? 1 : (id == "t_snd" ? 2 : (id == "t_ctl" ? 3 : 0));
            capAction = -1; capPad = -1;
            stab = ni;
            BuildItems();
            sel = ni;
            Invalidate();
        }

        private void BuildBuild()
        {
            if (bFailed)
            {
                Add("retry", "TRY AGAIN", 7, 190f, 454f, 200f, 48f, true);
                Add("back", "BACK", 5, 410f, 454f, 200f, 48f, true);
            }
            else
            {
                Add("cancel", "CANCEL", 6, 300f, 454f, 200f, 48f, true);
            }
        }

        private void BuildDone()
        {
            Add("doneplay", "PLAY", 0, 260f, 407f, 280f, 48f, true);
            Add("doneback", "BACK TO MENU", 5, 260f, 463f, 280f, 42f, true);
            
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
            Item y = Add("cyes", "YES, COMPILE", 8, 260f, 399f, 280f, 48f, true);
            y.Enabled = cOk;
            y.Hint = "This is not a recognised Ben 10 disc.";
            Add("cother", "CHOOSE ANOTHER FILE", 4, 260f, 457f, 280f, 42f, true);
        }

        private void DrawConfirmPanel(Graphics g)
        {
            RectangleF pr = new RectangleF(160f, 217f, 480f, 168f);
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
            if (quality >= 2) return;
            EnsureBlob();
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
            g.DrawImage(blobBmp, br, new RectangleF(0f, 0f, blobBmp.Width, blobBmp.Height), GraphicsUnit.Pixel);
            if (h > 0.25f && am > 0.9f) g.DrawImage(blobHi, br, new RectangleF(0f, 0f, blobHi.Width, blobHi.Height), GraphicsUnit.Pixel);
            using (Pen sp = new Pen(Color.FromArgb(A((70f + 60f * h) * am), 235, 255, 245), 1.2f))
                g.DrawLine(sp, b.X + b.Height * 0.45f, b.Y + 1.6f, b.Right - b.Height * 0.45f, b.Y + 1.6f);
            RectangleF lo = new RectangleF(b.X, b.Bottom - b.Height * 0.38f, b.Width, b.Height * 0.38f);
            using (LinearGradientBrush lb = new LinearGradientBrush(lo, Color.FromArgb(0, 120, 255, 170), Color.FromArgb(A((30f + 50f * h) * am), 120, 255, 170), 90f))
                g.FillRectangle(lb, lo);
        }

        private static ImageAttributes AlphaAttr(float a)
        {
            ImageAttributes ia = new ImageAttributes();
            ColorMatrix cm = new ColorMatrix();
            cm.Matrix33 = a < 0f ? 0f : (a > 1f ? 1f : a);
            ia.SetColorMatrix(cm);
            return ia;
        }

        private void EnsureBlob()
        {
            if (blobBmp != null) return;
            Bitmap raw = new Bitmap(128, 64, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(raw))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(0f, 0f, 127f, 63f);
                    using (PathGradientBrush pg = new PathGradientBrush(gp))
                    {
                        pg.CenterColor = Color.FromArgb(255, 190, 255, 215);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 190, 255, 215) };
                        g.FillPath(pg, gp);
                    }
                }
            }
            blobBmp = BakeAlpha(raw, 0.13f, false);
            blobHi = BakeAlpha(raw, 0.2f, false);
            raw.Dispose();
        }

        private static Bitmap BakeAlpha(Bitmap src, float a, bool flipX)
        {
            Bitmap o = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(o))
            using (ImageAttributes ia = AlphaAttr(a))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            if (flipX) o.RotateFlip(RotateFlipType.RotateNoneFlipX);
            return o;
        }

                private static readonly float[] siteX = new float[] { 462f, 492f, 522f, 552f, 580f, 602f, 622f, 655f, 672f, 694f, 716f, 744f, 776f };
        private static readonly float[] siteY = new float[] { 440f, 400f, 362f, 330f, 296f, 262f, 234f, 232f, 252f, 280f, 306f, 336f, 366f };
        private float blastT = -1f, blastX, blastY, blastNext = 6f;

        private void StepGlitch(float dt)
        {
            glitchAmt = 0f; glitchJit = 0f;
            if (!glitchOn) return;
            glitchT += dt;
            if (glitchT < 0f) return;
            float u = glitchT / 1.3f;
            if (u >= 1f) { glitchOn = false; return; }
            float s = 1f - u;
            float env = s * s * (3f - 2f * s);
            float at = Math.Min(1f, glitchT / 0.18f);
            at = at * at * (3f - 2f * at);
            glitchAmt = env * at;
            glitchJit = glitchAmt * glitchAmt;
        }

        private float GN(float seed)
        {
            return (float)(Math.Sin(time * 13.0 + seed * 2.3) * 0.6 + Math.Sin(time * 21.0 + seed * 5.1) * 0.4);
        }

        private void AdaptQuality(float ms)
        {
            emaMs = emaMs * 0.92f + ms * 0.08f;
            frameN++;
            if (frameN < 150 || frameN % 60 != 0) return;
            if (emaMs > 17f && quality < 2) quality++;
        }

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
                blastNext = 10f + Rf(rnd) * 20f; glitchOn = true; glitchT = -0.12f;
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

        private static string Friendly(string n)
        {
            string u = (n ?? "").ToUpper();
            if (u == "BEN-10-EU") return "EU VERSION (PAL)";
            if (u == "BEN-10-USA") return "USA VERSION (NTSC)";
            return u;
        }

        private void BuildChoose()
        {
            float tot = 34f + Math.Min(choices.Length, 4) * 56f + 42f; hdrY = 358f - tot / 2f; float y = hdrY + 34f;
            for (int i = 0; i < choices.Length && i < 4; i++)
            {
                Add("play:" + choices[i], Friendly(choices[i]), 0, 260f, y, 280f, 48f, true);
                y += 56f;
            }
            Add("cback", "BACK", 5, 260f, y, 280f, 42f, true);
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
            if (capAction >= 0) StepCapture();
            if (capPad >= 0) StepCapturePad();
            flash = Math.Max(0f, flash - dt * 1.8f);
            if (noticeLeft > 0f) noticeLeft -= dt;
            gearAngle += dt * 40f; bool anyHover = (sel >= 0 && sel < items.Count && items[sel].Enabled); devAmt += ((anyHover ? 1f : 0f) - devAmt) * Math.Min(1f, dt * (anyHover ? 6f : 16f)); float rs = 9f + 16f * devAmt; topAng += dt * rs; botAng -= dt * rs; if (topAng > 3600f) topAng -= 3600f; if (botAng < -3600f) botAng += 3600f; devPhase += dt * 0.12f; devPrev = sel; StepGlitch(dt); padTimer -= dt; if (padTimer <= 0f) { padTimer = 3f; if (!padBusy) { padBusy = true; System.Threading.ThreadPool.QueueUserWorkItem(delegate(object o) { try { PollPad(); } catch { } padBusy = false; }); } } StepBlast(dt);
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                float th = (i == sel && it.Enabled) ? 1f : 0f;
                it.Hover += (th - it.Hover) * Math.Min(1f, dt * (th > it.Hover ? 18f : 42f));
                if (th == 0f && it.Hover < 0.015f) it.Hover = 0f;
                float tp = (i == down && sel == down && it.Enabled) ? 1f : 0f;
                it.Press += (tp - it.Press) * Math.Min(1f, dt * (tp > it.Press ? 26f : 45f));
                if (tp == 0f && it.Press < 0.015f) it.Press = 0f;
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
            scale = Math.Min(Width / 800f, Height / 662f);
            if (scale <= 0f) scale = 1f;
            offX = (Width - 800f * scale) / 2f;
            offY = (Height - 662f * scale) / 2f;
        }

        private int HitTest(Point p)
        {
            UpdateScale();
            float x = (p.X - offX) / scale;
            float y = (p.Y - offY) / scale - DY;
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
            if (capPad >= 0) return;
            if (capAction >= 0)
            {
                if ((DateTime.Now - capStart).TotalMilliseconds < 300) return;
                int msc = 0;
                if (e.Button == MouseButtons.Left) msc = 1000;
                else if (e.Button == MouseButtons.Middle) msc = 1001;
                else if (e.Button == MouseButtons.Right) msc = 1002;
                else if (e.Button == MouseButtons.XButton1) msc = 1003;
                else if (e.Button == MouseButtons.XButton2) msc = 1004;
                if (msc > 0) CaptureToken(msc);
                return;
            }
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
                lastUpX = (e.X - offX) / scale;
                int d = down;
                down = -1;
                if (d >= 0 && hit == d) Activate(d);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (capAction >= 0 && e.Delta != 0 && (DateTime.Now - capStart).TotalMilliseconds >= 300) CaptureToken(e.Delta > 0 ? 1010 : 1011);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (capAction >= 0 || capPad >= 0) return true;
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
            if (capAction >= 0) { CaptureKey(e); return; }
            if (capPad >= 0)
            {
                if (e.KeyCode == Keys.Escape) { capPad = -1; ShowNotice("Cancelled."); RebuildKeep(); }
                e.Handled = true; e.SuppressKeyPress = true; return;
            }
            if (sel >= 0 && sel < items.Count && items[sel].Enabled && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
            {
                Item cur = items[sel];
                if (cur.Kind == 2) { CycleItem(cur, e.KeyCode == Keys.Right ? 1 : -1); e.Handled = true; return; }
                if (view == View.Settings && spage == 0 && cur.Id != null && cur.Id.StartsWith("t_"))
                {
                    int tj = sel, tst = (e.KeyCode == Keys.Right ? 1 : -1);
                    do { tj += tst; } while (tj >= 0 && tj < 4 && !items[tj].Enabled);
                    if (tj >= 0 && tj < 4) SwitchTab(items[tj].Id);
                    e.Handled = true; return;
                }
            }
            if (view == View.Settings && spage == 0 && items.Count > 5 && (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up))
            {
                if (e.KeyCode == Keys.Down && sel >= 0 && sel < 4) { sel = 4; e.Handled = true; return; }
                if (e.KeyCode == Keys.Up && sel == 4) { sel = stab; e.Handled = true; return; }
                if (e.KeyCode == Keys.Up && sel >= 0 && sel < 4) { sel = items.Count - 1; e.Handled = true; return; }
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (view == View.Settings && spage != 0) { SubBack(); } else if (view == View.Settings || view == View.Choose || view == View.Confirm) { view = View.Menu; BuildItems(); }
                e.Handled = true;
                return;
            }
            if (items.Count == 0) return;
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Right) { sel = (sel + 1) % items.Count; e.Handled = true; }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Left) { sel = (sel <= 0) ? items.Count - 1 : sel - 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space)
            {
                int i = sel; lastUpX = -1f;
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
            if (id.StartsWith("t_")) { SwitchTab(id); return; }
            if (it.Kind == 2)
            {
                int cd = 1;
                if (lastUpX >= it.Rect.Right - 210f && lastUpX < it.Rect.Right - 108f) cd = -1;
                lastUpX = -1f;
                CycleItem(it, cd);
                return;
            }
            if (HandleCtl(it)) return;
            if (it.Kind == 1)
            {
                it.On = !it.On;
                if (id == "fullscreen") setFull = it.On;
                else if (id == "widescreen") setWide = it.On;
                else if (id == "autoupdate") setUpdate = it.On; else if (id == "keepopen") setKeep = it.On;
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
            if (id == "compile") { PrepareConfirm(); view = View.Confirm; BuildItems(); return; } if (id == "cyes") { view = View.Menu; BuildItems(); RaiseClicked("compile"); return; } if (id == "cother") { view = View.Menu; BuildItems(); RaiseClicked("locate"); return; } if (id == "settings") { spage = 0; stab = 0; view = View.Settings; BuildItems(); return; }
            if (id == "sback" || id == "cback") { view = View.Menu; BuildItems(); RaiseClicked("refresh"); return; }
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

        // ================= controls and sound (keyboard, controller, volume) =================
        private class Act
        {
            public string Key, Label;
            public List<string> Def = new List<string>();
            public List<string> Cur = new List<string>();
            public bool Dirty;
        }
        private List<Act> acts = new List<Act>();
        private int spage, stab, grp, capAction = -1, aVol = 100;
        private float lastUpX = -1f, row2Right;
        private int capPad = -1;
        private ushort capBase;
        private DateTime lastPadPoll = DateTime.MinValue;
        private static readonly string[] PadActLabels = new string[] { "JUMP  (CROSS)", "CIRCLE", "SQUARE", "TRIANGLE", "L1", "R1" };
        private static readonly string[] PadActTargets = new string[] { "cross", "circle", "square", "triangle", "l1", "r1" };
        private static readonly string[] PadShow = new string[] { "A", "B", "X", "Y", "LB", "RB" };
        private static readonly ushort[] PadBits = new ushort[] { 0x1000, 0x2000, 0x4000, 0x8000, 0x0100, 0x0200 };
        private string gLook = "sharp_edges", gSharp = "sharp";
        private int gScale = 1;
        private const float CapSeconds = 5.5f;
        private static readonly string[] LookWords = new string[] { "sharp_edges", "sharp", "crisp_edges", "crisp", "soft_edges", "soft" };
        private static readonly string[] LookLabels = new string[] { "SHARP + EDGES", "SHARP", "CRISP + EDGES", "CRISP", "SOFT + EDGES", "SOFT" };
        private static readonly string[] SharpWords = new string[] { "soft", "sharp", "sharpest" };
        private static readonly string[] ScaleLabels = new string[] { "1X  (NATIVE)", "2X", "3X", "4X" };
        private bool aMute;
        private DateTime capStart = DateTime.MinValue;
        private string[] padMap = new string[] { "cross", "circle", "square", "triangle", "l1", "r1" };
        private static Dictionary<string, int> alias;
        private static readonly string[] GroupNames = new string[] { "FACE BUTTONS + START", "SHOULDERS + TRIGGERS", "D-PAD", "LEFT STICK (MOVE)", "RIGHT STICK (CAMERA)" };
        private static readonly int[][] GroupActs = new int[][] { new int[] { 0, 1, 2, 3, 4, 5 }, new int[] { 6, 7, 8, 9, 10, 11 }, new int[] { 12, 13, 14, 15 }, new int[] { 16, 17, 18, 19 }, new int[] { 20, 21, 22, 23 } };
        private static readonly string[] PadTargets = new string[] { "cross", "circle", "square", "triangle", "l1", "r1", "l2", "r2", "l3", "r3", "start", "select", "none" };
        private static readonly string[] PadOptions = new string[] { "CROSS", "CIRCLE", "SQUARE", "TRIANGLE", "L1", "R1", "L2", "R2", "L3", "R3", "START", "SELECT", "NONE" };
        private static readonly string[] PadKeys = new string[] { "a", "b", "x", "y", "lb", "rb" };
        private static readonly string[] PadLabels = new string[] { "A  (BOTTOM BUTTON)", "B  (RIGHT BUTTON)", "X  (LEFT BUTTON)", "Y  (TOP BUTTON)", "LB  (LEFT BUMPER)", "RB  (RIGHT BUMPER)" };
        private static readonly string[][] PadTemplates = new string[][] {
            new string[] { "cross", "circle", "square", "triangle", "l1", "r1" },
            new string[] { "circle", "cross", "triangle", "square", "l1", "r1" } };

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct XIPad { public uint Packet; public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }

        [System.Runtime.InteropServices.DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XIGetState(uint idx, out XIPad st);

        private static ushort PadMask()
        {
            ushort m = 0;
            try
            {
                for (uint k = 0; k < 4; k++) { XIPad st; if (XIGetState(k, out st) == 0) m |= st.Buttons; }
            }
            catch { }
            return m;
        }

        private string PadOfTarget(string target)
        {
            for (int i = 0; i < 6; i++) { if (padMap[i] == target) return PadShow[i]; }
            return "NONE";
        }

        private void StartPadCapture(int i)
        {
            capAction = -1;
            capPad = i; capStart = DateTime.Now; capBase = PadMask(); lastPadPoll = DateTime.MinValue;
            ShowNotice("Press a controller button. No input for 5 seconds = NONE.  Esc cancels.");
            RebuildKeep();
        }

        private void StepCapturePad()
        {
            int pi = capPad;
            double el = (DateTime.Now - capStart).TotalSeconds;
            if (el >= CapSeconds)
            {
                capPad = -1;
                for (int q = 0; q < 6; q++) { if (padMap[q] == PadActTargets[pi]) padMap[q] = "none"; }
                WritePad();
                ShowNotice(PadActLabels[pi] + " = NONE  (no input)");
                RebuildKeep();
                return;
            }
            Item row = FindItem("c_pq" + pi.ToString());
            if (row != null) row.Value = "PRESS A BUTTON...  " + ((int)Math.Ceiling(CapSeconds - el)).ToString();
            if ((DateTime.Now - lastPadPoll).TotalMilliseconds < 40) return;
            lastPadPoll = DateTime.Now;
            ushort nowM = PadMask();
            ushort fresh = (ushort)(nowM & ~capBase);
            capBase = (ushort)(capBase & nowM);
            if (el < 0.3 || fresh == 0) return;
            int phys = -1;
            for (int q = 0; q < 6; q++) { if ((fresh & PadBits[q]) != 0) { phys = q; break; } }
            if (phys < 0) { ShowNotice("That button cannot be remapped. Use A, B, X, Y, LB or RB."); return; }
            string oldT = padMap[phys];
            int holder = -1;
            for (int q = 0; q < 6; q++) { if (padMap[q] == PadActTargets[pi]) holder = q; }
            capPad = -1;
            if (holder >= 0 && holder != phys) padMap[holder] = oldT;
            padMap[phys] = PadActTargets[pi];
            WritePad();
            ShowNotice(PadActLabels[pi] + " = " + PadShow[phys]);
            RebuildKeep();
        }

        private void DrawCapturePopup(Graphics g)
        {
            if (capAction < 0 && capPad < 0) return;
            double el = (DateTime.Now - capStart).TotalSeconds;
            float left = (float)Math.Max(0.0, 1.0 - el / CapSeconds);
            RectangleF pr = new RectangleF(190f, 290f, 420f, 150f);
            using (GraphicsPath gp = Round(pr, 16f))
            {
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(238, 3, 12, 8))) g.FillPath(bb, gp);
                using (Pen pp = new Pen(Color.FromArgb(200, 120, 235, 80), 1.6f)) g.DrawPath(pp, gp);
            }
            string what = "";
            if (capAction >= 0 && capAction < acts.Count) what = acts[capAction].Label;
            else if (capPad >= 0) what = PadActLabels[capPad];
            string head = capAction >= 0 ? "PRESS A KEY OR MOUSE BUTTON" : "PRESS A CONTROLLER BUTTON";
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(150, 255, 130))) g.DrawString(head, fName, hb, new RectangleF(pr.X, pr.Y + 18f, pr.Width, 24f), sfCenter);
            using (SolidBrush wb = new SolidBrush(Color.FromArgb(232, 255, 226))) g.DrawString(what, fBig, wb, new RectangleF(pr.X, pr.Y + 48f, pr.Width, 40f), sfCenter);
            RectangleF bar = new RectangleF(pr.X + 40f, pr.Y + 100f, pr.Width - 80f, 6f);
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(60, 120, 235, 80))) g.FillRectangle(tb, bar);
            using (SolidBrush fb = new SolidBrush(Color.FromArgb(220, 120, 235, 80))) g.FillRectangle(fb, bar.X, bar.Y, bar.Width * left, bar.Height);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(140, 190, 150))) g.DrawString("No input = empty.   Esc cancels.", fSmall, sb, new RectangleF(pr.X, pr.Y + 114f, pr.Width, 20f), sfCenter);
        }

        private string CtlFile(string name)
        {
            if (string.IsNullOrEmpty(saveDir)) return null;
            return System.IO.Path.Combine(saveDir, name);
        }

        private static void EnsureDirOf(string path)
        {
            string d = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(d)) System.IO.Directory.CreateDirectory(d);
        }

        private static string Squash(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in s.ToLowerInvariant()) { if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c); }
            return sb.ToString();
        }

        private static bool SplitLine(string raw, out string name, out string val)
        {
            name = ""; val = "";
            string s = raw.Trim();
            if (s.Length == 0 || s[0] == '#' || s[0] == ';' || s[0] == '[') return false;
            int h = s.IndexOf(" #");
            if (h >= 0) s = s.Substring(0, h);
            int eq = s.IndexOf('=');
            if (eq <= 0) return false;
            name = s.Substring(0, eq).Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            val = s.Substring(eq + 1).Trim();
            return true;
        }

        private static void AddAlias(string k, int v) { alias[k] = v; }

        private static void BuildAlias()
        {
            alias = new Dictionary<string, int>();
            for (int i = 0; i < 26; i++) AddAlias(((char)('a' + i)).ToString(), 4 + i);
            for (int i = 1; i <= 9; i++) AddAlias(i.ToString(), 29 + i);
            AddAlias("0", 39);
            for (int i = 1; i <= 12; i++) AddAlias("f" + i.ToString(), 57 + i);
            AddAlias("enter", 40); AddAlias("return", 40); AddAlias("esc", 41); AddAlias("escape", 41);
            AddAlias("backspace", 42); AddAlias("tab", 43); AddAlias("space", 44); AddAlias("spacebar", 44); AddAlias("leertaste", 44);
            AddAlias("minus", 45); AddAlias("plus", 46); AddAlias("equals", 46); AddAlias("semicolon", 51);
            AddAlias("comma", 54); AddAlias("period", 55); AddAlias("caps", 57); AddAlias("capslock", 57);
            AddAlias("insert", 73); AddAlias("ins", 73); AddAlias("home", 74); AddAlias("pageup", 75); AddAlias("pgup", 75);
            AddAlias("delete", 76); AddAlias("del", 76); AddAlias("end", 77); AddAlias("pagedown", 78); AddAlias("pgdn", 78);
            AddAlias("right", 79); AddAlias("left", 80); AddAlias("down", 81); AddAlias("up", 82);
            AddAlias("keypadenter", 88); AddAlias("kpenter", 88); AddAlias("numenter", 88); AddAlias("numpadenter", 88);
            AddAlias("ctrl", 224); AddAlias("control", 224); AddAlias("lctrl", 224); AddAlias("leftctrl", 224);
            AddAlias("shift", 225); AddAlias("lshift", 225); AddAlias("leftshift", 225);
            AddAlias("alt", 226); AddAlias("lalt", 226); AddAlias("leftalt", 226);
            AddAlias("rctrl", 228); AddAlias("rightctrl", 228); AddAlias("rshift", 229); AddAlias("rightshift", 229);
            AddAlias("ralt", 230); AddAlias("rightalt", 230);
            AddAlias("leftmouse", 1000); AddAlias("middlemouse", 1001); AddAlias("rightmouse", 1002);
            AddAlias("mousex1", 1003); AddAlias("mousex2", 1004); AddAlias("wheelup", 1010); AddAlias("wheeldown", 1011);
        }

        // a key as the app keeps it: "#<SDL scancode>" when known, "?<text>" when not
        private static string ParseToken(string t)
        {
            t = t.Trim();
            if (t.Length == 0) return null;
            string sq = Squash(t);
            if (sq == "none") return null;
            if (sq.Length == 0) return "?" + t;
            if (sq.Length > 3 && sq.StartsWith("key"))
            {
                int n;
                if (int.TryParse(sq.Substring(3), out n) && n > 0) return "#" + n.ToString();
            }
            if (alias == null) BuildAlias();
            int code;
            if (alias.TryGetValue(sq, out code)) return "#" + code.ToString();
            return "?" + t;
        }

        private static string ScanName(int sc)
        {
            if (sc >= 4 && sc <= 29) return ((char)('A' + (sc - 4))).ToString();
            if (sc >= 30 && sc <= 38) return ((char)('1' + (sc - 30))).ToString();
            if (sc == 39) return "0";
            if (sc >= 58 && sc <= 69) return "F" + (sc - 57).ToString();
            if (sc >= 89 && sc <= 97) return "KP " + (sc - 88).ToString();
            switch (sc)
            {
                case 40: return "ENTER";
                case 41: return "ESC";
                case 42: return "BACKSPACE";
                case 43: return "TAB";
                case 44: return "SPACE";
                case 45: return "-";
                case 46: return "=";
                case 47: return "[";
                case 48: return "]";
                case 49: return "\\";
                case 51: return ";";
                case 52: return "'";
                case 53: return "`";
                case 54: return ",";
                case 55: return ".";
                case 56: return "/";
                case 57: return "CAPS LOCK";
                case 70: return "PRTSC";
                case 71: return "SCROLL LOCK";
                case 72: return "PAUSE";
                case 73: return "INSERT";
                case 74: return "HOME";
                case 75: return "PAGE UP";
                case 76: return "DELETE";
                case 77: return "END";
                case 78: return "PAGE DOWN";
                case 79: return "RIGHT";
                case 80: return "LEFT";
                case 81: return "DOWN";
                case 82: return "UP";
                case 83: return "NUM LOCK";
                case 84: return "KP /";
                case 85: return "KP *";
                case 86: return "KP -";
                case 87: return "KP +";
                case 88: return "KP ENTER";
                case 98: return "KP 0";
                case 99: return "KP .";
                case 224: return "L CTRL";
                case 225: return "L SHIFT";
                case 226: return "L ALT";
                case 228: return "R CTRL";
                case 229: return "R SHIFT";
                case 230: return "R ALT";
                case 1000: return "LEFT MOUSE";
                case 1001: return "MIDDLE MOUSE";
                case 1002: return "RIGHT MOUSE";
                case 1003: return "MOUSE X1";
                case 1004: return "MOUSE X2";
                case 1010: return "WHEEL UP";
                case 1011: return "WHEEL DOWN";
            }
            return "KEY " + sc.ToString();
        }

        private static int ScanOf(Keys k)
        {
            if (k >= Keys.A && k <= Keys.Z) return 4 + ((int)k - (int)Keys.A);
            if (k >= Keys.D1 && k <= Keys.D9) return 30 + ((int)k - (int)Keys.D1);
            if (k == Keys.D0) return 39;
            if (k >= Keys.F1 && k <= Keys.F12) return 58 + ((int)k - (int)Keys.F1);
            if (k >= Keys.NumPad1 && k <= Keys.NumPad9) return 89 + ((int)k - (int)Keys.NumPad1);
            if (k == Keys.NumPad0) return 98;
            switch (k)
            {
                case Keys.Return: return 40;
                case Keys.Escape: return 41;
                case Keys.Back: return 42;
                case Keys.Tab: return 43;
                case Keys.Space: return 44;
                case Keys.OemMinus: return 45;
                case Keys.Oemplus: return 46;
                case Keys.OemOpenBrackets: return 47;
                case Keys.OemCloseBrackets: return 48;
                case Keys.OemPipe: return 49;
                case Keys.OemSemicolon: return 51;
                case Keys.OemQuotes: return 52;
                case Keys.Oemtilde: return 53;
                case Keys.Oemcomma: return 54;
                case Keys.OemPeriod: return 55;
                case Keys.OemQuestion: return 56;
                case Keys.Capital: return 57;
                case Keys.PrintScreen: return 70;
                case Keys.Scroll: return 71;
                case Keys.Pause: return 72;
                case Keys.Insert: return 73;
                case Keys.Home: return 74;
                case Keys.Prior: return 75;
                case Keys.Delete: return 76;
                case Keys.End: return 77;
                case Keys.Next: return 78;
                case Keys.Right: return 79;
                case Keys.Left: return 80;
                case Keys.Down: return 81;
                case Keys.Up: return 82;
                case Keys.NumLock: return 83;
                case Keys.Divide: return 84;
                case Keys.Multiply: return 85;
                case Keys.Subtract: return 86;
                case Keys.Add: return 87;
                case Keys.Decimal: return 99;
                case Keys.ControlKey: return 224;
                case Keys.ShiftKey: return 225;
                case Keys.Menu: return 226;
            }
            return 0;
        }

        private static bool Reserved(int sc)
        {
            return sc == 58 || sc == 59 || sc == 60 || sc == 61 || sc == 62 || sc == 63 || sc == 65 || sc == 66 || sc == 68;
        }

        private static string TokText(string tok)
        {
            if (tok.StartsWith("#")) { int n; if (int.TryParse(tok.Substring(1), out n)) return ScanName(n); }
            if (tok.StartsWith("?")) return tok.Substring(1).ToUpperInvariant();
            return tok.ToUpperInvariant();
        }

        // what keys.ini gets for a key: "key 44" for a keyboard key, the runner's own names for mouse buttons and the wheel
        private static string TokFile(string tok)
        {
            if (tok.StartsWith("#"))
            {
                int n;
                if (int.TryParse(tok.Substring(1), out n))
                {
                    if (n == 1000) return "Left Mouse";
                    if (n == 1001) return "Middle Mouse";
                    if (n == 1002) return "Right Mouse";
                    if (n == 1003) return "Mouse X1";
                    if (n == 1004) return "Mouse X2";
                    if (n == 1010) return "Wheel Up";
                    if (n == 1011) return "Wheel Down";
                    return "key " + n.ToString();
                }
            }
            if (tok.StartsWith("?")) return tok.Substring(1);
            return tok;
        }

        private static string KeyText(Act a)
        {
            if (a.Cur.Count == 0) return "NONE";
            List<string> p = new List<string>();
            foreach (string t in a.Cur) p.Add(TokText(t));
            return string.Join(", ", p.ToArray());
        }

        private void AddAct(string key, string label, params int[] def)
        {
            Act a = new Act();
            a.Key = key; a.Label = label;
            for (int i = 0; i < def.Length; i++) { a.Def.Add("#" + def[i].ToString()); a.Cur.Add("#" + def[i].ToString()); }
            acts.Add(a);
        }

        private void EnsureActs()
        {
            if (acts.Count == 0)
            {
                AddAct("cross", "JUMP  (CROSS)", 44, 40, 88);
                AddAct("circle", "CIRCLE", 8, 1001);
                AddAct("square", "SQUARE", 1000);
                AddAct("triangle", "TRIANGLE", 1002);
                AddAct("start", "START", 41);
                AddAct("select", "SELECT", 43, 42);
                AddAct("l1", "L1", 225, 1004);
                AddAct("r1", "R1", 9);
                AddAct("l2", "L2", 20);
                AddAct("r2", "R2", 21);
                AddAct("l3", "L3  (STICK CLICK)", 27);
                AddAct("r3", "R3  (STICK CLICK)", 6);
                AddAct("dpad_up", "D-PAD UP", 82);
                AddAct("dpad_down", "D-PAD DOWN", 81);
                AddAct("dpad_left", "D-PAD LEFT", 80);
                AddAct("dpad_right", "D-PAD RIGHT", 79);
                AddAct("left_up", "MOVE UP", 26);
                AddAct("left_down", "MOVE DOWN", 22);
                AddAct("left_left", "MOVE LEFT", 4);
                AddAct("left_right", "MOVE RIGHT", 7);
                AddAct("right_up", "CAMERA UP", 12);
                AddAct("right_down", "CAMERA DOWN", 14);
                AddAct("right_left", "CAMERA LEFT", 13);
                AddAct("right_right", "CAMERA RIGHT", 15);
            }
            LoadKeys();
        }

        private void LoadKeys()
        {
            foreach (Act a in acts) { a.Cur = new List<string>(a.Def); a.Dirty = false; }
            string path = CtlFile("keys.ini");
            if (path == null || !System.IO.File.Exists(path)) return;
            string[] lines;
            try { lines = System.IO.File.ReadAllLines(path); } catch { return; }
            foreach (string raw in lines)
            {
                string name, val;
                if (!SplitLine(raw, out name, out val)) continue;
                foreach (Act a in acts)
                {
                    if (a.Key != name) continue;
                    List<string> toks = new List<string>();
                    foreach (string t in val.Split(','))
                    {
                        string c = ParseToken(t);
                        if (c != null && !toks.Contains(c)) toks.Add(c);
                    }
                    a.Cur = toks;
                }
            }
        }

        private static bool SameSet(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (string s in a) { if (!b.Contains(s)) return false; }
            return true;
        }

        // writes only the lines of the keys the player changed; a key back at its default loses its line; everything else in keys.ini stays
        private void SaveKeys()
        {
            try
            {
                string path = CtlFile("keys.ini");
                if (path == null) return;
                List<string> lines = new List<string>();
                if (System.IO.File.Exists(path)) lines.AddRange(System.IO.File.ReadAllLines(path));
                foreach (Act a in acts)
                {
                    if (!a.Dirty) continue;
                    for (int i = lines.Count - 1; i >= 0; i--)
                    {
                        string n, v;
                        if (SplitLine(lines[i], out n, out v) && n == a.Key) lines.RemoveAt(i);
                    }
                    if (a.Cur.Count == 0) lines.Add(a.Key + " = none");
                    else if (!SameSet(a.Cur, a.Def))
                    {
                        List<string> p = new List<string>();
                        foreach (string t in a.Cur) p.Add(TokFile(t));
                        lines.Add(a.Key + " = " + string.Join(", ", p.ToArray()));
                    }
                    a.Dirty = false;
                }
                EnsureDirOf(path);
                System.IO.File.WriteAllLines(path, lines.ToArray(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex) { ShowNotice("Could not save keys: " + ex.Message); }
        }

        private void BindKey(int ai, string tok)
        {
            Act a = acts[ai];
            List<string> old = new List<string>(a.Cur);
            a.Cur = new List<string>();
            a.Cur.Add(tok);
            a.Dirty = true;
            foreach (Act b in acts)
            {
                if (b == a) continue;
                if (b.Cur.Contains(tok))
                {
                    b.Cur.Remove(tok);
                    if (b.Cur.Count == 0) b.Cur = new List<string>(old);
                    b.Dirty = true;
                }
            }
            SaveKeys();
        }

        private void ResetKeys()
        {
            foreach (Act a in acts) { a.Cur = new List<string>(a.Def); a.Dirty = true; }
            SaveKeys();
        }

        // ---- controller template (pad.ini) and sound (audio.ini); the runner reads both when the game starts / the F3 F4 F6 keys change the sound
        private void ReadPad()
        {
            padMap = new string[] { "cross", "circle", "square", "triangle", "l1", "r1" };
            string path = CtlFile("pad.ini");
            if (path == null || !System.IO.File.Exists(path)) return;
            try
            {
                foreach (string raw in System.IO.File.ReadAllLines(path))
                {
                    string n, v;
                    if (!SplitLine(raw, out n, out v)) continue;
                    string vv = v.Trim().ToLowerInvariant();
                    for (int i = 0; i < 6; i++) { if (n == PadKeys[i] && Array.IndexOf(PadTargets, vv) >= 0) padMap[i] = vv; }
                }
            }
            catch { }
        }

        private void WritePad()
        {
            try
            {
                string path = CtlFile("pad.ini");
                if (path == null) return;
                string[] lines = new string[] {
                    "# Guardian of the Globe: controller buttons (pad button = PS2 button). Edited in the app: Settings > Controls and Sound.",
                    "a = " + padMap[0], "b = " + padMap[1], "x = " + padMap[2], "y = " + padMap[3],
                    "lb = " + padMap[4], "rb = " + padMap[5], "ls = l3", "rs = r3" };
                EnsureDirOf(path);
                System.IO.File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex) { ShowNotice("Could not save pad.ini: " + ex.Message); }
        }

        private int TemplateIndex()
        {
            for (int t = 0; t < 2; t++)
            {
                bool same = true;
                for (int i = 0; i < 6; i++) { if (padMap[i] != PadTemplates[t][i]) same = false; }
                if (same) return t;
            }
            return 2;
        }

        private static int PadTargetIndex(string s)
        {
            for (int i = 0; i < PadTargets.Length; i++) { if (PadTargets[i] == s) return i; }
            return 0;
        }

        private void ReadAudio()
        {
            aVol = 100; aMute = false;
            string path = CtlFile("audio.ini");
            if (path == null || !System.IO.File.Exists(path)) return;
            try
            {
                foreach (string raw in System.IO.File.ReadAllLines(path))
                {
                    string n, v;
                    if (!SplitLine(raw, out n, out v)) continue;
                    if (n == "volume") { int x; if (int.TryParse(v, out x)) aVol = Math.Max(0, Math.Min(100, x)); }
                    else if (n == "mute") { string m = v.ToLowerInvariant(); aMute = (m == "1" || m == "on" || m == "yes" || m == "true"); }
                }
            }
            catch { }
        }

        private void WriteAudio()
        {
            try
            {
                string path = CtlFile("audio.ini");
                if (path == null) return;
                string[] lines = new string[] { "# Guardian of the Globe: sound (in the game: F3 / F4 volume, F6 mute)", "volume = " + aVol.ToString(), "mute = " + (aMute ? "1" : "0") };
                EnsureDirOf(path);
                System.IO.File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex) { ShowNotice("Could not save sound: " + ex.Message); }
        }

        private static string[] VolOptions()
        {
            string[] o = new string[11];
            for (int i = 0; i <= 10; i++) o[i] = (i * 10).ToString() + "%";
            return o;
        }

        // ---- the pages (all inside the Settings view; spage: 1 hub, 2 keyboard groups, 3 one group, 4 controller)
        private void BuildSub()
        {
            float x = 200f, w = 400f, h = 34f, step = 38f;
            int n = 8;
            if (spage == 1) n = 5;
            else if (spage == 3) n = GroupActs[grp].Length + 2;
            float tot = 34f + (n - 1) * step + h;
            hdrY = 358f - tot / 2f;
            float y = hdrY + 34f;
            if (spage == 1)
            {
                ReadAudio();
                Item k1 = Add("c_kb", "KEYBOARD", -1, x, y, w, h, false); k1.Kind = 3; k1.Value = "EDIT"; y += step;
                Item p1 = Add("c_pad", "CONTROLLER", -1, x, y, w, h, false); p1.Kind = 3; p1.Value = "EDIT"; y += step;
                Item v1 = Add("c_vol", "VOLUME", -1, x, y, w, h, false); v1.Kind = 2; v1.Options = VolOptions();
                v1.Index = (aVol + 5) / 10; if (v1.Index > 10) v1.Index = 10; y += step;
                Item m1 = Add("c_mute", "MUTE", -1, x, y, w, h, false); m1.Kind = 1; m1.On = aMute; m1.Anim = m1.On ? 1f : 0f; y += step;
                Add("c_back", "BACK", 5, x, y, w, h, false);
            }
            else if (spage == 2)
            {
                EnsureActs();
                Item t2 = Add("c_title", "KEYBOARD", -1, x, y, w, h, false); t2.Enabled = false; y += step;
                for (int gi = 0; gi < GroupNames.Length; gi++)
                {
                    Item g2 = Add("c_grp" + gi.ToString(), GroupNames[gi], -1, x, y, w, h, false); g2.Kind = 3; g2.Value = "EDIT"; y += step;
                }
                Item r2 = Add("c_reset", "RESET KEYBOARD", -1, x, y, w, h, false); r2.Kind = 3; r2.Value = "DEFAULT"; y += step;
                Add("c_back", "BACK", 5, x, y, w, h, false);
            }
            else if (spage == 3)
            {
                EnsureActs();
                Item t3 = Add("c_title", GroupNames[grp], -1, x, y, w, h, false); t3.Enabled = false; y += step;
                int[] idx = GroupActs[grp];
                for (int i = 0; i < idx.Length; i++)
                {
                    Act a = acts[idx[i]];
                    Item r3 = Add("c_act" + idx[i].ToString(), a.Label, -1, x, y, w, h, false);
                    r3.Kind = 3; r3.Value = (capAction == idx[i]) ? "PRESS A KEY..." : KeyText(a); y += step;
                }
                Add("c_back", "BACK", 5, x, y, w, h, false);
            }
            else
            {
                ReadPad();
                for (int i = 0; i < 6; i++)
                {
                    Item r4 = Add("c_pq" + i.ToString(), PadActLabels[i], -1, x, y, w, h, false);
                    r4.Kind = 3; r4.Value = (capPad == i) ? "PRESS A BUTTON..." : PadOfTarget(PadActTargets[i]); y += step;
                }
                Item rs4 = Add("c_padreset", "RESET CONTROLLER", -1, x, y, w, h, false); rs4.Kind = 3; rs4.Value = "DEFAULT"; y += step;
                Add("c_back", "BACK", 5, x, y, w, h, false);
            }
        }

        private void RebuildKeep()
        {
            int k = sel;
            BuildItems();
            if (k >= 0 && k < items.Count) sel = k;
            Invalidate();
        }

        private void SubBack()
        {
            capAction = -1; capPad = -1;
            if (spage == 3) spage = 2;
            else if (spage == 2 || spage == 4) { spage = 0; stab = 3; }
            else spage = 0;
            BuildItems();
        }

        private void AdjustVol(int dir)
        {
            aVol = Math.Max(0, Math.Min(100, ((aVol + 5) / 10 + dir) * 10));
            WriteAudio();
            if (sel >= 0 && sel < items.Count && items[sel].Id == "c_vol") items[sel].Index = aVol / 10;
            Invalidate();
        }

        private Item FindItem(string id)
        {
            foreach (Item o in items) { if (o.Id == id) return o; }
            return null;
        }

        private bool HandleCtl(Item it)
        {
            string id = it.Id;
            if (id == null || !id.StartsWith("c_")) return false;
            if (id == "c_ctl") { spage = 1; BuildItems(); return true; }
            if (id == "c_back") { SubBack(); return true; }
            if (id == "c_kb") { spage = 2; BuildItems(); return true; }
            if (id == "c_pad") { spage = 4; BuildItems(); return true; }
            if (id == "c_reset") { EnsureActs(); ResetKeys(); ShowNotice("Keyboard keys are back to the defaults."); RebuildKeep(); return true; }
            if (id == "c_padreset") { padMap = (string[])PadTemplates[0].Clone(); WritePad(); ShowNotice("Controller buttons are back to the defaults."); RebuildKeep(); return true; }
            if (id.StartsWith("c_pq")) { StartPadCapture(int.Parse(id.Substring(4))); return true; }
            if (id == "c_vol")
            {
                it.Index = (it.Index + 1) % it.Options.Length;
                aVol = it.Index * 10; WriteAudio(); return true;
            }
            if (id == "c_mute") { it.On = !it.On; aMute = it.On; WriteAudio(); return true; }
            if (id.StartsWith("c_grp")) { grp = int.Parse(id.Substring(5)); spage = 3; BuildItems(); return true; }
            if (id.StartsWith("c_act"))
            {
                capAction = int.Parse(id.Substring(5));
                capStart = DateTime.Now;
                it.Value = "PRESS A KEY...";
                ShowNotice("Press a key or mouse button. No input for 5 seconds = NONE.  Esc cancels.");
                Invalidate();
                return true;
            }
            if (id == "c_tpl")
            {
                it.Index = (it.Index + 1) % 3;
                if (it.Index < 2)
                {
                    padMap = (string[])PadTemplates[it.Index].Clone();
                    WritePad();
                    for (int i = 0; i < 6; i++) { Item r = FindItem("c_pb" + i.ToString()); if (r != null) r.Index = PadTargetIndex(padMap[i]); }
                }
                Invalidate();
                return true;
            }
            if (id.StartsWith("c_pb"))
            {
                int pi = int.Parse(id.Substring(4));
                it.Index = (it.Index + 1) % PadTargets.Length;
                padMap[pi] = PadTargets[it.Index];
                WritePad();
                Item tp = FindItem("c_tpl");
                if (tp != null) tp.Index = TemplateIndex();
                Invalidate();
                return true;
            }
            return true;
        }

        private void CaptureToken(int sc)
        {
            int ai = capAction;
            capAction = -1;
            BindKey(ai, "#" + sc.ToString());
            ShowNotice(acts[ai].Label + " = " + ScanName(sc));
            RebuildKeep();
        }

        // while a row waits for a key: a countdown on the row, and after CapSeconds without input the row is set to NONE
        private void StepCapture()
        {
            double el = (DateTime.Now - capStart).TotalSeconds;
            if (el >= CapSeconds)
            {
                int ai = capAction;
                capAction = -1;
                if (ai >= 0 && ai < acts.Count)
                {
                    acts[ai].Cur = new List<string>();
                    acts[ai].Dirty = true;
                    SaveKeys();
                    ShowNotice(acts[ai].Label + " = NONE  (no input)");
                }
                RebuildKeep();
                return;
            }
            Item row = FindItem("c_act" + capAction.ToString());
            if (row != null) row.Value = "PRESS A KEY...  " + ((int)Math.Ceiling(CapSeconds - el)).ToString();
        }

        private void CaptureKey(KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if ((DateTime.Now - capStart).TotalMilliseconds < 300) return;
            Keys k = e.KeyCode;
            if (k == Keys.Escape) { capAction = -1; ShowNotice("Cancelled."); RebuildKeep(); return; }
            int sc = ScanOf(k);
            if (k == Keys.ShiftKey) sc = ((GetAsyncKeyState(0xA1) & 0x8000) != 0) ? 229 : 225;
            else if (k == Keys.ControlKey) sc = ((GetAsyncKeyState(0xA3) & 0x8000) != 0) ? 228 : 224;
            else if (k == Keys.Menu) sc = ((GetAsyncKeyState(0xA5) & 0x8000) != 0) ? 230 : 226;
            if (sc == 0) { ShowNotice("That key cannot be used."); return; }
            if (Reserved(sc)) { ShowNotice("F1-F6, F8, F9 and F11 belong to the program."); return; }
            int ai = capAction;
            capAction = -1;
            BindKey(ai, "#" + sc.ToString());
            ShowNotice(acts[ai].Label + " = " + ScanName(sc));
            RebuildKeep();
        }

        // "by EliasM108": tiny, right at the end of the title; a green dot, dim "by", green name with a faint glow
        private void DrawCredit(Graphics g)
        {
            StringFormat gt = StringFormat.GenericTypographic;
            float x = 26f, y = 644f;
            try
            {
                RectangleF tb = titleAll.GetBounds();
                if (tb.Width > 50f && row2Right > 50f && row2Right < 780f) { x = row2Right + 9f; y = tb.Bottom - 12f; }
                else if (tb.Width > 50f && tb.Right < 760f) { x = tb.Right + 8f; y = tb.Bottom - 12f; }
            }
            catch { }
            System.Drawing.Drawing2D.SmoothingMode oldSm = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Font fn = new Font("Segoe UI Semibold", 10f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                float wb = g.MeasureString("by", fFoot, 200, gt).Width;
                float nx = x + 9f + wb + 3f;
                using (SolidBrush dg = new SolidBrush(Color.FromArgb(40, 120, 235, 50))) g.FillEllipse(dg, x - 1.8f, y + 2.7f, 6.8f, 6.8f);
                using (SolidBrush dot = new SolidBrush(Color.FromArgb(210, 120, 235, 50))) g.FillEllipse(dot, x, y + 4.5f, 3.2f, 3.2f);
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(125, 155, 125))) g.DrawString("by", fFoot, bb, x + 9f, y + 0.5f, gt);
                float[] dx = new float[] { -1f, 1f, 0f, 0f };
                float[] dy = new float[] { 0f, 0f, -1f, 1f };
                using (SolidBrush gl = new SolidBrush(Color.FromArgb(26, 120, 235, 50)))
                {
                    for (int i = 0; i < 4; i++) g.DrawString("EliasM108", fn, gl, nx + dx[i], y + dy[i], gt);
                }
                using (SolidBrush nb = new SolidBrush(Color.FromArgb(160, 240, 90))) g.DrawString("EliasM108", fn, nb, nx, y, gt);
            }
            g.SmoothingMode = oldSm;
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
                if (beamTex != null) { beamTex.Dispose(); beamTex = null; }
                if (bloomBmp != null) { bloomBmp.Dispose(); bloomBmp = null; }
                if (blobBmp != null) { blobBmp.Dispose(); blobBmp = null; }
                if (blobHi != null) { blobHi.Dispose(); blobHi = null; }
                if (bloomA != null) { bloomA.Dispose(); bloomA = null; }
                if (bloomB != null) { bloomB.Dispose(); bloomB = null; }
                if (beamTex2 != null) { beamTex2.Dispose(); beamTex2 = null; }
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
                        pg.CenterColor = Color.FromArgb(36, 150, 205, 175);
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
                GraphicsState scn = g.Save();
                g.TranslateTransform(0f, DY);
                DrawStars(g);
                DrawClouds(g);
                DrawMoon(g);
                DrawMountains(g);
                DrawWater(g);
                DrawShore(g);
                DrawTrees(g);
                g.Restore(scn);
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
            row2Right = x0 + total;
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
                g.DrawString("Unofficial fan project  -  not affiliated with the owners of Ben 10  -  open-source recompilation tooling (MIT)", fFoot, ft, new RectangleF(0f, 646f, 800f, 14f), sfCenter);
            DrawCredit(g);
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
            try { long t0 = sw.ElapsedTicks; PaintAll(e.Graphics); AdaptQuality((sw.ElapsedTicks - t0) * 1000f / (float)System.Diagnostics.Stopwatch.Frequency); }
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
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            GraphicsState st = g.Save();
            g.TranslateTransform(offX, offY);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(0f, DY);
            DrawDevice(g); DrawBlasts(g);
            if (quality < 1) DrawFog(g);
            DrawFireflies(g);
            if (view == View.Menu) { DrawStatusPanel(g); DrawPadInfo(g); }
            else if (view == View.Settings) DrawHeader(g, "SETTINGS");
            else if (view == View.Choose) DrawHeader(g, "SELECT GAME"); else if (view == View.Confirm) DrawConfirmPanel(g);
            else if (view == View.Build) DrawBuildPanel(g);
            else DrawDonePanel(g);
            for (int i = 0; i < items.Count; i++) DrawHolo(g, items[i], i);
            DrawCapturePopup(g);
            DrawFlash(g);
            DrawNotice(g);
            g.Restore(st);
        }

                private const float DY = 62f;
        private const float PCX = 400f, PTOP = 176f, PBOT = 540f, PRX = 185f, PRY = 24f, BEAMW = 340f;

        private void EnsurePortalAssets()
        {
            if (beamTex != null) return;
            beamTex = new Bitmap(430, 256, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(beamTex))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle full = new Rectangle(0, 0, 430, 256);
                using (LinearGradientBrush lb = new LinearGradientBrush(full, Color.FromArgb(0, 110, 255, 150), Color.FromArgb(0, 110, 255, 150), 0f))
                {
                    ColorBlend cb = new ColorBlend(5);
                    cb.Positions = new float[] { 0f, 0.16f, 0.5f, 0.84f, 1f };
                    cb.Colors = new Color[] { Color.FromArgb(0, 110, 255, 150), Color.FromArgb(44, 110, 255, 150), Color.FromArgb(74, 170, 255, 190), Color.FromArgb(44, 110, 255, 150), Color.FromArgb(0, 110, 255, 150) };
                    lb.InterpolationColors = cb;
                    g.FillRectangle(lb, full);
                }
                Random r = new Random(11);
                for (int i = 0; i < 70; i++)
                {
                    float x = 30f + Rf(r) * 370f;
                    float len = 24f + Rf(r) * 110f;
                    float y0 = Rf(r) * 256f;
                    float dc = Math.Abs(x - 215f) / 215f;
                    int al = (int)((20f + Rf(r) * 70f) * (1f - dc * 0.7f));
                    using (Pen p = new Pen(Color.FromArgb(al, 175, 255, 195), 1f))
                    {
                        for (int w = -1; w <= 1; w++) g.DrawLine(p, x, y0 + w * 256f, x, y0 + len + w * 256f);
                    }
                }
                for (int i = 0; i < 90; i++)
                {
                    float x = 20f + Rf(r) * 390f, y = Rf(r) * 256f, s = 0.8f + Rf(r) * 1.8f;
                    float dc = Math.Abs(x - 215f) / 215f;
                    int al = (int)((70f + Rf(r) * 170f) * (1f - dc * 0.6f));
                    for (int w = -1; w <= 1; w++)
                    {
                        float yy = y + w * 256f;
                        using (SolidBrush gb = new SolidBrush(Color.FromArgb(al / 5, 150, 255, 170))) g.FillEllipse(gb, x - s * 2.2f, yy - s * 2.2f, s * 4.4f, s * 4.4f);
                        using (SolidBrush cb2 = new SolidBrush(Color.FromArgb(al, 215, 255, 225))) g.FillEllipse(cb2, x - s * 0.5f, yy - s * 0.5f, s, s);
                    }
                }
                using (Pen rim = new Pen(Color.FromArgb(34, 170, 255, 190), 1f))
                {
                    g.DrawLine(rim, 44f, 0f, 44f, 256f);
                    g.DrawLine(rim, 386f, 0f, 386f, 256f);
                }
            }
            bloomBmp = new Bitmap(320, 70, PixelFormat.Format32bppPArgb);
            using (Graphics g2 = Graphics.FromImage(bloomBmp))
            {
                g2.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(0f, 0f, 319f, 69f);
                    using (PathGradientBrush pg = new PathGradientBrush(gp))
                    {
                        pg.CenterColor = Color.FromArgb(255, 190, 255, 205);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 80, 255, 120) };
                        g2.FillPath(pg, gp);
                    }
                }
            }
        }

        private void DrawDevice(Graphics g)
        {
            EnsurePortalAssets();
            if (bloomA == null) { bloomA = BakeAlpha(bloomBmp, 0.55f, false); bloomB = BakeAlpha(bloomBmp, 0.85f, false); beamTex2 = BakeAlpha(beamTex, 0.6f, true); }
            float boost = 0.78f + 0.22f * devAmt;
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 1.9);
            float gl = glitchJit;
            PortalGlow(g, PTOP, boost, pulse, gl);
            PortalGlow(g, PBOT, boost, pulse, gl);
            DrawBeam(g, boost, pulse, gl);
            DrawRings(g, PTOP, topAng, boost, gl);
            DrawRings(g, PBOT, botAng, boost, gl);
        }

        private void PortalGlow(Graphics g, float cy, float boost, float pulse, float gl)
        {
            float jx = gl * 3f * GN(cy);
            RectangleF src = new RectangleF(0f, 0f, 320f, 70f);
            g.DrawImage(bloomA, new RectangleF(PCX - 235f + jx, cy - 44f, 470f, 88f), src, GraphicsUnit.Pixel);
            float k = 1f + 0.06f * pulse;
            g.DrawImage(bloomB, new RectangleF(PCX - 150f * k + jx, cy - 20f * k, 300f * k, 40f * k), src, GraphicsUnit.Pixel);
        }

        private void DrawBeam(Graphics g, float boost, float pulse, float gl)
        {
            float x0 = PCX - BEAMW / 2f;
            int bands = gl > 0.03f ? 5 : 1;
            float bh = (PBOT - PTOP) / bands;
            RectangleF src = new RectangleF(0f, 0f, 430f, 256f);
            for (int b = 0; b < bands; b++)
            {
                float off = (bands == 1) ? 0f : gl * 5f * GN(b * 1.7f);
                GraphicsState st = g.Save();
                g.SetClip(new RectangleF(x0 - 30f, PTOP + b * bh, BEAMW + 60f, bh + 1f), CombineMode.Intersect);
                g.TranslateTransform(off, 0f);
                float o1 = (time * 46f) % 256f;
                for (float y = PTOP - o1; y < PBOT; y += 256f)
                    g.DrawImage(beamTex, new RectangleF(x0, y, BEAMW, 256f), src, GraphicsUnit.Pixel);
                if (quality < 1)
                {
                    float o2 = (time * 83f) % 256f;
                    for (float y = PTOP - o2; y < PBOT; y += 256f)
                        g.DrawImage(beamTex2, new RectangleF(x0 + 15f, y, BEAMW - 30f, 256f), src, GraphicsUnit.Pixel);
                }
                g.Restore(st);
            }
        }

        private void RingArc(Graphics g, Pen p, int cr, int cg, int cb, float cx, float cy, float rx, float ry, float startDeg, float lenDeg, float alphaMax)
        {
            int segs = (int)(lenDeg / 12f);
            if (segs < 1) segs = 1;
            float step = lenDeg / segs;
            for (int s = 0; s < segs; s++)
            {
                float a0 = startDeg + s * step, a1 = a0 + step;
                float am = (a0 + a1) * 0.5f * (float)Math.PI / 180f;
                float front = 0.45f + 0.55f * (0.5f + 0.5f * (float)Math.Sin(am));
                float tail = (s + 1f) / segs;
                p.Color = Color.FromArgb(A(alphaMax * front * (0.3f + 0.7f * tail)), cr, cg, cb);
                PointF[] pts = new PointF[4];
                for (int k = 0; k < 4; k++)
                {
                    double a = (a0 + (a1 - a0) * k / 3.0) * Math.PI / 180.0;
                    pts[k] = new PointF(cx + (float)Math.Cos(a) * rx, cy + (float)Math.Sin(a) * ry);
                }
                g.DrawLines(p, pts);
            }
        }

        private void DrawRings(Graphics g, float cy, float ang, float boost, float gl)
        {
            float[] fr = new float[] { 1.0f, 0.86f, 0.70f, 0.50f };
            using (Pen thin = new Pen(Color.FromArgb(A(70f * boost), 120, 255, 160), 1f))
            using (Pen glow = new Pen(Color.FromArgb(40, 90, 255, 120), 7f))
            using (Pen core = new Pen(Color.FromArgb(200, 170, 255, 175), 2.4f))
            {
                glow.LineJoin = LineJoin.Round; glow.StartCap = LineCap.Round; glow.EndCap = LineCap.Round;
                core.LineJoin = LineJoin.Round; core.StartCap = LineCap.Round; core.EndCap = LineCap.Round;
                for (int i = 0; i < 4; i++)
                {
                    float rx = PRX * fr[i], ry = PRY * fr[i];
                    float jx = gl * 3f * GN(cy + i * 1.9f);
                    float cx = PCX + jx;
                    g.DrawEllipse(thin, cx - rx, cy - ry, rx * 2f, ry * 2f);
                    float a0 = ang * (1f + 0.3f * i) + i * 57f;
                    float len = 95f - 8f * i;
                    float amax = 235f * boost;
                    if (quality < 1) { RingArc(g, glow, 90, 255, 120, cx, cy, rx, ry, a0, len, amax * 0.25f); RingArc(g, glow, 90, 255, 120, cx, cy, rx, ry, a0 + 185f, len, amax * 0.25f); }
                    if (gl > 0.05f && i < 2)
                    {
                        float sp = gl * 1.5f;
                        RingArc(g, core, 120, 255, 255, cx - sp, cy, rx, ry, a0, len, amax * 0.45f);
                        RingArc(g, core, 235, 255, 110, cx + sp, cy, rx, ry, a0, len, amax * 0.45f);
                    }
                    RingArc(g, core, 170, 255, 175, cx, cy, rx, ry, a0, len, amax);
                    RingArc(g, core, 170, 255, 175, cx, cy, rx, ry, a0 + 185f, len, amax);
                }
                using (SolidBrush db = new SolidBrush(Color.FromArgb(200, 190, 255, 190)))
                {
                    for (int k = 0; k < 12; k++)
                    {
                        double a = (-ang * 0.6f + k * 30.0) * Math.PI / 180.0;
                        float px = PCX + gl * 3f * GN(cy) + (float)Math.Cos(a) * PRX * 1.08f;
                        float py = cy + (float)Math.Sin(a) * PRY * 1.08f;
                        float fb = 0.3f + 0.7f * (0.5f + 0.5f * (float)Math.Sin(a));
                        using (SolidBrush tb = new SolidBrush(Color.FromArgb(A(210f * fb * boost), 190, 255, 190)))
                            g.FillEllipse(tb, px - 1.8f, py - 1.4f, 3.6f, 2.8f);
                    }
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
                g.DrawImage(fogBmp, new RectangleF(x, 392f + i * 52f, 760f + i * 90f, 96f), new RectangleF(0f, 0f, fogBmp.Width, fogBmp.Height), GraphicsUnit.Pixel);
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
                g.DrawString(text, fHead, b, new RectangleF(300f, hdrY, 200f, 20f), sfCenter);
            using (Pen p = new Pen(Color.FromArgb(110, 150, 255, 130), 1.5f))
            {
                g.DrawLine(p, 200f, hdrY + 10f, 292f, hdrY + 10f);
                g.DrawLine(p, 508f, hdrY + 10f, 600f, hdrY + 10f);
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
            RectangleF pr = new RectangleF(160f, 214f, 480f, 224f);
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
            RectangleF pr = new RectangleF(160f, 211f, 480f, 184f);
            DrawGlass(g, pr, 16f, 1f);
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(150, 255, 120)))
                g.DrawString("READY TO PLAY", fBig, hb, new RectangleF(pr.X + 22f, pr.Y + 14f, 436f, 32f), sfLeft);
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(235, 255, 230)))
                g.DrawString(dName, fBig, nb, new RectangleF(pr.X + 22f, pr.Y + 54f, 436f, 32f), sfLeft);
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(150, 200, 160)))
                g.DrawString(dText, fBody, tb, new RectangleF(pr.X + 22f, pr.Y + 96f, 436f, 56f), sfWrap);
            using (SolidBrush ib = new SolidBrush(Color.FromArgb(170, 235, 165)))
                g.DrawString("In the game: F1 opens the menu, F11 switches full screen.", fSmall, ib, new RectangleF(pr.X + 22f, pr.Y + 156f, 436f, 18f), sfLeft);
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
                    GraphicsState gs2 = g.Save();
                    g.RotateTransform(gearAngle * 0.25f);
                    double[] da = new double[] { -15.0, -9.0, 9.0, 15.0 };
                    float[] dr = new float[] { 8.4f, 11.8f, 11.8f, 8.4f };
                    PointF[] gp = new PointF[32];
                    int n = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        for (int q = 0; q < 4; q++)
                        {
                            double a = (k * 45.0 + da[q]) * Math.PI / 180.0;
                            gp[n++] = new PointF((float)Math.Cos(a) * dr[q], (float)Math.Sin(a) * dr[q]);
                        }
                    }
                    g.FillPolygon(fill, gp);
                    g.DrawPolygon(pen, gp);
                    g.DrawEllipse(pen, -3.6f, -3.6f, 7.2f, 7.2f);
                    g.Restore(gs2);
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
                    g.DrawArc(pen, -9f, -9f, 18f, 18f, 20f, 290f);
                    PointF[] ah = new PointF[] { new PointF(8.1f, -5.0f), new PointF(0.9f, -5.7f), new PointF(6.1f, -11.9f) };
                    g.FillPolygon(fill, ah);
                    g.DrawPolygon(pen, ah);
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

        private Color Greyed(Color c)
        {
            float t = glitchAmt * 0.85f;
            int l = (int)(c.R * 0.3f + c.G * 0.59f + c.B * 0.11f);
            return Color.FromArgb(c.A, (int)(c.R + (l - c.R) * t), (int)(c.G + (l - c.G) * t), (int)(c.B + (l - c.B) * t));
        }

        private void DrawHolo(Graphics g, Item it, int index)
        {
            float h = Math.Max(it.Hover, it.Active ? 0.8f : 0f), press = it.Press;
            bool tab = (it.Id != null && it.Id.StartsWith("t_"));
            bool en = it.Enabled;
            RectangleF r = it.Rect;
            float cx = r.X + r.Width / 2f + ((en && !tab) ? 8f * h : 0f);
            float cy = r.Y + r.Height / 2f;
            float sc = 1f + (en ? 0.02f * h : 0f) - 0.03f * press;
            cx += glitchJit * 1.8f * GN(index * 1.3f);
            float am = (en ? 1f : 0.5f) * (1f - 0.12f * glitchAmt);
            

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

            if (glitchAmt > 0.01f) { using (SolidBrush gyb = new SolidBrush(Color.FromArgb(A(glitchAmt * 120f), 128, 134, 130))) g.FillPath(gyb, p); }
            using (LinearGradientBrush bb = new LinearGradientBrush(b, Color.FromArgb(200, 170, 255, 170), Color.FromArgb(200, 90, 210, 120), 0f))
            {
                ColorBlend cbl = new ColorBlend(3);
                cbl.Positions = new float[] { 0f, 0.5f, 1f };
                cbl.Colors = new Color[] { Greyed(Color.FromArgb(A((120f + 120f * h) * am), 170, 255, 170)), Greyed(Color.FromArgb(A((50f + 90f * h) * am), 90, 210, 120)), Greyed(Color.FromArgb(A((120f + 120f * h) * am), 170, 255, 170)) };
                bb.InterpolationColors = cbl;
                using (Pen bp = new Pen(bb, 1.6f)) g.DrawPath(bp, p);
            }
            float bl = 7f, bo = 4f;
            using (Pen kp = new Pen(Greyed(Color.FromArgb(A((70f + 170f * h) * am), 160, 255, 170)), 1.6f))
            {
                g.DrawLines(kp, new PointF[] { new PointF(b.X - bo, b.Y + bl), new PointF(b.X - bo, b.Y - bo), new PointF(b.X + bl, b.Y - bo) });
                g.DrawLines(kp, new PointF[] { new PointF(b.Right + bo, b.Y + bl), new PointF(b.Right + bo, b.Y - bo), new PointF(b.Right - bl, b.Y - bo) });
                g.DrawLines(kp, new PointF[] { new PointF(b.X - bo, b.Bottom - bl), new PointF(b.X - bo, b.Bottom + bo), new PointF(b.X + bl, b.Bottom + bo) });
                g.DrawLines(kp, new PointF[] { new PointF(b.Right + bo, b.Bottom - bl), new PointF(b.Right + bo, b.Bottom + bo), new PointF(b.Right - bl, b.Bottom + bo) });
            }

            Color gc = Greyed(Color.FromArgb(en ? 235 : 130, 150, 255, 165));
            Color tc = Greyed(en ? Color.FromArgb(228, 255, 224) : Color.FromArgb(104, 136, 110));
            Font f = it.Big ? fBtn : fRow;
            if (tab) DrawLabel(g, it.Text, fHead, b, sfCenter, tc, en, h);
            else if (it.Kind == 0)
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
                        g.DrawString(it.Value ?? "", fSmall, vb, new RectangleF(b.X + 150f, b.Y, b.Width - 150f - 16f, b.Height), sfPath);
                }
            }
            g.Restore(st);
        }
    }
}
