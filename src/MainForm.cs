using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NLNS
{
    public sealed class MainForm : Form
    {
        public const string Disclaimer = "NLNS is an independent community project and is not affiliated with or endorsed by NVIDIA Corporation or any game publisher.";
        public const string Caution = "Some multiplayer games and anti-cheat/file-integrity systems may reject modified game files. Restore originals before online play when required.";
        static readonly Color Background = Color.FromArgb(19, 23, 29), Surface = Color.FromArgb(29, 35, 43), Muted = Color.FromArgb(165, 177, 190), Accent = Color.FromArgb(81, 220, 198);
        readonly Engine engine = new Engine();
        readonly TableLayoutPanel layout = new TableLayoutPanel();
        readonly Panel welcome = new Panel();
        readonly TableLayoutPanel game = new TableLayoutPanel();
        readonly FlowLayoutPanel cards = new FlowLayoutPanel();
        readonly TextBox log = new TextBox();
        readonly Label root = new Label(), title = new Label(), result = new Label(), feedback = new Label();
        readonly Button disable, restore, scan, folder, details;
        readonly List<CheckBox> choices = new List<CheckBox>();
        readonly List<Control> busyControls = new List<Control>();
        readonly ToolTip tips = new ToolTip();
        bool busy;
        internal bool IsBusy { get { return busy; } }
        internal Engine TestEngine { get { return engine; } }
        internal string LogText { get { return log.Text; } }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle, int attr, ref int value, int size);

        public MainForm()
        {
            SuspendLayout();
            Text = "NLNS — No Learning, No Sampling";
            ClientSize = new Size(980, 860); MinimumSize = new Size(850, 740);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Background; ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 10); AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            AllowDrop = true;
            DragEnter += delegate(object sender, DragEventArgs e) { e.Effect = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; };
            DragDrop += delegate(object sender, DragEventArgs e) { HandleDrop(e.Data); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; feedback.Text = "Please wait for the current operation to finish."; } };
            layout.Dock = DockStyle.Fill; layout.Padding = new Padding(28, 22, 28, 14); layout.ColumnCount = 1; layout.RowCount = 6; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);
            var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize)); header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(Label("NLNS", 27, Color.WhiteSmoke, true), 0, 0);
            header.Controls.Add(Label("v1.0.0  /  OFFLINE", 9, Muted, false), 1, 0);
            header.Controls.Add(Label("No Learning, No Sampling", 11, Muted, false), 0, 1); layout.Controls.Add(header, 0, 0);
            var content = new Panel { Dock = DockStyle.Fill }; layout.Controls.Add(content, 0, 1);
            welcome.Dock = DockStyle.Fill; welcome.BackColor = Surface; content.Controls.Add(welcome);
            var intro = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(36), ColumnCount = 1, RowCount = 6 };
            intro.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); intro.RowStyles.Add(new RowStyle(SizeType.AutoSize)); intro.RowStyles.Add(new RowStyle(SizeType.AutoSize)); intro.RowStyles.Add(new RowStyle(SizeType.AutoSize)); intro.RowStyles.Add(new RowStyle(SizeType.AutoSize)); intro.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            intro.Controls.Add(Label("Select a Game", 24, Color.WhiteSmoke, true), 0, 1);
            intro.Controls.Add(Label("Safely diagnose DLSS-related game crashes.", 12, Muted, false), 0, 2);
            var chooser = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            chooser.Controls.Add(Button("Choose EXE", ChooseExe, true)); chooser.Controls.Add(Button("Choose Folder", ChooseFolder, false));
            intro.Controls.Add(chooser, 0, 3);
            intro.Controls.Add(Label("or drop a game EXE/folder here", 10, Muted, false), 0, 4);
            welcome.Controls.Add(intro);

            game.Dock = DockStyle.Fill; game.ColumnCount = 1; game.RowCount = 6;
            foreach (int height in new[] { 40, 40, 38, 42, 32 }) game.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            game.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            title.AutoSize = true; root.AutoSize = true; result.AutoSize = true; title.Dock = DockStyle.Fill; title.Font = new Font(Font.FontFamily, 18, FontStyle.Bold); title.AutoEllipsis = true;
            root.Dock = DockStyle.Fill; root.ForeColor = Muted; root.AutoEllipsis = true;
            result.Dock = DockStyle.Fill; result.ForeColor = Accent; result.Font = new Font(Font.FontFamily, 12, FontStyle.Bold);
            game.Controls.Add(title, 0, 0); game.Controls.Add(root, 0, 1); game.Controls.Add(result, 0, 2);
            var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            scan = Button("Scan", delegate { Run(delegate { engine.Scan(); }, null); }, false);
            folder = Button("Open Game Folder", OpenFolder, false);
            tools.Controls.Add(scan); tools.Controls.Add(folder); tools.Controls.Add(Button("Choose EXE", ChooseExe, false)); tools.Controls.Add(Button("Choose Folder", ChooseFolder, false));
            game.Controls.Add(tools, 0, 3); game.Controls.Add(Label("Detected Components", 12, Color.WhiteSmoke, true), 0, 4);
            cards.Dock = DockStyle.Fill; cards.AutoScroll = true; cards.FlowDirection = FlowDirection.TopDown; cards.WrapContents = false;
            cards.SizeChanged += delegate { foreach (Control card in cards.Controls) SizeCard(card); };
            game.Controls.Add(cards, 0, 5); content.Controls.Add(game); game.Visible = false;

            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
            disable = Button("Apply DLSS Bypass", DisableSelected, true); restore = Button("Restore DLSS", RestoreOriginals, false);
            actions.Controls.Add(disable); actions.Controls.Add(restore); layout.Controls.Add(actions, 0, 2);
            disable.Visible = restore.Visible = false;
            var statusLine = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
            statusLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); statusLine.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            feedback.AutoSize = true; feedback.Dock = DockStyle.Fill; feedback.ForeColor = Muted; feedback.Text = "Reversible by design. Your originals stay in the game folder."; feedback.AutoEllipsis = true;
            details = Button("Activity Log  +", ToggleDetails, false); statusLine.Controls.Add(feedback, 0, 0); statusLine.Controls.Add(details, 1, 0); layout.Controls.Add(statusLine, 0, 3);
            var logArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            logArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); logArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            log.Dock = DockStyle.Fill; log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.BackColor = Surface; log.ForeColor = Muted; log.BorderStyle = BorderStyle.None;
            logArea.Controls.Add(log, 0, 0); logArea.Controls.Add(Button("Copy Log", CopyLog, false), 1, 0); layout.Controls.Add(logArea, 0, 4);
            var legal = Label(Caution + "\n\n" + Disclaimer, 8, Muted, false); legal.Dock = DockStyle.Fill; layout.Controls.Add(legal, 0, 5);
            engine.Log = AppendLog;
            ResumeLayout(true);
        }
        protected override void OnShown(EventArgs e) { base.OnShown(e); Rectangle area = Screen.FromControl(this).WorkingArea; Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height)); PerformLayout(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); try { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); } catch (DllNotFoundException) { } }
        static Label Label(string text, float size, Color color, bool bold) { return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 5, 3, 7), Dock = DockStyle.Fill, ForeColor = color, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft }; }
        Button Button(string text, Action action, bool accent)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 34, MinimumSize = new Size(110, 34), Padding = new Padding(10, 3, 10, 3), Margin = new Padding(0, 0, 10, 0), FlatStyle = FlatStyle.Flat, BackColor = accent ? Accent : Surface, ForeColor = accent ? Background : Color.WhiteSmoke, Cursor = Cursors.Hand, AccessibleName = text };
            b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = accent ? Accent : Color.FromArgb(65, 77, 88);
            b.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(133, 239, 222) : Color.FromArgb(47, 58, 70);
            b.Click += delegate { if (!busy || text.StartsWith("Activity") || text == "Copy Log") action(); };
            busyControls.Add(b); return b;
        }
        void ChooseExe() { using (var d = new OpenFileDialog { Filter = "Game executable (*.exe)|*.exe", Title = "Select a game executable", CheckFileExists = true }) if (d.ShowDialog(this) == DialogResult.OK) SelectGame(d.FileName); }
        void ChooseFolder() { using (var d = new FolderBrowserDialog { Description = "Select one game's installation folder", ShowNewFolderButton = false }) if (d.ShowDialog(this) == DialogResult.OK) SelectGame(d.SelectedPath); }
        internal void HandleDrop(IDataObject data)
        {
            if (busy || !data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length != 1) { feedback.Text = "Drop one game EXE or folder at a time."; return; }
            SelectGame(paths[0]);
        }
        internal void SelectGame(string path) { Run(delegate { engine.Select(path); engine.Scan(); }, null); }
        internal void ShowCurrent() { Render(); }
        void DisableSelected()
        {
            string[] paths = choices.Where(c => c.Checked && c.Enabled).Select(c => (string)c.Tag).ToArray();
            Run(delegate { int n = engine.Disable(paths); engine.Scan(); return n; }, "disable");
        }
        void RestoreOriginals() { Run(delegate { int n = engine.Restore(); engine.Scan(); return n; }, "restore"); }
        internal void OpenFolder() { if (engine.Root != null) try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + engine.Root + "\"") { UseShellExecute = true }); } catch (Win32Exception) { feedback.Text = "Could not open File Explorer."; } }
        internal void CopyLog() { try { if (log.TextLength > 0) Clipboard.SetText(log.Text); feedback.Text = "Activity log copied."; } catch (ExternalException) { feedback.Text = "Clipboard is busy. Try again."; } }
        internal void ToggleDetails() { bool open = layout.RowStyles[4].Height == 0; layout.RowStyles[4].Height = open ? 125 * DeviceDpi / 96f : 0; details.Text = open ? "Activity Log  −" : "Activity Log  +"; }
        void AppendLog(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLog), message); return; }
            if (log.TextLength > 60000) log.Text = log.Text.Substring(log.TextLength - 40000);
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        }
        void Run(Action action, string kind) { Run(delegate { action(); return 0; }, kind); }
        void Run(Func<int> action, string kind)
        {
            if (busy) return; busy = true; SetBusy(true); feedback.Text = "Working…";
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object s, DoWorkEventArgs e) { e.Result = action(); };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e) { BeginInvoke(new Action(delegate {
                busy = false; SetBusy(false);
                if (e.Error != null) {
                    string message = e.Error is UnauthorizedAccessException ? "Access denied. Close the game. If this installation requires it, reopen NLNS using Run as administrator." : e.Error.Message;
                    if (engine.Root != null) message = message.Replace(engine.Root, "[game]");
                    AppendLog("Stopped: " + message); feedback.Text = "Operation stopped. See Activity Log."; tips.SetToolTip(feedback, message);
                    // Never leave stale actionable rows after an incomplete scan or operation.
                    choices.Clear(); cards.Controls.Clear(); disable.Enabled = restore.Enabled = false;
                    result.Text = "Unknown — scan or operation did not complete";
                    if (layout.RowStyles[4].Height == 0) ToggleDetails();
                    MessageBox.Show(this, message, "NLNS — operation stopped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                } else {
                    Render();
                    if (kind == "disable") feedback.Text = e.Result + " component(s) disabled. Test the game to see whether the issue still occurs.";
                    else if (kind == "restore") feedback.Text = "All recorded originals restored; SHA-256 verified.";
                    else feedback.Text = engine.Items.Count + " supported files detected. Ready to apply bypass; uncheck components to customize.";
                }
                worker.Dispose();
            })); };
            worker.RunWorkerAsync();
        }
        void SetBusy(bool value) { UseWaitCursor = value; foreach (Control c in busyControls) c.Enabled = !value; cards.Enabled = !value; }
        void Render()
        {
            if (engine.Root == null) return;
            welcome.Visible = false; game.Visible = true; game.BringToFront();
            title.Text = engine.Exe == null ? new DirectoryInfo(engine.Root).Name : Path.GetFileName(engine.Exe);
            root.Text = engine.Root; tips.SetToolTip(root, engine.Root);
            result.Text = engine.Items.Count + " supported files  ·  " + engine.State;
            cards.SuspendLayout(); foreach (Control c in cards.Controls.Cast<Control>().ToArray()) c.Dispose(); cards.Controls.Clear(); choices.Clear();
            if (engine.Items.Count == 0) cards.Controls.Add(new Label { Text = "No supported game-local DLSS files found.\nChoose another folder or rescan after checking the installation.", AutoSize = true, ForeColor = Muted, Padding = new Padding(15) });
            foreach (Component item in engine.Items) {
                var card = new TableLayoutPanel { BackColor = Surface, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = Math.Max(400, cards.ClientSize.Width - 24), ColumnCount = 3, RowCount = 3, Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 0, 0, 8) };
                card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26)); card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                card.RowStyles.Add(new RowStyle(SizeType.AutoSize)); card.RowStyles.Add(new RowStyle(SizeType.AutoSize)); card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var check = new CheckBox { Tag = item.Relative, Dock = DockStyle.Fill, Enabled = item.Status == "Active", Checked = item.Status == "Active", AccessibleName = item.Relative + ", " + item.Status };
                choices.Add(check); check.CheckedChanged += delegate { UpdateActions(); };
                card.Controls.Add(check, 0, 0);
                var componentTitle = Label(item.Label, 10, Color.WhiteSmoke, true); card.Controls.Add(componentTitle, 1, 0);
                componentTitle.Click += delegate { if (check.Enabled && !busy) check.Checked = !check.Checked; };
                card.Controls.Add(Label(item.Status, 10, item.Status == "Conflict" || item.Status == "Missing" || item.Status == "Unknown" ? Color.FromArgb(255, 197, 118) : Accent, true), 2, 0);
                var path = new TextBox { Text = item.Relative, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Surface, ForeColor = Color.Gainsboro, Dock = DockStyle.Fill, AccessibleName = "Game-relative DLL path" };
                tips.SetToolTip(path, item.Relative); card.Controls.Add(path, 0, 1); card.SetColumnSpan(path, 3);
                var detail = Label(item.Detail, 9, Muted, false); tips.SetToolTip(detail, item.Detail); card.Controls.Add(detail, 0, 2); card.SetColumnSpan(detail, 3); SizeCard(card); cards.Controls.Add(card);
            }
            cards.ResumeLayout(); disable.Visible = restore.Visible = true; UpdateActions();
        }
        void SizeCard(Control card) { int width = Math.Max(300, cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6); card.MinimumSize = new Size(width, 0); card.MaximumSize = new Size(width, 0); card.Width = width; }
        void UpdateActions() { disable.Enabled = !busy && choices.Any(c => c.Enabled && c.Checked); restore.Enabled = !busy && engine.Items.Any(i => i.Tracked && i.Status == "Disabled") && !engine.Items.Any(i => i.Tracked && i.Status != "Active" && i.Status != "Disabled"); }
        protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
    }
}






