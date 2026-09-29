using System.Collections.Generic;
using System.Xml.Serialization;

namespace EmsMod.Config.Schema
{
    /// <summary>
    /// The "go on duty" loadout: the start/respawn locations (hospitals for
    /// paramedics, fire stations for firefighters - one is picked at random each
    /// time you go on duty) and, per mode, the curated character models and
    /// vehicles the player can pick. Config-driven so custom uniforms/vehicles
    /// (add-on mods) are added by editing XML - no rebuild.
    /// </summary>
    public class DutyConfig
    {
        public List<Hospital> Hospitals { get; set; } = new List<Hospital>();

        // Firefighter start/respawn locations. Same shape as a hospital (a named
        // coordinate); a mode with StartLocationType "FireStation" starts here.
        [XmlArrayItem("FireStation")]
        public List<Hospital> FireStations { get; set; } = new List<Hospital>();

        public List<DutyMode> Modes { get; set; } = new List<DutyMode>();

        // AI partner rides along whenever the player is on duty.
        public bool EnablePartner { get; set; } = true;
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

        // Which location list this mode starts/respawns at: "Hospital" (default)
        // uses <Hospitals>; "FireStation" uses <FireStations>. One entry from the
        // chosen list is picked at random each time the player goes on duty.
        public string StartLocationType { get; set; } = "Hospital";

        // Ped model names - each is a "character + uniform" for MVP (model swap).
        public List<string> Characters { get; set; } = new List<string>();

        // Response vehicles for this mode. Each is a model + optional livery, so
        // one model can appear several times as different paint schemes (e.g.
        // NSW / Victorian / QLD) without replacing anything.
        public List<DutyVehicle> Vehicles { get; set; } = new List<DutyVehicle>();

        // Usable tools given on duty and browsable in the menu (weapon-slot
        // TOOLS only - fire extinguisher, flashlight - never violent weapons).
        public List<string> Equipment { get; set; } = new List<string>();
    }
}
