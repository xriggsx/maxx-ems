using System.Collections.Generic;
using Rage;
using Rage.Native;
using EmsMod.Config;
using EmsMod.Config.Schema;
using EmsMod.Utils;

namespace EmsMod.Core
{
    /// <summary>
    /// The "go on duty" system: puts the player in an EMS/fire uniform, spawns
    /// their response vehicle, starts them at the nearest hospital, lets them
    /// change loadout any time, and respawns them at the nearest hospital (with
    /// their vehicle, still in uniform) if they die - no fail state.
    ///
    /// This is the command-driven mechanics layer; the on-screen hospital menu is
    /// built on top of these calls once they're proven in-game.
    /// </summary>
    public static class DutyManager
    {
        private const string DutyOwner = "PlayerDuty";

        public static DutyConfig Config => ConfigLoader.Duty;

        private static bool _onDuty;
        private static int _modeIndex;
        private static int _charIndex;
        private static int _vehIndex;
        private static bool _wasDead;

        public static bool OnDuty => _onDuty;

        /// <summary>Go on duty in the named mode: teleport to the nearest
        /// hospital, put on the first uniform, and spawn the first vehicle.</summary>
        public static void GoOnDuty(string modeName)
        {
            Safe.Run(() =>
            {
                int modeIndex = FindModeIndex(modeName);
                if (modeIndex < 0)
                {
                    Log.Warn($"DutyManager: no duty mode named '{modeName}'.");
                    return;
                }

                _modeIndex = modeIndex;
                _charIndex = 0;
                _vehIndex = 0;
                _onDuty = true;
                _wasDead = false;

                Hospital hospital = NearestHospital();
                ApplyCurrentCharacter();
                if (hospital != null)
                {
                    TeleportToHospital(hospital);
                }
                SpawnCurrentVehicle();
                GiveModeEquipment(false);
                PartnerManager.Spawn(PartnerModelForCurrentMode());

                Log.Info($"DutyManager: on duty as {CurrentMode().Name} at {(hospital != null ? hospital.Name : "current location")}.");
            }, "DutyManager.GoOnDuty");
        }

        /// <summary>Go on duty with explicit menu selections.</summary>
        public static void GoOnDuty(int modeIndex, int charIndex, int vehIndex)
        {
            Safe.Run(() =>
            {
                if (modeIndex < 0 || modeIndex >= Config.Modes.Count)
                {
                    return;
                }

                _modeIndex = modeIndex;
                DutyMode mode = Config.Modes[modeIndex];
                _charIndex = Clamp(charIndex, mode.Characters.Count);
                _vehIndex = Clamp(vehIndex, mode.Vehicles.Count);
                _onDuty = true;
                _wasDead = false;

                Hospital hospital = NearestHospital();
                ApplyCurrentCharacter();
                if (hospital != null)
                {
                    TeleportToHospital(hospital);
                }
                SpawnCurrentVehicle();
                GiveModeEquipment(false);
                PartnerManager.Spawn(PartnerModelForCurrentMode());

                Log.Info($"DutyManager: on duty as {mode.Name} at {(hospital != null ? hospital.Name : "current location")}.");
            }, "DutyManager.GoOnDuty(indices)");
        }

        private static int Clamp(int index, int count)
        {
            if (count <= 0) { return 0; }
            if (index < 0) { return 0; }
            if (index >= count) { return count - 1; }
            return index;
        }

        /// <summary>Re-applies the current uniform + tools (no teleport / vehicle
        /// respawn). Used to restore the player after the wardrobe temporarily
        /// swaps them to a freemode ped.</summary>
        public static void ReapplyLoadout()
        {
            Safe.Run(() =>
            {
                if (!_onDuty)
                {
                    return;
                }
                ApplyCurrentCharacter();
                GiveModeEquipment(false);
            }, "DutyManager.ReapplyLoadout");
        }

        public static void OffDuty()
        {
            Safe.Run(() =>
            {
                EntitySpawnRegistry.CleanupOwner(DutyOwner);
                PartnerManager.Despawn();
                _onDuty = false;
                Log.Info("DutyManager: off duty.");
            }, "DutyManager.OffDuty");
        }

        public static void NextCharacter()
        {
            Safe.Run(() =>
            {
                if (!_onDuty) { Log.Info("DutyManager: not on duty."); return; }
                List<string> chars = CurrentMode().Characters;
                if (chars.Count == 0) { return; }
                _charIndex = (_charIndex + 1) % chars.Count;
                ApplyCurrentCharacter();
                // Swapping the player model wipes the ped's inventory, so re-give
                // the mode's tools, and rematch the partner's uniform.
                GiveModeEquipment(false);
                PartnerManager.Spawn(PartnerModelForCurrentMode());
                Log.Info($"DutyManager: character -> {chars[_charIndex]}.");
            }, "DutyManager.NextCharacter");
        }

        public static void NextVehicle()
        {
            Safe.Run(() =>
            {
                if (!_onDuty) { Log.Info("DutyManager: not on duty."); return; }
                List<string> vehs = CurrentMode().Vehicles;
                if (vehs.Count == 0) { return; }
                _vehIndex = (_vehIndex + 1) % vehs.Count;
                SpawnCurrentVehicle();
                Log.Info($"DutyManager: vehicle -> {vehs[_vehIndex]}.");
            }, "DutyManager.NextVehicle");
        }

