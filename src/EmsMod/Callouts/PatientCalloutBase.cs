using System;
using System.Drawing;
using Rage;
using Rage.Native;
using EmsMod.Config;
using EmsMod.Config.Schema;
using EmsMod.Core;
using EmsMod.Dialogue;
using EmsMod.Input;
using EmsMod.Utils;

namespace EmsMod.Callouts
{
    /// <summary>
    /// Shared flow for any "single patient with a minor injury" callout:
    /// dispatch popup + voice, drive to a map blip, on-foot arrival, spawn a
    /// waiting patient (+ optional scene vehicle), single-tap "help" with a
    /// short pause-safe treatment beat, then a config-driven transport roll and
    /// a positive resolution either way.
    ///
    /// Everything is driven by the callout's <see cref="CalloutConfig"/>, so a
    /// concrete callout is just a config file name - see CarAccident_Bleeding
    /// and BikeAccident_ScrapedKnee, which are only a couple of lines each. This
    /// is the Phase 5 "no duplicated logic" audit made concrete: new patient
    /// callouts are content (XML) + a thin subclass, not copied code.
    /// </summary>
    public abstract class PatientCalloutBase : CalloutBase
    {
        private const int SeverityMinor = 0;
        private const int SeverityModerate = 1;
        private const int SeveritySevere = 2;

        private static readonly Random Rng = new Random();

        private readonly CalloutConfig _config;
        private readonly Injury _injury;
        private readonly int _severity;

        private Vector3 _scenePosition;
        private float _sceneHeading;
        private Blip _sceneBlip;
        private Ped _patient;
        private Vehicle _sceneVehicle;

        private bool _isTreating;
        private float _treatmentElapsed;
        private bool _transported;
        private float _resolutionElapsed;

        private int _conversationIndex;
        private bool _currentLineShown;

        private Vehicle _ambulance;
        private Ped _paramedic;
        private Vector3 _ambulanceStaging;
        private int _transportPhase;
        private float _transportPhaseElapsed;
        private bool _phaseIssued;

        protected PatientCalloutBase()
        {
            // ConfigFileName is a constant per subclass, so virtual dispatch
            // from the base constructor is safe here.
            _config = ConfigLoader.LoadCallout(ConfigFileName);

            // Pick one injury from the callout's pool; its fixed severity keeps
            // the pose and description consistent. Fall back to the single
            // Severity value if no injuries are defined.
            if (_config.Injuries != null && _config.Injuries.Count > 0)
            {
                _injury = _config.Injuries[Rng.Next(_config.Injuries.Count)];
                _severity = ParseSeverity(_injury.Severity);
            }
            else
            {
                _severity = ParseSeverity(_config.Severity);
            }

            Log.Info($"{GetType().Name}: injury severity = {SeverityName(_severity)}.");
        }

        // --- Injury-or-callout text accessors (injury value wins if set) ---
        private static string Pick(string injuryValue, string fallback) =>
            string.IsNullOrWhiteSpace(injuryValue) ? fallback : injuryValue;

        private string DispatchTextValue => Pick(_injury?.DispatchText, _config.DispatchText);
        private string DispatchVoiceValue => Pick(_injury?.DispatchVoiceLine, _config.DispatchVoiceLine);
        private string OnSceneTextValue => Pick(_injury?.OnSceneText, _config.OnSceneText);
        private string OnSceneVoiceValue => Pick(_injury?.OnSceneVoiceLine, _config.OnSceneVoiceLine);
        private string TreatingTextValue => Pick(_injury?.TreatingText, _config.TreatingText);
        private string TreatedTextValue => Pick(_injury?.TreatedText, _config.TreatedText);
        private string TransportedTextValue => Pick(_injury?.TransportedText, _config.TransportedText);
        private string ResolutionVoiceValue => Pick(_injury?.ResolutionVoiceLine, _config.ResolutionVoiceLine);
        private string PatientThanksTextValue => Pick(_injury?.PatientThanksText, _config.PatientThanksText);

        private System.Collections.Generic.List<DialogueLine> AssessmentLinesValue =>
            (_injury != null && _injury.AssessmentLines != null && _injury.AssessmentLines.Count > 0)
                ? _injury.AssessmentLines
                : _config.AssessmentLines;

