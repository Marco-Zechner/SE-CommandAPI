using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class BasicBuiltInsTests
    {
        [Fact]
        public void RegisterPingAndWhoamiAsServerCommands()
        {
            CommandRegistry registry =
                CreateRegistryWithBuiltIns();

            CommandDefinition ping;
            CommandDefinition whoami;

            True(
                registry.TryResolve("ping", out ping),
                "Ping was not registered."
            );

            True(
                registry.TryResolve("whoami", out whoami),
                "Whoami was not registered."
            );

            Equal(
                CommandExecutionLocation.Server,
                ping.ExecutionLocation,
                "ping execution location"
            );

            Equal(
                CommandExecutionLocation.Server,
                whoami.ExecutionLocation,
                "whoami execution location"
            );
        }

        [Fact]
        public void PingReportsTrustedRequestExecution()
        {
            CommandRegistry registry =
                CreateRegistryWithBuiltIns();

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    Context(
                        "request-ping",
                        76561198000000042UL,
                        8123L,
                        "Marco",
                        3
                    ),
                    new CommandInput(
                        "ping",
                        new string[0]
                    )
                );

            True(
                result.IsSuccess,
                "Ping command failed."
            );

            Equal(
                "Pong",
                result.Title,
                "title"
            );

            Equal(
                "CommandAPI request completed.",
                result.Summary,
                "summary"
            );

            Equal(
                CommandSeverity.Success,
                result.Severity,
                "severity"
            );

            SequenceEqual(
                new[]
                {
                    "Request ID: request-ping",
                    "Executed on: server"
                },
                result.DetailLines,
                "detail lines"
            );
        }

        [Fact]
        public void WhoamiReportsTrustedRequesterIdentity()
        {
            CommandRegistry registry =
                CreateRegistryWithBuiltIns();

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    Context(
                        "request-whoami",
                        76561198000000042UL,
                        8123L,
                        "Marco",
                        3
                    ),
                    new CommandInput(
                        "whoami",
                        new string[0]
                    )
                );

            True(
                result.IsSuccess,
                "Whoami command failed."
            );

            Equal(
                "Who am I",
                result.Title,
                "title"
            );

            Equal(
                "Identity resolved by the server.",
                result.Summary,
                "summary"
            );

            Equal(
                CommandSeverity.Information,
                result.Severity,
                "severity"
            );

            SequenceEqual(
                new[]
                {
                    "Display name: Marco",
                    "Steam ID: 76561198000000042",
                    "Identity ID: 8123",
                    "Permission level: 3",
                    "Executed on: server"
                },
                result.DetailLines,
                "detail lines"
            );
        }

        private static CommandRegistry CreateRegistryWithBuiltIns()
        {
            var registry =
                new CommandRegistry();

            string errorMessage;

            True(
                CommandBuiltIns.TryRegister(
                    registry,
                    delegate
                    {
                        return new CommandStatusSnapshot(
                            "0.1.0",
                            "1.0.0",
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

            return registry;
        }

        private static CommandExecutionContext Context(
            string requestId,
            ulong steamId,
            long identityId,
            string displayName,
            int permissionLevel
        )
        {
            return new CommandExecutionContext(
                requestId,
                steamId,
                identityId,
                displayName,
                permissionLevel,
                true
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
