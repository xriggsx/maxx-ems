using System.Collections.Generic;

namespace EmsMod.Config.Schema
{
    /// <summary>
    /// Per-callout tunables loaded from Config/Callouts/&lt;CalloutType_InjuryName&gt;.xml.
    /// Everything a callout says, waits on, or rolls against lives here so text,
    /// timers, and the transport percentage can be tuned without a rebuild.
    /// Missing/invalid fields are backfilled by ConfigValidator, so a partial
    /// or malformed file still produces a playable callout.
    /// </summary>
    public class CalloutConfig
    {
        // --- Dispatch / accept ---
        public string DispatchText { get; set; } = "Car accident nearby. Someone has a small cut and needs a bandage.";
        public string DispatchVoiceLine { get; set; } = "Dispatch to all units. Minor car accident nearby. A person has a small cut and needs help. Please respond.";

        // --- En route ---
        public string EnRouteHelpText { get; set; } = "Drive to the yellow marker, then get out and walk up to the patient.";
        public string EnRouteVoiceLine { get; set; } = "Responding to the scene.";

        // --- On scene ---
        public string OnSceneText { get; set; } = "You're on scene. Check on the patient.";
        public string OnSceneVoiceLine { get; set; } = "On scene. Checking on the patient now.";

        // --- Assessment (single tap to help) ---
        public string AssessmentHelpText { get; set; } = "Press E (or X on controller) to help the patient.";
        public string TreatingText { get; set; } = "Putting on a bandage...";

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

        // Legacy random-roll transport chance; no longer used for the outcome
        // (severity decides now) but kept so old configs still deserialize.
        public int TransportPercentage { get; set; } = 40;

        // Scene distance ahead of the player is rolled randomly per dispatch
        // between these two (meters), snapped to a street, so callouts vary from
        // "short drive" to "proper drive". Min kept high enough to never be silly-close.
        public float SceneSpawnDistanceMin { get; set; } = 120f;
        public float SceneSpawnDistanceMax { get; set; } = 450f;

        // Legacy fixed distance (no longer used for the roll; kept for old configs).
        public float SceneSpawnDistance { get; set; } = 60f;

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

        // After treatment, the patient reacts happily and thanks the player, and
        // the callout holds this long (seconds) so the moment is visible before
        // everything despawns.
        public string PatientThanksScenario { get; set; } = "WORLD_HUMAN_CHEERING";
        public string PatientThanksText { get; set; } = "Thank you so much!";
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

        // Transport outcome: an ambulance drives in, the patient loads up, and
        // it drives off (instead of just a text notification).
        public bool ShowAmbulanceOnTransport { get; set; } = true;
        public string AmbulanceModel { get; set; } = "ambulance";
        public string ParamedicModel { get; set; } = "s_m_m_paramedic_01";
        // Overall safety cap for the whole ambulance sequence; it normally
        // finishes sooner (once the ambulance has driven off).
        public float TransportSequenceSeconds { get; set; } = 40f;
    }
}
