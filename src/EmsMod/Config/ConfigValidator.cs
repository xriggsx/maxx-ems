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

            if (config.TransportPercentage < 0 || config.TransportPercentage > 100)
            {
                Log.Warn($"ConfigValidator [{label}]: TransportPercentage was {config.TransportPercentage}, clamping to 0-100.");
                config.TransportPercentage = config.TransportPercentage < 0 ? 0 : 100;
            }

            if (config.SceneSpawnDistance <= 0f)
            {
                config.SceneSpawnDistance = defaults.SceneSpawnDistance;
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

            if (config.TransportSequenceSeconds < 0f)
            {
                config.TransportSequenceSeconds = defaults.TransportSequenceSeconds;
            }

            if (config.AssessmentLines == null)
            {
                config.AssessmentLines = new System.Collections.Generic.List<Schema.DialogueLine>();
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
            config.AssessmentHelpText = FallbackIfBlank(config.AssessmentHelpText, defaults.AssessmentHelpText);
            config.TreatingText = FallbackIfBlank(config.TreatingText, defaults.TreatingText);
            config.TreatedText = FallbackIfBlank(config.TreatedText, defaults.TreatedText);
            config.TransportedText = FallbackIfBlank(config.TransportedText, defaults.TransportedText);
            config.ResolutionVoiceLine = FallbackIfBlank(config.ResolutionVoiceLine, defaults.ResolutionVoiceLine);

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

            if (config.Modes == null || config.Modes.Count == 0)
            {
                Log.Warn("ConfigValidator: no duty modes in Duty config, using built-in defaults.");
                config.Modes = DefaultModes();
            }

            return config;
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
                    Characters = new System.Collections.Generic.List<string> { "s_m_m_paramedic_01", "s_f_y_scrubs_01" },
                    Vehicles = new System.Collections.Generic.List<string> { "ambulance" },
                },
                new Schema.DutyMode
                {
                    Name = "Firefighter",
                    Characters = new System.Collections.Generic.List<string> { "s_m_y_fireman_01" },
                    Vehicles = new System.Collections.Generic.List<string> { "firetruk" },
                },
            };
        }
    }
}
