using System;
using System.Collections.Generic;
using System.Text;

namespace MarcoZechner.CommandApi.Core
{
    public static class CommandInputParser
    {
        public const string Prefix = "/cmd";

        public static CommandParseResult Parse(
            string text
        )
        {
            return Parse(
                text,
                Prefix
            );
        }

        public static CommandParseResult Parse(
            string text,
            string prefix
        )
        {
            if (string.IsNullOrEmpty(text))
                return CommandParseResult.NotCommand();

            string normalizedPrefix =
                CommandInput.NormalizePrefix(prefix);

            int prefixStart = 0;

            while (
                prefixStart < text.Length
                && char.IsWhiteSpace(text[prefixStart])
            )
            {
                prefixStart++;
            }

            if (
                text.Length - prefixStart
                    < normalizedPrefix.Length
                || string.Compare(
                    text,
                    prefixStart,
                    normalizedPrefix,
                    0,
                    normalizedPrefix.Length,
                    StringComparison.OrdinalIgnoreCase
                ) != 0
            )
            {
                return CommandParseResult.NotCommand();
            }

            int contentStart =
                prefixStart + normalizedPrefix.Length;

            if (
                contentStart < text.Length
                && !char.IsWhiteSpace(text[contentStart])
            )
            {
                return CommandParseResult.NotCommand();
            }

            List<string> tokens;
            string errorMessage;

            if (
                !TryTokenize(
                    text,
                    contentStart,
                    out tokens,
                    out errorMessage
                )
            )
            {
                return CommandParseResult.Error(
                    errorMessage
                );
            }

            if (
                tokens.Count == 0
                || string.IsNullOrWhiteSpace(tokens[0])
            )
            {
                return CommandParseResult.Error(
                    "A command name is required."
                );
            }

            string commandName =
                tokens[0].ToLowerInvariant();

            var arguments =
                new List<string>(
                    Math.Max(0, tokens.Count - 1)
                );

            for (
                int index = 1;
                index < tokens.Count;
                index++
            )
            {
                arguments.Add(tokens[index]);
            }

            return CommandParseResult.Success(
                new CommandInput(
                    normalizedPrefix,
                    commandName,
                    arguments
                )
            );
        }

        private static bool TryTokenize(
            string text,
            int startIndex,
            out List<string> tokens,
            out string errorMessage
        )
        {
            tokens = new List<string>();
            errorMessage = null;

            var current =
                new StringBuilder();

            bool inQuotes = false;
            bool tokenStarted = false;

            for (
                int index = startIndex;
                index < text.Length;
                index++
            )
            {
                char character =
                    text[index];

                if (
                    inQuotes
                    && character == '\\'
                    && index + 1 < text.Length
                    && (
                        text[index + 1] == '"'
                        || text[index + 1] == '\\'
                    )
                )
                {
                    current.Append(text[index + 1]);
                    tokenStarted = true;
                    index++;
                    continue;
                }

                if (character == '"')
                {
                    inQuotes = !inQuotes;
                    tokenStarted = true;
                    continue;
                }

                if (
                    !inQuotes
                    && char.IsWhiteSpace(character)
                )
                {
                    if (tokenStarted)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        tokenStarted = false;
                    }

                    continue;
                }

                current.Append(character);
                tokenStarted = true;
            }

            if (inQuotes)
            {
                errorMessage =
                    "Quoted argument is not terminated.";

                return false;
            }

            if (tokenStarted)
                tokens.Add(current.ToString());

            return true;
        }
    }
}