        // Injury severity drives the patient's pose and whether they need
        // transport. "Random" (or anything unrecognised) rolls one per dispatch
        // so a callout still varies run-to-run until the diagnosis wheel sets it.
        private static int ParseSeverity(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                switch (value.Trim().ToLowerInvariant())
                {
                    case "minor": return SeverityMinor;
                    case "moderate": return SeverityModerate;
                    case "severe": return SeveritySevere;
                }
            }

            return Rng.Next(3);
        }

        private static string SeverityName(int severity)
        {
            switch (severity)
            {
                case SeveritySevere: return "Severe";
                case SeverityModerate: return "Moderate";
                default: return "Minor";
            }
        }

        /// <summary>The per-callout XML under Config/Callouts/ this callout loads.</summary>
        protected abstract string ConfigFileName { get; }

        /// <summary>Exposed so subclasses can read/override tuned values if needed.</summary>
        protected CalloutConfig Config => _config;

        protected override string DispatchMessage => DispatchTextValue;

        protected override void OnDispatched()
        {
            DialogueEngine.Speak(DispatchVoiceValue);
            Safe.Run(() => Game.DisplayNotification(DispatchTextValue), $"{GetType().Name}.OnDispatched notification");
        }

        protected override void OnEnRoute()
        {
            DialogueEngine.Speak(_config.EnRouteVoiceLine);

            Ped player = Game.LocalPlayer.Character;
            float spawnDistance = RandomSpawnDistance();
            Log.Info($"{GetType().Name}: scene distance {spawnDistance:F0}m.");
            Vector3 ahead = player.Position + player.ForwardVector * spawnDistance;

            // Land the scene on a proper drivable road node (with the road's
            // heading) so it's accessible and aligned to the road, not shoved
            // onto a lawn by a blind compass offset.
            ResolveSceneRoad(ahead);

            _sceneBlip = CreateBlip(_scenePosition, Color.Yellow, "EMS Call");
            if (_sceneBlip != null)
            {
                // GPS route to the scene - a clear, kid-friendly "go here" line.
                Safe.Run(() => _sceneBlip.IsRouteEnabled = true, $"{GetType().Name}.blip route");
            }

            // Spawn the scene NOW, with the blip, so the player sees the patient
            // and car in the distance as they approach - not popping in on top of
            // them at arrival.
            SpawnScene();
        }

        private void SpawnScene()
        {
            // Car sits on the road aligned to it; patient stands just off to the
            // side (along the road's right vector), facing the car - so nothing
            // lands in a yard from a blind offset.
            Vector3 right = RightVector(_sceneHeading);
            Vector3 patientPos = _scenePosition + right * 3f;

            _patient = SpawnPed(patientPos, HeadingToward(patientPos, _scenePosition));
            StartPatientPose();

            if (!string.IsNullOrWhiteSpace(_config.SceneVehicleModel))
            {
                _sceneVehicle = SpawnVehicle(_config.SceneVehicleModel, _scenePosition, _sceneHeading);

                if (_sceneVehicle != null && _config.DamageSceneVehicle)
                {
                    ApplyCrashedLook(_sceneVehicle);
                }
            }

            SpawnSceneFire();
        }

        /// <summary>Optional fire flavour for firefighter callouts: sets the
        /// scene vehicle alight and/or spawns a burning prop, and makes the
        /// player + patient fireproof so it stays kid-safe (no fail state).</summary>
        private void SpawnSceneFire()
        {
            if (string.IsNullOrWhiteSpace(_config.SceneFireProp) && !_config.BurnSceneVehicle)
            {
                return;
            }

            Safe.Run(() =>
            {
                Ped player = Game.LocalPlayer.Character;
                NativeFunction.Natives.SET_ENTITY_PROOFS(player, false, true, true, false, false, false, false, false);
                if (_patient != null && _patient.Exists())
                {
                    _patient.IsInvincible = true;
                }

                EntitySpawnRegistry.RegisterCleanupAction(InstanceId, () => Safe.Run(
                    () => NativeFunction.Natives.SET_ENTITY_PROOFS(Game.LocalPlayer.Character, false, false, false, false, false, false, false, false),
                    $"{GetType().Name}.revert proofs"));

                if (_config.BurnSceneVehicle && _sceneVehicle != null && _sceneVehicle.Exists())
                {
                    NativeFunction.Natives.START_ENTITY_FIRE(_sceneVehicle);
                }

                if (!string.IsNullOrWhiteSpace(_config.SceneFireProp))
                {
                    Vector3 firePos = _scenePosition - RightVector(_sceneHeading) * 3f;
                    var model = new Model(_config.SceneFireProp);
                    model.LoadAndWait();
                    var prop = new Rage.Object(model, firePos);
                    model.Dismiss();
                    if (prop != null && prop.Exists())
                    {
                        EntitySpawnRegistry.RegisterEntity(InstanceId, prop);
                        NativeFunction.Natives.START_ENTITY_FIRE(prop);
                    }
                }
            }, $"{GetType().Name}.SpawnSceneFire");
        }

