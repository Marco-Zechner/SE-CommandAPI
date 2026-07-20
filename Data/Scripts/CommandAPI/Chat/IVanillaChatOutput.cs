namespace MarcoZechner.CommandApi.Chat
{
    public interface IVanillaChatOutput
    {
        void WriteLine(
            string author,
            string message
        );
    }
}
