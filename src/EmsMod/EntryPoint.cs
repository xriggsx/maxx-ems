using Rage;

[assembly: Rage.Attributes.Plugin("EmsMod", Description = "Kid-friendly EMS roleplay mod", Author = "MaxxEms")]

namespace EmsMod
{
    public static class EntryPoint
    {
        public static void Main()
        {
            Game.LogTrivial("EmsMod: Plugin loaded successfully.");

            while (true)
            {
                GameFiber.Yield();
            }
        }
    }
}
