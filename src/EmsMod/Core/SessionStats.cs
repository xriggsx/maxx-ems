namespace EmsMod.Core
{
    /// <summary>
    /// In-memory progress for the current session only. Per the project rules
    /// there is NO persistence - these reset every time the plugin loads, so
    /// there's no save/load and nothing to migrate. Kept trivially simple on
    /// purpose (a friendly "look how many people you helped today" number, not
    /// a score system with penalties).
    /// </summary>
    public static class SessionStats
    {
        public static int PatientsHelped { get; private set; }

        public static void RecordPatientHelped()
        {
            PatientsHelped++;
        }
    }
}
