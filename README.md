# PZ Manager
### For that 1 build that took 2.5 years to release.

A desktop management tool for Project Zomboid dedicated servers running Build 42.

Built out of frustration with the vanilla server management experience — editing raw `.ini` files by hand, hunting down mod IDs across two separate fields, no visibility into what mods are actually loaded or whether they have dependencies, and a server console window that doesn't like displaying what I type into it.

This tool fixes all of that.

---

## Why does this exist?

Running a Project Zomboid dedicated server in Build 42 is more involved than it should be. Between the mod system requiring you to manually maintain `Mods=` and `WorkshopItems=`, sandbox settings buried in a Lua file that isn't really Lua, mods that update constantly forcing manual server restarts.

PZ Manager is a single `.exe` that handles all of it from one window.

---

## Features

### Mod Manager
Paste Workshop IDs and let the tool do the rest. It hits the Steam Workshop API to fetch the mod display name and Mod ID automatically — including the Mod ID text that authors bury in their descriptions. Dependency detection catches both formally declared Steam "Required Items" and text-stated requirements like "Requires NeatUI Framework". The generated `Mods=` and `WorkshopItems=` lines are always in sync and ready to save directly to the server `.ini`.

### Sandbox Editor
Full Build 42 sandbox settings across categorised sections — Zombie Lore, Loot, World, Player, Multiplayer, and Animals. Human-readable labels on the sliders (so `Speed = 4` shows as "Random" rather than just "4").

### Mod Sandbox
After running the server at least once with your mods loaded, the Mod Sandbox tab auto-scans `SandboxVars.lua` and builds a UI for each mod that adds its own sandbox settings. Each mod gets its own tab with typed fields — integer sliders, boolean toggles, dropdown selectors — built from the mod's own schema.

### Server Config
Edit the server `.ini` fields that actually matter — server name, passwords, player limits, ports, RCON config, JVM args — without opening Notepad.

### RCON Console
Launch the server as a hidden process (no separate console window) and pipe its output directly into the app. Connect via RCON to send commands, with command history and log tailing from the Logs folder. If the RCON port is blocked, commands fall back to the server's stdin pipe automatically, so player warnings and saves still go through during restarts.

### Auto Restart
Scheduled server restarts on a configurable real-time interval. Sends player warnings via RCON (or stdin fallback) at 30, 15, 5, and 1 minute before restart. Saves before shutdown. Kills the entire Java process tree on exit — not just the batch file — so the port actually frees up before the new instance starts. This prevents the malformed packet errors players get when reconnecting to a server that restarted too quickly.

### Backups
Manual and automatic backups of your server config, sandbox settings, and mod list. Auto-backup fires before each scheduled restart so every restart point is preserved. Configurable retention keeps the last N backups and auto-deletes older ones. Selective restore — you can restore just the mod list without touching the sandbox config, or vice versa.

### Auto Update
Checks GitHub for new releases on startup and shows a banner if a newer version is available.

---

## Building

### Single-file exe (recommended)
```
dotnet publish PZManager/PZManager.csproj -c Release /p:PublishProfile=SingleFile
```
Output: `publish\PZManager.exe` — self-contained, runs on any Windows x64 machine with nothing installed.

### From Visual Studio
Right-click the project → Publish → **SingleFile** profile → Publish.

---

## Requirements

- Windows x64
- .NET 8 SDK (to build — the published exe bundles the runtime and needs nothing installed to run)
- A Project Zomboid dedicated server install via SteamCMD

---


## License

GNU General Public License v3.0 — see [LICENSE](LICENSE) for details.

In short: free to use and modify, but any modified versions must also be open source under GPL v3. You can't take this code and redistribute it under a more restrictive license.
