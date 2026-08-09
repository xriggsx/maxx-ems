using Rage.Attributes;
using EmsMod.Core;
using EmsMod.Utils;

namespace EmsMod.Debug
{
    /// <summary>
    /// Command interface for the go-on-duty mechanics while the on-screen
    /// hospital menu is being built. These prove the model-swap / vehicle-spawn /
    /// teleport / respawn plumbing in-game before the menu sits on top of it.
    /// </summary>
    public static class DutyCommands
    {
        [ConsoleCommand(Name = "emsmod_duty", Description = "Go on duty as a Paramedic (nearest hospital, uniform + ambulance).")]
        public static void DutyParamedic()
        {
            Safe.Run(() => DutyManager.GoOnDuty("Paramedic"), "DutyCommands.DutyParamedic");
        }

        [ConsoleCommand(Name = "emsmod_duty_fire", Description = "Go on duty as a Firefighter (nearest hospital, uniform + fire truck).")]
        public static void DutyFirefighter()
        {
            Safe.Run(() => DutyManager.GoOnDuty("Firefighter"), "DutyCommands.DutyFirefighter");
        }

        [ConsoleCommand(Name = "emsmod_offduty", Description = "Go off duty (despawns the duty vehicle).")]
        public static void OffDuty()
        {
            Safe.Run(() => DutyManager.OffDuty(), "DutyCommands.OffDuty");
        }

        [ConsoleCommand(Name = "emsmod_duty_char", Description = "Cycle to the next character/uniform for the current mode.")]
        public static void NextCharacter()
        {
            Safe.Run(() => DutyManager.NextCharacter(), "DutyCommands.NextCharacter");
        }

        [ConsoleCommand(Name = "emsmod_duty_veh", Description = "Cycle to the next vehicle for the current mode.")]
        public static void NextVehicle()
        {
            Safe.Run(() => DutyManager.NextVehicle(), "DutyCommands.NextVehicle");
        }
    }
}
