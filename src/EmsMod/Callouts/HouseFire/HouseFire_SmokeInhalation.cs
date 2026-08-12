namespace EmsMod.Callouts
{
    /// <summary>
    /// Firefighter callout: a resident who breathed smoke from a fire. Reuses the
    /// full PatientCalloutBase flow; content (incl. scene fire) is in the config.
    /// </summary>
    public class HouseFire_SmokeInhalation : PatientCalloutBase
    {
        protected override string ConfigFileName => "HouseFire_SmokeInhalation.xml";
    }
}
