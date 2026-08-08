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
        // 0-100: rolled at resolution; treated-on-scene vs transported-to-hospital.
        public int TransportPercentage { get; set; } = 40;

        // How far ahead of the player the scene is placed (meters), snapped to a street.
        public float SceneSpawnDistance { get; set; } = 60f;

        // How close (meters), on foot, the player must get before arrival counts.
        public float ArrivalRadius { get; set; } = 12f;

        // Short "bandaging" beat after the single-tap help press (seconds).
        public float TreatmentSeconds { get; set; } = 4f;

        // Optional parked vehicle at the scene; empty/whitespace = no vehicle.
        public string SceneVehicleModel { get; set; } = "asea";
    }
}
