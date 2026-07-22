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
        public void ListOnlyPrefixesWithRegisteredCommands()
        {
            var registry =
                new CommandRegistry();

            Action unregisterCommandApi;
            string commandApiError;

            True(
                registry.TryRegister(
                    "/cmd",
                    CreateDefinition(
                        "status",
                        new string[0],
                        NoOpHandler
                    ),
                    out unregisterCommandApi,
                    out commandApiError
                ),
                commandApiError
            );

            Action unregisterIme;
            string imeError;

            True(
                registry.TryRegister(
                    "/IME",
                    CreateDefinition(
                        "theme",
                        new string[0],
                        NoOpHandler
                    ),
                    out unregisterIme,
                    out imeError
                ),
                imeError
            );

            Assert.Equal(
                new[] { "/cmd", "/ime" },
                registry.GetPrefixes()
            );

            unregisterIme();

            Assert.Equal(
                new[] { "/cmd" },
                registry.GetPrefixes()
            );

            unregisterCommandApi();

            Assert.Equal(
                new string[0],
                registry.GetPrefixes()
            );
        }

        [Fact]
        public void IdenticalCommandNamesCanExistUnderDifferentPrefixes()
        {
            var registry =
                new CommandRegistry();

            CommandDefinition commandApiStatus =
                CreateDefinition(
                    "status",
                    new string[0],
                    NoOpHandler
                );

            CommandDefinition imeStatus =
                CreateDefinition(
                    "status",
                    new string[0],
                    NoOpHandler
                );

            string commandApiError;

            True(
                registry.TryRegister(
                    "/cmd",
                    commandApiStatus,
                    out commandApiError
                ),
                commandApiError
            );

            string imeError;

            True(
                registry.TryRegister(
                    "/IME",
                    imeStatus,
                    out imeError
                ),
                imeError
            );

            CommandDefinition resolved;

            True(
                registry.TryResolve(
                    "/cmd",
                    "status",
                    out resolved
                ),
                "The /cmd command was not resolved."
            );

            Same(
                commandApiStatus,
                resolved,
                "/cmd status"
            );

            True(
                registry.TryResolve(
                    "/ime",
                    "STATUS",
                    out resolved
                ),
                "The /ime command was not resolved."
            );

            Same(
                imeStatus,
                resolved,
                "/ime status"
            );

            Equal(
                2,
                registry.Count,
                "registered count"
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

        [Fact]
        public void RegistrationHandleRemovesCanonicalNameAndAliases()
        {
            var registry =
                new CommandRegistry();

            Action unregister;
            string errorMessage;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "echo",
                        new[] { "say" },
                        NoOpHandler
                    ),
                    out unregister,
                    out errorMessage
                ),
                errorMessage
            );

            Equal(
                1,
                registry.Count,
                "registered count"
            );

            CommandDefinition resolved;

            True(
                registry.TryResolve(
                    "echo",
                    out resolved
                ),
                "Canonical name was not registered."
            );

            True(
                registry.TryResolve(
                    "say",
                    out resolved
                ),
                "Alias was not registered."
            );

            unregister();

            Equal(
                0,
                registry.Count,
                "unregistered count"
            );

            False(
                registry.TryResolve(
                    "echo",
                    out resolved
                ),
                "Canonical name remained registered."
            );

            False(
                registry.TryResolve(
                    "say",
                    out resolved
                ),
                "Alias remained registered."
            );
        }

        [Fact]
        public void RegistrationHandleIsIdempotentAndCannotRemoveReplacement()
        {
            var registry =
                new CommandRegistry();

            Action firstUnregister;
            string firstError;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "echo",
                        new string[0],
                        NoOpHandler
                    ),
                    out firstUnregister,
                    out firstError
                ),
                firstError
            );

            firstUnregister();
            firstUnregister();

            var replacement =
                CreateDefinition(
                    "echo",
                    new string[0],
                    NoOpHandler
                );

            Action replacementUnregister;
            string replacementError;

            True(
                registry.TryRegister(
                    replacement,
                    out replacementUnregister,
                    out replacementError
                ),
                replacementError
            );

            firstUnregister();

            CommandDefinition resolved;

            True(
                registry.TryResolve(
                    "echo",
                    out resolved
                ),
                "Replacement registration was removed by a stale handle."
            );

            Same(
                replacement,
                resolved,
                "replacement definition"
            );

            replacementUnregister();

            False(
                registry.TryResolve(
                    "echo",
                    out resolved
                ),
                "Replacement registration remained after disposal."
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
