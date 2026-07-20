using System;

namespace MarcoZechner.CommandApi.Core
{
    public sealed class CommandExecutor
    {
        private readonly CommandRegistry _registry;

        public CommandExecutor(
            CommandRegistry registry
        )
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            _registry = registry;
        }

        public CommandResult Execute(
            CommandExecutionContext context,
            CommandInput input
        )
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            CommandDefinition definition;

            if (
                !_registry.TryResolve(
                    input.CommandName,
                    out definition
                )
            )
            {
                return Failure(
                    "Unknown command",
                    "Unknown command '"
                        + input.CommandName
                        + "'.",
                    "/cmd help"
                );
            }

            if (
                context.PermissionLevel
                < definition.PermissionRequirement
            )
            {
                return Failure(
                    "Permission denied",
                    "You do not have permission to use '"
                        + definition.CanonicalName
                        + "'.",
                    null
                );
            }

            CommandResult locationFailure =
                ValidateExecutionLocation(
                    definition,
                    context
                );

            if (locationFailure != null)
                return locationFailure;

            try
            {
                CommandResult result =
                    definition.Handler(
                        context,
                        input
                    );

                if (result == null)
                {
                    return Failure(
                        "Command failed",
                        "The command could not be completed.",
                        null
                    );
                }

                return result;
            }
            catch (Exception)
            {
                return Failure(
                    "Command failed",
                    "The command could not be completed.",
                    null
                );
            }
        }

        private static CommandResult ValidateExecutionLocation(
            CommandDefinition definition,
            CommandExecutionContext context
        )
        {
            if (
                definition.ExecutionLocation
                    == CommandExecutionLocation.Server
                && !context.IsServer
            )
            {
                return Failure(
                    "Command unavailable",
                    "Command '"
                        + definition.CanonicalName
                        + "' must execute on the server.",
                    null
                );
            }

            if (
                definition.ExecutionLocation
                    == CommandExecutionLocation.Client
                && context.IsServer
            )
            {
                return Failure(
                    "Command unavailable",
                    "Command '"
                        + definition.CanonicalName
                        + "' must execute on the client.",
                    null
                );
            }

            if (
                definition.ExecutionLocation
                    == CommandExecutionLocation.Internal
            )
            {
                return Failure(
                    "Command unavailable",
                    "Command '"
                        + definition.CanonicalName
                        + "' is internal.",
                    null
                );
            }

            return null;
        }

        private static CommandResult Failure(
            string title,
            string summary,
            string usageHint
        )
        {
            return new CommandResult(
                false,
                title,
                summary,
                new string[0],
                CommandSeverity.Error,
                usageHint
            );
        }
    }
}
