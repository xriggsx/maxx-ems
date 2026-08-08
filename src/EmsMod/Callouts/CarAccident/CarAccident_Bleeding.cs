using System;
using System.Drawing;
using Rage;
using EmsMod.Config;
using EmsMod.Config.Schema;
using EmsMod.Dialogue;
using EmsMod.Input;
using EmsMod.Utils;

namespace EmsMod.Callouts
{
    /// <summary>
    /// First real callout (Phase 4). A minor car accident: dispatch popup +
    /// voice, drive to a map blip, walk up on foot, single-tap to "help"
    /// (put on a bandage), then a config-driven transport roll decides
    /// treated-on-scene vs. transported-to-hospital - both positive, no fail
    /// states. Everything it spawns (patient, scene vehicle, blip) is created
    /// through CalloutBase helpers, so cleanup on every end state is automatic.
    /// All text/timers/percentages come from Config/Callouts/CarAccident_Bleeding.xml.
    /// </summary>
    public class CarAccident_Bleeding : CalloutBase
    {
        private static readonly Random Rng = new Random();

        private readonly CalloutConfig _config;

        private Vector3 _scenePosition;
        private Blip _sceneBlip;
        private Ped _patient;
        private Vehicle _sceneVehicle;

        private bool _isTreating;
        private float _treatmentElapsed;
        private bool _transported;

        public CarAccident_Bleeding()
        {
            _config = ConfigLoader.LoadCallout("CarAccident_Bleeding.xml");
        }

        protected override string DispatchMessage => _config.DispatchText;

        protected override void OnDispatched()
        {
            DialogueEngine.Speak(_config.DispatchVoiceLine);
            Safe.Run(() => Game.DisplayNotification(_config.DispatchText), "CarAccident_Bleeding.OnDispatched notification");
        }

        protected override void OnEnRoute()
        {
            DialogueEngine.Speak(_config.EnRouteVoiceLine);

            Ped player = Game.LocalPlayer.Character;
            Vector3 ahead = player.Position + player.ForwardVector * _config.SceneSpawnDistance;

            // Snap the scene onto a nearby street so the patient/car aren't
            // placed in a building or the ocean; fall back to the raw point.
            _scenePosition = Safe.Run(
                () => World.GetNextPositionOnStreet(ahead),
                ahead,
                "CarAccident_Bleeding.GetNextPositionOnStreet");

            _sceneBlip = CreateBlip(_scenePosition, Color.Yellow, "EMS Call");
        }

        protected override bool IsArrivalComplete()
        {
            // Guide the player every frame while en route (the help box
            // re-arms each tick so it stays visible).
            Safe.Run(() => Game.DisplayHelp(_config.EnRouteHelpText), "CarAccident_Bleeding.EnRoute help");

            Ped player = Game.LocalPlayer.Character;
            float distance = _scenePosition.DistanceTo(player.Position);

            // On-foot requirement: if we advanced to OnScene while the player
            // was still driving, the vehicle-entry watchdog would instantly
            // abandon the callout. Requiring on-foot arrival avoids that.
            bool onFoot = player.CurrentVehicle == null;
            return onFoot && distance <= _config.ArrivalRadius;
        }

        protected override void OnSceneArrived()
        {
            // Arrived - drop the navigation blip.
            if (_sceneBlip != null)
            {
                Safe.Run(() =>
                {
                    if (_sceneBlip.IsValid())
                    {
                        _sceneBlip.Delete();
                    }
                }, "CarAccident_Bleeding.delete scene blip");
            }

            _patient = SpawnPed(_scenePosition);

            if (!string.IsNullOrWhiteSpace(_config.SceneVehicleModel))
            {
                _sceneVehicle = SpawnVehicle(_config.SceneVehicleModel, _scenePosition + new Vector3(3f, 0f, 0f));
            }

            DialogueEngine.Speak(_config.OnSceneVoiceLine);
            Safe.Run(() => Game.DisplaySubtitle(_config.OnSceneText, 5000), "CarAccident_Bleeding.OnScene subtitle");

            AdvanceToAssessment();
        }

        protected override bool IsAssessmentComplete()
        {
            if (!_isTreating)
            {
                Safe.Run(() => Game.DisplayHelp(_config.AssessmentHelpText), "CarAccident_Bleeding.Assessment help");

                if (InputManager.IsActionPressed(InputAction.Interact))
                {
                    _isTreating = true;
                    _treatmentElapsed = 0f;
                    Safe.Run(
                        () => Game.DisplaySubtitle(_config.TreatingText, (int)(_config.TreatmentSeconds * 1000f)),
                        "CarAccident_Bleeding.treating subtitle");
                }

                return false;
            }

            // Short, pause-safe "bandaging" beat, then the assessment is done.
            return AdvanceTimer(ref _treatmentElapsed, _config.TreatmentSeconds);
        }

        protected override void OnResolved()
        {
            // Single shared resolution path - a 5% and a 95% callout run this
            // exact code, only the config percentage differs.
            _transported = Rng.Next(100) < _config.TransportPercentage;

            string message = _transported ? _config.TransportedText : _config.TreatedText;
            Safe.Run(() => Game.DisplayNotification(message), "CarAccident_Bleeding.resolution notification");
            DialogueEngine.Speak(_config.ResolutionVoiceLine);

            Log.Info($"CarAccident_Bleeding [{InstanceId}] resolved: transported={_transported} " +
                     $"(rolled vs TransportPercentage={_config.TransportPercentage}).");
        }
    }
}
