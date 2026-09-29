# CLAUDE.md — GTA V EMS Mod Project Instructions

This file is read automatically at the start of every Claude Code session in this
project. See `ems-mod-project-notes.md` in this same folder for full design context,
vision, and build order — read that too if it's present.

## Project Summary
A private, family-use, kid-friendly EMS roleplay mod for GTA V, built as a
**standalone RAGE Plugin Hook (RPH) plugin** in C# (not an LSPDFR plugin). No
vitals/stat tracking. No violence, drugs, gunshots, or stabbings — all injuries stem
from accidents, illness, or fire. Built for a child with a handicap to enjoy — keep
interactions simple (single tap, not hold), support both keyboard and controller, no
punishment for wrong answers, no fail states.

## Hard Rules (never violate these)
- **No violent/mature content** — no weapons, drugs, gunshot wounds, stabbings, gore.
  All injury/callout content must stem from accidents, illness, or fire.
- **No punishment mechanics.** Wrong answers do nothing negative — patient just waits
  for the correct action. No countdown timers that fail the player. No score penalties.
- **Always clean up.** Every callout, on every end state (success, decline, cancel,
  player abandons scene), must despawn everything it spawned: peds, props, blips,
  temp vehicles. Never leave orphaned entities in the world.
- **Support both keyboard and controller** for every new interaction — never wire an
  interaction to only one input type.
- **Single-tap interactions**, not hold-to-interact.
- **Config over hard-coding.** Injury types, dialogue lines, timers, transport
  percentages, and UI text belong in the XML config, not hard-coded in logic —
  unless explicitly told otherwise for a quick test/prototype.
- **Avoid keybind/console-command collisions with common RPH plugins** (LSPDFR,
  Stop The Ped, etc.). This mod must coexist peacefully with other plugins running
  in the same `Plugins` folder — never assume it's the only plugin loaded.
- **Wrap risky operations in try-catch with logging.** Ped spawning, animation
  calls, config reads, and other operations that can throw should be wrapped and
  logged rather than left to crash the plugin (or the game) on an unhandled
  exception.
- **Callout popup (Y/N accept-decline) does NOT pause the game.** It appears
  on-screen while gameplay continues normally; player can ignore it and it
  auto-cancels after 15 seconds.
- **No progress persistence.** Patients-helped counts and any other progress reset
  every session — no save/load system needed.

## Dev Environment (Transitioning to Single Gaming-PC Setup)
- **Development is moving to the gaming PC** — the machine that has GTA V + full
  RPH install + Visual Studio — so building and in-game testing happen on one
  machine for a tight build → deploy → test loop. See `DEVELOPMENT.md` for the
  gaming-PC setup/handover steps.
- On the gaming PC, code can be built AND tested in-game there. The earlier setup
  used a separate code-only dev PC (no GTA V) that built and pushed via Git for the
  gaming PC to pull; that split still works but is no longer the primary flow.
- Claude still cannot observe or interact with a running GTA V session on any
  machine — in-game results (crashes, console output, behavior) always come from
  the user reporting back or sharing screenshots/log files, regardless of which
  machine hosts the code. "Test in-game" is always a user action, never something
  Claude verifies directly.
- **Sync method: Git** — `main` is the stable branch; feature/bugfix work happens
  on its own branch first (see Development Workflow Reminder below), merges back
  into `main` once confirmed working in-game.

