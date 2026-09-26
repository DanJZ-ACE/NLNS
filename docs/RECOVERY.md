# Recovery

Close the game and launcher, then select the same root and Scan. Preserve `.nlns`, any previous `.dlss-bypass` folder, and all disabled copies. Never delete a disabled file just to clear a warning: it may contain the only original.

- **Partial:** some files are active and some disabled. This can be intentional selection or an interrupted operation. Restore DLSS restores the verified disabled subset; it does not touch already-active verified originals.
- **Conflict:** examine each component's explanation. NLNS will not overwrite an original, choose between disabled copies, or accept a hash mismatch. Move copies aside only after independently identifying and backing up the bytes you intend to keep. A game's official repair feature can supply publisher files, but those may differ from NLNS's recorded baseline.
- **Missing:** neither recorded path exists. NLNS cannot reconstruct deleted bytes. Recover the recorded original from your own backup. If the exact bytes are unavailable, use official repair and treat the old NLNS state as an unresolved record.
- **Unknown:** no reliable baseline or no safe read access. Without a matching valid manifest, a renamed DLL cannot be authenticated as the original. Automatic restoration is intentionally blocked.

If moving the installation, restore before moving. Journals are bound to the selected absolute root and are not silently migrated. If the old and new manifests disagree, preserve both and investigate. There is no automatic state reset in v1.0.0.

Only after every recorded original has been restored and independently verified, and every disabled copy has been accounted for, may an experienced user archive the state folders outside the game directory to establish a new baseline after an official game update. NLNS does not perform this cleanup automatically.

