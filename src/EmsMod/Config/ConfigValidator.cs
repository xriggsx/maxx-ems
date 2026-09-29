using EmsMod.Config.Schema;
using EmsMod.Utils;

namespace EmsMod.Config
{
    public static class ConfigValidator
    {
        public static GeneralConfig ValidateGeneral(GeneralConfig config)
        {
            if (config == null)
            {
                Log.Warn("ConfigValidator: General config was null, using an all-default GeneralConfig.");
                return new GeneralConfig();
            }

            if (string.IsNullOrWhiteSpace(config.VoiceEngine))
            {
                Log.Warn("ConfigValidator: VoiceEngine missing/empty, defaulting to 'SystemSpeech'.");
                config.VoiceEngine = "SystemSpeech";
            }

            if (config.AcceptDeclineTimeoutSeconds <= 0)
            {
                Log.Warn($"ConfigValidator: AcceptDeclineTimeoutSeconds was {config.AcceptDeclineTimeoutSeconds}, defaulting to 15.");
                config.AcceptDeclineTimeoutSeconds = 15;
            }

            if (config.WalkedAwayDistanceMeters <= 0f)
            {
                Log.Warn($"ConfigValidator: WalkedAwayDistanceMeters was {config.WalkedAwayDistanceMeters}, defaulting to 40.");
                config.WalkedAwayDistanceMeters = 40f;
            }

            if (config.DispatchMinIntervalSeconds <= 0f)
            {
                config.DispatchMinIntervalSeconds = 45f;
            }

            if (config.DispatchMaxIntervalSeconds < config.DispatchMinIntervalSeconds)
            {
                config.DispatchMaxIntervalSeconds = config.DispatchMinIntervalSeconds + 60f;
            }

            return config;
        }

