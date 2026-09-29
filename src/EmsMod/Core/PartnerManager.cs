using Rage;
using Rage.Native;
using EmsMod.Config.Schema;
using EmsMod.Utils;

namespace EmsMod.Core
{
    /// <summary>
    /// The AI partner who rides along with the player: follows on foot, gets in
    /// the player's vehicle as a passenger, gets out when the player does, and is
    /// invincible so the player never loses their buddy. Spawned/despawned by
    /// DutyManager alongside the duty lifecycle; ticked every frame from EntryPoint.
    /// </summary>
    public static class PartnerManager
    {
        private const string Owner = "PlayerPartner";

        private const int ModeNone = 0;
        private const int ModeFollowing = 1;
        private const int ModeEntering = 2;
        private const int ModeInVehicle = 3;
        private const int ModeAssisting = 4;

        private static Ped _partner;
        private static int _mode;
        private static float _enteringElapsed;
        private const float ReenterRetrySeconds = 5f;

        public static void Spawn(string modelName, bool announce = true)
        {
            Despawn();

            Safe.Run(() =>
            {
                Ped player = Game.LocalPlayer.Character;
                Vector3 pos = player.Position - player.RightVector * 2f;

                Log.Info($"Partner: validating model {modelName}");
                var model = new Model(modelName);
                if (!model.IsValid)
                {
                    Log.Warn($"Partner: model '{modelName}' is invalid; skipping partner.");
                    if (announce)
                    {
                        Game.DisplayNotification($"~r~Partner couldn't spawn: model '{modelName}' is invalid.");
                    }
                    return;
                }

                Log.Info("Partner: loading model");
                model.LoadAndWait();
                Log.Info("Partner: creating ped");
                _partner = new Ped(model, pos, player.Heading);
                model.Dismiss();

                if (_partner == null || !_partner.Exists())
                {
                    Log.Warn("Partner: ped failed to create.");
                    if (announce)
                    {
                        Game.DisplayNotification("~r~Partner couldn't spawn: ped creation failed.");
                    }
                    return;
                }

                Log.Info("Partner: setting flags");
                _partner.BlockPermanentEvents = true;
                _partner.IsInvincible = true;
                EntitySpawnRegistry.RegisterEntity(Owner, _partner);

                _mode = ModeNone;
                Log.Info("Partner: issuing follow");
                IssueFollow();
                _mode = ModeFollowing;
                Log.Info("Partner: done");
                if (announce)
                {
                    Game.DisplayNotification("~g~Partner ready.");
                }
            }, "PartnerManager.Spawn");
        }

        /// <summary>Dress the partner in a saved outfit (after spawning them as a
        /// freemode ped) so they match a player wearing that outfit.</summary>
        public static void ApplyOutfit(Outfit outfit)
        {
            if (_partner == null || !_partner.Exists())
            {
                return;
            }
            OutfitStore.Apply(_partner, outfit);
        }

        public static void Despawn()
        {
            EntitySpawnRegistry.CleanupOwner(Owner);
            _partner = null;
            _mode = ModeNone;
        }

        /// <summary>Sends the partner to a scene position (e.g. beside a patient)
        /// and pauses the normal follow/vehicle behavior until ResumeFollowing()
        /// is called - used by callouts so the partner visibly helps instead of
        /// just trailing the player everywhere.</summary>
        public static void AssistAtScene(Vector3 position, float heading)
        {
            if (_partner == null || !_partner.Exists())
            {
                return;
            }

            Safe.Run(() =>
            {
                _partner.Tasks.ClearImmediately();
                _partner.Tasks.FollowNavigationMeshToPosition(position, heading, 1.3f);
            }, "PartnerManager.AssistAtScene");
            _mode = ModeAssisting;
        }

        /// <summary>Plays a looped animation on the partner (e.g. the same
        /// kneel/bandage clip the player is playing) while assisting at a scene.
        /// No-ops safely if the partner isn't currently assisting, doesn't exist,
        /// or the clip fails to load.</summary>
        public static void PlayAssistAnimation(string dictionary, string name)
        {
            if (_mode != ModeAssisting)
            {
                return;
            }

            AnimHelper.PlayLoopedSafe(_partner, dictionary, name, "PartnerManager.PlayAssistAnimation");
        }

        /// <summary>Hands the partner back to normal follow/vehicle behavior
        /// after assisting at a scene. Safe to call even if not currently
        /// assisting (e.g. as a cleanup safety net).</summary>
        public static void ResumeFollowing()
        {
            if (_mode != ModeAssisting)
            {
                return;
            }

            _mode = ModeNone;
            Safe.Run(() =>
            {
                if (_partner != null && _partner.Exists())
                {
                    _partner.Tasks.ClearImmediately();
                }
            }, "PartnerManager.ResumeFollowing");
        }

        public static void Tick()
        {
            if (_partner == null || !_partner.Exists())
            {
                return;
            }

            if (_mode == ModeAssisting)
            {
                // Held by AssistAtScene/ResumeFollowing (a callout is directing
                // the partner right now) - don't let the normal follow/vehicle
                // logic override it.
                return;
            }

            Safe.Run(() =>
            {
                Ped player = Game.LocalPlayer.Character;
                Vehicle vehicle = player.CurrentVehicle;

                if (vehicle != null && vehicle.Exists())
                {
                    // Player is in a vehicle - get the partner into it.
                    if (_partner.CurrentVehicle == vehicle)
                    {
                        _mode = ModeInVehicle;
                    }
                    else if (_mode != ModeEntering)
                    {
                        _partner.Tasks.EnterVehicle(vehicle, -1, 0);
                        _mode = ModeEntering;
                        _enteringElapsed = 0f;
                    }
                    else
                    {
                        // Still trying to get in - if a blocked door/bad path
                        // means the task never actually completes, periodically
                        // re-issue it instead of leaving the partner frozen
                        // outside for the rest of the drive.
                        _enteringElapsed += Game.FrameTime;
                        if (_enteringElapsed >= ReenterRetrySeconds)
                        {
                            _partner.Tasks.EnterVehicle(vehicle, -1, 0);
                            _enteringElapsed = 0f;
                        }
                    }
                }
                else
                {
                    // Player on foot - partner out and following.
                    if (_partner.CurrentVehicle != null)
                    {
                        _partner.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
                        _mode = ModeNone;
                    }
                    else if (_mode != ModeFollowing)
                    {
                        IssueFollow();
                        _mode = ModeFollowing;
                    }
                }
            }, "PartnerManager.Tick");
        }

        private static void IssueFollow()
        {
            Safe.Run(() =>
            {
                Ped player = Game.LocalPlayer.Character;
                // Persistent "follow just behind the leader" task (issued once).
                NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(
                    _partner, player, 0f, -1.5f, 0f, 2.0f, -1, 2.0f, true);
            }, "PartnerManager.IssueFollow");
        }
    }
}
