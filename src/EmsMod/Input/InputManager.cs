using System.Collections.Generic;
using System.Windows.Forms;
using Rage;
using EmsMod.Utils;

namespace EmsMod.Input
{
    /// <summary>
    /// The single sanctioned path for reading input. No file outside this
    /// folder should call raw keyboard/controller APIs directly — going
    /// through IsActionPressed is what guarantees every interaction supports
    /// both keyboard and controller, and is single-tap (edge-triggered, no
    /// "held" primitive is exposed).
    /// </summary>
    public static class InputManager
    {
        private static readonly Dictionary<InputAction, InputBinding> _bindings = new Dictionary<InputAction, InputBinding>
        {
            { InputAction.AcceptCallout, new InputBinding(Keys.Y, ControllerButtons.A) },
            { InputAction.DeclineCallout, new InputBinding(Keys.N, ControllerButtons.B) },
            { InputAction.Interact, new InputBinding(Keys.E, ControllerButtons.X) },
            { InputAction.ConfirmMenuOption, new InputBinding(Keys.E, ControllerButtons.X) },
        };

        public static bool IsActionPressed(InputAction action)
        {
            if (!_bindings.TryGetValue(action, out InputBinding binding))
            {
                Log.Warn($"InputManager: no binding registered for action '{action}'.");
                return false;
            }

            bool keyPressed = Safe.Run(() => Game.IsKeyDown(binding.Key), false, $"InputManager.IsActionPressed({action}) key");
            bool buttonPressed = Safe.Run(() => Game.IsControllerButtonDown(binding.ControllerButton), false, $"InputManager.IsActionPressed({action}) controller");

            return keyPressed || buttonPressed;
        }
    }
}
