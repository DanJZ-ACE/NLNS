# Release maintenance

Repository: https://github.com/DanJZ-ACE/NLNS. v1.0.0 is already published; do not move its tag or overwrite/delete its assets when publishing source.

For a future version:

1. Run `./build.ps1 -Test` on Windows in an interactive session and review the results and screenshots.
2. Validate native file/folder dialogs, Explorer opening, keyboard navigation, drag/drop, DPI transitions, protected-folder handling, and relevant game compatibility. Document any untested cases. Consider code signing for distribution identity.
3. Update version metadata and release documentation for intentional changes. Stage only reviewed source, tests, build tools, original assets, and documentation. Exclude game DLLs, saves, backups, personal logs, inspection artifacts, and build scratch files.
4. Commit and push the reviewed changes, create a new version tag, and publish a new release with its reviewed executable/portable ZIP and SHA-256 checksums.

A portable archive should include NLNS.exe, LICENSE, README.md, and referenced documentation/screenshots. Do not bundle the earlier reference executable, game/NVIDIA DLLs, or Windows/.NET runtime DLLs. `release/` and `bin/` remain ignored by Git.
