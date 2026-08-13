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

        private static Ped _partner;
        private static int _mode;

        public static void Spawn(string modelName)
        {
            Despawn();

            Safe.Run(() =>
            {
                Ped player = Game.LocalPlayer.Character;
                Vector3 pos = player.Position - player.RightVector * 2f;

                var model = new Model(modelName);
                model.LoadAndWait();
                _partner = new Ped(model, pos, player.Heading);
                model.Dismiss();

                if (_partner == null || !_partner.Exists())
                {
                    return;
                }

                _partner.BlockPermanentEvents = true;
                _partner.IsInvincible = true;
                EntitySpawnRegistry.RegisterEntity(Owner, _partner);

                _mode = ModeNone;
                IssueFollow();
                _mode = ModeFollowing;
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

        public static void Tick()
        {
            if (_partner == null || !_partner.Exists())
            {
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
