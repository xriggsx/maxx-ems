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

        // One random pick per variant pool, made once at dispatch so it stays
        // consistent for the life of this callout instance.
        private readonly VariantText _onSceneText;
        private readonly VariantText _treatingText;
        private readonly VariantText _patientThanksText;

        /// <summary>Bundles a variant-pool's one-time random pick with how it
        /// resolves against an injury override and the callout-level default,
        /// so each variant-pooled text field only needs one field + one
        /// constructor line + one accessor instead of three.</summary>
        private sealed class VariantText
        {
            private readonly string _picked;

            public VariantText(System.Collections.Generic.List<string> variants)
            {
                _picked = (variants != null && variants.Count > 0) ? variants[Rng.Next(variants.Count)] : null;
            }

            /// <summary>An explicit per-injury override still wins - it's a
            /// deliberate authored choice for that specific injury. Otherwise
            /// the pool pick stands in for the callout-level default.</summary>
            public string Resolve(string injuryValue, string configFallback) =>
                string.IsNullOrWhiteSpace(injuryValue) ? (_picked ?? configFallback) : injuryValue;
        }

        private Vector3 _scenePosition;
        private float _sceneHeading;
        private Blip _sceneBlip;
        private Ped _patient;
        private Vehicle _sceneVehicle;

        private bool _hasMedicBag;
        private bool _bagPickupStarted;
        private float _bagPickupElapsed;
        private bool _isTreating;
        private float _treatmentElapsed;
        private bool _transported;
        private float _resolutionElapsed;

        private int _conversationIndex;
        private bool _currentLineShown;

        private Vehicle _transportVehicle;
        private Blip _hospitalBlip;
        private Hospital _destinationHospital;
        private Rage.Object _stretcherProp;
        private Rage.Object _medicBagProp;
        private int _transportPhase;
        private float _transportPhaseElapsed;
        private bool _phaseIssued;
        private bool _gearSwapStarted;

        // Player-driven transport phases: walk to the ambulance and get the gear,
        // carry it to the patient, load the patient onto the stretcher, carry
        // them back, board them into the ambulance, drive to hospital, drop off.
        // "Request board" and "confirm boarded" are two separate phases (rather
        // than one phase with an extra bookkeeping flag) so each phase does
        // exactly one thing.
        private const int TransportWalkToVehicleForGear = 0;
        private const int TransportWalkToPatientWithGear = 1;
        private const int TransportLoadPatientOntoStretcher = 2;
        private const int TransportCarryToVehicle = 3;
        private const int TransportRequestBoard = 4;
        private const int TransportConfirmBoarded = 5;
        private const int TransportDrivingToHospital = 6;
        private const int TransportDroppingOff = 7;

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

            _onSceneText = new VariantText(_config.OnSceneTextVariants);
            _treatingText = new VariantText(_config.TreatingTextVariants);
            _patientThanksText = new VariantText(_config.PatientThanksTextVariants);

            Log.Info($"{GetType().Name}: injury severity = {SeverityName(_severity)}.");
        }

        // --- Injury-or-callout text accessors (injury value wins if set) ---
        private static string Pick(string injuryValue, string fallback) =>
            string.IsNullOrWhiteSpace(injuryValue) ? fallback : injuryValue;

        private string DispatchTextValue => Pick(_injury?.DispatchText, _config.DispatchText);
        private string DispatchVoiceValue => Pick(_injury?.DispatchVoiceLine, _config.DispatchVoiceLine);
        private string OnSceneTextValue => _onSceneText.Resolve(_injury?.OnSceneText, _config.OnSceneText);
        private string OnSceneVoiceValue => Pick(_injury?.OnSceneVoiceLine, _config.OnSceneVoiceLine);
        private string TreatingTextValue => _treatingText.Resolve(_injury?.TreatingText, _config.TreatingText);
        private string TreatedTextValue => Pick(_injury?.TreatedText, _config.TreatedText);
        private string TransportedTextValue => Pick(_injury?.TransportedText, _config.TransportedText);
        private string ResolutionVoiceValue => Pick(_injury?.ResolutionVoiceLine, _config.ResolutionVoiceLine);
        private string PatientThanksTextValue => _patientThanksText.Resolve(_injury?.PatientThanksText, _config.PatientThanksText);

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
                    // IsInvincible only stops the patient dying - without the
                    // same fire/explosion proofing the player gets, they can
                    // still catch fire and flail/ragdoll near the burning
                    // car/prop, which reads as violent for a kid-friendly mod.
                    NativeFunction.Natives.SET_ENTITY_PROOFS(_patient, false, true, true, false, false, false, false, false);
                }

                EntitySpawnRegistry.RegisterCleanupAction(InstanceId, () => Safe.Run(
                    () =>
                    {
                        NativeFunction.Natives.SET_ENTITY_PROOFS(Game.LocalPlayer.Character, false, false, false, false, false, false, false, false);
                        if (_patient != null && _patient.Exists())
                        {
                            NativeFunction.Natives.SET_ENTITY_PROOFS(_patient, false, false, false, false, false, false, false, false);
                        }
                    },
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
                        prop.IsPersistent = true;
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

            // Send the partner over to help with the patient instead of just
            // trailing the player. ResumeFollowing() is registered as a cleanup
            // action so the partner is guaranteed to snap back to normal
            // following no matter how this callout ends (resolved, abandoned,
            // walked away).
            if (_config.PartnerAssists && _patient != null && _patient.Exists())
            {
                Vector3 partnerSpot = _patient.Position - RightVector(_sceneHeading) * 1.5f;
                PartnerManager.AssistAtScene(partnerSpot, HeadingToward(partnerSpot, _patient.Position));
                EntitySpawnRegistry.RegisterCleanupAction(InstanceId, PartnerManager.ResumeFollowing);
            }

            AdvanceToAssessment();
        }

        protected override bool IsAssessmentComplete()
        {
            // Phase 0: walk to the back of the ambulance and get the medic bag
            // before treating anyone - every patient gets this step, whether
            // they end up needing transport or not. A short pause plays out
            // after the tap (face the vehicle, door open, pause, bag in hand,
            // door shut) instead of everything snapping into place on the same
            // frame, so it reads as an action rather than a jump-cut.
            if (!_hasMedicBag)
            {
                Vehicle vehicle = DutyManager.CurrentVehicle;
                if (vehicle == null || !vehicle.Exists())
                {
                    // No duty vehicle (e.g. dispatched without going on duty) -
                    // skip the bag step rather than block the callout forever.
                    _hasMedicBag = true;
                    return false;
                }

                if (!_bagPickupStarted)
                {
                    Safe.Run(() => Game.DisplayHelp(_config.MedicBagHelpText), $"{GetType().Name}.medic bag help");

                    Ped player = Game.LocalPlayer.Character;
                    if (player.Position.DistanceTo(RearOf(vehicle)) <= 3.5f && InputManager.IsActionPressed(InputAction.Interact))
                    {
                        Safe.Run(() => player.Heading = HeadingToward(player.Position, vehicle.Position), $"{GetType().Name}.face vehicle for bag");
                        Safe.Run(() => NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle, 2, false, false), $"{GetType().Name}.open ambulance for bag");
                        Safe.Run(() => Game.DisplaySubtitle(_config.MedicBagGettingText, 2000), $"{GetType().Name}.getting bag subtitle");
                        _bagPickupStarted = true;
                        _bagPickupElapsed = 0f;
                    }

                    return false;
                }

                if (AdvanceTimer(ref _bagPickupElapsed, _config.MedicBagPickupSeconds))
                {
                    _medicBagProp = TrySpawnCarriedProp(_config.MedicBagPropModel, PedBoneId.LeftHand);
                    Safe.Run(() => NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle, 2, false), $"{GetType().Name}.shut ambulance after bag");
                    Safe.Run(() => Game.DisplaySubtitle(_config.MedicBagGotText, 3000), $"{GetType().Name}.medic bag subtitle");
                    _hasMedicBag = true;
                }

                return false;
            }

            // Everything past this point is face-to-face patient care - require
            // the player to actually be standing close to the patient before a
            // tap registers, instead of the conversation/treatment triggering
            // from wherever the player happens to be standing.
            bool nearPatient = Game.LocalPlayer.Character.Position.DistanceTo(_patient.Position) <= _config.PatientInteractRadius;

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

                Safe.Run(
                    () => Game.DisplayHelp(nearPatient ? _config.ConversationHelpText : _config.WalkCloserHelpText),
                    $"{GetType().Name}.conversation help");

                if (nearPatient && InputManager.IsActionPressed(InputAction.Interact))
                {
                    _conversationIndex++;
                    _currentLineShown = false;
                }

                return false;
            }

            // Phase 2: single-tap to help, then a short pause-safe bandage beat.
            if (!_isTreating)
            {
                Safe.Run(
                    () => Game.DisplayHelp(nearPatient ? _config.AssessmentHelpText : _config.WalkCloserHelpText),
                    $"{GetType().Name}.Assessment help");

                if (nearPatient && InputManager.IsActionPressed(InputAction.Interact))
                {
                    Ped player = Game.LocalPlayer.Character;
                    Safe.Run(() => player.Heading = HeadingToward(player.Position, _patient.Position), $"{GetType().Name}.face patient for treatment");

                    _isTreating = true;
                    _treatmentElapsed = 0f;
                    Safe.Run(
                        () => Game.DisplaySubtitle(TreatingTextValue, (int)(_config.TreatmentSeconds * 1000f)),
                        $"{GetType().Name}.treating subtitle");
                    StartTreatmentAnimation();
                    if (_config.PartnerAssists)
                    {
                        PartnerManager.PlayAssistAnimation(_config.TreatmentAnimDictionary, _config.TreatmentAnimName);
                    }
                }

                return false;
            }

            if (AdvanceTimer(ref _treatmentElapsed, _config.TreatmentSeconds))
            {
                // Stand back up before resolving.
                Safe.Run(() => Game.LocalPlayer.Character.Tasks.Clear(), $"{GetType().Name}.clear treatment anim");
                if (_config.PartnerAssists)
                {
                    PartnerManager.ResumeFollowing();
                }
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

            if (_transported && _config.TransportToHospital)
            {
                StartPlayerTransport();
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
            // _transportVehicle is only ever set once, in StartPlayerTransport,
            // and never cleared - so "is player transport active" is fully
            // derivable from it without a separate bool to keep in sync.
            if (_transportVehicle != null)
            {
                // Overall safety cap so the sequence can never hang the callout.
                if (AdvanceTimer(ref _resolutionElapsed, _config.TransportSequenceSeconds))
                {
                    return true;
                }

                return TickPlayerTransport();
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

        /// <summary>Drives the player-driven transport sequence one frame at a
        /// time: (0) walk to the back of the vehicle and swap the medic bag for
        /// the stretcher, (1) carry it to the patient, (2) the patient starts
        /// walking themselves to the vehicle, (3) carry/walk back to the
        /// vehicle, (4) single tap to send them into the back seat, (5) confirm
        /// they're actually aboard, (6) drive to the nearest hospital, (7) drop
        /// off. Every phase with a
        /// timeout is config-driven so it can never hang forever; the two
        /// purely player-input phases (0, 1) rely on the overall
        /// TransportSequenceSeconds cap instead, matching this project's "no
        /// fail state, patient just waits" rule. Returns true when clear to
        /// despawn.</summary>
        private bool TickPlayerTransport()
        {
            // Use the vehicle pinned at transport start, not whatever
            // DutyManager.CurrentVehicle is right now - if the player swaps
            // vehicles mid-transport (duty menu), the old one (with the patient
            // inside) is deleted and CurrentVehicle would silently point at an
            // empty replacement.
            Vehicle vehicle = _transportVehicle;
            if (vehicle == null || !vehicle.Exists())
            {
                // Duty vehicle vanished mid-transport (e.g. loadout swapped) -
                // resolve gracefully rather than hang forever.
                return true;
            }

            if (_patient == null || !_patient.Exists())
            {
                return true;
            }

            bool firstTick = !_phaseIssued;
            _phaseIssued = true;
            Ped player = Game.LocalPlayer.Character;

            switch (_transportPhase)
            {
                case TransportWalkToVehicleForGear: // walk to the back of the ambulance and swap the bag for the stretcher
                    Vector3 rear = RearOf(vehicle);
                    Safe.Run(
                        () => Game.DisplayHelp(_config.TransportGearHelpText),
                        $"{GetType().Name}.gear help");

                    if (!_gearSwapStarted)
                    {
                        if (player.Position.DistanceTo(rear) <= 3.5f && InputManager.IsActionPressed(InputAction.Interact))
                        {
                            Safe.Run(() => player.Heading = HeadingToward(player.Position, vehicle.Position), $"{GetType().Name}.face vehicle for stretcher");
                            Safe.Run(() =>
                            {
                                NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle, 2, false, false);
                                NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle, 3, false, false);
                            }, $"{GetType().Name}.open ambulance doors");
                            Safe.Run(
                                () => Game.DisplaySubtitle(_config.TransportGettingGearText, 2000),
                                $"{GetType().Name}.getting gear subtitle");
                            _gearSwapStarted = true;
                            _transportPhaseElapsed = 0f;
                        }
                        break;
                    }

                    if (AdvanceTimer(ref _transportPhaseElapsed, _config.TransportGearPickupSeconds))
                    {
                        // This patient needs the hospital, not more bandaging -
                        // the medic bag goes back in the ambulance and the
                        // stretcher comes out in its place.
                        if (_medicBagProp != null && _medicBagProp.Exists())
                        {
                            Safe.Run(() =>
                            {
                                _medicBagProp.Detach();
                                _medicBagProp.Delete();
                            }, $"{GetType().Name}.stow medic bag");
                            _medicBagProp = null;
                        }
                        _stretcherProp = TrySpawnCarriedProp(_config.StretcherPropModel, PedBoneId.RightHand);

                        Safe.Run(
                            () => Game.DisplaySubtitle(_config.TransportGotGearText, 3000),
                            $"{GetType().Name}.gear subtitle");
                        GoToTransportPhase(TransportWalkToPatientWithGear);
                    }
                    break;

                case TransportWalkToPatientWithGear: // carry the gear over to the patient
                    Safe.Run(
                        () => Game.DisplayHelp(_config.TransportCarryToPatientHelpText),
                        $"{GetType().Name}.carry to patient help");

                    if (player.Position.DistanceTo(_patient.Position) <= 3.5f && InputManager.IsActionPressed(InputAction.Interact))
                    {
                        Safe.Run(() => player.Heading = HeadingToward(player.Position, _patient.Position), $"{GetType().Name}.face patient for stretcher");
                        GoToTransportPhase(TransportLoadPatientOntoStretcher);
                    }
                    break;

                case TransportLoadPatientOntoStretcher: // patient onto the stretcher
                    if (firstTick)
                    {
                        // NOTE: earlier versions rigidly attached the patient's
                        // whole body onto the small hand-held stretcher prop and
                        // played the lying-flat pose on them - that pinned a
                        // full adult body at the player's hand height, which
                        // looked broken rather than like a carried stretcher.
                        // Keep the stretcher/bag as cosmetic hand-held props
                        // only, and have the patient walk to the vehicle under
                        // their own power instead - the same proven approach
                        // the transport sequence used before the stretcher
                        // props were added. Tasks.Clear() (not ClearImmediately)
                        // lets the patient blend out of the lying pose into the
                        // walk instead of snapping between the two.
                        Safe.Run(() =>
                        {
                            _patient.Tasks.Clear();
                            _patient.Tasks.FollowNavigationMeshToPosition(RearOf(vehicle), 0f, 1.0f);
                        }, $"{GetType().Name}.patient walk to vehicle");
                        Safe.Run(
                            () => Game.DisplaySubtitle(_config.TransportPatientLoadedText, 3000),
                            $"{GetType().Name}.loaded subtitle");
                    }
                    GoToTransportPhase(TransportCarryToVehicle);
                    break;

                case TransportCarryToVehicle: // carry the patient back to the vehicle
                    Safe.Run(
                        () => Game.DisplayHelp(_config.TransportCarryToVehicleHelpText),
                        $"{GetType().Name}.carry to vehicle help");

                    bool reachedVehicle = player.Position.DistanceTo(RearOf(vehicle)) <= 3.5f;
                    if (reachedVehicle || AdvanceTimer(ref _transportPhaseElapsed, _config.TransportCarryToVehicleTimeoutSeconds))
                    {
                        GoToTransportPhase(TransportRequestBoard);
                    }
                    break;

                case TransportRequestBoard: // single tap to send the patient into the back seat
                    Safe.Run(
                        () => Game.DisplayHelp(_config.TransportLoadIntoVehicleHelpText),
                        $"{GetType().Name}.load into vehicle help");

                    // Require the player to actually be at the vehicle - the
                    // patient usually catches up during TransportCarryToVehicle,
                    // but this stops the tap firing well before either of them
                    // has actually arrived.
                    if (player.Position.DistanceTo(RearOf(vehicle)) <= 3.5f && InputManager.IsActionPressed(InputAction.Interact))
                    {
                        Safe.Run(() => player.Heading = HeadingToward(player.Position, vehicle.Position), $"{GetType().Name}.face vehicle for boarding");
                        StowGear();
                        Safe.Run(() =>
                        {
                            _patient.Tasks.Clear();
                            _patient.Tasks.EnterVehicle(vehicle, -1, 1);
                        }, $"{GetType().Name}.patient enter duty vehicle");
                        GoToTransportPhase(TransportConfirmBoarded);
                    }
                    break;

                case TransportConfirmBoarded: // wait until the patient is actually aboard
                    if (_patient.IsInAnyVehicle(false))
                    {
                        // Confirmed aboard - only now is it safe to move on to
                        // the drive; advancing without this would let the
                        // sequence "arrive" with no patient in the car.
                        Safe.Run(() =>
                        {
                            NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle, 2, false);
                            NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle, 3, false);
                        }, $"{GetType().Name}.shut ambulance doors");
                        GoToTransportPhase(TransportDrivingToHospital);
                    }
                    else if (AdvanceTimer(ref _transportPhaseElapsed, _config.TransportBoardVehicleTimeoutSeconds))
                    {
                        // Couldn't path into the vehicle in time (blocked route,
                        // awkward parking spot) - resolve now instead of
                        // silently pretending the drive happened.
                        PlayPatientThanks();
                        return true;
                    }
                    break;

                case TransportDrivingToHospital: // player drives to the nearest hospital
                    if (firstTick)
                    {
                        _destinationHospital = DutyManager.NearestHospital(_scenePosition);
                        if (_destinationHospital != null)
                        {
                            Vector3 hospitalPos = new Vector3(_destinationHospital.X, _destinationHospital.Y, _destinationHospital.Z);
                            _hospitalBlip = CreateBlip(hospitalPos, Color.DodgerBlue, "Hospital");
                            if (_hospitalBlip != null)
                            {
                                Safe.Run(() => _hospitalBlip.IsRouteEnabled = true, $"{GetType().Name}.hospital blip route");
                            }
                        }
                        Safe.Run(
                            () => Game.DisplaySubtitle(_config.TransportDriveSubtitle, 5000),
                            $"{GetType().Name}.drive to hospital subtitle");
                    }

                    Safe.Run(
                        () => Game.DisplayHelp(_config.TransportDriveHelpText),
                        $"{GetType().Name}.drive help");

                    bool patientStillAboard = _patient != null && _patient.Exists() && _patient.IsInAnyVehicle(false);
                    bool atHospital = _destinationHospital != null &&
                        vehicle.Position.DistanceTo(new Vector3(_destinationHospital.X, _destinationHospital.Y, _destinationHospital.Z)) <= 15f;
                    if (!patientStillAboard || atHospital || _destinationHospital == null)
                    {
                        GoToTransportPhase(TransportDroppingOff);
                    }
                    break;

                case TransportDroppingOff: // patient gets out at the hospital
                    if (firstTick)
                    {
                        if (_hospitalBlip != null && _hospitalBlip.IsValid())
                        {
                            _hospitalBlip.Delete();
                            _hospitalBlip = null;
                        }
                        if (_patient != null && _patient.Exists())
                        {
                            Safe.Run(() => _patient.Tasks.LeaveVehicle(LeaveVehicleFlags.None), $"{GetType().Name}.patient leave at hospital");
                        }
                    }

                    bool dropped = _patient == null || !_patient.Exists() || !_patient.IsInAnyVehicle(false);
                    if (dropped || AdvanceTimer(ref _transportPhaseElapsed, _config.TransportDropOffTimeoutSeconds))
                    {
                        PlayPatientThanks();
                        return true;
                    }
                    break;
            }

            return false;
        }

        protected override void OnTick()
        {
            // Safety net: if the patient becomes invalid while the partner is
            // off assisting at the scene (e.g. despawned mid-assessment), make
            // sure the partner isn't left frozen in "assisting" mode forever -
            // it's a no-op if the partner isn't currently assisting.
            if (_config.PartnerAssists && (_patient == null || !_patient.Exists()))
            {
                PartnerManager.ResumeFollowing();
            }

            if (!_config.ShowSceneMarker)
            {
                return;
            }

            // Ground marker while heading to the scene and through treatment.
            if (State == CalloutState.EnRoute || State == CalloutState.OnScene || State == CalloutState.Assessment)
            {
                DrawGroundMarker(_scenePosition);
                return;
            }

            // The "walk to the back of the vehicle for the gear" transport step
            // is a specific spot that's easy to miss at a glance - mark it too.
            if (State == CalloutState.Resolution && _transportPhase == TransportWalkToVehicleForGear &&
                _transportVehicle != null && _transportVehicle.Exists())
            {
                DrawGroundMarker(RearOf(_transportVehicle));
            }
        }

        private void DrawGroundMarker(Vector3 position)
        {
            Safe.Run(() =>
                NativeFunction.Natives.DRAW_MARKER(
                    1,
                    position.X, position.Y, position.Z - 1f,
                    0f, 0f, 0f,
                    0f, 0f, 0f,
                    1.5f, 1.5f, 1f,
                    255, 220, 0, 120,
                    false, false, 2, false,
                    0, 0, false),
                $"{GetType().Name}.draw marker");
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

        /// <summary>Thin wrapper kept so every call site in this file reads the
        /// same as before - the actual logic lives in AnimHelper.PlayLoopedSafe,
        /// shared with PartnerManager (which plays the same clips on the
        /// partner during PlayAssistAnimation).</summary>
        private static bool PlayLoopedAnimSafe(Ped ped, string dictionary, string name, string context) =>
            AnimHelper.PlayLoopedSafe(ped, dictionary, name, context);

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

        /// <summary>Transport outcome: the player walks back to their own duty
        /// vehicle and swaps the medic bag (already carried since the
        /// assessment step) for the stretcher (a cosmetic hand prop), walks it
        /// to the patient, then the patient walks themselves back to the
        /// vehicle and boards before the player drives to the nearest
        /// hospital. Falls back to the on-scene thank-you if the player has no
        /// duty vehicle right now, so the callout still resolves cleanly.</summary>
        private void StartPlayerTransport()
        {
            Vehicle vehicle = DutyManager.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists())
            {
                // Most common cause: this callout was dispatched via a debug
                // command without going on duty first (DutyManager.OnDuty is
                // what spawns the duty vehicle) - make that visible instead of
                // silently skipping straight to the thank-you, which made the
                // whole stretcher sequence look like it didn't exist.
                Log.Warn($"{GetType().Name}: no duty vehicle available, skipping player transport (go on duty first via F4/emsmod_duty).");
                Safe.Run(
                    () => Game.DisplayNotification("~r~No duty vehicle - go on duty (F4) first to transport patients."),
                    $"{GetType().Name}.no duty vehicle notify");
                PlayPatientThanks();
                return;
            }

            _transportVehicle = vehicle;

            // Lock the duty vehicle so it can't be swapped/deleted out from
            // under the patient once they're riding in it. Unlocked via the
            // registry's cleanup no matter how this callout ends.
            DutyManager.VehicleSwapLocked = true;
            EntitySpawnRegistry.RegisterCleanupAction(InstanceId, () => DutyManager.VehicleSwapLocked = false);

            GoToTransportPhase(TransportWalkToVehicleForGear);
        }

        /// <summary>Point just behind the vehicle, used as the "back doors"
        /// interaction spot for getting/stowing the gear.</summary>
        /// <summary>The real rear-door interaction spot for this specific
        /// vehicle model, not a guessed fixed distance. An ambulance/fire
        /// truck's actual rear bumper can be much farther from the vehicle's
        /// center than a fixed offset assumes, which put the old "walk here to
        /// open the back" spot inside the vehicle's own body for longer
        /// models - the likely cause of "can't get the door/stretcher tap to
        /// register." GetDimensions returns the model's real rearBottomLeft
        /// corner in local space (Y is negative = behind center), so this
        /// scales correctly per vehicle instead of one guess for every model.</summary>
        private static Vector3 RearOf(Vehicle vehicle)
        {
            return Safe.Run(() =>
            {
                Vector3 rearBottomLeft, frontTopRight;
                vehicle.Model.GetDimensions(out rearBottomLeft, out frontTopRight);
                // A bit further back than the bumper itself so the player
                // stands clear of the vehicle, not clipped into it.
                float rearOffset = rearBottomLeft.Y - 0.75f;
                return vehicle.GetOffsetPosition(new Vector3(0f, rearOffset, 0f));
            }, vehicle.Position - vehicle.ForwardVector * 2.5f, "PatientCalloutBase.RearOf");
        }

        /// <summary>Spawns a prop and attaches it to the player's hand so it
        /// visibly rides along while carried. Best-effort: an empty/invalid
        /// model name (e.g. overridden blank in the callout XML) just skips the
        /// visual - the rest of the transport sequence doesn't depend on it.</summary>
        private Rage.Object TrySpawnCarriedProp(string modelName, PedBoneId hand)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            return Safe.Run(() =>
            {
                var model = new Model(modelName);
                if (!model.IsValid)
                {
                    Log.Warn($"{GetType().Name}: prop model '{modelName}' is invalid; carrying without it.");
                    Game.DisplayNotification($"~r~Prop '{modelName}' is invalid - carrying without it.");
                    return null;
                }

                model.LoadAndWait();
                Ped player = Game.LocalPlayer.Character;
                var prop = new Rage.Object(model, player.Position);
                model.Dismiss();

                if (prop == null || !prop.Exists())
                {
                    return null;
                }

                prop.IsPersistent = true;
                EntitySpawnRegistry.RegisterEntity(InstanceId, prop);
                int boneIndex = player.GetBoneIndex(hand);
                prop.AttachTo(player, boneIndex, Vector3.Zero, new Rotator());
                return prop;
            }, null, $"{GetType().Name}.TrySpawnCarriedProp({modelName})");
        }

        /// <summary>Detaches the patient from the stretcher and clears both
        /// carried props once the patient is loaded into the vehicle. The props
        /// stay registered for cleanup either way, so a failure here can never
        /// leave them behind.</summary>
        private void StowGear()
        {
            Safe.Run(() =>
            {
                if (_patient != null && _patient.Exists())
                {
                    _patient.Detach();
                }
                if (_stretcherProp != null && _stretcherProp.Exists())
                {
                    _stretcherProp.Detach();
                    _stretcherProp.Delete();
                }
                if (_medicBagProp != null && _medicBagProp.Exists())
                {
                    _medicBagProp.Detach();
                    _medicBagProp.Delete();
                }
            }, $"{GetType().Name}.StowGear");
        }

        /// <summary>Sets _scenePosition and _sceneHeading to a proper drivable
        /// road node near <paramref name="near"/> (with the road's heading) so
        /// scenes land on a reachable road, aligned to it.</summary>
        private void ResolveSceneRoad(Vector3 near)
        {
            RoadHelper.ResolveNearestRoad(near, GetType().Name, out _scenePosition, out _sceneHeading);
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
