using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandBuiltInsTests
    {
        [Fact]
        public void EnumerateDefinitionsInRegistrationOrder()
        {
            var registry =
                new CommandRegistry();

            Register(
                registry,
                Definition(
                    "first",
                    new string[0],
                    0,
                    CommandExecutionLocation.Server
                )
            );

            Register(
                registry,
                Definition(
                    "second",
                    new string[0],
                    0,
                    CommandExecutionLocation.Server
                )
            );

            CommandDefinition[] firstRead =
                registry.GetDefinitions();

            Equal(
                2,
                firstRead.Length,
                "definition count"
            );

            Equal(
                "first",
                firstRead[0].CanonicalName,
                "first definition"
            );

            Equal(
                "second",
                firstRead[1].CanonicalName,
                "second definition"
            );

            firstRead[0] = null;

            Equal(
                "first",
                registry.GetDefinitions()[0].CanonicalName,
                "stored definition"
            );
        }

        [Fact]
        public void HelpListsOnlyExecutableVisibleCommands()
        {
            var registry =
                new CommandRegistry();


            Register(
                registry,
                Definition(
                    "admin",
                    new string[0],
                    3,
                    CommandExecutionLocation.Server
                )
            );

            Register(
                registry,
                Definition(
                    "clientonly",
                    new string[0],
                    0,
                    CommandExecutionLocation.Client
                )
            );

            Register(
                registry,
                Definition(
                    "internal",
                    new string[0],
                    0,
                    CommandExecutionLocation.Internal
                )
            );

            RegisterBuiltIns(registry);

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    ServerContext(0),
                    new CommandInput(
                        "help",
                        new string[0]
                    )
                );

            True(
                result.IsSuccess,
                "Help command failed."
            );

            Equal(
                "Command help",
                result.Title,
                "title"
            );

            Equal(
                "Available commands: 5",
                result.Summary,
                "summary"
            );

            SequenceEqual(
                new[]
                {
                    "help - Lists available commands.",
                    "ping - Tests CommandAPI request execution.",
                    "whoami - Reports your server-derived identity.",
                    "status - Reports CommandAPI status.",
                    "mods - Lists mods with registered commands."
                },
                result.DetailLines,
                "detail lines"
            );
        }

        [Fact]
        public void ClientHelpListsCommandsThatCanBeSubmittedFromClient()
        {
            var registry = new CommandRegistry();

            Register(registry, Definition("admin", new string[0], 3, CommandExecutionLocation.Server));
            Register(registry, Definition("clientonly", new string[0], 0, CommandExecutionLocation.Client));
            Register(registry, Definition("internal", new string[0], 0, CommandExecutionLocation.Internal));
            RegisterBuiltIns(registry);

            var executor = new CommandExecutor(registry);
            CommandResult result = executor.Execute(ClientContext(0), new CommandInput("help", new string[0]));

            True(result.IsSuccess, "Help command failed.");
            Equal("Available commands: 6", result.Summary, "summary");
            SequenceEqual(
                new[]
                {
                    "clientonly - clientonly description",
                    "help - Lists available commands.",
                    "ping - Tests CommandAPI request execution.",
                    "whoami - Reports your server-derived identity.",
                    "status - Reports CommandAPI status.",
                    "mods - Lists mods with registered commands."
                },
                result.DetailLines,
                "detail lines"
            );
        }

        [Fact]
        public void HelpResolvesAliasesToDetailedMetadata()
        {
            var registry =
                new CommandRegistry();

            Register(
                registry,
                new CommandDefinition(
                    "echo",
                    new[] { "say" },
                    "Echoes supplied text.",
                    "Returns text without changing its casing.",
                    "echo <text>",
                    "Utility",
                    CommandExecutionLocation.Server,
                    0,
                    "ExampleMod",
                    NoOpHandler
                )
            );

            RegisterBuiltIns(registry);

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    ServerContext(0),
                    new CommandInput(
                        "help",
                        new[] { "SAY" }
                    )
                );

            True(
                result.IsSuccess,
                "Detailed help failed."
            );

            Equal(
                "Help: echo",
                result.Title,
                "title"
            );

            Equal(
                "Echoes supplied text.",
                result.Summary,
                "summary"
            );

            SequenceEqual(
                new[]
                {
                    "Usage: /cmd echo <text>",
                    "Aliases: say",
                    "Category: Utility",
                    "Returns text without changing its casing."
                },
                result.DetailLines,
                "detail lines"
            );
        }

        [Fact]
        public void ModsListsDistinctOwnersAndLiveCommandCounts()
        {
            var registry = new CommandRegistry();
            Register(registry, Definition("alpha", new string[0], 0, CommandExecutionLocation.Client, "Example.One"));
            Register(registry, Definition("beta", new string[0], 0, CommandExecutionLocation.Server, "Example.One"));
            Register(registry, Definition("gamma", new string[0], 0, CommandExecutionLocation.Server, "Example.Two"));
            RegisterBuiltIns(registry);

            var executor = new CommandExecutor(registry);
            CommandResult result = executor.Execute(ClientContext(0), new CommandInput("mods", new string[0]));

            True(result.IsSuccess, "Mods command failed.");
            Equal("Registered mods", result.Title, "title");
            Equal("Mods with registered commands: 3. Page 1/1.", result.Summary, "summary");
            SequenceEqual(
                new[]
                {
                    "CommandAPI [/cmd] - 5 commands",
                    "Example.One [/cmd] - 2 commands",
                    "Example.Two [/cmd] - 1 command"
                },
                result.DetailLines,
                "detail lines"
            );
        }
        [Fact]
        public void ModsListsAllPrefixesOwnedByMod()
        {
            var registry = new CommandRegistry();
            Register(registry, "/alpha", Definition("one", new string[0], 0, CommandExecutionLocation.Client, "Example.Mod"));
            Register(registry, "/beta", Definition("two", new string[0], 0, CommandExecutionLocation.Client, "Example.Mod"));
            RegisterBuiltIns(registry);

            var executor = new CommandExecutor(registry);
            CommandResult result = executor.Execute(ClientContext(0), new CommandInput("mods", new string[0]));

            True(result.IsSuccess, "Mods command failed.");
            SequenceEqual(
                new[]
                {
                    "CommandAPI [/cmd] - 5 commands",
                    "Example.Mod [/alpha, /beta] - 2 commands"
                },
                result.DetailLines,
                "detail lines"
            );
        }
        [Fact]
        public void ModsPagesLongProviderListsWithoutExceedingVanillaDetailLimit()
        {
            var registry = new CommandRegistry();

            for (int index = 1; index <= 9; index++)
                Register(registry, Definition("command" + index, new string[0], 0, CommandExecutionLocation.Client, "Example." + index.ToString("00")));

            RegisterBuiltIns(registry);

            var executor = new CommandExecutor(registry);
            CommandResult first = executor.Execute(ClientContext(0), new CommandInput("mods", new string[0]));
            CommandResult second = executor.Execute(ClientContext(0), new CommandInput("mods", new[] { "2" }));

            True(first.IsSuccess, "First mods page failed.");
            Equal("Mods with registered commands: 10. Page 1/2.", first.Summary, "first summary");
            Equal(8, first.DetailLines.Length, "first page detail count");
            Equal("CommandAPI [/cmd] - 5 commands", first.DetailLines[0], "first page first line");
            Equal("Example.07 [/cmd] - 1 command", first.DetailLines[7], "first page last line");
            Equal("/cmd mods 2", first.UsageHint, "first page next-page hint");

            True(second.IsSuccess, "Second mods page failed.");
            Equal("Mods with registered commands: 10. Page 2/2.", second.Summary, "second summary");
            SequenceEqual(new[] { "Example.08 [/cmd] - 1 command", "Example.09 [/cmd] - 1 command" }, second.DetailLines, "second page");
            Equal<string>(null, second.UsageHint, "second page next-page hint");
        }

        [Fact]
        public void ModsRejectsInvalidPage()
        {
            var registry = new CommandRegistry();
            RegisterBuiltIns(registry);

            var executor = new CommandExecutor(registry);
            CommandResult zero = executor.Execute(ClientContext(0), new CommandInput("mods", new[] { "0" }));
            CommandResult missing = executor.Execute(ClientContext(0), new CommandInput("mods", new[] { "2" }));
            CommandResult extra = executor.Execute(ClientContext(0), new CommandInput("mods", new[] { "1", "2" }));

            True(!zero.IsSuccess, "Page zero unexpectedly succeeded.");
            Equal("/cmd mods [page]", zero.UsageHint, "page zero usage");
            True(!missing.IsSuccess, "Out-of-range page unexpectedly succeeded.");
            Equal("Invalid mods page", missing.Title, "out-of-range title");
            True(!extra.IsSuccess, "Multiple page arguments unexpectedly succeeded.");
        }
        [Fact]
        public void StatusUsesLiveRegistryCountAndProviderSnapshot()
        {
            var registry =
                new CommandRegistry();


            RegisterBuiltIns(registry);

            Register(
                registry,
                Definition(
                    "late",
                    new string[0],
                    0,
                    CommandExecutionLocation.Server
                )
            );

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    ServerContext(0),
                    new CommandInput(
                        "status",
                        new string[0]
                    )
                );

            True(
                result.IsSuccess,
                "Status command failed."
            );

            Equal(
                "CommandAPI status",
                result.Title,
                "title"
            );

            SequenceEqual(
                new[]
                {
                    "CommandAPI mod version: 0.1.0",
                    "CommandAPI API version: 1.2.0",
                    "Protocol version: 1.0.0",
                    "Registered commands: 6",
                    "Network: Not initialized",
                    "Presentation: VanillaChat",
                    "External providers: 0"
                },
                result.DetailLines,
                "detail lines"
            );
        }

        private static void RegisterBuiltIns(
            CommandRegistry registry
        )
        {
            string errorMessage;

            True(
                CommandBuiltIns.TryRegister(
                    registry,
                    delegate
                    {
                        return new CommandStatusSnapshot(
                            "0.1.0",
                            "1.2.0",
                            "1.0.0",
                            "Not initialized",
                            "VanillaChat",
                            0
                        );
                    },
                    out errorMessage
                ),
                errorMessage
            );
        }

        private static CommandDefinition Definition(
            string canonicalName,
            string[] aliases,
            int permissionRequirement,
            CommandExecutionLocation location,
            string ownerId = "CommandAPI.Tests"
        )
        {
            return new CommandDefinition(
                canonicalName,
                aliases,
                canonicalName + " description",
                canonicalName + " detailed help",
                canonicalName,
                "Tests",
                location,
                permissionRequirement,
                ownerId,
                NoOpHandler
            );
        }

        private static void Register(
            CommandRegistry registry,
            CommandDefinition definition
        )
        {
            string errorMessage;

            True(
                registry.TryRegister(
                    definition,
                    out errorMessage
                ),
                errorMessage
            );
        }

        private static void Register(CommandRegistry registry, string prefix, CommandDefinition definition)
        {
            string errorMessage;
            True(registry.TryRegister(prefix, definition, out errorMessage), errorMessage);
        }

        private static CommandExecutionContext ClientContext(int permissionLevel)
        {
            return new CommandExecutionContext("request-builtins", 76561198000000042UL, 42L, "Client Player", permissionLevel, false);
        }

        private static CommandExecutionContext ServerContext(
            int permissionLevel
        )
        {
            return new CommandExecutionContext(
                "request-builtins",
                76561198000000042UL,
                42L,
                "Server Player",
                permissionLevel,
                true
            );
        }

        private static CommandResult NoOpHandler(
            CommandExecutionContext context,
            CommandInput input
        )
        {
            return new CommandResult(
                true,
                "Completed",
                "Command completed.",
                new string[0],
                CommandSeverity.Success,
                null
            );
        }

        private static void SequenceEqual(
            string[] expected,
            string[] actual,
            string label
        )
        {
            Equal(
                expected.Length,
                actual.Length,
                label + " count"
            );

            for (
                int index = 0;
                index < expected.Length;
                index++
            )
            {
                Equal(
                    expected[index],
                    actual[index],
                    label + "[" + index + "]"
                );
            }
        }

        private static void True(
            bool condition,
            string message
        )
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Equal<T>(
            T expected,
            T actual,
            string label
        )
        {
            if (!EqualityComparer<T>.Default.Equals(
                expected,
                actual
            ))
            {
                throw new InvalidOperationException(
                    label
                    + ": expected "
                    + expected
                    + ", actual "
                    + actual
                );
            }
        }
    }
}
