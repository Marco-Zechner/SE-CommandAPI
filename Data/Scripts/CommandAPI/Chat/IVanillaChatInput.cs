namespace MarcoZechner.CommandApi.Chat
{
    public interface IVanillaChatInput
    {
        event VanillaChatMessageEnteredHandler MessageEntered;
    }
}
