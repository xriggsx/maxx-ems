namespace EmsMod.Callouts
{
    /// <summary>
    /// Someone got hurt playing sport. Thin subclass - all behavior and polish
    /// come from PatientCalloutBase; content is in the XML config.
    /// </summary>
    public class SportsInjury_PossibleFracture : PatientCalloutBase
    {
        protected override string ConfigFileName => "SportsInjury_PossibleFracture.xml";
    }
}