        public static CalloutConfig ValidateCallout(CalloutConfig config, string label)
        {
            if (config == null)
            {
                Log.Warn($"ConfigValidator: callout config '{label}' was null, using an all-default CalloutConfig.");
                return new CalloutConfig();
            }

            // Fall back to a fresh instance's defaults for any numeric field that
            // is missing/out of range, and any text field left empty.
            var defaults = new CalloutConfig();

            if (config.SceneSpawnDistanceMin <= 0f)
            {
                config.SceneSpawnDistanceMin = defaults.SceneSpawnDistanceMin;
            }

            if (config.SceneSpawnDistanceMax < config.SceneSpawnDistanceMin)
            {
                config.SceneSpawnDistanceMax = config.SceneSpawnDistanceMin + 100f;
            }

            if (config.ArrivalRadius <= 0f)
            {
                config.ArrivalRadius = defaults.ArrivalRadius;
            }

            if (config.TreatmentSeconds < 0f)
            {
                config.TreatmentSeconds = defaults.TreatmentSeconds;
            }

            if (config.ResolutionHoldSeconds < 0f)
            {
                config.ResolutionHoldSeconds = defaults.ResolutionHoldSeconds;
            }

            if (config.TransportSequenceSeconds <= 0f)
            {
                config.TransportSequenceSeconds = defaults.TransportSequenceSeconds;
            }

            if (config.TransportCarryToVehicleTimeoutSeconds <= 0f)
            {
                config.TransportCarryToVehicleTimeoutSeconds = defaults.TransportCarryToVehicleTimeoutSeconds;
            }

            if (config.TransportBoardVehicleTimeoutSeconds <= 0f)
            {
                config.TransportBoardVehicleTimeoutSeconds = defaults.TransportBoardVehicleTimeoutSeconds;
            }

            if (config.TransportDropOffTimeoutSeconds <= 0f)
            {
                config.TransportDropOffTimeoutSeconds = defaults.TransportDropOffTimeoutSeconds;
            }

            if (config.MedicBagPickupSeconds <= 0f)
            {
                config.MedicBagPickupSeconds = defaults.MedicBagPickupSeconds;
            }

            if (config.PatientInteractRadius <= 0f)
            {
                config.PatientInteractRadius = defaults.PatientInteractRadius;
            }

            if (config.TransportGearPickupSeconds <= 0f)
            {
                config.TransportGearPickupSeconds = defaults.TransportGearPickupSeconds;
            }

            if (config.AssessmentLines == null)
            {
                config.AssessmentLines = new System.Collections.Generic.List<Schema.DialogueLine>();
            }

            if (config.OnSceneTextVariants == null)
            {
                config.OnSceneTextVariants = new System.Collections.Generic.List<string>();
            }

            if (config.TreatingTextVariants == null)
            {
                config.TreatingTextVariants = new System.Collections.Generic.List<string>();
            }

            if (config.PatientThanksTextVariants == null)
            {
                config.PatientThanksTextVariants = new System.Collections.Generic.List<string>();
            }

            if (string.IsNullOrWhiteSpace(config.DispatchMode))
            {
                config.DispatchMode = defaults.DispatchMode;
            }

            if (config.Injuries == null)
            {
                config.Injuries = new System.Collections.Generic.List<Schema.Injury>();
            }
            else
            {
                foreach (Schema.Injury injury in config.Injuries)
                {
                    if (injury != null && injury.AssessmentLines == null)
                    {
                        injury.AssessmentLines = new System.Collections.Generic.List<Schema.DialogueLine>();
                    }
                }
            }

            // Text: an empty element (<X></X>) deserializes to "" and would show
            // a blank prompt/notification - backfill those to the defaults.
            config.DispatchText = FallbackIfBlank(config.DispatchText, defaults.DispatchText);
            config.DispatchVoiceLine = FallbackIfBlank(config.DispatchVoiceLine, defaults.DispatchVoiceLine);
            config.EnRouteHelpText = FallbackIfBlank(config.EnRouteHelpText, defaults.EnRouteHelpText);
            config.EnRouteVoiceLine = FallbackIfBlank(config.EnRouteVoiceLine, defaults.EnRouteVoiceLine);
            config.OnSceneText = FallbackIfBlank(config.OnSceneText, defaults.OnSceneText);
            config.OnSceneVoiceLine = FallbackIfBlank(config.OnSceneVoiceLine, defaults.OnSceneVoiceLine);
            config.MedicBagHelpText = FallbackIfBlank(config.MedicBagHelpText, defaults.MedicBagHelpText);
            config.MedicBagGettingText = FallbackIfBlank(config.MedicBagGettingText, defaults.MedicBagGettingText);
            config.MedicBagGotText = FallbackIfBlank(config.MedicBagGotText, defaults.MedicBagGotText);
            config.WalkCloserHelpText = FallbackIfBlank(config.WalkCloserHelpText, defaults.WalkCloserHelpText);
            config.AssessmentHelpText = FallbackIfBlank(config.AssessmentHelpText, defaults.AssessmentHelpText);
            config.TreatingText = FallbackIfBlank(config.TreatingText, defaults.TreatingText);
            config.TreatedText = FallbackIfBlank(config.TreatedText, defaults.TreatedText);
            config.TransportedText = FallbackIfBlank(config.TransportedText, defaults.TransportedText);
            config.ResolutionVoiceLine = FallbackIfBlank(config.ResolutionVoiceLine, defaults.ResolutionVoiceLine);
            config.ConversationHelpText = FallbackIfBlank(config.ConversationHelpText, defaults.ConversationHelpText);
            config.PatientThanksText = FallbackIfBlank(config.PatientThanksText, defaults.PatientThanksText);
            config.TransportGearHelpText = FallbackIfBlank(config.TransportGearHelpText, defaults.TransportGearHelpText);
            config.TransportGettingGearText = FallbackIfBlank(config.TransportGettingGearText, defaults.TransportGettingGearText);
            config.TransportGotGearText = FallbackIfBlank(config.TransportGotGearText, defaults.TransportGotGearText);
            config.TransportCarryToPatientHelpText = FallbackIfBlank(config.TransportCarryToPatientHelpText, defaults.TransportCarryToPatientHelpText);
            config.TransportPatientLoadedText = FallbackIfBlank(config.TransportPatientLoadedText, defaults.TransportPatientLoadedText);
            config.TransportCarryToVehicleHelpText = FallbackIfBlank(config.TransportCarryToVehicleHelpText, defaults.TransportCarryToVehicleHelpText);
            config.TransportLoadIntoVehicleHelpText = FallbackIfBlank(config.TransportLoadIntoVehicleHelpText, defaults.TransportLoadIntoVehicleHelpText);
            config.TransportDriveSubtitle = FallbackIfBlank(config.TransportDriveSubtitle, defaults.TransportDriveSubtitle);
            config.TransportDriveHelpText = FallbackIfBlank(config.TransportDriveHelpText, defaults.TransportDriveHelpText);

            return config;
        }

