using System.IO;
using System.Xml.Serialization;
using EmsMod.Config.Schema;
using EmsMod.Utils;

namespace EmsMod.Config
{
    public static class ConfigLoader
    {
        private static GeneralConfig _general;

        public static GeneralConfig General => _general ?? (_general = LoadGeneral());

        public static void ReloadGeneral()
        {
            _general = LoadGeneral();
            Log.Info("General config reloaded.");
        }

        private static GeneralConfig LoadGeneral()
        {
            string path = Path.Combine(ModPaths.ConfigDirectory, "General.xml");
            GeneralConfig loaded = Safe.Run(() => DeserializeGeneral(path), null, "ConfigLoader.LoadGeneral");
            return ConfigValidator.ValidateGeneral(loaded);
        }

        private static GeneralConfig DeserializeGeneral(string path)
        {
            if (!File.Exists(path))
            {
                Log.Warn($"ConfigLoader: General.xml not found at '{path}', falling back to defaults.");
                return null;
            }

            using (FileStream stream = File.OpenRead(path))
            {
                var serializer = new XmlSerializer(typeof(GeneralConfig));
                return (GeneralConfig)serializer.Deserialize(stream);
            }
        }

        /// <summary>
        /// Loads a per-callout config from Config/Callouts/&lt;fileName&gt;. Same two
        /// defense layers as General: Safe.Run around deserialization (broken XML
        /// -> null -> all-default config), then ConfigValidator backfills any
        /// missing/out-of-range field. Never throws; always returns a usable config.
        /// </summary>
        public static CalloutConfig LoadCallout(string fileName)
        {
            string path = Path.Combine(ModPaths.ConfigDirectory, "Callouts", fileName);
            CalloutConfig loaded = Safe.Run(() => DeserializeCallout(path), null, $"ConfigLoader.LoadCallout({fileName})");
            return ConfigValidator.ValidateCallout(loaded, fileName);
        }

        private static CalloutConfig DeserializeCallout(string path)
        {
            if (!File.Exists(path))
            {
                Log.Warn($"ConfigLoader: callout config not found at '{path}', falling back to defaults.");
                return null;
            }

            using (FileStream stream = File.OpenRead(path))
            {
                var serializer = new XmlSerializer(typeof(CalloutConfig));
                return (CalloutConfig)serializer.Deserialize(stream);
            }
        }
    }
}
