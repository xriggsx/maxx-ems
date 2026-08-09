# ems-mod-project-notes.md — Design Context, Vision, and Build Order

Companion to `CLAUDE.md` (which holds the hard rules and technical/process
instructions). This file holds the evolving **content and feature design** —
the callout roster, outcome mechanics, and any cross-cutting interaction
features — so it survives across sessions instead of living only in chat.

## Callout Roster (locked 2026-07-31)

30 callouts, split evenly EMS/Fire. Naming convention: `CalloutType_InjuryName`.

Every callout with a patient has a single `TransportPercentage` (0–100) config
value. Outcome (treated on scene vs. transported to hospital) is randomly
rolled against that percentage at resolution time — there is **no hardcoded
"always"/"usually" branching anywhere in code**; a 95% and a 5% callout run
through the exact same resolution logic, just tuned differently in XML. This
is what keeps outcomes unpredictable run-to-run while staying fully
config-driven. Callouts with no patient (e.g. `ChimneyFire_Extinguish`) have
no transport roll at all — marked N/A below.

Percentages below are starting defaults — tune freely in config, no rebuild
needed.

### EMS / Paramedic (15)

| Callout | TransportPercentage |
|---|---|
| `CarAccident_Bleeding` | 40 |
| `CarAccident_BrokenArm` | 85 |
| `BikeAccident_ScrapedKnee` | 10 |
| `PlaygroundFall_TwistedAnkle` | 35 |
| `SportsInjury_PossibleFracture` | 70 |
| `AllergicReaction_BadReaction` | 45 |
| `HeatExhaustion_Dizziness` | 30 |
| `Choking_MildObstruction` | 40 |
| `Seizure_NeedsHospitalCheck` | 95 |
| `DiabeticEmergency_LowBloodSugar` | 35 |
| `Fainting_Dizziness` | 15 |
| `Asthma_BreathingTrouble` | 50 |
| `Drowning_NearMiss` | 95 |
| `FoodPoisoning_StomachAche` | 10 |
| `Hypothermia_ColdExposure` | 40 |

### Fire Brigade (15)

| Callout | TransportPercentage |
|---|---|
| `HouseFire_SmokeInhalation` | 75 |
| `HouseFire_TrappedResident` | 80 |
| `VehicleFire_Rescue` | 45 |
| `ChimneyFire_Extinguish` | N/A — no patient |
| `GrillFire_BackyardFlareup` | N/A — no patient |
| `CampfireOutOfControl_Extinguish` | N/A — no patient |
| `BrushFire_Containment` | N/A — no patient |
| `DumpsterFire_Extinguish` | N/A — no patient |
| `ElectricalFire_Extinguish` | 30 |
| `GasLeak_Evacuation` | 25 |
| `CatInTree_Rescue` | N/A — no patient |
| `StuckElevator_Rescue` | N/A — no patient |
| `FloodWater_Rescue` | 40 |
| `DownedTree_ClearPath` | 30 |
| `SmokeAlarm_FalseAlarmCheck` | N/A — no patient |

## Outcome model — SUPERSEDED by injury severity (updated 2026-08-09)