        private static string FallbackIfBlank(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static DutyConfig ValidateDuty(DutyConfig config)
        {
            if (config == null)
            {
                config = new DutyConfig();
            }

            if (config.Hospitals == null || config.Hospitals.Count == 0)
            {
                Log.Warn("ConfigValidator: no hospitals in Duty config, using built-in defaults.");
                config.Hospitals = DefaultHospitals();
            }

            if (config.FireStations == null || config.FireStations.Count == 0)
            {
                Log.Warn("ConfigValidator: no fire stations in Duty config, using built-in defaults.");
                config.FireStations = DefaultFireStations();
            }

            if (config.Modes == null || config.Modes.Count == 0)
            {
                Log.Warn("ConfigValidator: no duty modes in Duty config, using built-in defaults.");
                config.Modes = DefaultModes();
            }

            return config;
        }

        private static System.Collections.Generic.List<Schema.Hospital> DefaultFireStations()
        {
            return new System.Collections.Generic.List<Schema.Hospital>
            {
                new Schema.Hospital { Name = "Davis Fire Station", X = 198.5f, Y = -1642.5f, Z = 29.8f, Heading = 320f },
                new Schema.Hospital { Name = "El Burro Heights Fire Station", X = 1191.7f, Y = -1467.3f, Z = 34.9f, Heading = 90f },
                new Schema.Hospital { Name = "Rockford Hills Fire Station", X = -671.0f, Y = -1103.6f, Z = 22.3f, Heading = 50f },
                new Schema.Hospital { Name = "Sandy Shores Fire Station", X = 1697.5f, Y = 3584.0f, Z = 35.4f, Heading = 210f },
                new Schema.Hospital { Name = "Paleto Bay Fire Station", X = -379.5f, Y = 6120.5f, Z = 31.5f, Heading = 45f },
            };
        }

        private static System.Collections.Generic.List<Schema.Hospital> DefaultHospitals()
        {
            return new System.Collections.Generic.List<Schema.Hospital>
            {
                new Schema.Hospital { Name = "Pillbox Hill Medical Center", X = 298.6f, Y = -584.9f, Z = 43.3f, Heading = 70f },
                new Schema.Hospital { Name = "Central Los Santos Medical Center", X = 340.6f, Y = -1396.6f, Z = 32.5f, Heading = 240f },
                new Schema.Hospital { Name = "Mount Zonah Medical Center", X = -449.7f, Y = -340.3f, Z = 34.5f, Heading = 250f },
                new Schema.Hospital { Name = "Sandy Shores Medical Center", X = 1839.6f, Y = 3672.9f, Z = 34.3f, Heading = 210f },
                new Schema.Hospital { Name = "Paleto Bay Care Center", X = -247.8f, Y = 6330.3f, Z = 32.4f, Heading = 220f },
            };
        }

        private static System.Collections.Generic.List<Schema.DutyMode> DefaultModes()
        {
            return new System.Collections.Generic.List<Schema.DutyMode>
            {
                new Schema.DutyMode
                {
                    Name = "Paramedic",
                    Characters = new System.Collections.Generic.List<string> { "s_m_m_paramedic_01", "s_f_y_scrubs_01", "s_m_m_doctor_01" },
                    Vehicles = new System.Collections.Generic.List<Schema.DutyVehicle> { new Schema.DutyVehicle { Model = "ambulance", Name = "Ambulance" } },
                    Equipment = new System.Collections.Generic.List<string> { "weapon_flashlight" },
                },
                new Schema.DutyMode
                {
                    Name = "Firefighter",
                    StartLocationType = "FireStation",
                    Characters = new System.Collections.Generic.List<string> { "s_m_y_fireman_01" },
                    Vehicles = new System.Collections.Generic.List<Schema.DutyVehicle> { new Schema.DutyVehicle { Model = "firetruk", Name = "Fire Truck" } },
                    Equipment = new System.Collections.Generic.List<string> { "weapon_fireextinguisher", "weapon_flashlight" },
                },
            };
        }
    }
}
