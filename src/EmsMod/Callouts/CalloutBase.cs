using System;
using System.Drawing;
using Rage;
using EmsMod.Config;
using EmsMod.Core;
using EmsMod.Input;
using EmsMod.UI;
using EmsMod.Utils;

namespace EmsMod.Callouts
{
    /// <summary>
    /// Owns every state transition, watchdog, and cleanup call so concrete
    /// callouts only ever implement content hooks. Every callout - including
    /// ones written long after this class - gets vehicle-entry/walked-away/
    /// pause-safe-timer handling and guaranteed cleanup for free.
    /// </summary>
    public abstract class CalloutBase
    {
        private const float WalkedAwayCheckIntervalSeconds = 1f;

        private float _acceptDeclineElapsed;
        private float _walkedAwayElapsed;
        private Vector3 _sceneAnchor;

        protected CalloutBase()
        {
            InstanceId = Guid.NewGuid().ToString("N");
        }

        public string InstanceId { get; }

        public CalloutState State { get; private set; }

        public bool IsFinished { get; private set; }

        protected abstract string DispatchMessage { get; }

        public void Start()
        {
            SetState(CalloutState.Dispatched);
            SetState(CalloutState.AwaitingAcceptDecline);
            Safe.Run(OnDispatched, $"CalloutBase.OnDispatched [{GetType().Name}]");
        }

        public void Tick()
        {
            Safe.Run(RunWatchdogs, $"CalloutBase.RunWatchdogs [{GetType().Name}]");

            Safe.Run(() =>
            {
                CalloutState before;
                do
                {
                    before = State;
                    TickCurrentState();
                }
                while (State != before && State != CalloutState.CleanedUp);
            }, $"CalloutBase.TickCurrentState [{GetType().Name}]");

            // Per-frame content hook (e.g. drawing a scene marker). Runs after
            // the state machine so State reflects this frame.
            Safe.Run(OnTick, $"CalloutBase.OnTick [{GetType().Name}]");
        }

        /// <summary>Runs once when the callout is dispatched (popup shown).
        /// Good place for a dispatch voice line / notification.</summary>
        protected virtual void OnDispatched()
        {
        }

        protected virtual void OnAccepted()
        {
        }

        /// <summary>Runs once when the player accepts and the callout enters
        /// EnRoute - set up the scene position, a map blip, GPS help text, etc.
        /// here.</summary>
        protected virtual void OnEnRoute()
        {
        }

        /// <summary>Ticked every frame while EnRoute; the callout stays EnRoute
        /// until this returns true, then advances to OnScene. Default is an
        /// immediate arrival (matches the original pass-through behavior).</summary>
        protected virtual bool IsArrivalComplete()
        {
            return true;
        }

        protected virtual void OnSceneArrived()
        {
        }

        protected virtual bool IsAssessmentComplete()
        {
            return false;
        }

        /// <summary>Runs once when the callout enters Resolution (transport roll,
        /// resolution message/voice, any "thank you" reaction setup).</summary>
        protected virtual void OnResolved()
        {
        }

        /// <summary>Ticked every frame while in Resolution; cleanup is held until
        /// this returns true. Default is immediate (original behavior); override
        /// to hold briefly so a resolution reaction is visible before despawn.</summary>
        protected virtual bool IsResolutionComplete()
        {
            return true;
        }

        /// <summary>Ticked every frame the callout is active, regardless of state.
        /// For per-frame drawing (markers) and similar. Default does nothing.</summary>
        protected virtual void OnTick()
        {
        }

        protected void AdvanceToAssessment()
        {
            SetState(CalloutState.Assessment);
        }

        protected void AdvanceToResolution()
        {
            SetState(CalloutState.Resolution);
        }

        protected Ped SpawnPed(Vector3 position, float heading = 0f)
        {
            var ped = new Ped(position, heading)
            {
                BlockPermanentEvents = true
            };
            ped.Tasks.ClearImmediately();
            ped.Tasks.StandStill(-1);

            EntitySpawnRegistry.RegisterEntity(InstanceId, ped);
            return ped;
        }

        /// <summary>Spawns a vehicle registered for automatic cleanup. Returns
        /// null (logged) if the model fails to load, so a bad model name can
        /// never abort the rest of the callout setup.</summary>
        protected Vehicle SpawnVehicle(string model, Vector3 position, float heading = 0f)
        {
            return Safe.Run(() =>
            {
                var vehicle = new Vehicle(model, position, heading);
                EntitySpawnRegistry.RegisterEntity(InstanceId, vehicle);
                return vehicle;
            }, null, $"CalloutBase.SpawnVehicle({model}) [{GetType().Name}]");
        }

