using System;
using System.Speech.Synthesis;

namespace EmsMod.Dialogue
{
    /// <summary>
    /// Phase 1 dispatch voice. Only ever calls SpeakAsync, never the blocking
    /// Speak() - that would freeze the GameFiber (and the game) for its
    /// duration. Completion is detected by polling State from Tick().
    /// </summary>
    public class SystemSpeechVoice : IDispatchVoice
    {
        private readonly SpeechSynthesizer _synthesizer;
        private Action _pendingCompletion;

        public SystemSpeechVoice()
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
        }

        public bool IsSpeaking { get; private set; }

        public void Speak(string line, Action onComplete = null)
        {
            _pendingCompletion = onComplete;
            IsSpeaking = true;
            _synthesizer.SpeakAsync(line);
        }

        public void Stop()
        {
            _synthesizer.SpeakAsyncCancelAll();
            IsSpeaking = false;
            _pendingCompletion = null;
        }

        public void Tick()
        {
            if (!IsSpeaking || _synthesizer.State != SynthesizerState.Ready)
            {
                return;
            }

            IsSpeaking = false;
            Action callback = _pendingCompletion;
            _pendingCompletion = null;
            callback?.Invoke();
        }
    }
}
