using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Chat
{
    public delegate void VanillaChatCommandSubmitter(
        ulong senderId,
        CommandInput input
    );
}
