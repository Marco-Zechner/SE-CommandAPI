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

            string[] builtInNames =
            {
                "help",
                "ping",
                "whoami",
                "status",
                "mods"
            };

            CommandDefinition existing;

            for (
                int index = 0;
                index < builtInNames.Length;
                index++
            )
            {
                string name =
                    builtInNames[index];

                if (registry.TryResolve(name, out existing))
                {
                    errorMessage =
                        "Command name '"
                        + name
                        + "' is already registered.";

                    return false;
                }
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

            var ping =
                new CommandDefinition(
                    "ping",
                    new string[0],
                    "Tests CommandAPI request execution.",
                    "Confirms that a command request reached the authoritative server.",
                    "ping",
                    "CommandAPI",
                    CommandExecutionLocation.Server,
                    0,
                    OwnerId,
                    BuildPingResult
                );

            var whoami =
                new CommandDefinition(
                    "whoami",
                    new string[0],
                    "Reports your server-derived identity.",
                    "Reports requester identity and permission data derived from trusted server state.",
                    "whoami",
                    "CommandAPI",
                    CommandExecutionLocation.Server,
                    0,
                    OwnerId,
                    BuildWhoAmIResult
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

            var mods = new CommandDefinition(
                "mods",
                new string[0],
                "Lists mods with registered commands.",
                "Lists CommandAPI and external mods that currently own one or more registered commands.",
                "mods [page]",
                "CommandAPI",
                CommandExecutionLocation.Either,
                0,
                OwnerId,
                delegate(CommandExecutionContext context, CommandInput input) { return BuildModsResult(registry, input); }
            );

            CommandDefinition[] definitions =
            {
                help,
                ping,
                whoami,
                status,
                mods
            };

            for (
                int index = 0;
                index < definitions.Length;
                index++
            )
            {
                if (
                    !registry.TryRegister(
                        definitions[index],
                        out errorMessage
                    )
                )
                {
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private static CommandResult BuildPingResult(
            CommandExecutionContext context,
            CommandInput input
        )
        {
            if (input.Arguments.Length != 0)
            {
                return Failure(
                    "Invalid ping request",
                    "Ping does not accept arguments.",
                    "/cmd ping"
                );
            }

            return new CommandResult(
                true,
                "Pong",
                "CommandAPI request completed.",
                new[]
                {
                    "Request ID: " + context.RequestId,
                    "Executed on: "
                        + (
                            context.IsServer
                                ? "server"
                                : "client"
                        )
                },
                CommandSeverity.Success,
                null
            );
        }

        private static CommandResult BuildWhoAmIResult(
            CommandExecutionContext context,
            CommandInput input
        )
        {
            if (input.Arguments.Length != 0)
            {
                return Failure(
                    "Invalid whoami request",
                    "Whoami does not accept arguments.",
                    "/cmd whoami"
                );
            }

            return new CommandResult(
                true,
                "Who am I",
                "Identity resolved by the server.",
                new[]
                {
                    "Display name: "
                        + context.RequesterDisplayName,
                    "Steam ID: "
                        + context.RequesterSteamId,
                    "Identity ID: "
                        + context.RequesterIdentityId,
                    "Permission level: "
                        + context.PermissionLevel,
                    "Executed on: "
                        + (
                            context.IsServer
                                ? "server"
                                : "client"
                        )
                },
                CommandSeverity.Information,
                null
            );
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
                    input.Prefix + " help [command]"
                );
            }

            if (arguments.Length == 1)
            {
                CommandDefinition definition;

                if (
                    !registry.TryResolve(
                        input.Prefix,
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
                        input.Prefix + " help"
                    );
                }

                return BuildDetailedHelp(input.Prefix, definition);
            }

            CommandDefinition[] definitions =
                registry.GetDefinitions(input.Prefix);

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
            string prefix,
            CommandDefinition definition
        )
        {
            var lines =
                new List<string>();

            lines.Add(
                "Usage: " + prefix + " " + definition.Usage
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

        private static CommandResult BuildModsResult(CommandRegistry registry, CommandInput input)
        {
            const int pageSize = 8;

            if (input.Arguments.Length > 1)
                return Failure("Invalid mods request", "Mods accepts at most one page number.", "/cmd mods [page]");

            int page = 1;
            if (input.Arguments.Length == 1 && (!int.TryParse(input.Arguments[0], out page) || page < 1))
                return Failure("Invalid mods request", "Page must be a positive whole number.", "/cmd mods [page]");

            var commandCountsByOwner = new Dictionary<string, int>(StringComparer.Ordinal);
            var prefixesByOwner = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            string[] registeredPrefixes = registry.GetPrefixes();

            for (int prefixIndex = 0; prefixIndex < registeredPrefixes.Length; prefixIndex++)
            {
                string prefix = registeredPrefixes[prefixIndex];
                CommandDefinition[] definitions = registry.GetDefinitions(prefix);

                for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
                {
                    string ownerId = definitions[definitionIndex].OwnerId;
                    int count;
                    commandCountsByOwner[ownerId] = commandCountsByOwner.TryGetValue(ownerId, out count) ? count + 1 : 1;

                    List<string> prefixes;
                    if (!prefixesByOwner.TryGetValue(ownerId, out prefixes))
                    {
                        prefixes = new List<string>();
                        prefixesByOwner.Add(ownerId, prefixes);
                    }

                    if (!prefixes.Contains(prefix))
                        prefixes.Add(prefix);
                }
            }

            var owners = new List<string>(commandCountsByOwner.Keys);
            owners.Sort(StringComparer.Ordinal);

            int pageCount = Math.Max(1, (owners.Count + pageSize - 1) / pageSize);
            if (page > pageCount)
                return Failure("Invalid mods page", "Page " + page + " does not exist. Available pages: 1-" + pageCount + ".", "/cmd mods [page]");

            int firstIndex = (page - 1) * pageSize;
            int lineCount = Math.Min(pageSize, owners.Count - firstIndex);
            var lines = new string[lineCount];

            for (int index = 0; index < lineCount; index++)
            {
                string ownerId = owners[firstIndex + index];
                int commandCount = commandCountsByOwner[ownerId];
                List<string> prefixes = prefixesByOwner[ownerId];
                prefixes.Sort(StringComparer.Ordinal);
                lines[index] = ownerId + " [" + string.Join(", ", prefixes.ToArray()) + "] - " + commandCount + (commandCount == 1 ? " command" : " commands");
            }

            return new CommandResult(
                true,
                "Registered mods",
                "Mods with registered commands: " + owners.Count + ". Page " + page + "/" + pageCount + ".",
                lines,
                CommandSeverity.Information,
                page < pageCount ? "/cmd mods " + (page + 1) : null
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

            return new CommandResult(
                true,
                "CommandAPI status",
                "CommandAPI is running.",
                new[]
                {
                    "CommandAPI mod version: "
                        + snapshot.ModVersion,
                    "CommandAPI API version: "
                        + snapshot.ApiVersion,
                    "Protocol version: "
                        + snapshot.ProtocolVersion,
                    "Registered commands: "
                        + registry.Count,
                    "Network: "
                        + snapshot.NetworkState,
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
