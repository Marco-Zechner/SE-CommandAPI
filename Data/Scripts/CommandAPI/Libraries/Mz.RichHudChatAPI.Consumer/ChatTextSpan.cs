using System;

namespace Mz.RichHudChatApi
{
    public sealed class ChatTextSpan
    {
        public int Start { get; }

        public int Length { get; }

        public ChatTextStyle Style { get; }

        public ChatTextSpan(
            int start,
            int length,
            ChatTextStyle style
        )
        {
            if (start < 0)
                throw new ArgumentException(
                    "A text span cannot start before the text.",
                    nameof(start)
                );

            if (length <= 0)
                throw new ArgumentException(
                    "A text span must contain at least one character.",
                    nameof(length)
                );

            if (start > int.MaxValue - length)
                throw new ArgumentException(
                    "The text span range is too large.",
                    nameof(length)
                );

            if (!Enum.IsDefined(typeof(ChatTextStyle), style))
                throw new ArgumentException(
                    "The text style is not supported.",
                    nameof(style)
                );

            Start = start;
            Length = length;
            Style = style;
        }
    }
}