        protected override bool IsArrivalComplete()
        {
            Safe.Run(() => Game.DisplayHelp(_config.EnRouteHelpText), $"{GetType().Name}.EnRoute help");

            Ped player = Game.LocalPlayer.Character;
            float distance = _scenePosition.DistanceTo(player.Position);

            // On-foot requirement: arriving still inside a vehicle would trip the
            // vehicle-entry watchdog and instantly abandon the callout.
            bool onFoot = player.CurrentVehicle == null;
            return onFoot && distance <= _config.ArrivalRadius;
        }

        protected override void OnSceneArrived()
        {
            // Scene (patient + car) was already spawned at en-route time; arriving
            // just drops the navigation blip and starts the on-scene interaction.
            if (_sceneBlip != null)
            {
                Safe.Run(() =>
                {
                    if (_sceneBlip.IsValid())
                    {
                        _sceneBlip.Delete();
                    }
                }, $"{GetType().Name}.delete scene blip");
            }

            // Re-apply the pose now that the player has arrived and the patient
            // is definitely streamed in - a pose set at far-away spawn time
            // often doesn't take until the ped is loaded.
            if (_patient != null && _patient.Exists())
            {
                StartPatientPose();
            }

            DialogueEngine.Speak(OnSceneVoiceValue);
            Safe.Run(() => Game.DisplaySubtitle(OnSceneTextValue, 5000), $"{GetType().Name}.OnScene subtitle");

            AdvanceToAssessment();
        }

        protected override bool IsAssessmentComplete()
        {
            // Phase 1: the injury conversation - one line per single tap. The
            // patient says how they're doing, the player reassures. Pure flavor,
            // no right/wrong answers.
            var assessmentLines = AssessmentLinesValue;
            if (assessmentLines != null && _conversationIndex < assessmentLines.Count)
            {
                if (!_currentLineShown)
                {
                    SpeakLine(assessmentLines[_conversationIndex]);
                    _currentLineShown = true;
                }

                Safe.Run(() => Game.DisplayHelp(_config.ConversationHelpText), $"{GetType().Name}.conversation help");

                if (InputManager.IsActionPressed(InputAction.Interact))
                {
                    _conversationIndex++;
                    _currentLineShown = false;
                }

                return false;
            }

            // Phase 2: single-tap to help, then a short pause-safe bandage beat.
            if (!_isTreating)
            {
                Safe.Run(() => Game.DisplayHelp(_config.AssessmentHelpText), $"{GetType().Name}.Assessment help");

                if (InputManager.IsActionPressed(InputAction.Interact))
                {
                    _isTreating = true;
                    _treatmentElapsed = 0f;
                    Safe.Run(
                        () => Game.DisplaySubtitle(TreatingTextValue, (int)(_config.TreatmentSeconds * 1000f)),
                        $"{GetType().Name}.treating subtitle");
                    StartTreatmentAnimation();
                }

                return false;
            }

            if (AdvanceTimer(ref _treatmentElapsed, _config.TreatmentSeconds))
            {
                // Stand back up before resolving.
                Safe.Run(() => Game.LocalPlayer.Character.Tasks.Clear(), $"{GetType().Name}.clear treatment anim");
                return true;
            }

            return false;
        }

