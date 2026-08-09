namespace EmsMod.Config.Schema
{
    /// <summary>
    /// One line of an on-scene conversation: who's talking and what they say.
    /// Spoken via TTS and shown as a subtitle. Used for the injury assessment
    /// chat (patient describes the injury, player reassures) that the player
    /// advances one single tap at a time.
    /// </summary>
    public class DialogueLine
    {
        public string Speaker { get; set; } = "";

        public string Text { get; set; } = "";
    }
}
