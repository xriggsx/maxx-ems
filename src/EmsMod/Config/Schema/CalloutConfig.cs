using System.Collections.Generic;

namespace EmsMod.Config.Schema
{
    /// <summary>
    /// Per-callout tunables loaded from Config/Callouts/&lt;CalloutType_InjuryName&gt;.xml.
    /// Everything a callout says, waits on, or rolls against lives here so text
    /// and timers can be tuned without a rebuild. Missing/invalid fields are
    /// backfilled by ConfigValidator, so a partial or malformed file still
    /// produces a playable callout.
    /// </summary>
    public class CalloutConfig
    {
        // Which duty mode this callout belongs to ("Paramedic"/"Firefighter") -
        // must match a <DutyMode><Name> in Duty.xml. DispatchManager reads this
        // to only offer the callout while the player is wearing that mode's
        // uniform, so adding/renaming a mode's callouts is a config edit, not
        // a code change.
        public string DispatchMode { get; set; } = "Paramedic";

        // --- Dispatch / accept ---
        public string DispatchText { get; set; } = "Car accident nearby. Someone has a small cut and needs a bandage.";
        public string DispatchVoiceLine { get; set; } = "Dispatch to all units. Minor car accident nearby. A person has a small cut and needs help. Please respond.";

        // --- En route ---
        public string EnRouteHelpText { get; set; } = "Drive to the yellow marker, then get out and walk up to the patient.";
        public string EnRouteVoiceLine { get; set; } = "Responding to the scene.";

        // --- On scene ---
        public string OnSceneText { get; set; } = "You're on scene. Check on the patient.";
        public string OnSceneVoiceLine { get; set; } = "On scene. Checking on the patient now.";
        // Optional pool of alternate on-scene lines - one is picked at random
        // per dispatch instead of OnSceneText, so repeated callouts of the same
        // type don't say the exact same thing every time. Empty = just use
        // OnSceneText above.
        public List<string> OnSceneTextVariants { get; set; } = new List<string>();

        // --- Assessment (single tap to help) ---
        // Every patient gets this step first, regardless of severity: walk to
        // the back of the duty vehicle and grab the medic bag before treating
        // anyone. A short pause plays out after the tap (door open -> pause ->
        // bag in hand -> door shut) instead of everything happening in the
        // same instant, so it reads as an action rather than a jump-cut.
        public string MedicBagHelpText { get; set; } = "Walk to the back of your vehicle, then press E (or X) to get the medic bag.";
        public string MedicBagGettingText { get; set; } = "Getting the medic bag...";
        public string MedicBagGotText { get; set; } = "Got the medic bag.";
        public float MedicBagPickupSeconds { get; set; } = 1.2f;

        // Shown instead of the interact prompt when the player is on scene but
        // not yet close enough to the patient to start talking/treating them.
        public string WalkCloserHelpText { get; set; } = "Walk closer to the patient.";
        // How close (meters) the player must be to the patient before the
        // conversation/treatment taps register, so treating always happens
        // with the player actually standing next to (and facing) them.
        public float PatientInteractRadius { get; set; } = 2.5f;

        public string AssessmentHelpText { get; set; } = "Press E (or X on controller) to help the patient.";
        public string TreatingText { get; set; } = "Putting on a bandage...";
        // Optional pool of alternate treating lines, picked at random per
        // dispatch instead of TreatingText - this is the line shown on nearly
        // every single callout, so varying it does the most to cut repetition.
        public List<string> TreatingTextVariants { get; set; } = new List<string>();

        // --- Resolution ---
        public string TreatedText { get; set; } = "All better! The patient is treated and feeling good.";
        public string TransportedText { get; set; } = "The patient is taken to the hospital for a checkup. Great job!";
        public string ResolutionVoiceLine { get; set; } = "Nice work. The patient is going to be just fine.";

