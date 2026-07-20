using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandRegistryTests
    {
        [Fact]
        public void ResolveCanonicalNameCaseInsensitively()
        {
            var registry =
                new CommandRegistry();

            string errorMessage;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "echo",
                        new[] { "say" },
                        NoOpHandler
                    ),
                    out errorMessage
                ),
                errorMessage
            );

            CommandDefinition resolved;

            True(
                registry.TryResolve(
                    "EcHo",
                    out resolved
                ),
                "Canonical lookup failed."
            );

            Equal(
                "echo",
                resolved.CanonicalName,
                "canonical name"
            );
        }

        [Fact]
        public void ResolveAliasCaseInsensitively()
        {
            var registry =
                new CommandRegistry();

            string errorMessage;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "echo",
                        new[] { "say" },
                        NoOpHandler
                    ),
                    out errorMessage
                ),
                errorMessage
            );

            CommandDefinition resolved;

            True(
                registry.TryResolve(
                    "SAY",
                    out resolved
                ),
                "Alias lookup failed."
            );

            Equal(
                "echo",
                resolved.CanonicalName,
                "canonical name"
            );
        }

        [Fact]
        public void RejectDuplicateCanonicalName()
        {
            var registry =
                new CommandRegistry();

            string firstError;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "echo",
                        new string[0],
                        NoOpHandler
                    ),
                    out firstError
                ),
                firstError
            );

            string duplicateError;

            False(
                registry.TryRegister(
                    CreateDefinition(
                        "ECHO",
                        new string[0],
                        NoOpHandler
                    ),
                    out duplicateError
                ),
                "Duplicate registration unexpectedly succeeded."
            );

            Equal(
                "Command name 'echo' is already registered.",
                duplicateError,
                "duplicate error"
            );
        }

        [Fact]
        public void RejectAliasCollision()
        {
            var registry =
                new CommandRegistry();

            string firstError;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "echo",
                        new[] { "say" },
                        NoOpHandler
                    ),
                    out firstError
                ),
                firstError
            );

            string collisionError;

            False(
                registry.TryRegister(
                    CreateDefinition(
                        "say",
                        new string[0],
                        NoOpHandler
                    ),
                    out collisionError
                ),
                "Alias collision unexpectedly succeeded."
            );

            Equal(
                "Command name 'say' is already registered.",
                collisionError,
                "collision error"
            );
        }

        [Fact]
        public void HandlerReceivesTrustedContextAndReturnsStructuredResult()
        {
            CommandExecutionContext observedContext = null;
            CommandInput observedInput = null;

            CommandHandler handler =
                delegate(
                    CommandExecutionContext context,
                    CommandInput input
                )
                {
                    observedContext = context;
                    observedInput = input;

                    return new CommandResult(
                        true,
                        "Echo",
                        "Command completed.",
                        new[] { input.Arguments[0] },
                        CommandSeverity.Success,
                        null
                    );
                };

            var registry =
                new CommandRegistry();

            string errorMessage;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "echo",
                        new string[0],
                        handler
                    ),
                    out errorMessage
                ),
                errorMessage
            );

            CommandDefinition definition;

            True(
                registry.TryResolve(
                    "echo",
                    out definition
                ),
                "Registered command was not resolved."
            );

            var context =
                new CommandExecutionContext(
                    "request-42",
                    76561198000000042UL,
                    8123L,
                    "Marco",
                    3,
                    true
                );

            var input =
                new CommandInput(
                    "echo",
                    new[] { "MiXeD Value" }
                );

            CommandResult result =
                definition.Handler(
                    context,
                    input
                );

            Same(
                context,
                observedContext,
                "execution context"
            );

            Same(
                input,
                observedInput,
                "command input"
            );

            Equal(
                "request-42",
                observedContext.RequestId,
                "request id"
            );

            Equal(
                76561198000000042UL,
                observedContext.RequesterSteamId,
                "Steam ID"
            );

            Equal(
                8123L,
                observedContext.RequesterIdentityId,
                "identity ID"
            );

            Equal(
                "Marco",
                observedContext.RequesterDisplayName,
                "display name"
            );

            Equal(
                3,
                observedContext.PermissionLevel,
                "permission level"
            );

            True(
                observedContext.IsServer,
                "Server execution flag was false."
            );

            True(
                result.IsSuccess,
                "Structured result was not successful."
            );

            Equal(
                "Echo",
                result.Title,
                "result title"
            );

            Equal(
                "Command completed.",
                result.Summary,
                "result summary"
            );

            Equal(
                CommandSeverity.Success,
                result.Severity,
                "result severity"
            );

            Equal(
                "MiXeD Value",
                result.DetailLines[0],
                "result detail"
            );

            Equal(
                "CommandAPI",
                definition.OwnerId,
                "owner"
            );

            Equal(
                CommandExecutionLocation.Server,
                definition.ExecutionLocation,
                "execution location"
            );
        }

        private static CommandDefinition CreateDefinition(
            string canonicalName,
            string[] aliases,
            CommandHandler handler
        )
        {
            return new CommandDefinition(
                canonicalName,
                aliases,
                "Echoes text.",
                "Returns the supplied text to the requester.",
                "echo <text>",
                "Utility",
                CommandExecutionLocation.Server,
                0,
                "CommandAPI",
                handler
            );
        }

        private static CommandResult NoOpHandler(
            CommandExecutionContext context,
            CommandInput input
        )
        {
            return new CommandResult(
                true,
                "OK",
                "Completed.",
                new string[0],
                CommandSeverity.Success,
                null
            );
        }

        private static void True(
            bool condition,
            string message
        )
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void False(
            bool condition,
            string message
        )
        {
            if (condition)
                throw new InvalidOperationException(message);
        }

        private static void Same(
            object expected,
            object actual,
            string label
        )
        {
            if (!ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    label + ": references differ"
                );
            }
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
