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
                ApplyCurrentUniform();
                if (hospital != null)
                {
                    TeleportToHospital(hospital);
                }
                SpawnCurrentVehicle();
                GiveModeEquipment(false);
                SpawnMatchingPartner();

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
                _charIndex = Clamp(charIndex, UniformCount(modeIndex));
                _vehIndex = Clamp(vehIndex, mode.Vehicles.Count);
                _onDuty = true;
                _wasDead = false;

                Hospital hospital = NearestHospital();
                ApplyCurrentUniform();
                if (hospital != null)
                {
                    TeleportToHospital(hospital);
                }
                SpawnCurrentVehicle();
                GiveModeEquipment(false);
                SpawnMatchingPartner();

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
                ApplyCurrentUniform();
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
                int uCount = UniformCount(_modeIndex);
                if (uCount == 0) { return; }
                _charIndex = (_charIndex + 1) % uCount;
                ApplyCurrentUniform();
                // Swapping the player model wipes the ped's inventory, so re-give
                // the mode's tools, and rematch the partner's uniform.
                GiveModeEquipment(false);
                SpawnMatchingPartner();
                Log.Info($"DutyManager: uniform -> {UniformName(_modeIndex, _charIndex)}.");
            }, "DutyManager.NextCharacter");
        }

        public static void NextVehicle()
        {
            Safe.Run(() =>
            {
                if (!_onDuty) { Log.Info("DutyManager: not on duty."); return; }
                List<DutyVehicle> vehs = CurrentMode().Vehicles;
                if (vehs.Count == 0) { return; }
                _vehIndex = (_vehIndex + 1) % vehs.Count;
                SpawnCurrentVehicle();
                Log.Info($"DutyManager: vehicle -> {VehicleName(_modeIndex, _vehIndex)}.");
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
                    ApplyCurrentUniform();
                    if (hospital != null)
                    {
                        TeleportToHospital(hospital);
                    }
                    SpawnCurrentVehicle();
                    GiveModeEquipment(false);
                    SpawnMatchingPartner();
                }
            }, "DutyManager.Tick");
        }

        private static DutyMode CurrentMode() => Config.Modes[_modeIndex];

        private static string PartnerModelForCurrentMode()
        {
            // Partner matches the player's uniform. For a character uniform that's
            // the ped model; for a saved outfit it's a freemode ped (the outfit
            // pieces are applied to the partner right after spawn).
            DutyMode mode = CurrentMode();
            int charCount = mode.Characters.Count;
            if (_charIndex < charCount && charCount > 0)
            {
                return mode.Characters[_charIndex];
            }
            return "mp_m_freemode_01";
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

        // The uniform list per mode = ped-model characters, then saved wardrobe
        // outfits (which show for every mode). _charIndex spans this combined list.
        public static int UniformCount(int modeIndex)
        {
            if (modeIndex < 0 || modeIndex >= Config.Modes.Count)
            {
                return 0;
            }
            return Config.Modes[modeIndex].Characters.Count + OutfitStore.Outfits.Count;
        }

        public static string UniformName(int modeIndex, int index)
        {
            if (modeIndex < 0 || modeIndex >= Config.Modes.Count)
            {
                return "(none)";
            }
            DutyMode mode = Config.Modes[modeIndex];
            if (index >= 0 && index < mode.Characters.Count)
            {
                return mode.Characters[index];
            }
            int oi = index - mode.Characters.Count;
            List<Outfit> outfits = OutfitStore.Outfits;
            return (oi >= 0 && oi < outfits.Count) ? outfits[oi].Name : "(none)";
        }

        private static void ApplyCurrentUniform()
        {
            DutyMode mode = CurrentMode();
            int charCount = mode.Characters.Count;

            if (_charIndex < charCount)
            {
                if (charCount == 0) { return; }
                ApplyPlayerModel(mode.Characters[_charIndex]);
                return;
            }

            // A saved wardrobe outfit: freemode ped + the saved pieces.
            int oi = _charIndex - charCount;
            List<Outfit> outfits = OutfitStore.Outfits;
            if (oi >= 0 && oi < outfits.Count)
            {
                ApplyPlayerModel("mp_m_freemode_01");
                OutfitStore.Apply(Game.LocalPlayer.Character, outfits[oi]);
            }
        }

        private static void ApplyPlayerModel(string modelName)
        {
            Safe.Run(() =>
            {
                var model = new Model(modelName);
                model.LoadAndWait();
                NativeFunction.Natives.SET_PLAYER_MODEL(Game.LocalPlayer, model.Hash);
                NativeFunction.Natives.SET_PED_DEFAULT_COMPONENT_VARIATION(Game.LocalPlayer.Character);
                model.Dismiss();
            }, $"DutyManager.ApplyPlayerModel({modelName})");
        }

        private static Outfit CurrentOutfitOrNull()
        {
            int charCount = CurrentMode().Characters.Count;
            if (_charIndex < charCount)
            {
                return null;
            }
            int oi = _charIndex - charCount;
            List<Outfit> outfits = OutfitStore.Outfits;
            return (oi >= 0 && oi < outfits.Count) ? outfits[oi] : null;
        }

        // Spawn the partner matching the player's uniform (character model, or a
        // freemode ped wearing the same saved outfit).
        private static void SpawnMatchingPartner()
        {
            SpawnMatchingPartner();
            Outfit outfit = CurrentOutfitOrNull();
            if (outfit != null)
            {
                PartnerManager.ApplyOutfit(outfit);
            }
        }

        private static void SpawnCurrentVehicle()
        {
            List<DutyVehicle> vehs = CurrentMode().Vehicles;
            if (vehs.Count == 0)
            {
                return;
            }

            // Replace any existing duty vehicle.
            EntitySpawnRegistry.CleanupOwner(DutyOwner);

            DutyVehicle veh = vehs[Clamp(_vehIndex, vehs.Count)];
            string vehModel = veh.Model;
            Safe.Run(() =>
            {
                Ped character = Game.LocalPlayer.Character;

                // Spawn on the nearest drivable road node, aligned to the road,
                // so the vehicle is never blocked in and can drive straight off.
                Vector3 near = character.Position + character.ForwardVector * 8f;
                Vector3 pos = near;
                float heading = character.Heading;

                Safe.Run(() =>
                {
                    Vector3 node;
                    float nodeHeading;
                    bool found = NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(
                        near.X, near.Y, near.Z, out node, out nodeHeading, 1, 3.0f, 0);
                    if (found)
                    {
                        pos = node;
                        heading = nodeHeading;
                    }
                }, "DutyManager.vehicle road node");

                var vehicle = new Vehicle(vehModel, pos, heading) { IsPersistent = true };
                EntitySpawnRegistry.RegisterEntity(DutyOwner, vehicle);
                if (veh.Livery >= 0)
                {
                    Safe.Run(() => NativeFunction.Natives.SET_VEHICLE_LIVERY(vehicle, veh.Livery), "DutyManager.vehicle livery");
                }
                Safe.Run(() => NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle), "DutyManager.vehicle on ground");
            }, $"DutyManager.SpawnCurrentVehicle({vehModel})");
        }

        public static int VehicleCount(int modeIndex)
        {
            if (modeIndex < 0 || modeIndex >= Config.Modes.Count)
            {
                return 0;
            }
            return Config.Modes[modeIndex].Vehicles.Count;
        }

        public static string VehicleName(int modeIndex, int index)
        {
            if (modeIndex < 0 || modeIndex >= Config.Modes.Count)
            {
                return "(none)";
            }
            List<DutyVehicle> vehs = Config.Modes[modeIndex].Vehicles;
            if (index < 0 || index >= vehs.Count)
            {
                return "(none)";
            }
            DutyVehicle v = vehs[index];
            return string.IsNullOrWhiteSpace(v.Name) ? v.Model : v.Name;
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
