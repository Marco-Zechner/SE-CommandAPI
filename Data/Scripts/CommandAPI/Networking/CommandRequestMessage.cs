using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Networking
{
    public sealed class CommandRequestMessage
    {
        private readonly string[] _arguments;

        public string RequestId { get; }

        public string Prefix { get; }

        public string CommandName { get; }

        public string[] Arguments
        {
            get
            {
                return Copy(_arguments);
            }
        }

        public CommandRequestMessage(
            string requestId,
            string commandName,
            IList<string> arguments
        )
            : this(
                requestId,
                CommandInputParser.Prefix,
                commandName,
                arguments
            )
        {
        }

        public CommandRequestMessage(
            string requestId,
            string prefix,
            string commandName,
            IList<string> arguments
        )
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException(
                    "A request ID is required.",
                    nameof(requestId)
                );
            }

            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException(
                    "A command name is required.",
                    nameof(commandName)
                );
            }

            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            RequestId = requestId;
            Prefix = CommandInput.NormalizePrefix(prefix);
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

        private static string[] Copy(string[] source)
        {
            var copy = new string[source.Length];

            for (
                int index = 0;
                index < source.Length;
                index++
            )
            {
                copy[index] = source[index];
            }

            return copy;
        }
    }
}
