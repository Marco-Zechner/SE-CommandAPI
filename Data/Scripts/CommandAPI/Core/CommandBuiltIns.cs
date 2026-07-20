using System;
using System.Collections.Generic;

namespace MarcoZechner.CommandApi.Core
{
    public static class CommandBuiltIns
    {
        private const string OwnerId = "CommandAPI";

        public static bool TryRegister(
            CommandRegistry registry,
            CommandStatusProvider statusProvider,
            out string errorMessage
        )
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (statusProvider == null)
                throw new ArgumentNullException(
                    nameof(statusProvider)
                );

            CommandDefinition existing;

            if (registry.TryResolve("help", out existing))
            {
                errorMessage =
                    "Command name 'help' is already registered.";

                return false;
            }

            if (registry.TryResolve("status", out existing))
            {
                errorMessage =
                    "Command name 'status' is already registered.";

                return false;
            }

            var help =
                new CommandDefinition(
                    "help",
                    new string[0],
                    "Lists available commands.",
                    "Lists commands you can execute or shows detailed help for one command.",
                    "help [command]",
                    "CommandAPI",
                    CommandExecutionLocation.Either,
                    0,
                    OwnerId,
                    delegate(
                        CommandExecutionContext context,
                        CommandInput input
                    )
                    {
                        return BuildHelpResult(
                            registry,
                            context,
                            input
                        );
                    }
                );

            var status =
                new CommandDefinition(
                    "status",
                    new string[0],
                    "Reports CommandAPI status.",
                    "Reports current CommandAPI runtime and integration state.",
                    "status",
                    "CommandAPI",
                    CommandExecutionLocation.Either,
                    0,
                    OwnerId,
                    delegate(
                        CommandExecutionContext context,
                        CommandInput input
                    )
                    {
                        return BuildStatusResult(
                            registry,
                            statusProvider,
                            input
                        );
                    }
                );

            if (!registry.TryRegister(help, out errorMessage))
                return false;

            if (!registry.TryRegister(status, out errorMessage))
                return false;

            errorMessage = null;
            return true;
        }

        private static CommandResult BuildHelpResult(
            CommandRegistry registry,
            CommandExecutionContext context,
            CommandInput input
        )
        {
            string[] arguments =
                input.Arguments;

            if (arguments.Length > 1)
            {
                return Failure(
                    "Invalid help request",
                    "Help accepts at most one command name.",
                    "/cmd help [command]"
                );
            }

            if (arguments.Length == 1)
            {
                CommandDefinition definition;

                if (
                    !registry.TryResolve(
                        arguments[0],
                        out definition
                    )
                    || !CanExecute(definition, context)
                )
                {
                    return Failure(
                        "Unknown command",
                        "Unknown command '"
                            + arguments[0]
                            + "'.",
                        "/cmd help"
                    );
                }

                return BuildDetailedHelp(definition);
            }

            CommandDefinition[] definitions =
                registry.GetDefinitions();

            var lines =
                new List<string>();

            for (
                int index = 0;
                index < definitions.Length;
                index++
            )
            {
                CommandDefinition definition =
                    definitions[index];

                if (!CanExecute(definition, context))
                    continue;

                lines.Add(
                    definition.CanonicalName
                    + " - "
                    + definition.ShortDescription
                );
            }

            return new CommandResult(
                true,
                "Command help",
                "Available commands: " + lines.Count,
                lines.ToArray(),
                CommandSeverity.Information,
                null
            );
        }

        private static CommandResult BuildDetailedHelp(
            CommandDefinition definition
        )
        {
            var lines =
                new List<string>();

            lines.Add(
                "Usage: /cmd " + definition.Usage
            );

            string[] aliases =
                definition.Aliases;

            if (aliases.Length > 0)
            {
                lines.Add(
                    "Aliases: " + JoinNames(aliases)
                );
            }

            lines.Add(
                "Category: " + definition.Category
            );

            if (
                !string.IsNullOrWhiteSpace(
                    definition.HelpText
                )
            )
            {
                lines.Add(definition.HelpText);
            }

            return new CommandResult(
                true,
                "Help: " + definition.CanonicalName,
                definition.ShortDescription,
                lines.ToArray(),
                CommandSeverity.Information,
                null
            );
        }

        private static CommandResult BuildStatusResult(
            CommandRegistry registry,
            CommandStatusProvider statusProvider,
            CommandInput input
        )
        {
            if (input.Arguments.Length != 0)
            {
                return Failure(
                    "Invalid status request",
                    "Status does not accept arguments.",
                    "/cmd status"
                );
            }

            CommandStatusSnapshot snapshot =
                statusProvider();

            if (snapshot == null)
            {
                return Failure(
                    "Status unavailable",
                    "CommandAPI status is currently unavailable.",
                    null
                );
            }

            string richHudState =
                snapshot.RichHudChatAvailable
                    ? "available"
                    : "unavailable";

            return new CommandResult(
                true,
                "CommandAPI status",
                "CommandAPI is running.",
                new[]
                {
                    "CommandAPI version: "
                        + snapshot.CommandApiVersion,
                    "Protocol version: "
                        + snapshot.ProtocolVersion,
                    "Registered commands: "
                        + registry.Count,
                    "Network: "
                        + snapshot.NetworkState,
                    "RichHudChat: "
                        + richHudState,
                    "Presentation: "
                        + snapshot.PresentationAdapter,
                    "External providers: "
                        + snapshot.ExternalProviderCount
                },
                CommandSeverity.Information,
                null
            );
        }

        private static bool CanExecute(
            CommandDefinition definition,
            CommandExecutionContext context
        )
        {
            if (
                context.PermissionLevel
                < definition.PermissionRequirement
            )
            {
                return false;
            }

            if (
                definition.ExecutionLocation
                    == CommandExecutionLocation.Internal
            )
            {
                return false;
            }

            if (
                definition.ExecutionLocation
                    == CommandExecutionLocation.Server
                && !context.IsServer
            )
            {
                return false;
            }

            if (
                definition.ExecutionLocation
                    == CommandExecutionLocation.Client
                && context.IsServer
            )
            {
                return false;
            }

            return true;
        }

        private static string JoinNames(
            string[] names
        )
        {
            string value =
                names[0];

            for (
                int index = 1;
                index < names.Length;
                index++
            )
            {
                value += ", " + names[index];
            }

            return value;
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
