# DEVELOPMENT.md — Gaming-PC Setup & Handover

This is the onboarding doc for continuing EmsMod development **on the gaming PC**
(the machine that has GTA V + RAGE Plugin Hook), so you get a tight
build → deploy → test loop on one machine instead of building on a separate dev
PC and transferring builds.

## 1. Prerequisites to install / verify on the gaming PC

| Tool | Why | Notes |
|---|---|---|
| **Visual Studio 2022** | Build the C# project | Already installed. Needs the **.NET desktop development** workload. |
| **.NET Framework 4.8 targeting pack** | Compile against .NET Framework 4.8 | If a build fails with `MSB3644` (missing reference assemblies), install this: VS Installer → Modify → Individual Components → search "4.8" → check **.NET Framework 4.8 targeting pack**. |
| **Git for Windows** | Pull the repo | https://git-scm.com/download/win |
| **.NET Framework 4.8 runtime** | Run the plugin under RPH | Already present — the plugin has already loaded and executed on this machine, which proves it. |
| **RAGE Plugin Hook** | Host the plugin | Already installed (v1.131.1424.17745). |

The RPH SDK reference DLL is **already committed** to the repo at
`lib/RagePluginHookSDK.dll` (its assembly identity is `RagePluginHook`), so you do
**not** need to download or install the RPH SDK separately — cloning the repo gives
you everything needed to build.

## 2. Get the code

```
git clone https://github.com/riggs99/maxxems.git
```

Clone it **outside** the GTA V install directory (e.g. `C:\source\maxxems`). It's a
private repo, so Git will prompt you to sign in to GitHub (account: `riggs99`).

## 3. Build

Open `EmsMod.sln` in Visual Studio. Config is **Debug | x64** (already the project
defaults — the project targets .NET Framework 4.8 / x64 to match the RPH SDK).
Build the solution (Ctrl+Shift+B).

A post-build step automatically copies `EmsMod.dll` + `EmsMod.pdb` into
`dist/Plugins/` inside the repo.

## 4. Deploy to RPH & test

Copy from `dist/Plugins/` into your GTA V install's real `Plugins` folder:
- `EmsMod.dll` and `EmsMod.pdb` → `<GTA V>\Plugins\`
- `EmsMod\` folder (contains `Config\General.xml`) → `<GTA V>\Plugins\EmsMod\` (only
  needed the first time / when config changes)

Optional: set up a directory junction so `dist/Plugins` items point straight into
the real `Plugins` folder, removing the manual copy each build. Not required.

Then launch GTA V through RAGE Plugin Hook, open the RPH console (**F4**), and run
the `emsmod_*` console commands (see section 6).

## 5. Where the project is right now

Standalone RPH plugin in C# (.NET Framework 4.8), **not** an LSPDFR plugin. Full
design rules live in `CLAUDE.md`; the callout roster and feature design live in
`ems-mod-project-notes.md`.

**Completed (Phases 0–3):**
- Scaffolding, correct RPH entry-point pattern, clean build.
- Foundation infra: `Log`/`Safe` (error wrapping + rolling log file),
  `ConfigLoader`/`ConfigValidator` (XML config with safe fallbacks),
  `EntitySpawnRegistry` (guaranteed cleanup), `InputManager` (unified
  keyboard+controller, single-tap by construction).
- `PromptUI` (non-blocking accept/decline popup) + `DialogueEngine`/TTS voice
  behind swappable interfaces.
- `CalloutBase`/`CalloutManager` state machine with three watchdogs
  (vehicle-entry, walked-away, pause-safe timers) + `TestCallout` vertical slice.

**Verified in-game so far:** plugin loads; `emsmod_test_log` executes correctly.
Fixed along the way: config path resolution under RPH's AppDomain shadow-copy
(`ModPaths` now derives from the game exe path); console commands not registering
(explicit `Type[]` overload of `AddConsoleCommands`); command names using method
names instead of the attribute `Name` (now `Name = "..."` explicitly).

**Open issue:** intermittent "console command already defined, renamed to X2"
conflicts on *some* launches — ruled out duplicate DLL and duplicate process; a
re-entry guard on `EntryPoint.Main` was added on the theory that RPH sometimes
invokes `Main()` twice. Unconfirmed — if it recurs, check `EmsMod.log` for the
`EntryPoint.Main invoked again` warning to confirm/deny that theory. Note: even
when it happens, the commands still work (under the `2`-suffixed name), so it does
not block functional testing.

**Next up (Phase 4):** first real callout, `CarAccident_Bleeding` — real config
XML, dialogue, assessment options, and the transport-percentage resolution logic,
built entirely on the now-proven CalloutBase infra.

## 6. Console commands (all temporary debug/smoke-test commands)

- `emsmod_test_log` — logs one line at each level.
- `emsmod_test_safe` — throws inside Safe.Run to prove it's caught.
- `emsmod_reloadconfig` — reloads General.xml and logs the values.
- `emsmod_test_spawn` / `emsmod_test_cleanup` — spawn/despawn a test ped.
- `emsmod_debug_countspawned` — reports the registry's live count (should be 0 after cleanup).
- `emsmod_test_input_start` / `emsmod_test_input_stop` — logs any bound key/button press.
- `emsmod_test_prompt` — shows the accept/decline popup with a 15s pause-aware timeout.
- `emsmod_test_voice` — speaks a test dispatch line via TTS.
- `emsmod_test_callout_start` — dispatches the full TestCallout flow.

## 7. Logs

Everything logs to both the RPH console (F4) **and** a rolling file at
`<GTA V>\Plugins\EmsMod\Logs\EmsMod.log`. When something misbehaves, that file (look
for `[ERROR]` or `UNHANDLED EXCEPTION`) is the thing to read — it persists after the
game closes and has full stack traces.

## 8. Git conventions

- Branch is **`main`** (not `master`).
- **No `Co-Authored-By` trailer** in commit messages.
- Small, focused commits — one logical change each, so history stays bisectable.
