namespace EmsMod.Input
{
    public enum InputAction
    {
        AcceptCallout,
        DeclineCallout,
        Interact,
        ConfirmMenuOption,

        // Menu navigation (duty menu now; reused by other menus/wheels later).
        OpenDutyMenu,
        MenuUp,
        MenuDown,
        MenuLeft,
        MenuRight,
        MenuAccept,
        MenuBack
    }
}
