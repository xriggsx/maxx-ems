namespace EmsMod.Callouts
{
    /// <summary>
    /// Someone is having trouble breathing (asthma). Thin subclass - all behavior
    /// and polish come from PatientCalloutBase; content is in the XML config.
    /// </summary>
    public class Asthma_BreathingTrouble : PatientCalloutBase
    {
        protected override string ConfigFileName => "Asthma_BreathingTrouble.xml";
    }
}
