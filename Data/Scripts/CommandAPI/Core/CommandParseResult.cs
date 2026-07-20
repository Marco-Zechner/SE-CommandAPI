using System;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandParseResult
    {
        public CommandParseStatus Status { get; }

        public CommandInput Input { get; }

        public string ErrorMessage { get; }

        private CommandParseResult(
            CommandParseStatus status,
            CommandInput input,
            string errorMessage
        )
        {
            Status = status;
            Input = input;
            ErrorMessage = errorMessage;
        }

        public static CommandParseResult NotCommand()
        {
            return new CommandParseResult(
                CommandParseStatus.NotCommand,
                null,
                null
            );
        }

        public static CommandParseResult Success(
            CommandInput input
        )
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            return new CommandParseResult(
                CommandParseStatus.Success,
                input,
                null
            );
        }

        public static CommandParseResult Error(
            string errorMessage
        )
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                throw new ArgumentException(
                    "Error message is required.",
                    nameof(errorMessage)
                );

            return new CommandParseResult(
                CommandParseStatus.Error,
                null,
                errorMessage
            );
        }
    }
}
