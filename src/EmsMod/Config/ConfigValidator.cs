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
    }
}
