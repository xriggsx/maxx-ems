using System;
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
        }

        protected virtual void OnAccepted()
        {
        }

        protected virtual void OnSceneArrived()
        {
        }

        protected virtual bool IsAssessmentComplete()
        {
            return false;
        }

        protected virtual void OnResolved()
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
                    // No travel/arrival tracking yet - a future callout that
                    // needs one can hold this state until its own condition
                    // is met instead of relying on this immediate pass-through.
                    SetState(CalloutState.OnScene);
                    break;

                case CalloutState.Assessment:
                    if (IsAssessmentComplete())
                    {
                        SetState(CalloutState.Resolution);
                    }
                    break;

                case CalloutState.Resolution:
                    OnResolved();
                    SetState(CalloutState.CleaningUp);
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
                case CalloutState.Declined:
                case CalloutState.Abandoned:
                    PromptUI.Hide();
                    break;

                case CalloutState.OnScene:
                    _sceneAnchor = Game.LocalPlayer.Character.Position;
                    _walkedAwayElapsed = 0f;
                    OnSceneArrived();
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