        protected override void OnResolved()
        {
            // Outcome is now driven by the injury severity (deterministic), not a
            // random roll: a severe injury needs the hospital, lighter ones are
            // treated on scene. (The diagnosis wheel will set severity later.)
            _transported = _severity >= SeveritySevere;
            _resolutionElapsed = 0f;

            // Helping is always a win - both outcomes count, no penalties.
            SessionStats.RecordPatientHelped();

            string message = _transported ? TransportedTextValue : TreatedTextValue;
            Safe.Run(() => Game.DisplayNotification(message), $"{GetType().Name}.resolution notification");
            DialogueEngine.Speak(ResolutionVoiceValue);

            if (_config.PlaySuccessSound)
            {
                PlaySuccessSound();
            }

            if (_config.ShowPatientsHelpedCount)
            {
                Safe.Run(
                    () => Game.DisplayNotification($"Patients helped today: {SessionStats.PatientsHelped}"),
                    $"{GetType().Name}.patients-helped count");
            }

            if (_transported && _config.ShowAmbulanceOnTransport)
            {
                StartAmbulanceTransport();
            }
            else
            {
                PlayPatientThanks();
            }

            Log.Info($"{GetType().Name} [{InstanceId}] resolved: severity={SeverityName(_severity)}, " +
                     $"transported={_transported}. PatientsHelped={SessionStats.PatientsHelped}.");
        }

        protected override bool IsResolutionComplete()
        {
            if (_transported && _config.ShowAmbulanceOnTransport && _ambulance != null && _ambulance.Exists())
            {
                // Overall safety cap so the sequence can never hang the callout.
                if (AdvanceTimer(ref _resolutionElapsed, _config.TransportSequenceSeconds))
                {
                    return true;
                }

                return TickTransport();
            }

            // Treated on scene: just hold on the thank-you beat.
            return AdvanceTimer(ref _resolutionElapsed, _config.ResolutionHoldSeconds);
        }

        private void GoToTransportPhase(int phase)
        {
            _transportPhase = phase;
            _transportPhaseElapsed = 0f;
            _phaseIssued = false;
        }

