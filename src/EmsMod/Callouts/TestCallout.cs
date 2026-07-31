using Rage;
using EmsMod.Input;
using EmsMod.Utils;

namespace EmsMod.Callouts
{
    /// <summary>
    /// Deliberately content-free vertical slice: dispatch text, popup, spawn
    /// one idle ped, single-tap done, despawn. Proves the CalloutBase
    /// pattern (state machine, watchdogs, cleanup) before any real content
    /// is built on top of it. Not a real callout type - permanent callout
    /// types get their own folder under Callouts/, e.g. Callouts/CarAccident/.
    /// </summary>
    public class TestCallout : CalloutBase
    {
        protected override string DispatchMessage => "Test Callout: Car accident on Vinewood Blvd.";

        protected override void OnSceneArrived()
        {
            Vector3 position = Game.LocalPlayer.Character.Position + new Vector3(2f, 0f, 0f);
            SpawnPed(position);
            AdvanceToAssessment();
        }

        protected override bool IsAssessmentComplete()
        {
            return InputManager.IsActionPressed(InputAction.ConfirmMenuOption);
        }

        protected override void OnResolved()
        {
            Log.Info($"TestCallout [{InstanceId}]: resolved.");
        }
    }
}
