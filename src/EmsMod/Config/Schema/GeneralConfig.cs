namespace EmsMod.Config.Schema
{
    public class GeneralConfig
    {
        public string VoiceEngine { get; set; } = "SystemSpeech";

        public int AcceptDeclineTimeoutSeconds { get; set; } = 15;

        public float WalkedAwayDistanceMeters { get; set; } = 40f;

        public bool DebugLoggingEnabled { get; set; } = true;
    }
}