## Tech Stack
- **Dev PC:** Visual Studio Community 2022 (.NET desktop development workload),
  RAGE Plugin Hook SDK (reference DLLs only, not full runtime install), .NET
  Framework Developer Pack (matching RPH's target version), Git
- **Gaming PC:** GTA V (Legacy/Story Mode), RAGE Plugin Hook (full runtime install),
  .NET Framework + VC++ Redistributables (runtime versions), Git (to pull builds)
- **Project type:** C# Class Library (.NET Framework, not .NET Core/5+) referencing
  `RAGE Plugin Hook.dll`

## Architecture
- Standalone RPH plugin (`Plugins` folder), no LSPDFR dependency
- C#, Visual Studio 2022 project, .NET Framework
- XML config for injury types, dialogue, timers, callout settings
- Dispatch voice: Phase 1 = `System.Speech` TTS, Phase 2 = pre-recorded audio via NAudio
- Naming convention: `CalloutType_InjuryName` style (e.g. `CarAccident_Bleeding`) —
  keep config keys and code identifiers consistent with this

### RPH Plugin Entry Point Pattern (verified/correct — do not use a `Plugin` base
### class with `Initialize()`, that was an earlier mistake and is wrong)
RPH plugins use an assembly attribute + a static `Main()` method as the entry
point, not a class inheriting from a `Plugin` base class:

```csharp
using Rage;

[assembly: Rage.Attributes.Plugin("EmsMod", Description = "EMS mod core", Author = "YourName")]

public static class EntryPoint
{
    public static void Main()
    {
        Game.LogTrivial("EmsMod: Plugin loaded successfully.");
        // Your plugin runs in its own fiber — needs a loop with GameFiber.Yield()
        // to keep executing every frame, rather than hooking a separate tick event.
        while (true)
        {
            GameFiber.Yield();
            // per-frame logic here
        }
    }
}
```
- Reference `RagePluginHook.dll` from the RPH SDK folder as the project reference
- Use `GameFiber.Sleep(ms)` to throttle repeated actions (e.g. avoid notification
  spam while a key is held down)
- Convention: rename the default `Class1.cs` to `EntryPoint.cs`

## Full Test Checklist — Run After EVERY Implementation
Before considering any change "done," verify all of the following that are relevant
to what changed. Don't skip this because a change seems small.

### Functional
- [ ] The specific feature just built/changed works as intended, tested in-game
- [ ] Both **keyboard and controller** input tested for any new/changed interaction
- [ ] Rapid/spammed button presses on the new interaction don't cause duplicate
      spawns, duplicate menus, or broken state
- [ ] No console/log errors or warnings appear on load or during the interaction
      (check RPH console output)

### Cleanup & State
- [ ] All peds, props, blips, and temp vehicles created by the feature are properly
      despawned on every end state (success, decline, cancel, player walks away)
- [ ] No leftover T-posing or frozen peds after a callout ends
- [ ] Re-triggering the same feature a second time in the same session works
      identically to the first (no state leaking between runs)

### Edge Cases
- [ ] Player entering a vehicle mid-interaction doesn't break/softlock the feature
- [ ] Player walking away mid-interaction resolves gracefully (per the defined
      behavior — check project notes for current decision on this)
- [ ] Pausing the game mid-timer pauses the timer (doesn't keep counting in background)
- [ ] Wrong-answer paths produce no punishment, no crash, no soft-lock — patient
      simply waits for correct input

### Content & Design Compliance
- [ ] Nothing violent, weapon-related, or drug-related was introduced
- [ ] Any new UI text/dialogue is age-appropriate and matches the kid-friendly tone
- [ ] New interactions remain single-tap, not hold-to-interact
- [ ] Difficulty/complexity stayed simple — no vitals/numeric stat tracking snuck in

### Config & Data
- [ ] New tunable values (timers, percentages, text) were added to the XML config,
      not hard-coded, unless explicitly a quick prototype
- [ ] Config changes don't crash the plugin if a value is missing/malformed
      (basic error handling / sensible defaults in place)

### Regression Check
- [ ] Previously working features were spot-checked and still work after this change
      (especially if shared code like `Cleanup()` or the callout base class was touched)

### Plugin Compatibility (one-time / periodic check, not every single change)
- [ ] New keybinds or console commands don't collide with common RPH plugins
      (LSPDFR, Stop The Ped, etc.)
- [ ] Mod tested alongside whatever other plugins are actually installed on the
      gaming PC, confirming no visual glitches or input conflicts

### Housekeeping
- [ ] Code has basic comments explaining non-obvious logic
- [ ] Naming follows the project's convention
- [ ] Changes committed to Git with a clear message describing what changed, once
      the above checks pass

## Development Workflow Reminder
Small loop: change one thing → build → reload plugin via RPH console (`~`) instead of
full game restart when possible → run the relevant checklist items above → commit if
it passes. Avoid stacking multiple untested features before checking any of them.

**Branch per feature/fix.** Whenever starting a new feature or debugging an existing
one, create a new branch off `main` first (e.g. `feature/diagnosis-wheel`,
`fix/stretcher-attach`) — don't commit in-progress or untested work directly to
`main`. Work the small loop above on that branch. Once the user has confirmed the
feature/fix actually works in-game (not just "builds clean"), merge the branch back
into `main` and it becomes the new stable baseline. `main` should always be the
last-known-working state, not a work-in-progress log. Ask the user before merging if
it's unclear whether they've finished testing.

## When Adding a New Callout Type
Every new callout should reuse the shared base pattern already established (base
`Callout` class, shared `Cleanup()`, shared assessment/dialogue system) rather than
duplicating logic. If something doesn't fit the existing pattern, flag it and discuss
before building a one-off — the goal is one consistent system that scales, not a pile
of special cases.

## Version Compatibility (watch for this)
- GTA V updates can temporarily break RPH compatibility — the gaming PC's game
  version and RPH version should be checked/pinned deliberately rather than
  auto-updated once things are working, to avoid random breakage.
- The RPH SDK version referenced on the dev PC must match the RPH runtime version
  installed on the gaming PC. Re-verify this whenever either side updates.

## Playtesting Milestone (distinct from the technical test checklist)
At each major milestone, once a feature passes the technical test checklist above,
it should also be actually played by the intended player (the user's son) to check
for confusion points, difficulty, or friction — this is a design validation step,
not a bug check, and matters as much as the technical checklist.

## Division of Labor (Important — What Claude Can/Can't Actually Do)
- **Claude has no access to the user's dev PC, gaming PC, GTA V, or OpenIV.** Claude
  cannot install anything, run OpenIV, edit `dlclist.xml` on the actual machine,
  launch the game, or perform any hands-on step directly — regardless of permission
  granted by the user, this is a hard technical limitation, not a policy choice.
- **What Claude CAN do:** write/edit code and config files (C#, XML, meta files),
  explain exact step-by-step OpenIV/install instructions for the user to follow,
  interpret readmes/file contents the user pastes in, find vehicle names or
  clothing component numbers from pasted file contents, troubleshoot from
  logs/errors the user reports back, and write install/setup documentation.
- **What the user does:** all physical actions — running OpenIV, installing
  vehicle/uniform add-on mods, editing files on their own machine, launching and
  testing the game, and reporting back results/errors for Claude to act on.
- For third-party vehicle/uniform mods: user finds and downloads mods they like:
  Claude helps interpret install instructions, resolve conflicts, and write the
  code that references the correct vehicle names/component numbers once installed.
- **Prefer add-on mode over replace mode** for third-party vehicle/uniform mods,
  matching PulseEMS's own approach (custom DLC pack via `dlclist.xml`, not
  overwriting base game files). Add-on mode avoids conflicts with other plugins
  and doesn't alter the vehicle/ped globally — only the mod's own spawned entities
  use it. Only use replace mode if the user explicitly asks for it.
- Siren customization (colors/flash patterns) lives in `carvariations.meta` /
  `carcols.meta`, separate from livery/texture work — can be adjusted without
  touching any 3D models or textures.

## Open Design Questions (check with user before assuming)
- Partner: scripted AI companion vs. real co-op with a second human player — not
  yet decided, don't build one path over the other without confirming
- Reward/celebration feel (success sound, patients-helped counter) — not yet
  scoped in detail

## Distribution
This mod is **private/family use only** — no public release planned. No need to
worry about licensing, packaging for strangers, or generalized compatibility beyond
this household's setup.
