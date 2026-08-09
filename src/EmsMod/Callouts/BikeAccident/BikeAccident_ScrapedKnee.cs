namespace EmsMod.Callouts
{
    /// <summary>
    /// Bike accident: a rider with a scraped knee - the classic "clean it up
    /// and pop a bandage on" call, almost always treated on scene (low transport
    /// percentage). Proves a new patient callout is just a config file + this
    /// thin subclass, with zero duplicated logic. Tuning lives in
    /// Config/Callouts/BikeAccident_ScrapedKnee.xml.
    /// </summary>
    public class BikeAccident_ScrapedKnee : PatientCalloutBase
    {
        protected override string ConfigFileName => "BikeAccident_ScrapedKnee.xml";
    }
}
