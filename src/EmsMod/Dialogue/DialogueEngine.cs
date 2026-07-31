using System;
using EmsMod.Config;
using EmsMod.Utils;

namespace EmsMod.Dialogue
{
    /// <summary>
    /// Facade over IDispatchVoice. Callers only ever touch this class, never
    /// a concrete voice implementation - swapping SystemSpeechVoice for
    /// NAudioVoice later is a config change plus a new class, no touches
    /// here or in any callout.
    /// </summary>
    public static class DialogueEngine
    {
        private static IDispatchVoice _voice;

        public static void Initialize()
        {
            string engine = ConfigLoader.General.VoiceEngine;
            _voice = Safe.Run(() => CreateVoice(engine), null, "DialogueEngine.Initialize") ?? new SystemSpeechVoice();
        }

        public static void Speak(string line, Action onComplete = null)
        {
            Safe.Run(() => _voice?.Speak(line, onComplete), "DialogueEngine.Speak");
        }

        public static bool IsSpeaking => _voice?.IsSpeaking ?? false;

        public static void Stop()
        {
            Safe.Run(() => _voice?.Stop(), "DialogueEngine.Stop");
        }

        public static void Tick()
        {
            Safe.Run(() => _voice?.Tick(), "DialogueEngine.Tick");
        }

        private static IDispatchVoice CreateVoice(string engine)
        {
            switch (engine)
            {
                case "SystemSpeech":
                default:
                    return new SystemSpeechVoice();
            }
        }
    }
}
