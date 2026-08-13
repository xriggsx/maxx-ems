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

    public class DutyVehicle
    {
        public string Model { get; set; } = "";
        // Livery index to apply, or -1 for the vehicle's default paint.
        public int Livery { get; set; } = -1;
        // Menu display name; falls back to Model if empty.
        public string Name { get; set; } = "";
    }

    public class DutyMode
    {
        // "Paramedic" / "Firefighter".
        public string Name { get; set; } = "";

        // Ped model names - each is a "character + uniform" for MVP (model swap).
        public List<string> Characters { get; set; } = new List<string>();

        // Response vehicles for this mode. Each is a model + optional livery, so
        // one model can appear several times as different paint schemes (e.g.
        // NSW / Victorian / QLD) without replacing anything.
        public List<DutyVehicle> Vehicles { get; set; } = new List<DutyVehicle>();

        // Usable tools given on duty and browsable in the menu (weapon-slot
        // TOOLS only - fire extinguisher, flashlight - never violent weapons).
        public List<string> Equipment { get; set; } = new List<string>();

        // Ped model for the AI partner who rides along in this mode. Empty =
        // use the mode's first character.
        public string PartnerModel { get; set; } = "";
    }
}
