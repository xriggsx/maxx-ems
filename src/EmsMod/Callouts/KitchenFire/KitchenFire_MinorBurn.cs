namespace EmsMod.Callouts
{
    /// <summary>
    /// Firefighter callout: someone got a burn from a small kitchen/BBQ fire.
    /// Reuses the full PatientCalloutBase flow; content is in the config.
    /// </summary>
    public class KitchenFire_MinorBurn : PatientCalloutBase
    {
        protected override string ConfigFileName => "KitchenFire_MinorBurn.xml";
    }
}