        /// <summary>Drives the transport sequence one frame at a time:
        /// (0) ambulance drives in and stops short, (1) paramedic gets out and
        /// walks to the patient, (2) patient loads into the back while the medic
        /// returns to the driver seat, (3) ambulance drives off. Every phase has
        /// a timeout so it can never hang. Returns true when clear to despawn.</summary>
        private bool TickTransport()
        {
            bool firstTick = !_phaseIssued;
            _phaseIssued = true;

            switch (_transportPhase)
            {
                case 0: // ambulance driving in, stopping short of the scene
                    if (_ambulance.Position.DistanceTo(_ambulanceStaging) <= 8f ||
                        _ambulance.Position.DistanceTo(_scenePosition) <= 16f ||
                        AdvanceTimer(ref _transportPhaseElapsed, 22f))
                    {
                        Safe.Run(() =>
                        {
                            if (_paramedic != null && _paramedic.Exists())
                            {
                                _paramedic.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
                            }
                        }, $"{GetType().Name}.medic leave vehicle");
                        GoToTransportPhase(1);
                    }
                    break;

                case 1: // paramedic out, walking to the patient
                    if (firstTick && _paramedic != null && _paramedic.Exists() && _patient != null && _patient.Exists())
                    {
                        Safe.Run(
                            () => _paramedic.Tasks.FollowNavigationMeshToPosition(_patient.Position, 0f, 1.3f),
                            $"{GetType().Name}.medic walk to patient");
                    }

                    bool medicReached = _paramedic == null || !_paramedic.Exists() || _patient == null || !_patient.Exists() ||
                                        _paramedic.Position.DistanceTo(_patient.Position) <= 3.5f;
                    if (medicReached || AdvanceTimer(ref _transportPhaseElapsed, 12f))
                    {
                        GoToTransportPhase(2);
                    }
                    break;

                case 2: // load the patient; medic returns to the driver seat
                    if (firstTick)
                    {
                        if (_patient != null && _patient.Exists())
                        {
                            Safe.Run(() =>
                            {
                                _patient.Tasks.ClearImmediately();
                                _patient.Tasks.EnterVehicle(_ambulance, -1, 1);
                            }, $"{GetType().Name}.patient enter ambulance");
                        }

                        if (_paramedic != null && _paramedic.Exists())
                        {
                            Safe.Run(
                                () => _paramedic.Tasks.EnterVehicle(_ambulance, -1, -1),
                                $"{GetType().Name}.medic re-enter");
                        }
                    }

                    bool patientLoaded = _patient == null || !_patient.Exists() || _patient.IsInAnyVehicle(false);
                    if (patientLoaded || AdvanceTimer(ref _transportPhaseElapsed, 14f))
                    {
                        GoToTransportPhase(3);
                    }
                    break;

                case 3: // ambulance drives away
                    if (firstTick && _paramedic != null && _paramedic.Exists())
                    {
                        Safe.Run(() =>
                        {
                            Vector3 awayRaw = _scenePosition + DirNormalized(_scenePosition - Game.LocalPlayer.Character.Position) * 140f;
                            Vector3 away = Safe.Run(() => World.GetNextPositionOnStreet(awayRaw), awayRaw, $"{GetType().Name}.transport exit point");
                            _paramedic.Tasks.DriveToPosition(away, 18f, VehicleDrivingFlags.Normal, 5f);
                        }, $"{GetType().Name}.ambulance drive off");
                    }

                    if (_ambulance.Position.DistanceTo(_scenePosition) >= 80f ||
                        AdvanceTimer(ref _transportPhaseElapsed, 14f))
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }

        protected override void OnTick()
        {
            if (!_config.ShowSceneMarker)
            {
                return;
            }

            // Show the ground marker while heading to the scene and until the
            // patient has been treated (i.e. up to and including Assessment).
            if (State != CalloutState.EnRoute && State != CalloutState.OnScene && State != CalloutState.Assessment)
            {
                return;
            }

            Safe.Run(() =>
                NativeFunction.Natives.DRAW_MARKER(
                    1,
                    _scenePosition.X, _scenePosition.Y, _scenePosition.Z - 1f,
                    0f, 0f, 0f,
                    0f, 0f, 0f,
                    1.5f, 1.5f, 1f,
                    255, 220, 0, 120,
                    false, false, 2, false,
                    0, 0, false),
                $"{GetType().Name}.draw scene marker");
        }

        private void StartPatientPose()
        {
            if (_patient == null)
            {
                return;
            }

            // Clear the StandStill task SpawnPed applied so the pose can take.
            Safe.Run(() => _patient.Tasks.ClearImmediately(), $"{GetType().Name}.clear for pose");

            // 1a) Explicit per-injury pose clip wins if set and valid.
            if (_injury != null &&
                !string.IsNullOrWhiteSpace(_injury.PoseAnimDictionary) &&
                !string.IsNullOrWhiteSpace(_injury.PoseAnimName) &&
                PlayLoopedAnimSafe(_patient, _injury.PoseAnimDictionary, _injury.PoseAnimName, $"{GetType().Name}.injury pose anim"))
            {
                return;
            }

            // 1b) Explicit per-callout pose clip.
            bool hasPoseAnim = !string.IsNullOrWhiteSpace(_config.PatientPoseAnimDictionary) &&
                               !string.IsNullOrWhiteSpace(_config.PatientPoseAnimName);
            if (hasPoseAnim &&
                PlayLoopedAnimSafe(_patient, _config.PatientPoseAnimDictionary, _config.PatientPoseAnimName, $"{GetType().Name}.patient pose anim"))
            {
                return;
            }

            // 2) Pose by injury severity: worse hurt = lower to the ground.
            //    Moderate uses the same medic-kneel dict the player treat pose
            //    uses (confirmed to load in-game). Severe lies down.
            if (_severity == SeveritySevere &&
                PlayLoopedAnimSafe(_patient, "amb@world_human_sunbathe@male@back@base", "base", $"{GetType().Name}.severe pose"))
            {
                return;
            }

            if (_severity == SeverityModerate &&
                PlayLoopedAnimSafe(_patient, "amb@medic@standing@kneel@base", "base", $"{GetType().Name}.moderate pose"))
            {
                return;
            }

            // 3) Minor (or any pose clip that failed to load): a standing "waiting
            //    for help" scenario.
            if (!string.IsNullOrWhiteSpace(_config.PatientScenario))
            {
                Safe.Run(
                    () => NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(_patient, _config.PatientScenario, 0, true),
                    $"{GetType().Name}.patient scenario");
            }
        }

        private void StartTreatmentAnimation()
        {
            PlayLoopedAnimSafe(
                Game.LocalPlayer.Character,
                _config.TreatmentAnimDictionary,
                _config.TreatmentAnimName,
                $"{GetType().Name}.treatment animation");
        }

        /// <summary>Plays a looped animation, but ONLY after confirming the anim
        /// dictionary exists and loads within a timeout. A missing/invalid dict
        /// would otherwise make RPH's PlayAnimation stall waiting on a load that
        /// never completes, so guarding it keeps a bad clip name harmless (the
        /// ped just keeps its previous pose).</summary>
        private bool PlayLoopedAnimSafe(Ped ped, string dictionary, string name, string context)
        {
            if (ped == null || !ped.Exists() ||
                string.IsNullOrWhiteSpace(dictionary) || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return Safe.Run(() =>
            {
                // Use the typed generic form so RPH marshals the native's return
                // as a real bool. The plain dynamic form could throw converting
                // the native's numeric return to bool, which would silently skip
                // EVERY animation (the likely cause of the pose/kneel not playing).
                bool exists = NativeFunction.Natives.DOES_ANIM_DICT_EXIST<bool>(dictionary);
                if (!exists)
                {
                    Log.Warn($"{context}: animation dictionary '{dictionary}' does not exist; skipping.");
                    return false;
                }

                var animDict = new AnimationDictionary(dictionary);
                animDict.LoadAndWait();
                ped.Tasks.PlayAnimation(animDict, name, 4f, AnimationFlags.Loop);
                Log.Info($"{context}: playing '{dictionary}' / '{name}'.");
                return true;
            }, false, context);
        }

        private void ApplyCrashedLook(Vehicle vehicle)
        {
            Safe.Run(() =>
            {
                NativeFunction.Natives.SET_VEHICLE_BODY_HEALTH(vehicle, 400f);
                NativeFunction.Natives.SET_VEHICLE_ENGINE_HEALTH(vehicle, 300f);
                // Dent the front end. Kid-friendly - a crashed look, no fire.
                NativeFunction.Natives.SET_VEHICLE_DAMAGE(vehicle, 0f, 2.5f, 0.5f, 900f, 2f, true);
                vehicle.IsEngineOn = false;
            }, $"{GetType().Name}.crashed look");
        }

        private void SpeakLine(DialogueLine line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Text))
            {
                return;
            }

            DialogueEngine.Speak(line.Text);
            string subtitle = string.IsNullOrWhiteSpace(line.Speaker) ? line.Text : $"{line.Speaker}: {line.Text}";
            Safe.Run(() => Game.DisplaySubtitle(subtitle, 4000), $"{GetType().Name}.conversation line");
        }

