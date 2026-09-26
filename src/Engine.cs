using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace NLNS
{
    public sealed class Record
    {
        public string RelativePath { get; set; }
        public string DisabledRelativePath { get; set; }
        public string SHA256 { get; set; }
        public long Size { get; set; }
        public string OriginalPath { get; set; } // Read-only compatibility with the reference manifest.
        public string OriginalName { get; set; }
    }
    public sealed class Journal
    {
        public int Version { get; set; }
        public string Root { get; set; }
        public List<Record> Files { get; set; }
        public Journal() { Version = 1; Files = new List<Record>(); }
    }
    public sealed class Component
    {
        public string Relative, DisabledRelative, Status, Detail, Hash;
        public long Size;
        public bool Tracked;
        public string Name { get { return Path.GetFileName(Relative); } }
        public string Label { get { return Engine.Friendly(Name); } }
    }
    public sealed class Engine
    {
        public const string Suffix = ".nlns-disabled";
        public const string RunningMessage = "This game is currently running. Close it before disabling or restoring DLSS components.";
        public static readonly string[] Names = { "nvngx_dlss.dll", "nvngx_dlssg.dll", "nvngx_dlssd.dll" };
        static readonly string[] Suffixes = { Suffix, ".dlss-bypass-disabled", ".codex-disabled" };
        static readonly HashSet<string> Skip = new HashSet<string>(new[] { ".nlns", ".dlss-bypass", ".git", "backup", "backups", "codex-backup", "_UnInstall" }, StringComparer.OrdinalIgnoreCase);
        public string Root { get; private set; }
        public string Exe { get; private set; }
        public List<Component> Items { get; private set; }
        public List<string> Notes { get; private set; }
        readonly List<string> executables = new List<string>();
        Journal journal;
        public Action<string> Log = delegate { };
        // Dependency seam for deterministic process and interruption tests; never set by the application.
        internal Action RunningGuard = null;
        internal Action<int> AfterMove = null;
        public Engine() { Items = new List<Component>(); Notes = new List<string>(); }
        public static string Friendly(string name)
        {
            switch (name.ToLowerInvariant()) {
                case "nvngx_dlss.dll": return "DLSS Super Resolution";
                case "nvngx_dlssg.dll": return "DLSS Frame Generation";
                case "nvngx_dlssd.dll": return "DLSS Ray Reconstruction";
                default: return name;
            }
        }
        static bool Equal(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        static bool Within(string path, string root) { return Equal(path, root) || path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase); }
        public static void NoLinks(string path)
        {
            for (string p = Path.GetFullPath(path); !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p)) {
                try { if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("Links and redirected paths are not supported."); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
        public void Select(string selection)
        {
            string full = Path.GetFullPath(selection);
            NoLinks(full);
            string exe = null;
            if (File.Exists(full)) {
                if (!Equal(Path.GetExtension(full), ".exe")) throw new IOException("Select a game EXE or folder.");
                exe = full; full = Path.GetDirectoryName(full);
            }
            if (!Directory.Exists(full)) throw new IOException("The selected folder does not exist.");
            full = full.TrimEnd('\\');
            if (Equal(full, Path.GetPathRoot(full).TrimEnd('\\')) || full.StartsWith("\\\\", StringComparison.Ordinal))
                throw new IOException("Select an individual game on a local drive.");
            foreach (Environment.SpecialFolder f in new[] { Environment.SpecialFolder.Windows, Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolder.System, Environment.SpecialFolder.SystemX86 }) {
                string p = Environment.GetFolderPath(f);
                if (p.Length > 0 && Within(full, p)) throw new IOException("Windows and shared system folders are protected.");
            }
            foreach (Environment.SpecialFolder f in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.Desktop, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData }) {
                if (Equal(full, Environment.GetFolderPath(f))) throw new IOException("Select one game's installation folder, not a shared folder.");
            }
            if (full.Split('\\').Any(p => p.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase)))
                throw new IOException("NVIDIA driver and application folders are protected.");
            Root = full; Exe = exe; Items.Clear(); Notes.Clear();
        }
        internal string Safe(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOfAny(new[] { ':', '/', '\0' }) >= 0 ||
                relative.Split('\\').Any(s => s.Length == 0 || s == "." || s == ".." || s.EndsWith(" ") || s.EndsWith(".")))
                throw new IOException("Invalid relative path in state record.");
            string p = Path.GetFullPath(Path.Combine(Root, relative));
            if (!Within(p, Root) || Equal(p, Root)) throw new IOException("State path escapes the game folder.");
            NoLinks(p); return p;
        }
        string Rel(string p) { return p.Substring(Root.Length + 1); }
        static bool Supported(string name) { return Names.Contains(name, StringComparer.OrdinalIgnoreCase); }
        bool Excluded(string relative) { return relative.Split('\\').Any(p => Skip.Contains(p) || p.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase)); }
        internal static string Hash(Stream s) { s.Position = 0; using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", ""); }
        [StructLayout(LayoutKind.Sequential)] struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle, out FileInformation info);
        static void SingleLink(FileStream stream)
        {
            FileInformation info;
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out info) || info.Links != 1)
                throw new IOException("File identity cannot be verified or the file has multiple hard links. Modification is blocked.");
        }
        public static string HashFile(string p) { using (var s = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read)) { SingleLink(s); return Hash(s); } }
        Journal ReadJournal(string relative, bool legacy)
        {
            string p = Safe(relative);
            if (Directory.Exists(p)) throw new IOException("State record path is occupied by a directory.");
            if (!File.Exists(p)) return new Journal { Root = Root };
            if (new FileInfo(p).Length > 1024 * 1024) throw new IOException("State record is too large.");
            Journal j;
            try { j = new JavaScriptSerializer().Deserialize<Journal>(File.ReadAllText(p)); }
            catch (Exception ex) { if (!(ex is ArgumentException) && !(ex is InvalidOperationException)) throw; throw new IOException("State record is unreadable. Preserve it and the disabled files for recovery."); }
            if (j == null || j.Version != 1 || !Equal(j.Root, Root) || j.Files == null || j.Files.Count > 2000)
                throw new IOException("State record belongs to another folder or has an unsupported format.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Record r in j.Files) {
                if (r == null) throw new IOException("Invalid state entry.");
                string original = Safe(r.RelativePath); Safe(r.DisabledRelativePath);
                if (!Supported(Path.GetFileName(original)) || Excluded(r.RelativePath) || !seen.Add(r.RelativePath) ||
                    !Suffixes.Any(s => Equal(r.DisabledRelativePath, r.RelativePath + s)) || r.Size < 0 ||
                    r.SHA256 == null || r.SHA256.Length != 64 || !r.SHA256.All(Uri.IsHexDigit) ||
                    (legacy && (!Equal(r.OriginalPath, original) || !Equal(r.OriginalName, Path.GetFileName(original)))))
                    throw new IOException("Unsafe or duplicate state entry. No files were changed.");
            }
            return j;
        }
        Journal Load()
        {
            Journal j = ReadJournal(".nlns\\manifest.json", false);
            Journal old = ReadJournal(".dlss-bypass\\manifest.json", true);
            foreach (Record r in old.Files) {
                Record existing = j.Files.FirstOrDefault(e => Equal(e.RelativePath, r.RelativePath));
                if (existing == null) j.Files.Add(r);
                else if (!Equal(existing.SHA256, r.SHA256) || existing.Size != r.Size || !Equal(existing.DisabledRelativePath, r.DisabledRelativePath))
                    throw new IOException("NLNS and previous utility state records disagree.");
            }
            return j;
        }
        public string State
        {
            get {
                if (Items.Any(i => i.Status == "Conflict")) return "Conflict";
                if (Items.Any(i => i.Status == "Unknown")) return "Unknown";
                if (Items.Any(i => i.Status == "Missing")) return "Missing";
                if (Items.Any(i => i.Status == "Active") && Items.Any(i => i.Status == "Disabled")) return "Partial";
                return Items.Count == 0 ? "No supported files" : Items[0].Status;
            }
        }
        public void Scan()
        {
            if (Root == null) throw new IOException("Select a game first.");
            NoLinks(Root); Items.Clear(); Notes.Clear(); executables.Clear();
            journal = Load();
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dirs = new Stack<string>(); dirs.Push(Root); int count = 0;
            while (dirs.Count > 0) {
                string dir = dirs.Pop(); NoLinks(dir);
                foreach (string p in Directory.EnumerateFileSystemEntries(dir)) {
                    if (++count > 100000) throw new IOException("Folder is too large. Select a more specific game folder.");
                    string relative = Rel(p); FileAttributes attr = File.GetAttributes(p);
                    if ((attr & FileAttributes.ReparsePoint) != 0) { Notes.Add("Skipped a linked or redirected entry."); continue; }
                    if ((attr & FileAttributes.Directory) != 0) {
                        if (!Excluded(relative)) dirs.Push(p);
                        continue;
                    }
                    if (Equal(Path.GetExtension(p), ".exe")) executables.Add(p);
                    string name = Path.GetFileName(p), baseRel = relative;
                    foreach (string suffix in Suffixes) if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { name = name.Substring(0, name.Length - suffix.Length); baseRel = relative.Substring(0, relative.Length - suffix.Length); break; }
                    if (Supported(name)) found.Add(baseRel);
                }
            }
            foreach (Record r in journal.Files) found.Add(r.RelativePath);
            foreach (string rel in found.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)) {
                Record r = journal.Files.FirstOrDefault(e => Equal(e.RelativePath, rel));
                var c = new Component { Relative = rel, Tracked = r != null, DisabledRelative = r == null ? rel + Suffix : r.DisabledRelativePath };
                Items.Add(c);
                try {
                    string original = Safe(rel);
                    var copies = Suffixes.Select(s => rel + s).Where(s => File.Exists(Safe(s)) || Directory.Exists(Safe(s))).ToList();
                    bool active = File.Exists(original);
                    if (Directory.Exists(original) || copies.Any(s => Directory.Exists(Safe(s))) || copies.Count > 1 || (active && copies.Count > 0)) {
                        c.Status = "Conflict"; c.Detail = "Original and disabled copy coexist, multiple disabled copies exist, or a directory occupies a file path. Nothing will be overwritten.";
                    } else if (!active && copies.Count == 0) {
                        c.Status = "Missing"; c.Detail = "Both the recorded original and its disabled copy are missing. Restore requires the exact recorded bytes.";
                    } else if (r == null && !active) {
                        c.Status = "Unknown"; c.Detail = "Disabled copy has no trusted state record. Its original hash cannot be established; automatic restore is blocked.";
                    } else if (r != null && !active && !Equal(copies[0], r.DisabledRelativePath)) {
                        c.Status = "Conflict"; c.Detail = "Recorded disabled copy is missing; a differently named copy exists.";
                    } else {
                        string path = active ? original : Safe(copies[0]);
                        using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { SingleLink(s); c.Hash = Hash(s); c.Size = s.Length; }
                        if (r != null && (!Equal(c.Hash, r.SHA256) || c.Size != r.Size)) {
                            c.Status = "Conflict"; c.Detail = "SHA-256 or size mismatch. File differs from the recorded original. Automatic modification is blocked.";
                        } else {
                            c.Status = active ? "Active" : "Disabled";
                            c.Detail = active ? (r == null ? "Original filename is present. Ready to record and disable." : "Original SHA-256 verified. Restored, or a prepared disable did not finish.") : "Original filename is absent. Disabled copy matches the recorded SHA-256; exact restore is available.";
                        }
                    }
                } catch (IOException) { c.Status = "Unknown"; c.Detail = "File cannot be read safely. Close programs using it and check the path."; }
                  catch (UnauthorizedAccessException) { c.Status = "Unknown"; c.Detail = "Read access denied. Check folder permissions."; }
            }
            Log(Items.Count + " supported files tracked; " + State + ".");
            if (Notes.Count > 0) Log(Notes.Count + " linked/redirected entries skipped; scan excludes backup and NVIDIA directories.");
        }
        public void CheckRunning()
        {
            if (RunningGuard != null) { RunningGuard(); return; }
            var targets = executables.Concat(Exe == null ? new string[0] : new[] { Exe }).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var group in targets.GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)) {
                foreach (Process process in Process.GetProcessesByName(group.Key)) using (process) {
                    try {
                        if (process.HasExited) continue;
                        string path = process.MainModule.FileName;
                        if (group.Any(p => Equal(p, path))) throw new IOException(RunningMessage);
                    } catch (System.ComponentModel.Win32Exception) { throw new IOException("Cannot verify a matching running process. Close the game and launcher before continuing."); }
                      catch (InvalidOperationException) { if (!process.HasExited) throw new IOException("Cannot verify whether the game is running."); }
                }
            }
        }
        void Save()
        {
            string dest = Safe(".nlns\\manifest.json"), temp = Safe(".nlns\\" + Guid.NewGuid().ToString("N") + ".tmp");
            byte[] bytes = new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(journal));
            if (bytes.Length > 1024 * 1024) throw new IOException("State record would exceed the supported size.");
            try {
                using (var s = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { s.Write(bytes, 0, bytes.Length); s.Flush(true); }
                NoLinks(dest);
                if (File.Exists(dest)) File.Replace(temp, dest, null); else File.Move(temp, dest);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public int Disable(IEnumerable<string> selected) { return Change(false, selected.ToArray()); }
        public int Restore() { return Change(true, null); }
        int Change(bool restore, string[] selected)
        {
            if (Root == null) throw new IOException("Select a game first.");
            CheckRunning();
            string stateDir = Safe(".nlns"); Directory.CreateDirectory(stateDir); NoLinks(stateDir);
            using (var operationLock = new FileStream(Safe(".nlns\\operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                Scan(); CheckRunning();
                if (restore && Items.Any(i => i.Tracked && (i.Status == "Conflict" || i.Status == "Missing" || i.Status == "Unknown")))
                    throw new IOException("Restore stopped: a tracked file is missing, ambiguous, or changed. See component details; nothing was overwritten.");
                var wanted = new HashSet<string>(selected ?? new string[0], StringComparer.OrdinalIgnoreCase);
                if (!restore && wanted.Any(p => !Items.Any(i => Equal(i.Relative, p)))) throw new IOException("Selection changed. Rescan before disabling.");
                var targets = Items.Where(i => restore ? i.Tracked && i.Status == "Disabled" : wanted.Contains(i.Relative)).ToList();
                if (!restore && targets.Any(i => i.Status != "Active" && i.Status != "Disabled")) throw new IOException("Selected component has an unsafe state. See Details.");
                targets = targets.Where(i => i.Status == (restore ? "Disabled" : "Active")).ToList();
                var handles = new List<FileStream>();
                try {
                    foreach (Component c in targets) {
                        string from = Safe(restore ? c.DisabledRelative : c.Relative), to = Safe(restore ? c.Relative : c.DisabledRelative);
                        if (File.Exists(to) || Directory.Exists(to)) throw new IOException("Filename collision. No file will be overwritten.");
                        // Keep write access excluded across verification, journal commit, and rename.
                        var stream = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); handles.Add(stream);
                        SingleLink(stream);
                        if (!Equal(Hash(stream), c.Hash) || stream.Length != c.Size) throw new IOException("File changed after scan. Rescan before continuing.");
                        if (!c.Tracked) journal.Files.Add(new Record { RelativePath = c.Relative, DisabledRelativePath = c.DisabledRelative, SHA256 = c.Hash, Size = c.Size });
                    }
                    if (targets.Count == 0) { Log(restore ? "All recorded originals are already restored." : "No active components selected."); return 0; }
                    Save(); // Durable intent for EVERY file, before the first rename. Disk state determines recovery.
                    for (int i = 0; i < targets.Count; i++) {
                        CheckRunning(); Component c = targets[i];
                        string from = Safe(restore ? c.DisabledRelative : c.Relative), to = Safe(restore ? c.Relative : c.DisabledRelative);
                        // Revalidate pathname as well as held handle immediately before the atomic no-overwrite rename.
                        if (!Equal(HashFile(from), c.Hash)) throw new IOException("File changed before rename.");
                        File.Move(from, to);
                        if (!Equal(HashFile(to), c.Hash)) throw new IOException("Post-rename SHA-256 mismatch. Preserve state and files for recovery.");
                        Log((restore ? "Restored; original SHA-256 verified: " : "Disabled; original SHA-256 verified: ") + c.Relative);
                        if (AfterMove != null) AfterMove(i + 1);
                    }
                } finally { foreach (var h in handles) h.Dispose(); }
                Log(restore ? "All originals restored successfully." : targets.Count + " component(s) disabled. Launch the game and see whether the issue still occurs.");
                return targets.Count;
            }
        }
    }
}