        /// <summary>Creates a map blip registered for automatic cleanup. Blip
        /// isn't an Entity, so it's tracked via a cleanup action instead.</summary>
        protected Blip CreateBlip(Vector3 position, Color color, string name = null)
        {
            return Safe.Run(() =>
            {
                var blip = new Blip(position) { Color = color };
                if (!string.IsNullOrEmpty(name))
                {
                    blip.Name = name;
                }

                EntitySpawnRegistry.RegisterCleanupAction(InstanceId, () =>
                {
                    if (blip != null && blip.IsValid())
                    {
                        blip.Delete();
                    }
                });
                return blip;
            }, null, $"CalloutBase.CreateBlip [{GetType().Name}]");
        }

        protected static bool AdvanceTimer(ref float elapsedSeconds, float thresholdSeconds)
        {
            if (!Game.IsPaused)
            {
                elapsedSeconds += Game.FrameTime;
            }

            return elapsedSeconds >= thresholdSeconds;
        }

        private void TickCurrentState()
        {
            switch (State)
            {
                case CalloutState.AwaitingAcceptDecline:
                    if (InputManager.IsActionPressed(InputAction.AcceptCallout))
                    {
                        OnAccepted();
                        SetState(CalloutState.EnRoute);
                    }
                    else if (InputManager.IsActionPressed(InputAction.DeclineCallout))
                    {
                        SetState(CalloutState.Declined);
                    }
                    else if (AdvanceTimer(ref _acceptDeclineElapsed, ConfigLoader.General.AcceptDeclineTimeoutSeconds))
                    {
                        SetState(CalloutState.Declined);
                    }
                    break;

                case CalloutState.EnRoute:
                    // Hold EnRoute until the callout reports arrival. The base
                    // default returns true immediately (original pass-through);
                    // a callout with a scene to travel to overrides
                    // IsArrivalComplete to hold here until the player arrives.
                    if (IsArrivalComplete())
                    {
                        SetState(CalloutState.OnScene);
                    }
                    break;

                case CalloutState.Assessment:
                    if (IsAssessmentComplete())
                    {
                        SetState(CalloutState.Resolution);
                    }
                    break;

                case CalloutState.Resolution:
                    // OnResolved ran on entry (SetState); hold here until the
                    // callout reports the resolution moment is finished.
                    if (IsResolutionComplete())
                    {
                        SetState(CalloutState.CleaningUp);
                    }
                    break;

                case CalloutState.CleaningUp:
                    EntitySpawnRegistry.CleanupOwner(InstanceId);
                    SetState(CalloutState.CleanedUp);
                    break;

                case CalloutState.Declined:
                case CalloutState.Abandoned:
                    EntitySpawnRegistry.CleanupOwner(InstanceId);
                    SetState(CalloutState.CleanedUp);
                    break;
            }
        }

        private void SetState(CalloutState next)
        {
            State = next;
            Log.Info($"{GetType().Name} [{InstanceId}]: -> {next}");

            switch (next)
            {
                case CalloutState.AwaitingAcceptDecline:
                    _acceptDeclineElapsed = 0f;
                    PromptUI.Show(DispatchMessage);
                    break;

                case CalloutState.EnRoute:
                    PromptUI.Hide();
                    Safe.Run(OnEnRoute, $"CalloutBase.OnEnRoute [{GetType().Name}]");
                    break;

                case CalloutState.Declined:
                case CalloutState.Abandoned:
                    PromptUI.Hide();
                    break;

                case CalloutState.OnScene:
                    _sceneAnchor = Game.LocalPlayer.Character.Position;
                    _walkedAwayElapsed = 0f;
                    OnSceneArrived();
                    break;

                case CalloutState.Resolution:
                    Safe.Run(OnResolved, $"CalloutBase.OnResolved [{GetType().Name}]");
                    break;

                case CalloutState.CleanedUp:
                    IsFinished = true;
                    break;
            }
        }

        private void RunWatchdogs()
        {
            if (State != CalloutState.OnScene && State != CalloutState.Assessment)
            {
                return;
            }

            Ped character = Game.LocalPlayer.Character;

            if (character.CurrentVehicle != null)
            {
                Log.Info($"{GetType().Name} [{InstanceId}]: player entered a vehicle mid-interaction, abandoning.");
                SetState(CalloutState.Abandoned);
                return;
            }

            if (AdvanceTimer(ref _walkedAwayElapsed, WalkedAwayCheckIntervalSeconds))
            {
                _walkedAwayElapsed = 0f;
                float distance = _sceneAnchor.DistanceTo(character.Position);
                if (distance > ConfigLoader.General.WalkedAwayDistanceMeters)
                {
                    Log.Info($"{GetType().Name} [{InstanceId}]: player walked away ({distance:F1}m), abandoning.");
                    SetState(CalloutState.Abandoned);
                }
            }
        }
    }
}