        private void PlaySuccessSound()
        {
            if (string.IsNullOrWhiteSpace(_config.SuccessSoundName))
            {
                return;
            }

            Safe.Run(
                () => NativeFunction.Natives.PLAY_SOUND_FRONTEND(-1, _config.SuccessSoundName, _config.SuccessSoundSet ?? "", true),
                $"{GetType().Name}.success sound");
        }

        private void PlayPatientThanks()
        {
            if (_patient != null && _patient.Exists())
            {
                float faceHeading = HeadingToward(_patient.Position, Game.LocalPlayer.Character.Position);
                Safe.Run(() =>
                {
                    _patient.Tasks.Clear();
                    _patient.Heading = faceHeading;
                    if (!string.IsNullOrWhiteSpace(_config.PatientThanksScenario))
                    {
                        NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(_patient, _config.PatientThanksScenario, 0, true);
                    }
                }, $"{GetType().Name}.patient thanks reaction");
            }

            if (!string.IsNullOrWhiteSpace(PatientThanksTextValue))
            {
                Safe.Run(() => Game.DisplaySubtitle(PatientThanksTextValue, 4000), $"{GetType().Name}.patient thanks subtitle");
            }
        }

        /// <summary>Transport outcome: an ambulance drives in with a paramedic,
        /// the patient walks over and loads up, then it drives away. Best-effort
        /// AI sequence - if the ambulance fails to spawn we fall back to the
        /// on-scene thank-you so the callout still resolves cleanly.</summary>
        private void StartAmbulanceTransport()
        {
            Ped player = Game.LocalPlayer.Character;

            // Spawn the ambulance back down the road (away from the player's side)
            // so it visibly drives in to the scene.
            Vector3 awayDir = DirNormalized(_scenePosition - player.Position);
            Vector3 rawSpawn = _scenePosition + awayDir * 45f;
            Vector3 spawnPoint = Safe.Run(
                () => World.GetNextPositionOnStreet(rawSpawn),
                rawSpawn,
                $"{GetType().Name}.ambulance spawn point");

            float ambulanceHeading = HeadingToward(spawnPoint, _scenePosition);
            _ambulance = SpawnVehicle(_config.AmbulanceModel, spawnPoint, ambulanceHeading);

            if (_ambulance == null)
            {
                // No ambulance - still give a satisfying resolution.
                PlayPatientThanks();
                return;
            }

            Safe.Run(() => _ambulance.IsSirenOn = true, $"{GetType().Name}.ambulance siren");

            // Stop a bit short of the scene so the ambulance never drives into
            // the patient/player huddle.
            Vector3 towardScene = DirNormalized(_scenePosition - spawnPoint);
            _ambulanceStaging = _scenePosition - towardScene * 10f;

            if (!string.IsNullOrWhiteSpace(_config.ParamedicModel))
            {
                _paramedic = SpawnPed(spawnPoint + new Vector3(2f, 0f, 0f));
                if (_paramedic != null)
                {
                    Safe.Run(() =>
                    {
                        _paramedic.WarpIntoVehicle(_ambulance, -1);
                        // Normal (not Emergency) driving obeys traffic and stops
                        // for peds, so it doesn't plough through the scene.
                        _paramedic.Tasks.DriveToPosition(_ambulanceStaging, 15f, VehicleDrivingFlags.Normal, 4f);
                    }, $"{GetType().Name}.paramedic drive-in");
                }
            }

            // The paramedic then gets out, loads the patient, and drives off -
            // handled phase by phase in TickTransport once the ambulance arrives.
            _transportPhase = 0;
            _transportPhaseElapsed = 0f;
            _phaseIssued = false;
        }

