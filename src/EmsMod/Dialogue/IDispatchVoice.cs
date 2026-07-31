using System;

namespace EmsMod.Dialogue
{
    public interface IDispatchVoice
    {
        void Speak(string line, Action onComplete = null);

        bool IsSpeaking { get; }

        void Stop();

        /// <summary>
        /// Called once per frame so completion can be detected by polling
        /// instead of relying on a background-thread event callback.
        /// </summary>
        void Tick();
    }
}