        /// <summary>Ticked every frame from EntryPoint: if the on-duty player
        /// dies, put them back at the nearest hospital with their kit once the
        /// game respawns them.</summary>
        public static void Tick()
        {
            if (!_onDuty)
            {
                return;
            }

            Safe.Run(() =>
            {
                Ped character = Game.LocalPlayer.Character;
                if (character == null || !character.Exists())
                {
                    return;
                }

                if (character.IsDead)
                {
                    _wasDead = true;
                    return;
                }

                if (_wasDead && character.IsAlive)
                {
                    _wasDead = false;
                    Log.Info("DutyManager: player respawning at nearest hospital.");
                    Hospital hospital = NearestHospital();
                    ApplyCurrentCharacter();
                    if (hospital != null)
                    {
                        TeleportToHospital(hospital);
                    }
                    SpawnCurrentVehicle();
                    GiveModeEquipment(false);
                    PartnerManager.Spawn(PartnerModelForCurrentMode());
                }
            }, "DutyManager.Tick");
        }

        private static DutyMode CurrentMode() => Config.Modes[_modeIndex];

        private static string PartnerModelForCurrentMode()
        {
            // Partner always wears the SAME uniform as the player (their current
            // character), so they look like a matching crew.
            DutyMode mode = CurrentMode();
            if (mode.Characters.Count > 0)
            {
                int idx = (_charIndex >= 0 && _charIndex < mode.Characters.Count) ? _charIndex : 0;
                return mode.Characters[idx];
            }
            if (!string.IsNullOrWhiteSpace(mode.PartnerModel))
            {
                return mode.PartnerModel;
            }
            return "s_m_m_paramedic_01";
        }

        private static int FindModeIndex(string modeName)
        {
            for (int i = 0; i < Config.Modes.Count; i++)
            {
                if (string.Equals(Config.Modes[i].Name, modeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static Hospital NearestHospital()
        {
            Vector3 playerPos = Game.LocalPlayer.Character.Position;
            Hospital nearest = null;
            float best = float.MaxValue;

            foreach (Hospital h in Config.Hospitals)
            {
                float d = playerPos.DistanceTo(new Vector3(h.X, h.Y, h.Z));
                if (d < best)
                {
                    best = d;
                    nearest = h;
                }
            }

            return nearest;
        }

        private static void TeleportToHospital(Hospital hospital)
        {
            Safe.Run(() =>
            {
                Ped character = Game.LocalPlayer.Character;
                character.Position = new Vector3(hospital.X, hospital.Y, hospital.Z);
                character.Heading = hospital.Heading;
            }, "DutyManager.TeleportToHospital");
        }

        private static void ApplyCurrentCharacter()
        {
            List<string> chars = CurrentMode().Characters;
            if (chars.Count == 0)
            {
                return;
            }

            string modelName = chars[_charIndex];
            Safe.Run(() =>
            {
                var model = new Model(modelName);
                model.LoadAndWait();
                NativeFunction.Natives.SET_PLAYER_MODEL(Game.LocalPlayer, model.Hash);
                NativeFunction.Natives.SET_PED_DEFAULT_COMPONENT_VARIATION(Game.LocalPlayer.Character);
                model.Dismiss();
            }, $"DutyManager.ApplyCurrentCharacter({modelName})");
        }

        private static void SpawnCurrentVehicle()
        {
            List<string> vehs = CurrentMode().Vehicles;
            if (vehs.Count == 0)
            {
                return;
            }

            // Replace any existing duty vehicle.
            EntitySpawnRegistry.CleanupOwner(DutyOwner);

            string vehModel = vehs[_vehIndex];
            Safe.Run(() =>
            {
                Ped character = Game.LocalPlayer.Character;
                Vector3 pos = character.Position + character.RightVector * 4f;
                var vehicle = new Vehicle(vehModel, pos, character.Heading);
                EntitySpawnRegistry.RegisterEntity(DutyOwner, vehicle);
            }, $"DutyManager.SpawnCurrentVehicle({vehModel})");
        }

        /// <summary>Gives the current mode's tools to the player (kid-friendly
        /// tools only, e.g. fire extinguisher / flashlight).</summary>
        private static void GiveModeEquipment(bool equipLastAsActive)
        {
            List<string> equip = CurrentMode().Equipment;
            if (equip == null || equip.Count == 0)
            {
                return;
            }

            Safe.Run(() =>
            {
                Ped ped = Game.LocalPlayer.Character;
                for (int i = 0; i < equip.Count; i++)
                {
                    bool equipNow = equipLastAsActive && i == equip.Count - 1;
                    ped.Inventory.GiveNewWeapon(new WeaponAsset(equip[i]), 4000, equipNow);
                }
            }, "DutyManager.GiveModeEquipment");
        }

        /// <summary>Menu hook: give and equip a specific tool from the current
        /// mode's equipment list, so the player can grab it on the spot.</summary>
        public static void EquipItem(int index)
        {
            Safe.Run(() =>
            {
                if (!_onDuty)
                {
                    Log.Info("DutyManager: not on duty.");
                    return;
                }

                List<string> equip = CurrentMode().Equipment;
                if (equip == null || equip.Count == 0 || index < 0 || index >= equip.Count)
                {
                    return;
                }

                Ped ped = Game.LocalPlayer.Character;
                ped.Inventory.GiveNewWeapon(new WeaponAsset(equip[index]), 4000, true);
                Log.Info($"DutyManager: equipped {equip[index]}.");
            }, "DutyManager.EquipItem");
        }
    }
}
