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
    }
}
