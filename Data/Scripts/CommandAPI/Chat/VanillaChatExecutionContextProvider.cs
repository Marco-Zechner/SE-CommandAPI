using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Chat
{
    public delegate CommandExecutionContext
        VanillaChatExecutionContextProvider(
            ulong senderId
        );
}
