# NLNS v1.0.0 — local release candidate

Historical September 12 development report. The v1.0.0 release was subsequently published at https://github.com/DanJZ-ACE/NLNS. The original source was recovered and published on September 26; see SOURCE_PUBLICATION.md for current verification. The functional and safety changes below describe development relative to the earlier reference utility, not changes made during source publication.

| Deliverable | Relative path |
| --- | --- |
| Portable executable | release/NLNS.exe |
| Source project | src/NLNS.csproj |
| README | README.md |
| Visual confirmation | docs/screenshots/welcome.png and components.png |
| Portable archive | release/NLNS-v1.0.0-windows.zip |

Original EXE: **31,744 bytes (31 KiB)**. Final EXE: **64000 bytes (62.5 KiB)**. Increase: **32,256 bytes**, approximately **2.02×** original. No runtime is bundled.

Language/framework: C#, Windows Forms, CLR 4 / .NET Framework; supported runtime .NET Framework 4.8+ on Windows 10/11. AnyCPU managed PE; 64-bit process on 64-bit Windows. No new external dependencies or NuGet packages. The Windows-provided Framework supplies JSON, hashing and UI APIs.

Functional changes: NLNS branding/version/icon; quiet initial selection view; Apply DLSS Bypass and Restore DLSS primary buttons; active components selected by default with independent checkboxes for customization; per-file friendly labels, exact filenames and relative paths; concise status; collapsible Activity Log and Copy Log; asynchronous scans and operations.

Safety changes: persisted original hashes are never silently replaced; write-through atomic precommitted journal; no-overwrite renames; source write locks; process checks; per-root operation lock; explicit Conflict/Missing/Unknown states; path/manifest validation; reparse and hardlink refusal; old manifest compatibility only when verified. Restore is available for all tracked safe disabled files. No automatic process termination or elevation.

Preserved behavior: recursive discovery, untouched/already-disabled states, disable/restore/reapply, mixed states, empty folders, collision refusal, SHA-256 checks and separate tracking of repeated filenames. Earlier real-game outcomes remain reference evidence, not new compatibility claims. No commercial game was modified or tested in this work.

Validation: **22 passed, 0 failed**; see VALIDATION.md for individual categories and untested manual flows. A separate packaged-process launch and clean-close smoke check passed. Screenshots were rendered from the actual form and visually inspected. Binary metadata and icon are integrated; code signature is absent.

Supported component mapping:

- nvngx_dlss.dll — DLSS Super Resolution
- nvngx_dlssg.dll — DLSS Frame Generation
- nvngx_dlssd.dll — DLSS Ray Reconstruction

Mapping reference: [NVIDIA documentation](https://docs.nvidia.com/datacenter/tesla/driver-installation-guide/gaming.html).

License conclusion: MIT for newly written project source/docs and original icon assets. Platform libraries are referenced, not bundled. No third-party code or proprietary DLL is redistributed. See PROVENANCE.md.

SHA-256 of NLNS.exe:

    723AC3995C679A450C0996BF040B96C28B75A1AEA805C377F8613BC7C28C4140
Remaining limits: unsigned Windows release candidate; manual dialog/Explorer/UAC/DPI/accessibility checks remain; game updates can cause hash conflicts; untracked disabled files cannot be automatically restored; no network/reparse/long-path support; process/path races and hardware failures cannot be completely prevented; user must retain original bytes and state files. See README.md and RECOVERY.md.

Intentionally omitted: updaters/downloads, telemetry, game database, automatic game launch, global driver/settings changes, injection, process termination, DRM/anti-cheat changes and mod-manager features. These are outside a focused reversible per-game troubleshooter.

Proposed source tree for nlns-dlss:

    .gitignore
    LICENSE
    README.md
    build.ps1
    assets/
      nlns.ico
      nlns.png
    src/
      NLNS.csproj
      AssemblyInfo.cs
      app.manifest
      Engine.cs
      MainForm.cs
      Program.cs
    tests/Tests.cs
    tools/IconBuilder.cs
    docs/
      ARCHITECTURE.md
      PROVENANCE.md
      RECOVERY.md
      RELEASING.md
      RELEASE_REPORT.md
      VALIDATION.md
      screenshots/
        welcome.png
        components.png

Build/test/release output and local inspection files are ignored. The existing v1.0.0 tag and release assets are preserved. See RELEASING.md for future releases.