        /// <summary>Sets _scenePosition and _sceneHeading to a proper drivable
        /// road node near <paramref name="near"/> (with the road's heading) so
        /// scenes land on a reachable road, aligned to it. Falls back to the
        /// street-position helper, then to the raw point.</summary>
        private void ResolveSceneRoad(Vector3 near)
        {
            bool found = false;

            Safe.Run(() =>
            {
                Vector3 pos;
                float heading;
                found = NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(
                    near.X, near.Y, near.Z, out pos, out heading, 1, 3.0f, 0);
                if (found)
                {
                    _scenePosition = pos;
                    _sceneHeading = heading;
                }
            }, $"{GetType().Name}.closest vehicle node w/ heading");

            if (found)
            {
                return;
            }

            _scenePosition = Safe.Run(
                () => World.GetNextPositionOnStreet(near),
                near,
                $"{GetType().Name}.next position on street");
            _sceneHeading = 0f;
        }

        /// <summary>A random scene distance (meters) for this dispatch, so
        /// callouts vary from a short drive to a proper drive.</summary>
        private float RandomSpawnDistance()
        {
            float min = _config.SceneSpawnDistanceMin;
            float max = _config.SceneSpawnDistanceMax;
            if (max < min)
            {
                float tmp = min;
                min = max;
                max = tmp;
            }
            return min + (float)Rng.NextDouble() * (max - min);
        }

        /// <summary>Unit vector pointing to the right of a GTA heading (degrees).</summary>
        private static Vector3 RightVector(float headingDegrees)
        {
            double rad = headingDegrees * Math.PI / 180.0;
            return new Vector3((float)Math.Cos(rad), (float)Math.Sin(rad), 0f);
        }

        private static Vector3 DirNormalized(Vector3 v)
        {
            float length = v.Length();
            if (length <= 0.001f)
            {
                return new Vector3(0f, 1f, 0f);
            }

            return new Vector3(v.X / length, v.Y / length, v.Z / length);
        }

        /// <summary>GTA heading (degrees) that points from <paramref name="from"/>
        /// toward <paramref name="to"/>. Computed directly to avoid depending on a
        /// specific RPH math helper signature.</summary>
        private static float HeadingToward(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float heading = (float)(Math.Atan2(-dir.X, dir.Y) * 180.0 / Math.PI);
            if (heading < 0f)
            {
                heading += 360f;
            }

            return heading;
        }
    }
}
