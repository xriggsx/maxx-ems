using System.Collections.Generic;

namespace EmsMod.Config.Schema
{
    /// <summary>
    /// The "go on duty" loadout: which hospitals exist (for nearest-hospital
    /// start/respawn) and, per mode, the curated character models and vehicles
    /// the player can pick. Config-driven so custom uniforms/vehicles (add-on
    /// mods) are added by editing XML - no rebuild.
    /// </summary>
    public class DutyConfig
    {
        public List<Hospital> Hospitals { get; set; } = new List<Hospital>();
        public List<DutyMode> Modes { get; set; } = new List<DutyMode>();
    }

    public class Hospital
    {
        public string Name { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
    }

    public class DutyMode
    {
        // "Paramedic" / "Firefighter".
        public string Name { get; set; } = "";

        // Ped model names - each is a "character + uniform" for MVP (model swap).
        public List<string> Characters { get; set; } = new List<string>();

        // Response vehicle model names for this mode.
        public List<string> Vehicles { get; set; } = new List<string>();

        // Usable tools given on duty and browsable in the menu (weapon-slot
        // TOOLS only - fire extinguisher, flashlight - never violent weapons).
        public List<string> Equipment { get; set; } = new List<string>();

        // Ped model for the AI partner who rides along in this mode. Empty =
        // use the mode's first character.
        public string PartnerModel { get; set; } = "";
    }
}
