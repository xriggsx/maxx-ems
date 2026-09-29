namespace EmsMod.Config.Schema
{
    public class GeneralConfig
    {
        public string VoiceEngine { get; set; } = "SystemSpeech";

        public int AcceptDeclineTimeoutSeconds { get; set; } = 15;

        public float WalkedAwayDistanceMeters { get; set; } = 40f;

        // How long between automatic callouts while on duty with nothing
        // active - a random wait in this range, so calls come in on their own
        // instead of needing a console command every time.
        public float DispatchMinIntervalSeconds { get; set; } = 45f;
        public float DispatchMaxIntervalSeconds { get; set; } = 120f;

        public bool DebugLoggingEnabled { get; set; } = true;
    }
}