        // --- Numbers / behavior ---
        // The pool of possible injuries for this callout - one is picked at
        // random per dispatch, and ITS severity drives the pose/transport so the
        // description and pose always match. Define a few with varied severities.
        // If empty, falls back to the single Severity value below.
        public List<Injury> Injuries { get; set; } = new List<Injury>();

        // Fallback severity used only when Injuries is empty: "Minor"/"Moderate"/
        // "Severe", or "Random" to roll one per dispatch.
        public string Severity { get; set; } = "Random";

        // Scene distance ahead of the player is rolled randomly per dispatch
        // between these two (meters), snapped to a street, so callouts vary from
        // "short drive" to "proper drive". Min kept high enough to never be silly-close.
        public float SceneSpawnDistanceMin { get; set; } = 120f;
        public float SceneSpawnDistanceMax { get; set; } = 450f;

        // How close (meters), on foot, the player must get before arrival counts.
        public float ArrivalRadius { get; set; } = 12f;

        // Short "bandaging" beat after the single-tap help press (seconds).
        public float TreatmentSeconds { get; set; } = 4f;

        // Optional parked vehicle at the scene; empty/whitespace = no vehicle.
        public string SceneVehicleModel { get; set; } = "asea";

        // Optional idle scenario the patient plays while waiting for help (reads
        // as "waiting/hurt" without a risky custom animation). Empty/whitespace =
        // patient just stands still. Kept kid-friendly - no injury/collapse poses.
        public string PatientScenario { get; set; } = "WORLD_HUMAN_STAND_IMPATIENT";

        // --- Polish ---

        // A glowing marker on the ground at the scene while heading there and
        // during treatment, so it's obvious where to go / stand.
        public bool ShowSceneMarker { get; set; } = true;

        // Animation the player plays during the treatment beat (kneeling medic
        // pose). Empty dictionary/name = player just stands. Loader verifies the
        // dictionary exists first, so an invalid clip is harmless.
        public string TreatmentAnimDictionary { get; set; } = "amb@medic@standing@kneel@base";
        public string TreatmentAnimName { get; set; } = "base";

        // If true and an AI partner is on duty, the partner walks over to the
        // patient and mirrors the player's treatment animation during the
        // assessment step, instead of just following the player around.
        public bool PartnerAssists { get; set; } = true;

        // Props the player physically carries from the back of their duty
        // vehicle to the patient during severe-patient transport. Best-guess
        // model names - if either turns out invalid for your install, set it
        // blank here (no rebuild needed) and the sequence still works, just
        // without that item visibly carried.
        public string StretcherPropModel { get; set; } = "prop_ld_stretcher_01";
        public string MedicBagPropModel { get; set; } = "prop_para_bag_01";

        // After treatment, the patient reacts happily and thanks the player, and
        // the callout holds this long (seconds) so the moment is visible before
        // everything despawns.
        public string PatientThanksScenario { get; set; } = "WORLD_HUMAN_CHEERING";
        public string PatientThanksText { get; set; } = "Thank you so much!";
        // Optional pool of alternate thank-you lines, picked at random per
        // dispatch instead of PatientThanksText. Empty = just use the text above.
        public List<string> PatientThanksTextVariants { get; set; } = new List<string>();
        public float ResolutionHoldSeconds { get; set; } = 5f;

        // Give the scene vehicle a crashed look (dents, engine off). Kept
        // kid-friendly - no fire. Off for non-car scenes (e.g. a bike).
        public bool DamageSceneVehicle { get; set; } = false;

        // --- Fire scene (firefighter callouts) ---
        // Optional burning prop spawned at the scene as fire flavour (e.g. a
        // barrel/debris). Empty = none. The player and patient are made
        // fireproof while on scene so it stays kid-safe (no fail state).
        public string SceneFireProp { get; set; } = "";
        // Set the scene vehicle on fire (for a vehicle-fire rescue).
        public bool BurnSceneVehicle { get; set; } = false;

