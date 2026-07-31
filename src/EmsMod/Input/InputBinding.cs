using System.Windows.Forms;
using Rage;

namespace EmsMod.Input
{
    public class InputBinding
    {
        public Keys Key { get; }

        public ControllerButtons ControllerButton { get; }

        public InputBinding(Keys key, ControllerButtons controllerButton)
        {
            Key = key;
            ControllerButton = controllerButton;
        }
    }
}