The random `TransportPercentage` roll above is **replaced** by an **injury
severity** model, driven by the diagnosis (the question wheel, once built).
Every patient callout (via `PatientCalloutBase`) rolls/*will be assigned* one of
three severities:

- **Minor** — patient stands; treated on scene.
- **Moderate** — patient kneels; treated on scene.
- **Severe** — patient lies down; **needs transport to hospital**.

Severity is currently `Random` per dispatch (config `<Severity>`), and will be
set by the diagnosis wheel once that exists. The `TransportPercentage` field is
kept only for backward-compatible config deserialization; it no longer drives
the outcome.

**This same formula is reused for the firefighter side's *patient* callouts**
(SmokeInhalation, TrappedResident, VehicleFire_Rescue, etc.) as thin subclasses
of `PatientCalloutBase` — no re-coding. **No-patient fire callouts**
(ChimneyFire_Extinguish, GrillFire, CatInTree_Rescue, StuckElevator, etc.) do
NOT use severity/transport; they get a separate fire-specific resolution built
when fire mode is built.

**Fire mode gets its own severity vocabulary (future, not now):** the same
3-tier mechanic, but expressed in *firefighter* terms (e.g. smoke level / how
trapped someone is / fire spread), not the medical "Minor/Moderate/Severe"
labels. Decided/deferred 2026-08-09 — build it when fire mode is built, after
the paramedic side is nailed down.

## Cross-Cutting Feature: "Call Paramedics" (fire mode, future — noted 2026-08-09)

In firefighter mode the player stays a firefighter but can **call NPC
paramedics** on callouts where badly-injured patients are on scene. The NPC
paramedics then handle the patient using the **same ambulance-arrives →
medics-get-out → load-patient → drive-off sequence already built in
`PatientCalloutBase` for severe transport**. So this is mostly wiring an
existing mechanism to a "Call Paramedics" action (sibling of "Call Police"
below). Build with fire mode, after the paramedic side is nailed down.

## Cross-Cutting Feature: "Call Police" (locked 2026-07-31)

- Available as a single-tap menu option on **every** callout, no per-callout
  config gate — one consistent interaction everywhere rather than a
  special-cased subset, matching the mod's single-consistent-interaction
  philosophy.
- **Functional, not just cosmetic**: tapping it spawns an officer ped (+
  patrol vehicle) via the same `EntitySpawnRegistry` every other spawn uses
  (so cleanup is automatic on every end state, no special-casing needed),
  and the officer performs a visible scene action — current best guess is a
  "walk to a spot, then play a directing-traffic animation/scenario" task
  sequence, reusing the same spawn-and-task-assign pattern already used for
  patient peds. Exact native/scenario to use needs to be confirmed once this
  is actually built and tested in-game (dev PC can't verify GTA native
  behavior directly).
- No new `CalloutBase` state is needed — it's an optional side-action
  available during the on-scene/assessment states, not a state transition.
- Not yet scheduled into a specific build phase — introduce it once
  `CalloutBase`/`AssessmentMenuUI`/`EntitySpawnRegistry` exist (Phase 3+ in
  the architecture plan), likely alongside or just after the first real
  callout (`CarAccident_Bleeding`) since it needs a second ped "role" beyond
  the patient to shake out in the same infra.

## Player Kit — Vehicles & Uniforms (scope, locked 2026-08-08)

The mod is fundamentally about **playing as ambulance/paramedics and fire
fighters**. Beyond the callout content, two player-facing systems are core (not
optional extras):

1. **EMS vehicle spawning** — the player can spawn and drive the appropriate
   response vehicle (ambulance for EMS calls, fire truck/engine for fire calls).
   Prefer **add-on** vehicle mods over replace mode (per CLAUDE.md), spawned via
   the same `EntitySpawnRegistry`/cleanup discipline as callout entities.
2. **EMS uniforms** — the player can switch between EMS outfits: at minimum a
   **paramedic** uniform and a **firefighter** uniform. Single-tap/simple
   selection, keyboard + controller, consistent with the rest of the mod.

Division of labor (per CLAUDE.md): the **user** finds/installs the add-on
vehicle and uniform mods via OpenIV and reports the vehicle model names /
clothing component numbers; **Claude** writes the spawn code and the
uniform-swap code that references those names/components. These are built
**after** callout 1 is polished to 100% and locked as the base, so they slot
onto proven infra.

## "Callout 1 to 100%" — the canonical base

`CarAccident_Bleeding` (via `PatientCalloutBase`) is being polished to a finished
state so every future callout inherits a complete, consistent experience rather
than each re-solving presentation. Current base already provides: dispatch popup
+ TTS voice, map blip + GPS route, on-ground scene marker, on-foot arrival,
waiting patient pose, scene vehicle (optional crashed look), single-tap kneel-to-
treat animation, config-driven transport roll, patient thank-you reaction with a
hold, and guaranteed cleanup on every end state. Remaining "100%" items are
tracked with the user in-session (pending in-game feedback each iteration).

## Build Order Reference

See **`ARCHITECTURE.md`** (committed in the repo root) for the full design
blueprint — entity registry, input abstraction, `CalloutBase` state machine,
config validation, and the phased build order with a status note on what's
done and what differed from the original plan. See `DEVELOPMENT.md` for the
gaming-PC setup and current-state handover. Summary of phases:

0. Scaffolding — **done**, builds clean (.NET Framework 4.8 / x64, RPH SDK
   v1.131.1424.17745 vendored in `lib/`).
1. Foundation infra (`Log`/`Safe`, `ConfigLoader`, `EntitySpawnRegistry`,
   `InputManager`) — **done**, no callout yet, each piece smoke-tested
   independently via debug console commands.
2. `PromptUI` (accept/decline) + `DialogueEngine`/TTS skeleton. — **done**
3. `CalloutBase`/`CalloutManager` skeleton + trivial `TestCallout` vertical
   slice — proves the whole pattern (watchdogs, cleanup, state machine)
   before any real content. — **done**
4. First real callout: `CarAccident_Bleeding`. — **done** (verified in-game
   2026-08-08: dispatch popup + TTS, drive-to blip, on-foot arrival, patient +
   scene car spawn, single-tap "help" + short bandage beat, config-driven
   transport roll, positive resolution either way, full cleanup).
5. Second/third callouts as thin subclasses — audit point for shared logic.
   — **next up**
6. Remaining roster + "Call Police" + NAudio voice swap, expanding
   incrementally from proven infra.
