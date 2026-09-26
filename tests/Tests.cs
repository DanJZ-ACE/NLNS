using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace NLNS
{
    internal static class Tests
    {
        static int passed, failed;
        static bool uiOnly;
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink(string name, string existing, IntPtr security);
        static string fixture;
        static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        static void Throws(Action action) { bool threw = false; try { action(); } catch (IOException) { threw = true; } Assert(threw, "Expected safe refusal"); }
        static void Test(string name, Action action)
        {
            if (uiOnly && !name.StartsWith("UI ")) return;
            fixture = Path.Combine(Path.GetTempPath(), "NLNS-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(fixture);
            try { action(); Console.WriteLine("PASS " + name); passed++; }
            catch (Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex); failed++; }
            finally { // Delete only the exact fresh fixture created by this test, never a selected game.
                string full = Path.GetFullPath(fixture);
                if (Path.GetDirectoryName(full).TrimEnd('\\') == Path.GetTempPath().TrimEnd('\\') && Path.GetFileName(full).StartsWith("NLNS-tests-"))
                    try { Directory.Delete(full, true); } catch (IOException) { }
            }
        }
        static string FileAt(string relative, string bytes) { string p = Path.Combine(fixture, relative); Directory.CreateDirectory(Path.GetDirectoryName(p)); File.WriteAllText(p, bytes); return p; }
        static Engine NewEngine() { var e = new Engine(); e.Select(fixture); e.Scan(); return e; }
        static Component Only(Engine e) { e.Scan(); return e.Items.Single(); }
        static void Legacy(string rel, string suffix)
        {
            string p = Path.Combine(fixture, rel);
            var j = new Journal { Root = fixture, Files = new List<Record> { new Record { RelativePath = rel, DisabledRelativePath = rel + suffix, OriginalPath = p, OriginalName = Path.GetFileName(p), SHA256 = Engine.HashFile(p), Size = new FileInfo(p).Length } } };
            FileAt(".dlss-bypass\\manifest.json", new JavaScriptSerializer().Serialize(j)); File.Move(p, p + suffix);
        }
        static IEnumerable<Control> All(Control c) { foreach (Control child in c.Controls) { yield return child; foreach (Control sub in All(child)) yield return sub; } }
        static void PumpUntil(Func<bool> done) { var sw = Stopwatch.StartNew(); while (!done() && sw.ElapsedMilliseconds < 15000) { Application.DoEvents(); Thread.Sleep(15); } Assert(done(), "UI work timed out"); Application.DoEvents(); }
        [STAThread] static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--wait") { Thread.Sleep(30000); return 0; }
            uiOnly = args.Contains("--ui");
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs error) { Console.WriteLine("UI ERROR " + error.Exception); }; Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Test("empty and unsupported folders", delegate { Assert(NewEngine().Items.Count == 0, "empty"); FileAt("other.dll", "untouched"); Assert(NewEngine().Items.Count == 0, "unsupported"); });
            for (int number = 1; number <= 3; number++) { int n = number;
                Test(n + " supported DLLs: disable, already disabled, restore, reapply", delegate {
                    foreach (string name in Engine.Names.Take(n)) FileAt(name, "synthetic " + name);
                    var e = NewEngine(); Assert(e.Items.Count == n && e.State == "Active", "untouched");
                    Assert(e.Disable(e.Items.Select(i => i.Relative)) == n, "disable"); e.Scan(); Assert(e.State == "Disabled", "disabled");
                    Assert(e.Disable(e.Items.Select(i => i.Relative)) == 0, "idempotent disable"); Assert(e.Restore() == n, "restore");
                    e.Scan(); Assert(e.State == "Active", "active restored"); Assert(e.Restore() == 0, "idempotent restore");
                    e.Disable(e.Items.Select(i => i.Relative)); Assert(e.Restore() == n, "reapply");
                    foreach (string name in Engine.Names.Take(n)) Assert(File.ReadAllText(Path.Combine(fixture, name)) == "synthetic " + name, "exact bytes");
                });
            }
            Test("nested duplicates, case insensitive names and selective disabling", delegate {
                FileAt("one\\NVStreamline\\production\\nvngx_dlssg.dll", "one"); FileAt("two\\NVNGX_DLSSG.DLL", "two"); FileAt("sl.interposer.dll", "leave alone");
                var e = NewEngine(); Assert(e.Items.Count == 2, "independent files"); string chosen = e.Items[0].Relative;
                e.Disable(new[] { chosen }); e.Scan(); Assert(e.State == "Partial", "selective state"); Assert(e.Items.Count(i => i.Status == "Active") == 1, "other remains");
                Assert(e.Restore() == 1, "one restore"); Assert(File.ReadAllText(Path.Combine(fixture, "sl.interposer.dll")) == "leave alone", "unrelated intact");
            });
            Test("collision refuses overwrite", delegate {
                string p = FileAt(Engine.Names[0], "original"); FileAt(Engine.Names[0] + Engine.Suffix, "collision"); var e = NewEngine();
                Assert(e.State == "Conflict", "collision status"); Throws(delegate { e.Disable(new[] { Engine.Names[0] }); }); Assert(File.ReadAllText(p) == "original", "original intact");
            });
            Test("untracked disabled and multiple disabled copies", delegate {
                FileAt(Engine.Names[0] + Engine.Suffix, "unknown"); var e = NewEngine(); Assert(e.State == "Unknown", "unknown hash"); Assert(e.Restore() == 0, "no guessed restore");
                FileAt(Engine.Names[0] + ".codex-disabled", "another"); e.Scan(); Assert(e.State == "Conflict", "multiple copies");
            });
            Test("hash mismatch blocks restore and reapply", delegate {
                string p = FileAt(Engine.Names[0], "original"); var e = NewEngine(); e.Disable(new[] { Engine.Names[0] }); File.WriteAllText(p + Engine.Suffix, "tampered");
                Assert(Only(e).Status == "Conflict", "tamper detected"); Throws(delegate { e.Restore(); }); Assert(!File.Exists(p), "no restore");
                File.Move(p + Engine.Suffix, p); e.Scan(); Throws(delegate { e.Disable(new[] { Engine.Names[0] }); });
            });
            Test("missing original and missing disabled copy", delegate {
                string p = FileAt(Engine.Names[0], "original"); var e = NewEngine(); e.Disable(new[] { Engine.Names[0] }); Assert(Only(e).Status == "Disabled", "original missing is disabled");
                File.Delete(p + Engine.Suffix); Assert(Only(e).Status == "Missing", "both missing"); Throws(delegate { e.Restore(); });
            });
            Test("restore destination collision", delegate {
                string p = FileAt(Engine.Names[0], "original"); var e = NewEngine(); e.Disable(new[] { Engine.Names[0] }); File.WriteAllText(p, "replacement");
                Assert(Only(e).Status == "Conflict", "both exist"); Throws(delegate { e.Restore(); }); Assert(File.ReadAllText(p) == "replacement", "replacement preserved");
            });
            Test("directory collision", delegate {
                FileAt(Engine.Names[0], "original"); Directory.CreateDirectory(Path.Combine(fixture, Engine.Names[0] + Engine.Suffix)); var e = NewEngine();
                Assert(e.State == "Conflict", "directory conflict"); Throws(delegate { e.Disable(new[] { Engine.Names[0] }); });
            });
            Test("interrupted disable: restart, mixed state, exact restore", delegate {
                foreach (string n in Engine.Names) FileAt(n, "synthetic " + n);
                var e = NewEngine(); e.AfterMove = delegate { throw new IOException("simulated interruption"); };
                Throws(delegate { e.Disable(Engine.Names); }); e = NewEngine(); Assert(e.State == "Partial", "partial recovery"); Assert(e.Items.All(i => i.Tracked), "all intent durable");
                Assert(e.Restore() == 1, "restore only completed change"); e.Scan(); Assert(e.State == "Active", "recovered");
            });
            Test("interrupted restore: restart and complete", delegate {
                foreach (string n in Engine.Names) FileAt(n, n); var e = NewEngine(); e.Disable(Engine.Names);
                e.AfterMove = delegate { throw new IOException("simulated interruption"); }; Throws(delegate { e.Restore(); });
                e = NewEngine(); Assert(e.State == "Partial", "partial restore"); Assert(e.Restore() == 2, "finish remaining");
            });
            foreach (string suffix in new[] { ".dlss-bypass-disabled", ".codex-disabled" }) { string s = suffix;
                Test("reference manifest migration " + s, delegate {
                    FileAt("nested\\" + Engine.Names[0], "reference fixture"); Legacy("nested\\" + Engine.Names[0], s); var e = NewEngine();
                    Assert(e.State == "Disabled", "legacy verified"); Assert(e.Restore() == 1, "legacy restored"); e.Scan(); e.Disable(new[] { "nested\\" + Engine.Names[0] }); Assert(e.Restore() == 1, "legacy reapply");
                });
            }
            Test("invalid manifest, traversal, wrong root and whitelist", delegate {
                FileAt(Engine.Names[0], "safe"); var e = NewEngine();
                Throws(delegate { e.Safe("..\\outside.dll"); }); Throws(delegate { e.Safe("file:stream"); }); Throws(delegate { e.Safe("nested\\..\\nvngx_dlss.dll"); });
                FileAt(".nlns\\manifest.json", "invalid JSON"); Throws(delegate { e.Scan(); });
                FileAt(".nlns\\manifest.json", "{\"Version\":1,\"Root\":\"other\",\"Files\":[]}"); Throws(delegate { e.Scan(); });
                var j = new Journal { Root = fixture, Files = new List<Record> { new Record { RelativePath = "arbitrary.dll", DisabledRelativePath = "arbitrary.dll" + Engine.Suffix, SHA256 = new string('A', 64), Size = 1 } } };
                FileAt(".nlns\\manifest.json", new JavaScriptSerializer().Serialize(j)); Throws(delegate { e.Scan(); });
            });
            Test("backup exclusion and protected selection", delegate {
                FileAt("backups\\" + Engine.Names[0], "skip"); FileAt("NVIDIA Corporation\\" + Engine.Names[1], "skip"); Assert(NewEngine().Items.Count == 0, "excluded");
                var e = new Engine(); Throws(delegate { e.Select(Environment.GetFolderPath(Environment.SpecialFolder.Windows)); }); Throws(delegate { e.Select(Path.GetPathRoot(fixture)); });
            });
            Test("running process blocks disable and restore", delegate {
                string helper = Path.Combine(fixture, "FixtureGame.exe"); File.Copy(Application.ExecutablePath, helper); FileAt(Engine.Names[0], "original");
                var e = new Engine(); e.Select(helper); e.Scan();
                using (var p = Process.Start(new ProcessStartInfo(helper, "--wait") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })) {
                    Thread.Sleep(250); Throws(delegate { e.Disable(Engine.Names.Take(1)); }); Assert(File.Exists(Path.Combine(fixture, Engine.Names[0])), "running intact"); p.Kill(); p.WaitForExit();
                }
                e.Disable(Engine.Names.Take(1));
                using (var p = Process.Start(new ProcessStartInfo(helper, "--wait") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })) {
                    Thread.Sleep(250); Throws(delegate { e.Restore(); }); p.Kill(); p.WaitForExit();
                }
                Assert(e.Restore() == 1, "restore after exit");
            });
            Test("write-locked original refuses modification", delegate {
                string p = FileAt(Engine.Names[0], "original"); var e = NewEngine();
                using (var locked = new FileStream(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { e.Scan(); Assert(e.State == "Unknown", "locked unknown"); Throws(delegate { e.Disable(new[] { Engine.Names[0] }); }); }
                Assert(File.ReadAllText(p) == "original", "locked preserved");
            });
            Test("operation lock serializes instances", delegate {
                FileAt(Engine.Names[0], "original"); var e = NewEngine(); Directory.CreateDirectory(Path.Combine(fixture, ".nlns"));
                using (var locked = new FileStream(Path.Combine(fixture, ".nlns\\operation.lock"), FileMode.Create, FileAccess.ReadWrite, FileShare.None)) Throws(delegate { e.Disable(new[] { Engine.Names[0] }); });
            });
            Test("hardlinked files are refused", delegate {
                string original = FileAt("unrelated.bin", "shared bytes");
                Assert(CreateHardLink(Path.Combine(fixture, Engine.Names[0]), original, IntPtr.Zero), "create synthetic hardlink");
                var e = NewEngine(); Assert(e.State == "Unknown", "hardlink unknown"); Throws(delegate { e.Disable(new[] { Engine.Names[0] }); });
                Assert(File.ReadAllText(original) == "shared bytes", "other link unchanged");
            });
            Test("UI folder/EXE/drop, selective action, restore, copy log, rendered screenshots", delegate {
                Directory.CreateDirectory("docs\\screenshots");
                string gamePath = FileAt("ExampleGame.exe", "synthetic selection only");
                FileAt("NVStreamline\\production\\" + Engine.Names[1], "frame"); FileAt(Engine.Names[0], "super"); FileAt(Engine.Names[2], "ray");
                using (var form = new MainForm()) {
                    form.Show(); Application.DoEvents();
                    using (var b = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(b, new Rectangle(Point.Empty, form.Size)); b.Save("docs\\screenshots\\welcome.png"); }
                    form.SelectGame(fixture); PumpUntil(delegate { return !form.IsBusy && form.TestEngine.Items.Count == 3; });
                    var data = new DataObject(DataFormats.FileDrop, new[] { gamePath }); form.HandleDrop(data);
                    PumpUntil(delegate { return !form.IsBusy && form.TestEngine.Exe == gamePath; });
                    var checks = All(form).OfType<CheckBox>().Where(c => c.Enabled).ToArray(); Assert(checks.Length == 3, "checkboxes=" + checks.Length + "; total=" + All(form).OfType<CheckBox>().Count() + "; state=" + form.TestEngine.State + "; log=" + form.LogText); Assert(checks.All(c => c.Checked), "active components selected by default"); foreach (var check in checks.Skip(1)) check.Checked = false;
                    All(form).OfType<Button>().Single(b => b.Text == "Apply DLSS Bypass").PerformClick(); PumpUntil(delegate { return !form.IsBusy && form.TestEngine.State == "Partial"; });
                    // Public screenshots intentionally replace the temporary absolute root with an illustrative label.
                    foreach (Label label in All(form).OfType<Label>()) if (label.Text == fixture) label.Text = @"D:\Games\Example Game";
                    using (var b = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(b, new Rectangle(Point.Empty, form.Size)); b.Save("docs\\screenshots\\components.png"); }
                    form.CopyLog(); Assert(Clipboard.GetText().Contains("SHA-256 verified"), "clipboard");
                    All(form).OfType<Button>().Single(b => b.Text == "Restore DLSS").PerformClick(); PumpUntil(delegate { return !form.IsBusy && form.TestEngine.State == "Active"; });
                    var dropFolder = new DataObject(DataFormats.FileDrop, new[] { fixture }); form.HandleDrop(dropFolder); PumpUntil(delegate { return !form.IsBusy && form.TestEngine.Exe == null; });
                    All(form).OfType<Button>().Single(b => b.Text == "Apply DLSS Bypass").PerformClick(); PumpUntil(delegate { return !form.IsBusy && form.TestEngine.State == "Disabled"; });
                    Assert(form.TestEngine.Items.All(i => i.Status == "Disabled"), "default action bypasses all active components");
                    All(form).OfType<Button>().Single(b => b.Text == "Restore DLSS").PerformClick(); PumpUntil(delegate { return !form.IsBusy && form.TestEngine.State == "Active"; });
                    form.Close();
                }
            });
            Test("UI enlarged text layout at 150 and 200 percent", delegate {
                foreach (float scale in new[] { 1.5f, 2f }) using (var form = new MainForm()) {
                    form.Show(); Application.DoEvents();
                    // Stress text measurement without changing the user's display settings.
                    var fonts = All(form).Select(c => new { Control = c, Font = c.Font }).ToArray();
                    form.SuspendLayout();
                    foreach (var entry in fonts) entry.Control.Font = new Font(entry.Font.FontFamily, entry.Font.Size * scale, entry.Font.Style);
                    // Keep the same window size to stress wrapping as text grows.
                    form.ResumeLayout(true); Application.DoEvents();
                    foreach (Label label in All(form).OfType<Label>().Where(l => l.Visible)) {
                        Size preferred = label.GetPreferredSize(new Size(label.Width, 0));
                        Assert(label.Height >= preferred.Height, "Clipped text: " + label.Text);
                        Assert(label.Bottom <= label.Parent.ClientSize.Height, "Text outside parent: " + label.Text);
                    }
                    Directory.CreateDirectory(".local");
                    using (var b = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(b, new Rectangle(Point.Empty, form.Size)); b.Save(".local/layout-" + (int)(scale * 100) + ".png"); }
                    form.Close();
                }
            });
            Console.WriteLine(passed + " passed; " + failed + " failed."); return failed == 0 ? 0 : 1;
        }
    }
}







