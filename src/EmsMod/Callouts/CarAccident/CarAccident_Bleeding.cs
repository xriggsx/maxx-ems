namespace EmsMod.Callouts
{
    /// <summary>
    /// Minor car accident: a person with a small cut who needs a bandage.
    /// All behavior comes from PatientCalloutBase; this class only names its
    /// config file. Tuning lives in Config/Callouts/CarAccident_Bleeding.xml.
    /// </summary>
    public class CarAccident_Bleeding : PatientCalloutBase
    {
        protected override string ConfigFileName => "CarAccident_Bleeding.xml";
    }
}
