# TyrSharp2 Project Notes

## Session Management Improvements

### UI Grouping & Search
- Sessions are now grouped by `CaptureLabel` in the `SessionsView`.
- Each group occupies a single row with a dropdown (Combo) to select between sessions in that group.
- **Search:** Added a search bar to filter sessions by name or machine name in real-time.
- The "Name" column shows either the label (with an icon) or the session ID.
- Compacted sessions (archived) are indicated with a zip icon.

### Duration Formatting
- The "Range" column has been renamed to "Duration".
- Durations are formatted as human-readable strings (e.g., `1h 2m 3s`, `4m 5s`) instead of raw seconds.

### Session Compaction (.tyrlog)
- Added support for `.tyrlog` archives (zipped session folders).
- **Manual Compaction:** Buttons added to "Compact" selected sessions or "Compact All" uncompacted sessions.
- **Auto Compaction:** `SessionsView.AutoCompact` (bool) can be enabled via user config to automatically archive finished sessions in the background.
- **On-Demand Loading:** Compacted sessions are automatically decompressed to a temporary system folder (`Path.GetTempPath()/TyrSharp/Sessions`) when opened for playback and cleaned up on switch/close.
- **Metadata:** Metadata (like labels) can be edited directly inside the `.tyrlog` archive.
- **Import/Export:** Optimized to work directly with `.tyrlog` files.

### Configuration
- `SessionsView.KeepWindowOpen` (User): Whether to restore the session window on startup.
- `SessionsView.AutoCompact` (User): Automatically compress completed sessions into .tyrlog archives in the background.
