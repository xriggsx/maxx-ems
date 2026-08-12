namespace EmsMod.Callouts
{
    /// <summary>
    /// Someone took a fall at a playground/park. Thin subclass - all behavior and
    /// polish come from PatientCalloutBase; content is in the XML config.
    /// </summary>
    public class PlaygroundFall_TwistedAnkle : PatientCalloutBase
    {
        protected override string ConfigFileName => "PlaygroundFall_TwistedAnkle.xml";
    }
}
