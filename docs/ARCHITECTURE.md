# Architecture and invariants

NLNS is a small C# / .NET Framework Windows Forms application. `Engine.cs` contains filesystem behavior; `MainForm.cs` owns the UI; `Program.cs` only starts the message loop. The PowerShell build invokes the installed C# compiler without network access. AnyCPU runs as 64-bit on 64-bit Windows. Windows provides the managed runtime and native GUI APIs.

## Preserved reference contract

Recursive whitelist-based discovery; independent game-relative paths; rename-only disabling; per-game manifest before modifications; SHA-256 and size validation; no-overwrite restoration; idempotent disable/restore; mixed active/disabled states; nested files and duplicate names; backup-directory exclusion; redirected-path refusal; process checks.

## Changes

Selective active-file checkboxes replace all-or-nothing disable. A `.nlns` journal and `.nlns-disabled` suffix identify new operations. The old validated manifest format and recorded suffixes remain readable. Untracked disabled files are now explicitly Unknown, rather than acquiring a new assumed original hash. Persisted baselines are never silently replaced after a file changes. Conflicting old/new manifests stop the scan.

The journal is serialized to a fresh file, flushed with write-through, and atomically moved/replaced before renaming any DLL. All selected sources are preflighted and held open with write sharing excluded. Rename is same-directory `File.Move`, never an overwrite. Paths and source hashes are rechecked immediately before rename; destination hash is checked afterwards. The application does not write DLL contents. A per-root exclusive file lock prevents concurrent NLNS operations.

The journal retains prepared records after interruption or restoration. Actual file existence and hashes determine state; there is no success flag that can drift from disk. A restore stops before modifying anything if any tracked record is unsafe. A mid-operation IO failure can leave a recoverable subset changed; the UI stops, clears stale actions, and directs the user to rescan.

Root selection, relative paths and manifest entries are validated. Whitelisted basenames and allowed suffixes must match, entries must be unique, roots must match, and paths cannot traverse outside the selected directory or use alternate streams. Reparse points are refused on modification paths and excluded from scanning. Windows/system and NVIDIA directories are protected. Backup exclusions cannot be bypassed by a manifest entry.

Running selected/discovered executables are checked before state writes and before each rename. A matching process whose image cannot be checked blocks modification. No process is terminated. The UI runs IO on a BackgroundWorker and marshals completion onto the form's UI thread. Actions and selection are disabled while busy; closing waits for completion.

## State interpretation

| Disk and journal evidence | State |
| --- | --- |
| Original present, no disabled copy, no record | Active; not authenticity-verified |
| Original present and matches record | Active; restored or prepared operation not completed |
| Original absent; recorded disabled copy matches hash/size | Disabled |
| Some Active and some Disabled | Partial (may be intentional selection or interruption) |
| Both names, several suffixes, directory collision, hash mismatch | Conflict |
| Record exists; original and disabled copy absent | Missing |
| Unrecorded disabled copy, unsafe/unreadable file | Unknown |

No final application command-line test hooks, updater, web stack, scheduler, game launcher, injection, driver changes or network code are included. Internal test seams are inaccessible from the UI and remain unset in release execution.

## Limits of guarantees

This is a cooperative local troubleshooting utility, not a defense against another process deliberately swapping directories, mutating a journal or starting a game between checks. Source handles exclude writes, but allow deletion for rename, so hostile pathname races remain possible. Atomic file operations are not a multi-file transaction; filesystem/hardware failure can exceed the recovery guarantees. Exact restoration means equality with the recorded original bytes, not proof that those bytes came from a publisher.
