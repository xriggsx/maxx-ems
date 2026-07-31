namespace EmsMod.Callouts
{
    public enum CalloutState
    {
        Dispatched,
        AwaitingAcceptDecline,
        EnRoute,
        OnScene,
        Assessment,
        Resolution,
        CleaningUp,
        CleanedUp,
        Declined,
        Abandoned
    }
}
