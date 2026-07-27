using System;

namespace Mz.RichHudChatApi
{
    public sealed class ChatCompanionState
    {
        private readonly ChatCompanionItem[] _items;
        private readonly ChatTextSpan[] _headerSpans;
        private readonly ChatTextSpan[] _inputSpans;

        public ChatCompanionItem[] Items =>
            (ChatCompanionItem[])_items.Clone();

        public int SelectedIndex { get; }

        public string HeaderText { get; }

        public ChatTextSpan[] HeaderSpans =>
            (ChatTextSpan[])_headerSpans.Clone();

        public string Footer { get; }

        public string ErrorText { get; }

        public ChatTextSpan[] InputSpans =>
            (ChatTextSpan[])_inputSpans.Clone();

        public ChatCompanionState(
            ChatCompanionItem[] items,
            int selectedIndex = -1,
            string headerText = null,
            ChatTextSpan[] headerSpans = null,
            string footer = null,
            string errorText = null,
            ChatTextSpan[] inputSpans = null
        )
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _items =
                new ChatCompanionItem[items.Length];

            for (int index = 0; index < items.Length; index++)
            {
                if (items[index] == null)
                    throw new ArgumentException(
                        "Companion items cannot contain null.",
                        nameof(items)
                    );

                _items[index] = items[index];
            }

            if (selectedIndex < -1)
                throw new ArgumentException(
                    "The selected index cannot be less than -1.",
                    nameof(selectedIndex)
                );

            if (
                selectedIndex >= items.Length
                && selectedIndex != -1
            )
            {
                throw new ArgumentException(
                    "The selected index is outside the companion items.",
                    nameof(selectedIndex)
                );
            }

            SelectedIndex = selectedIndex;
            HeaderText = headerText;
            Footer = footer;
            ErrorText = errorText;
            _headerSpans =
                ChatCompanionItem.CopySpans(headerSpans);

            _inputSpans =
                ChatCompanionItem.CopySpans(inputSpans);
        }
    }
}