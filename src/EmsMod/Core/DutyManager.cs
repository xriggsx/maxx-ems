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

        private static readonly System.Random _rng = new System.Random();

        private static bool _onDuty;
        private static int _modeIndex;
        private static int _charIndex;
        private static int _vehIndex;
        private static bool _wasDead;
        private static Vehicle _currentVehicle;

        public static bool OnDuty => _onDuty;

        /// <summary>Index into Config.Modes for whichever mode the player is
        /// actually on duty as right now - only meaningful while OnDuty. Used
        /// by the duty menu to detect when its highlighted Mode row has been
        /// scrolled away from the mode actually being worn, so it doesn't let
        /// "grab equipment" apply against the wrong mode's list.</summary>
        public static int CurrentModeIndex => _modeIndex;

        /// <summary>The name ("Paramedic"/"Firefighter") of whichever mode the
        /// player is actually on duty as right now, or null if not on duty -
        /// used to pick automatic dispatches matching the current uniform.</summary>
        public static string CurrentModeName =>
            (_onDuty && _modeIndex >= 0 && _modeIndex < Config.Modes.Count) ? Config.Modes[_modeIndex].Name : null;

        /// <summary>The player's current duty vehicle (ambulance/fire truck), so
        /// callouts can load a transported patient into it and drive to the
        /// hospital, instead of spawning a separate NPC ambulance.</summary>
        public static Vehicle CurrentVehicle => _currentVehicle;

        /// <summary>Set by an active player-driven patient transport so the duty
        /// vehicle can't be deleted out from under a patient riding in it (e.g.
        /// switching vehicles via the duty menu mid-transport).</summary>
        public static bool VehicleSwapLocked { get; set; }

        /// <summary>Checked by anything that would move/despawn the player or
        /// their duty vehicle (going off duty, re-entering duty setup, swapping
        /// vehicles) - refuses and notifies instead of silently orphaning a
        /// patient mid-transport. Returns true if the action should be blocked.</summary>
        private static bool BlockedByVehicleSwapLock(string actionDescription)
        {
            if (!VehicleSwapLocked)
            {
                return false;
            }

            Log.Info($"DutyManager: {actionDescription} blocked - patient transport in progress.");
            Safe.Run(
                () => Game.DisplayNotification($"~r~Can't {actionDescription} while transporting a patient."),
                "DutyManager.vehicle swap locked notify");
            return true;
        }

        private static bool _enteringDuty;

        /// <summary>Runs the full "arrive on duty" sequence at a location:
        /// uniform, teleport, vehicle, equipment, partner. Shared by both
        /// GoOnDuty overloads and the death-respawn path in Tick() so a fix or
        /// change to this sequence - including the vehicle-swap-lock guard and
        /// the non-blocking fiber wrap - only has to be made in one place.
        /// Runs on its own fiber: TeleportToLocation holds the ped in place for
        /// up to ~2s waiting on collision to load, and every caller here runs
        /// inline in the shared main loop alongside PartnerManager/DutyMenu/
        /// CalloutManager/DispatchManager, so blocking would freeze all of
        /// those (including an active callout's state machine) for that span.</summary>
        private static void EnterDutyAt(Hospital start, bool announcePartner = true)
        {
            if (BlockedByVehicleSwapLock("change duty setup"))
            {
                return;
            }

            if (_enteringDuty)
            {
                Log.Info("DutyManager: EnterDutyAt already in progress, ignoring re-entrant call.");
                return;
            }
            _enteringDuty = true;

            GameFiber.StartNew(() =>
            {
                Safe.Run(() =>
                {
                    ApplyCurrentUniform();
                    if (start != null)
                    {
                        TeleportToLocation(start);
                    }
                    SpawnCurrentVehicle();
                    GiveModeEquipment();
                    SpawnMatchingPartner(announcePartner);
                }, "DutyManager.EnterDutyAt");
                _enteringDuty = false;
            }, "DutyManager-EnterDuty");
        }

        /// <summary>Go on duty in the named mode: teleport to a start location,
        /// put on the first uniform, and spawn the first vehicle.</summary>
        public static void GoOnDuty(string modeName)
        {
            Safe.Run(() =>
            {
                if (BlockedByVehicleSwapLock("go on duty"))
                {
                    return;
                }

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

                Hospital start = PickStartLocation();
                EnterDutyAt(start);

                Log.Info($"DutyManager: on duty as {CurrentMode().Name} at {(start != null ? start.Name : "current location")}.");
            }, "DutyManager.GoOnDuty");
        }

        /// <summary>Go on duty with explicit menu selections.</summary>
        public static void GoOnDuty(int modeIndex, int charIndex, int vehIndex)
        {
            Safe.Run(() =>
            {
                if (BlockedByVehicleSwapLock("go on duty"))
                {
                    return;
                }

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

                Hospital start = PickStartLocation();
                EnterDutyAt(start);

                Log.Info($"DutyManager: on duty as {mode.Name} at {(start != null ? start.Name : "current location")}.");
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
                GiveModeEquipment();
            }, "DutyManager.ReapplyLoadout");
        }

        public static void OffDuty()
        {
            Safe.Run(() =>
            {
                if (BlockedByVehicleSwapLock("go off duty"))
                {
                    return;
                }

                EntitySpawnRegistry.CleanupOwner(DutyOwner);
                PartnerManager.Despawn();
                _onDuty = false;
                _currentVehicle = null;
                Log.Info("DutyManager: off duty.");
            }, "DutyManager.OffDuty");
        }

        public static void NextCharacter()
        {
            Safe.Run(() =>
            {
                if (!_onDuty) { Log.Info("DutyManager: not on duty."); return; }
                if (BlockedByVehicleSwapLock("change uniform"))
                {
                    return;
                }
                int uCount = UniformCount(_modeIndex);
                if (uCount == 0) { return; }
                _charIndex = (_charIndex + 1) % uCount;
                ApplyCurrentUniform();
                // Swapping the player model wipes the ped's inventory, so re-give
                // the mode's tools, and rematch the partner's uniform. Not
                // announced - this fires on every uniform cycle, not just going
                // on duty, so a notification here would spam the menu.
                GiveModeEquipment();
                SpawnMatchingPartner(announce: false);
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
                    Log.Info("DutyManager: player respawning at a duty station.");

                    // EnterDutyAt runs itself on its own fiber and checks the
                    // vehicle-swap lock, so this stays non-blocking and (if the
                    // player died mid-transport) correctly leaves the duty
                    // vehicle/patient alone instead of deleting it out from
                    // under them - the lock clears once that transport
                    // resolves, and the player can reopen the duty menu then.
                    Hospital start = PickStartLocation();
                    EnterDutyAt(start);
                }
            }, "DutyManager.Tick");
        }

        private static DutyMode CurrentMode() => Config.Modes[_modeIndex];

        // Partner always mirrors the player's current uniform, so switching
        // uniforms (NextCharacter, which re-spawns the partner) always brings
        // the partner along in a matching look - no separate "dedicated
        // partner model" override.
        private static string PartnerModelForCurrentMode()
        {
            DutyMode mode = CurrentMode();
            int charCount = mode.Characters.Count;

            if (_charIndex < charCount && charCount > 0)
            {
                return mode.Characters[_charIndex];
            }

            // Player is wearing a saved wardrobe outfit (freemode ped) - the
            // partner must also be freemode so CurrentOutfitOrNull's pieces can
            // be applied to it right after spawn (a fixed character model's
            // skeleton/components wouldn't match).
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

        // The list of start/respawn locations for the current mode: fire stations
        // for a "FireStation" mode, hospitals otherwise (with a hospital fallback
        // if a mode has no fire stations configured).
        private static List<Hospital> StartLocationsForCurrentMode()
        {
            DutyMode mode = CurrentMode();
            bool wantsFireStations = string.Equals(
                mode.StartLocationType, "FireStation", System.StringComparison.OrdinalIgnoreCase);
            if (wantsFireStations && Config.FireStations != null && Config.FireStations.Count > 0)
            {
                return Config.FireStations;
            }
            return Config.Hospitals;
        }

        // Pick a random start location from the current mode's list so the player
        // starts at a different hospital / fire station each time they go on duty.
        private static Hospital PickStartLocation()
        {
            List<Hospital> list = StartLocationsForCurrentMode();
            if (list == null || list.Count == 0)
            {
                return null;
            }
            return list[_rng.Next(list.Count)];
        }

        /// <summary>Finds the hospital nearest a given position - used to route a
        /// transported patient to the closest one, regardless of duty mode (a
        /// firefighter still takes an injured patient to a hospital, not a fire
        /// station).</summary>
        public static Hospital NearestHospital(Vector3 from)
        {
            Hospital nearest = null;
            float best = float.MaxValue;

            foreach (Hospital h in Config.Hospitals)
            {
                float d = from.DistanceTo(new Vector3(h.X, h.Y, h.Z));
                if (d < best)
                {
                    best = d;
                    nearest = h;
                }
            }

            return nearest;
        }

        private static void TeleportToLocation(Hospital location)
        {
            Safe.Run(() =>
            {
                Ped character = Game.LocalPlayer.Character;
                Vector3 target = new Vector3(location.X, location.Y, location.Z + 2f);
                character.Position = target;
                character.Heading = location.Heading;

                // Request the world/collision at the destination and hold the ped
                // there until it's loaded, so the player doesn't fall through the
                // map ("under the city") before the ground streams in.
                NativeFunction.Natives.REQUEST_COLLISION_AT_COORD(target.X, target.Y, target.Z);
                int tries = 0;
                while (tries < 120)
                {
                    bool loaded = Safe.Run(
                        () => (bool)NativeFunction.Natives.HAS_COLLISION_LOADED_AROUND_ENTITY<bool>(character),
                        false,
                        "DutyManager.collision check");
                    if (loaded)
                    {
                        break;
                    }
                    character.Position = target; // keep them pinned while it loads
                    GameFiber.Yield();
                    tries++;
                }

                // Drop onto the ground once collision is present.
                Safe.Run(() => NativeFunction.Natives.SET_PED_COORDS_KEEP_VEHICLE(
                    character, location.X, location.Y, location.Z), "DutyManager.settle on ground");
            }, "DutyManager.TeleportToLocation");
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
        private static void SpawnMatchingPartner(bool announce = true)
        {
            if (!Config.EnablePartner)
            {
                Log.Info("DutyManager: partner disabled (EnablePartner=false in Duty.xml).");
                if (announce)
                {
                    Safe.Run(() => Game.DisplayNotification("~r~Partner disabled (EnablePartner=false in Duty.xml)."), "DutyManager.partner disabled notify");
                }
                return;
            }
            PartnerManager.Spawn(PartnerModelForCurrentMode(), announce);
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

            if (VehicleSwapLocked)
            {
                // A patient is currently riding in the duty vehicle mid-transport
                // - deleting it now (e.g. via "Next Vehicle") would eject/ragdoll
                // them out of a moving/parked vehicle. Refuse instead.
                Log.Info("DutyManager: vehicle swap blocked - patient transport in progress.");
                Safe.Run(() => Game.DisplayNotification("~r~Can't swap vehicles while transporting a patient."), "DutyManager.vehicle swap locked notify");
                return;
            }

            // Replace any existing duty vehicle.
            EntitySpawnRegistry.CleanupOwner(DutyOwner);
            _currentVehicle = null;

            DutyVehicle veh = vehs[Clamp(_vehIndex, vehs.Count)];
            string vehModel = veh.Model;
            Safe.Run(() =>
            {
                Ped character = Game.LocalPlayer.Character;

                // Spawn on the nearest drivable road node, aligned to the road,
                // so the vehicle is never blocked in and can drive straight off.
                Vector3 near = character.Position + character.ForwardVector * 8f;
                Log.Info($"SpawnVehicle: model={vehModel} - finding road node");
                RoadHelper.ResolveNearestRoad(near, "DutyManager.SpawnVehicle", out Vector3 pos, out float heading);

                Log.Info($"SpawnVehicle: creating {vehModel}");
                var vehicle = new Vehicle(vehModel, pos, heading) { IsPersistent = true };
                EntitySpawnRegistry.RegisterEntity(DutyOwner, vehicle);
                _currentVehicle = vehicle;
                Log.Info("SpawnVehicle: created");
                if (veh.Livery >= 0)
                {
                    Safe.Run(() => NativeFunction.Natives.SET_VEHICLE_LIVERY(vehicle, veh.Livery), "DutyManager.vehicle livery");
                }
                Safe.Run(() => NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle), "DutyManager.vehicle on ground");
                Log.Info("SpawnVehicle: done");
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
        /// tools only, e.g. fire extinguisher / flashlight). None are equipped
        /// as the active item - the player selects one from the duty menu.</summary>
        private static void GiveModeEquipment()
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
                    ped.Inventory.GiveNewWeapon(new WeaponAsset(equip[i]), 4000, false);
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
