namespace Mz.RichHudChatApi
{
    public sealed class ChatParticipantRegistration
    {
        public string DisplayName { get; }

        public bool ReplacesVanillaChat { get; }

        public ChatParticipantRegistration(
            string displayName = null,
            bool replacesVanillaChat = false
        )
        {
            DisplayName =
                string.IsNullOrWhiteSpace(displayName)
                    ? null
                    : displayName.Trim();

            ReplacesVanillaChat =
                replacesVanillaChat;
        }
    }
}