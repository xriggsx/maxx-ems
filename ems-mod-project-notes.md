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

## Build Order Reference

See the approved architecture plan (entity registry, input abstraction,
`CalloutBase` state machine, config validation, phased build order) —
captured in this project's memory and originally written to
`C:\Users\sgmi6\.claude\plans\swirling-booping-taco.md`. Summary of phases:

0. Scaffolding — **done**, builds clean (.NET Framework 4.8 / x64, RPH SDK
   v1.131.1424.17745 vendored in `lib/`).
1. Foundation infra (`Log`/`Safe`, `ConfigLoader`, `EntitySpawnRegistry`,
   `InputManager`) — **next up**, no callout yet, each piece smoke-tested
   independently via debug console commands.
2. `PromptUI` (accept/decline) + `DialogueEngine`/TTS skeleton.
3. `CalloutBase`/`CalloutManager` skeleton + trivial `TestCallout` vertical
   slice — proves the whole pattern (watchdogs, cleanup, state machine)
   before any real content.
4. First real callout: `CarAccident_Bleeding`.
5. Second/third callouts as thin subclasses — audit point for shared logic.
6. Remaining roster + "Call Police" + NAudio voice swap, expanding
   incrementally from proven infra.
