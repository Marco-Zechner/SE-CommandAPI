using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;

namespace MarcoZechner.CommandApi.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            var tests =
                new List<TestCase>
                {
                    new TestCase(
                        "Enumerate definitions in registration order",
                        EnumerateDefinitionsInRegistrationOrder
                    ),
                    new TestCase(
                        "Help lists only executable visible commands",
                        HelpListsOnlyExecutableVisibleCommands
                    ),
                    new TestCase(
                        "Help resolves aliases to detailed metadata",
                        HelpResolvesAliasesToDetailedMetadata
                    ),
                    new TestCase(
                        "Status uses live registry count and provider snapshot",
                        StatusUsesLiveRegistryCountAndProviderSnapshot
                    )
                };

            int failures = 0;

            foreach (TestCase test in tests)
            {
                try
                {
                    test.Action();
                    Console.WriteLine("PASS " + test.Name);
                }
                catch (Exception exception)
                {
                    failures++;

                    Console.WriteLine(
                        "FAIL "
                        + test.Name
                        + ": "
                        + exception.Message
                    );
                }
            }

            Console.WriteLine(
                failures == 0
                    ? "OK built-in tests passed: " + tests.Count
                    : "FAILED built-in tests: "
                        + failures
                        + " of "
                        + tests.Count
            );

            return failures == 0 ? 0 : 1;
        }

        private static void EnumerateDefinitionsInRegistrationOrder()
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

        private static void HelpListsOnlyExecutableVisibleCommands()
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
                "Available commands: 4",
                result.Summary,
                "summary"
            );

            SequenceEqual(
                new[]
                {
                    "help - Lists available commands.",
                    "ping - Tests CommandAPI request execution.",
                    "whoami - Reports your server-derived identity.",
                    "status - Reports CommandAPI status."
                },
                result.DetailLines,
                "detail lines"
            );
        }

        private static void HelpResolvesAliasesToDetailedMetadata()
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

        private static void StatusUsesLiveRegistryCountAndProviderSnapshot()
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
                    "CommandAPI version: 0.1.0",
                    "Protocol version: 1.0.0",
                    "Registered commands: 5",
                    "Network: Not initialized",
                    "RichHudChat: unavailable",
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
                            "1.0.0",
                            "Not initialized",
                            false,
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
            CommandExecutionLocation location
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
                "CommandAPI.Tests",
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

        private sealed class TestCase
        {
            public string Name { get; }

            public Action Action { get; }

            public TestCase(
                string name,
                Action action
            )
            {
                Name = name;
                Action = action;
            }
        }
    }
}
