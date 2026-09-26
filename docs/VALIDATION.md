# v1.0.0 release candidate validation

Tested on Windows with .NET Framework 4.8.1 installed. All fixtures contain synthetic text bytes with supported filenames; no real game DLLs are loaded or modified. The reference binary was inspected statically, not executed.

**22 automated tests passed, 0 failed.** The suite covers:

- Empty and unsupported folders.
- One, two and three supported DLLs: initial state, disable, already-disabled, restore, already-restored, reapply and exact-byte restoration.
- Recursive nested detection, duplicate filenames in separate paths, uppercase names, selective disable, partial state and unrelated DLL preservation.
- Existing destination, two disabled suffixes, original/disabled coexistence and directory collisions.
- Untracked disabled files, missing copies and deliberate SHA-256 mismatch, including a changed restored original.
- Interruptions after a real rename during both disable and restore; fresh engine loads the precommitted journal and completes exact restoration.
- Both recorded previous-utility suffixes, legacy manifest validation, restore and reapply.
- Malformed/wrong-root manifests, path traversal, alternate streams and unsupported target names.
- Backup and NVIDIA directory exclusion; protected-root refusal.
- A real synthetic running EXE blocks disable and restore; operations succeed after that test process exits.
- Write-locked files, concurrent-operation lock contention and real NTFS hardlinks.
- Actual form selection handlers for folder and EXE, file-drop data for both, checkbox selection, Apply DLSS Bypass and Restore DLSS button actions, mixed-state display, Copy Log with clipboard readback, and screenshot rendering.

The test harness initially found a UI completion-thread problem. Completion now explicitly marshals to the form thread, and the UI regression test passes. Visual inspection also identified clipped header text and unreadable disabled checkbox labels; both were corrected. Screenshots show all three components at the default size, with exact paths and readable status.

The compiled release EXE was launched in its own process. Its top-level NLNS window was found, and posting a normal close request produced a clean exit. Product/version metadata and embedded icon were checked. The binary is unsigned.

## Remaining manual validation

Native dialog interaction itself (the tested handlers receive paths directly), Explorer window behavior, Windows drag/drop transport, UAC/protected-installation handling, accessibility/screen-reader behavior, mixed-monitor DPI transitions and real-game outcomes have not been verified in this environment. DPI/as-invoker manifests and shell launch arguments are implemented, but are not substitutes for those checks. Rendered screenshots use this environment's window theme; Windows 11 chrome may differ.

Source/config/docs were searched for hard-coded personal paths and disallowed release terminology; no matches were found. Release contents exclude original binaries, proprietary DLLs, game data, logs, inspection dumps and test executables. Dependency review found only installed platform APIs and original project-owned assets.


The primary workflow additionally verifies all active files start checked, Apply DLSS Bypass disables all of them without checkbox interaction, and Restore DLSS restores them. The selective case explicitly unchecks other components and verifies they remain active.

Text-spacing follow-up: defined the 96-DPI design baseline, replaced fixed text row heights with content sizing, and made version/action columns fit their labels. Two focused UI checks passed, including the existing selection/apply/restore workflow and a 150%/200% enlarged-font layout regression. Rendered enlarged text was visually inspected; actual mixed-monitor DPI transitions remain a manual check. The unchanged backend suite was not rerun for this layout-only change.

## Source publication revalidation — September 26, 2026

The complete recovered suite now reports **23 passed, 0 failed**, including the enlarged-text regression. See [SOURCE_PUBLICATION.md](SOURCE_PUBLICATION.md) for binary comparison and current verification limits. Earlier results above are historical development records.
