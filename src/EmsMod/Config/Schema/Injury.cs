using System.Collections.Generic;

namespace EmsMod.Config.Schema
{
    /// <summary>
    /// One possible injury for a callout. A callout defines several of these
    /// (e.g. a minor, a moderate, and a severe one); one is picked at random per
    /// dispatch, and ITS severity drives the patient's pose and whether they need
    /// transport - so the pose and the description always match the injury.
    ///
    /// Any text left blank falls back to the callout-level default, so an injury
    /// only needs to specify what makes it different.
    /// </summary>
    public class Injury
    {
        // "Minor" (stands, treated on scene), "Moderate" (kneels, treated on
        // scene), or "Severe" (lies down, needs transport to hospital).
        public string Severity { get; set; } = "Moderate";

        public string DispatchText { get; set; } = "";
        public string DispatchVoiceLine { get; set; } = "";
        public string OnSceneText { get; set; } = "";
        public string OnSceneVoiceLine { get; set; } = "";
        public List<DialogueLine> AssessmentLines { get; set; } = new List<DialogueLine>();
        public string TreatingText { get; set; } = "";
        public string TreatedText { get; set; } = "";
        public string TransportedText { get; set; } = "";
        public string ResolutionVoiceLine { get; set; } = "";
        public string PatientThanksText { get; set; } = "";

        // Optional explicit pose clip; if blank, the pose is chosen by Severity.
        public string PoseAnimDictionary { get; set; } = "";
        public string PoseAnimName { get; set; } = "";
    }
}
