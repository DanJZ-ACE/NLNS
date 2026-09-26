# Original source publication — September 26, 2026

## Recovery

Recovered the intact September 12 NLNS workspace: `src/Engine.cs`, `MainForm.cs`, `Program.cs`, `AssemblyInfo.cs`, `app.manifest`, `NLNS.csproj`, `build.ps1`, `tests/Tests.cs`, `tools/IconBuilder.cs`, original icon assets, documentation, and screenshots. These were untracked in the original local Git repository. No decompilation or reconstruction was necessary for this publication.

The historical PROVENANCE.md describes how NLNS was originally developed from the functional contract of an earlier 31,744-byte reference utility. That history is distinct from recovering the original NLNS v1.0.0 source now.

Application source, project, build script, tests, icon generator, assets, and original screenshots were copied unchanged. Existing public LICENSE and root components.png were retained. README gained source/build instructions; historical release documentation was clarified.

## Verification

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build.ps1 -Test`: **23 passed, 0 failed**, including synthetic filesystem recovery and actual WinForms handlers, clipboard, and 150%/200% enlarged-text checks.
- Rebuilt executable: **64,000 bytes**.
- Recovered original executable SHA-256 matches GitHub's published v1.0.0 executable digest exactly: `723ac3995c679a450c0996bf040b96c28b75a1aea805c377f8613bc7c28c4140`.
- Rebuilt executable SHA-256: `df01da9703ef31b4b33246420f9f0af588e652eb47ffc677895d7a3ccad5e03c`.
- Full binary comparison found differences only in the PE COFF timestamp (offset 136, 4 bytes), the compiler-generated PrivateImplementationDetails type GUID string (offset 30260, 36 bytes), and module version GUID (offset 42460, 16 bytes). Masking those fields makes the entire files identical. No application behavior or other binary content changed.
- Original module GUID: `b70767cd-3231-4565-b9d8-23869f343ba3`; rebuilt GUID: `3cddfd6a-1eae-4f9f-8462-f42efd0dc974`. This compiler does not produce byte-identical builds.

The existing v1.0.0 release assets and tag are retained. The existing tag predates source publication; obtain source from main or the source-publication commit, not the automatic source archive for that old tag.

## Limits

No real game was modified. The separate executable launch/close smoke attempt could not discover the main window when launched hidden in the sandbox, so that check is unverified in this publication run; the process was stopped afterward. Actual form tests passed. The original development report records a successful earlier process smoke check.

Visual Studio/MSBuild with the 4.8 targeting pack was not retested. Native dialogs, Explorer integration, protected-folder elevation, accessibility, actual mixed-monitor DPI transitions, and real-game compatibility retain the manual-validation caveats in VALIDATION.md. Enlarged-font tests do not replace actual monitor testing. The executable remains unsigned.
