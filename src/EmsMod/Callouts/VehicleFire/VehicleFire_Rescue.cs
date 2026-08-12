namespace EmsMod.Callouts
{
    /// <summary>
    /// Firefighter callout: a person needing help by a burning car. Reuses the
    /// full PatientCalloutBase flow; the scene car is set alight via config.
    /// </summary>
    public class VehicleFire_Rescue : PatientCalloutBase
    {
        protected override string ConfigFileName => "VehicleFire_Rescue.xml";
    }
}
