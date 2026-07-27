using System;
using System.Collections.Generic;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandInput
    {
        private readonly string[] _arguments;

        public string Prefix { get; }

        public string CommandName { get; }

        public string[] Arguments
        {
            get
            {
                var copy =
                    new string[_arguments.Length];

                for (
                    int index = 0;
                    index < _arguments.Length;
                    index++
                )
                {
                    copy[index] = _arguments[index];
                }

                return copy;
            }
        }

        public CommandInput(
            string commandName,
            IList<string> arguments
        )
            : this(
                CommandInputParser.Prefix,
                commandName,
                arguments
            )
        {
        }

        public CommandInput(
            string prefix,
            string commandName,
            IList<string> arguments
        )
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException(
                    "Command name is required.",
                    nameof(commandName)
                );
            }

            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            Prefix = NormalizePrefix(prefix);
            CommandName = commandName;
            _arguments = new string[arguments.Count];

            for (
                int index = 0;
                index < arguments.Count;
                index++
            )
            {
                if (arguments[index] == null)
                {
                    throw new ArgumentException(
                        "Command arguments cannot contain null values.",
                        nameof(arguments)
                    );
                }

                _arguments[index] = arguments[index];
            }
        }

        internal static string NormalizePrefix(
            string prefix
        )
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException(
                    "A command prefix is required.",
                    nameof(prefix)
                );
            }

            string normalized =
                prefix.Trim();

            if (
                normalized.Length < 2
                || normalized[0] != '/'
            )
            {
                throw new ArgumentException(
                    "A command prefix must begin with '/'.",
                    nameof(prefix)
                );
            }

            for (
                int index = 1;
                index < normalized.Length;
                index++
            )
            {
                if (char.IsWhiteSpace(normalized[index]))
                {
                    throw new ArgumentException(
                        "A command prefix cannot contain whitespace.",
                        nameof(prefix)
                    );
                }
            }

            return normalized.ToLowerInvariant();
        }
    }
}
