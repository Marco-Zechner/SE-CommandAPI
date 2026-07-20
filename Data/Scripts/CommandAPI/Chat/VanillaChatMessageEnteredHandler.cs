namespace MarcoZechner.CommandApi.Chat
{
    public delegate void VanillaChatMessageEnteredHandler(
        ulong senderId,
        string message,
        ref bool sendToOthers
    );
}
