using System;
using System.Collections.Generic;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandInput
    {
        private readonly string[] _arguments;

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
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException(
                    "Command name is required.",
                    nameof(commandName)
                );

            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            CommandName = commandName;
            _arguments = new string[arguments.Count];

            for (
                int index = 0;
                index < arguments.Count;
                index++
            )
            {
                _arguments[index] = arguments[index];
            }
        }
    }
}
