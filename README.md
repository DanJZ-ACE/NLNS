# NLNS — No Learning, No Sampling

**v1.0.0 · A lightweight, reversible DLSS compatibility troubleshooter for Windows.**

NLNS helps troubleshoot games that crash, hang, or fail to launch when local DLSS components, DLSS injection tools, or graphics-mod setups cause compatibility conflicts.

Instead of deleting or replacing files, NLNS temporarily disables supported DLSS components by renaming them, records their original state, and allows them to be safely restored afterward with SHA-256 verification.

The goal is simple:

**Select → Scan → Apply DLSS Bypass → Test → Restore**

## Why NLNS exists

Modern games may load NVIDIA DLSS components during graphics initialization. In some configurations, conflicts involving DLSS DLLs, injected graphics tools, mods, overlays, or other rendering components can prevent a game from starting correctly.

Manually troubleshooting this often means finding several DLLs scattered throughout a game directory, renaming them individually, remembering where they came from, and later restoring everything correctly.

NLNS automates that process while keeping it reversible.

It does **not** install replacement DLSS libraries, modify NVIDIA drivers, or permanently alter the game.

## How to use

1. Run `NLNS.exe`.
2. Choose the game's executable or installation folder.
3. NLNS recursively scans the selected game directory for supported DLSS components.
4. Active components are selected by default.
5. Click **Apply DLSS Bypass**.
6. Launch the game normally and test whether the issue still occurs.
7. Close the game.
8. Click **Restore DLSS** to restore the recorded originals.

Individual components can be unchecked before applying the bypass if you want to test them selectively.

Use **Scan** again after game updates or other external changes.

The **Activity Log** provides additional information about detected files and operations.

> Some multiplayer games and anti-cheat or file-integrity systems may reject modified game files. Restore the original files before online play whenever required.

## Supported components

| Filename | Component |
| --- | --- |
| `nvngx_dlss.dll` | DLSS Super Resolution |
| `nvngx_dlssg.dll` | DLSS Frame Generation |
| `nvngx_dlssd.dll` | DLSS Ray Reconstruction |

Detection is based on the exact filename and does not make any claim about the origin, authenticity, or version of a DLL.

NLNS never loads or executes the detected DLLs.

## Recursive scanning

DLSS components are not always located beside the main game executable.

NLNS scans recursively through the selected game directory, including nested locations such as:

```text
NVStreamline\production
