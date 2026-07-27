using System;

namespace Mz.RichHudChatApi
{
    public sealed class ChatCompanionItem
    {
        private readonly ChatTextSpan[] _primarySpans;

        public string Key { get; }

        public string PrimaryText { get; }

        public string SecondaryText { get; }

        public string CompletionText { get; }

        public ChatTextSpan[] PrimarySpans =>
            (ChatTextSpan[])_primarySpans.Clone();

        public ChatCompanionItem(
            string primaryText,
            string secondaryText = null,
            string completionText = null,
            string key = null,
            ChatTextSpan[] primarySpans = null
        )
        {
            if (string.IsNullOrWhiteSpace(primaryText))
                throw new ArgumentException(
                    "Primary companion text is required.",
                    nameof(primaryText)
                );

            Key = key;
            PrimaryText = primaryText;
            SecondaryText = secondaryText;
            CompletionText = completionText;
            _primarySpans = CopySpans(primarySpans);
        }

        internal static ChatTextSpan[] CopySpans(
            ChatTextSpan[] spans
        )
        {
            if (spans == null || spans.Length == 0)
                return new ChatTextSpan[0];

            var copy =
                new ChatTextSpan[spans.Length];

            for (int index = 0; index < spans.Length; index++)
            {
                if (spans[index] == null)
                    throw new ArgumentException(
                        "Text spans cannot contain null.",
                        nameof(spans)
                    );

                copy[index] = spans[index];
            }

            return copy;
        }
    }
}