        // Optional sitting/injured pose (looped animation) the patient holds
        // while waiting, instead of PatientScenario. Both empty = fall back to
        // PatientScenario, then to just standing. Kept kid-friendly.
        public string PatientPoseAnimDictionary { get; set; } = "";
        public string PatientPoseAnimName { get; set; } = "";

        // On-scene injury conversation, advanced one single-tap at a time
        // (patient says whether they're okay/hurt; player reassures). Empty =
        // skip straight to the "press to help" treatment step.
        public List<DialogueLine> AssessmentLines { get; set; } = new List<DialogueLine>();
        public string ConversationHelpText { get; set; } = "Press E (or X on controller) to talk to the patient.";

        // Friendly success feedback. Sound is a GTA frontend sound (name + set).
        public bool PlaySuccessSound { get; set; } = true;
        public string SuccessSoundName { get; set; } = "CHECKPOINT_PERFECT";
        public string SuccessSoundSet { get; set; } = "HUD_MINI_GAME_SOUNDSET";
        public bool ShowPatientsHelpedCount { get; set; } = true;

        // Transport outcome for a severe patient: helped onto a stretcher,
        // walked to the player's own duty vehicle, loaded in, then the player
        // drives them to the nearest hospital for drop-off (instead of just a
        // text notification).
        public bool TransportToHospital { get; set; } = true;
        // Overall safety cap (seconds) for the whole sequence - stretcher load,
        // walk, drive, drop-off - so it can never hang forever. Generous by
        // design since a real drive across the map can take a couple of
        // minutes; this only kicks in as a last-resort fallback.
        public float TransportSequenceSeconds { get; set; } = 300f;

        // --- Player-driven transport (stretcher/medic bag sequence) text ---
        // The patient needs the hospital - swap the medic bag back in for the
        // stretcher (a severe patient needs carrying, not more bandaging).
        public string TransportGearHelpText { get; set; } = "Walk to the back of your vehicle, then press E (or X) to swap your medic bag for the stretcher.";
        public string TransportGettingGearText { get; set; } = "Getting the stretcher...";
        public string TransportGotGearText { get; set; } = "Got the stretcher.";
        // Same short pause-before-swap beat as the medic bag pickup.
        public float TransportGearPickupSeconds { get; set; } = 1.2f;
        public string TransportCarryToPatientHelpText { get; set; } = "Bring the stretcher to the patient, then press E (or X) to load them on.";
        public string TransportPatientLoadedText { get; set; } = "Patient loaded onto the stretcher.";
        public string TransportCarryToVehicleHelpText { get; set; } = "Carry the patient back to the ambulance.";
        public string TransportLoadIntoVehicleHelpText { get; set; } = "Press E (or X on controller) to load the patient into the ambulance.";
        public string TransportDriveSubtitle { get; set; } = "Drive the patient to the hospital.";
        public string TransportDriveHelpText { get; set; } = "Drive to the hospital marked on your GPS.";

        // --- Player-driven transport phase timeouts (seconds) ---
        // Phases gated purely on player input (walk to the vehicle/patient) have
        // no timeout by design - per the "no fail state, patient just waits"
        // rule, a player who hasn't found the spot yet just keeps waiting rather
        // than the callout giving up on them (the overall TransportSequenceSeconds
        // cap above is still the last-resort safety net).
        // How long the patient is given to walk itself to the vehicle before
        // moving on anyway.
        public float TransportCarryToVehicleTimeoutSeconds { get; set; } = 60f;
        // How long the patient is given to path into the vehicle after being
        // told to board, before giving up and resolving on-scene instead.
        public float TransportBoardVehicleTimeoutSeconds { get; set; } = 14f;
        // How long the drop-off beat holds before cleanup, once the patient has
        // left the vehicle (or as a cap if they don't).
        public float TransportDropOffTimeoutSeconds { get; set; } = 10f;
    }
}
