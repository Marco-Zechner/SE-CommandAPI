using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Core;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandExecutorTests
    {
        [Fact]
        public void ExecuteRegisteredCommand()
        {
            var registry =
                new CommandRegistry();

            string registrationError;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "echo",
                        CommandExecutionLocation.Server,
                        0,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            return Success(
                                input.Arguments[0]
                            );
                        }
                    ),
                    out registrationError
                ),
                registrationError
            );

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    ServerContext(0),
                    new CommandInput(
                        "EcHo",
                        new[] { "MiXeD" }
                    )
                );

            True(
                result.IsSuccess,
                "Execution did not succeed."
            );

            Equal(
                "MiXeD",
                result.DetailLines[0],
                "result detail"
            );
        }

        [Fact]
        public void ResolveCommandUsingInputPrefix()
        {
            var registry =
                new CommandRegistry();

            string commandApiError;

            True(
                registry.TryRegister(
                    "/cmd",
                    CreateDefinition(
                        "status",
                        CommandExecutionLocation.Server,
                        0,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            return Success("CommandAPI");
                        }
                    ),
                    out commandApiError
                ),
                commandApiError
            );

            string imeError;

            True(
                registry.TryRegister(
                    "/ime",
                    CreateDefinition(
                        "status",
                        CommandExecutionLocation.Server,
                        0,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            return Success("IME");
                        }
                    ),
                    out imeError
                ),
                imeError
            );

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    ServerContext(0),
                    new CommandInput(
                        "/IME",
                        "STATUS",
                        new string[0]
                    )
                );

            True(
                result.IsSuccess,
                "Prefix-scoped execution did not succeed."
            );

            Equal(
                "IME",
                result.DetailLines[0],
                "resolved prefix"
            );
        }

        [Fact]
        public void UnknownCommandUsesInputPrefixInUsageHint()
        {
            var executor =
                new CommandExecutor(
                    new CommandRegistry()
                );

            CommandResult result =
                executor.Execute(
                    ServerContext(0),
                    new CommandInput(
                        "/ime",
                        "missing",
                        new string[0]
                    )
                );

            False(
                result.IsSuccess,
                "Unknown command unexpectedly succeeded."
            );

            Equal(
                "/ime help",
                result.UsageHint,
                "usage hint"
            );
        }

        [Fact]
        public void ReturnStructuredUnknownCommandFailure()
        {
            var executor =
                new CommandExecutor(
                    new CommandRegistry()
                );

            CommandResult result =
                executor.Execute(
                    ServerContext(0),
                    new CommandInput(
                        "missing",
                        new string[0]
                    )
                );

            False(
                result.IsSuccess,
                "Unknown command unexpectedly succeeded."
            );

            Equal(
                "Unknown command",
                result.Title,
                "title"
            );

            Equal(
                "Unknown command 'missing'.",
                result.Summary,
                "summary"
            );

            Equal(
                CommandSeverity.Error,
                result.Severity,
                "severity"
            );

            Equal(
                "/cmd help",
                result.UsageHint,
                "usage hint"
            );
        }

        [Fact]
        public void SubmitClientCommandLocally()
        {
            var registry =
                new CommandRegistry();

            bool handlerCalled = false;
            string registrationError;

            True(
                registry.TryRegister(
                    "/ime",
                    CreateDefinition(
                        "theme",
                        CommandExecutionLocation.Client,
                        0,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            handlerCalled = true;

                            False(
                                context.IsServer,
                                "Client command received a server context."
                            );

                            return Success("local-client");
                        }
                    ),
                    out registrationError
                ),
                registrationError
            );

            var executor =
                new CommandExecutor(registry);

            int serverSubmissionCount = 0;
            CommandResult observedResult = null;

            var dispatcher =
                new CommandSubmissionDispatcher(
                    registry,
                    executor,
                    delegate(
                        ulong senderId,
                        string requestId
                    )
                    {
                        Equal(
                            42UL,
                            senderId,
                            "local sender"
                        );

                        Equal(
                            "local-client-001",
                            requestId,
                            "local request ID"
                        );

                        return ClientContext(0);
                    },
                    delegate(
                        string requestId,
                        CommandInput input
                    )
                    {
                        serverSubmissionCount++;
                    },
                    delegate(CommandResult result)
                    {
                        observedResult = result;
                    }
                );

            dispatcher.Submit(
                42UL,
                "local-client-001",
                new CommandInput(
                    "/ime",
                    "theme",
                    new string[0]
                )
            );

            True(
                handlerCalled,
                "Client handler did not execute locally."
            );

            Equal(
                0,
                serverSubmissionCount,
                "server submission count"
            );

            True(
                observedResult != null,
                "Local result was not presented."
            );

            True(
                observedResult.IsSuccess,
                "Local client command did not succeed."
            );

            Equal(
                "local-client",
                observedResult.DetailLines[0],
                "local result"
            );
        }

        [Fact]
        public void SubmitEitherCommandLocally()
        {
            var registry =
                new CommandRegistry();

            string registrationError;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "status",
                        CommandExecutionLocation.Either,
                        0,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            return Success(
                                context.IsServer
                                    ? "server"
                                    : "client"
                            );
                        }
                    ),
                    out registrationError
                ),
                registrationError
            );

            CommandResult observedResult = null;

            var dispatcher =
                new CommandSubmissionDispatcher(
                    registry,
                    new CommandExecutor(registry),
                    delegate(
                        ulong senderId,
                        string requestId
                    )
                    {
                        return ClientContext(0);
                    },
                    delegate(
                        string requestId,
                        CommandInput input
                    )
                    {
                        throw new InvalidOperationException(
                            "Either command was sent to the server."
                        );
                    },
                    delegate(CommandResult result)
                    {
                        observedResult = result;
                    }
                );

            dispatcher.Submit(
                43UL,
                "local-either-001",
                new CommandInput(
                    "status",
                    new string[0]
                )
            );

            True(
                observedResult != null,
                "Either result was not presented locally."
            );

            Equal(
                "client",
                observedResult.DetailLines[0],
                "Either execution side"
            );
        }

        [Fact]
        public void SubmitServerCommandThroughNetwork()
        {
            var registry =
                new CommandRegistry();

            bool handlerCalled = false;
            string registrationError;

            True(
                registry.TryRegister(
                    "/ime",
                    CreateDefinition(
                        "admin",
                        CommandExecutionLocation.Server,
                        0,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            handlerCalled = true;
                            return Success("server");
                        }
                    ),
                    out registrationError
                ),
                registrationError
            );

            int serverSubmissionCount = 0;
            int localResultCount = 0;

            var dispatcher =
                new CommandSubmissionDispatcher(
                    registry,
                    new CommandExecutor(registry),
                    delegate(
                        ulong senderId,
                        string requestId
                    )
                    {
                        throw new InvalidOperationException(
                            "Server command requested a local context."
                        );
                    },
                    delegate(
                        string requestId,
                        CommandInput input
                    )
                    {
                        serverSubmissionCount++;

                        Equal(
                            "server-001",
                            requestId,
                            "server request ID"
                        );

                        Equal(
                            "/ime",
                            input.Prefix,
                            "server prefix"
                        );

                        Equal(
                            "admin",
                            input.CommandName,
                            "server command"
                        );
                    },
                    delegate(CommandResult result)
                    {
                        localResultCount++;
                    }
                );

            dispatcher.Submit(
                44UL,
                "server-001",
                new CommandInput(
                    "/ime",
                    "admin",
                    new string[0]
                )
            );

            False(
                handlerCalled,
                "Server handler executed locally."
            );

            Equal(
                1,
                serverSubmissionCount,
                "server submission count"
            );

            Equal(
                0,
                localResultCount,
                "local result count"
            );
        }

        [Fact]
        public void DenyInsufficientPermissionBeforeHandler()
        {
            bool handlerCalled = false;

            var registry =
                new CommandRegistry();

            string registrationError;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "admin",
                        CommandExecutionLocation.Server,
                        3,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            handlerCalled = true;
                            return Success("executed");
                        }
                    ),
                    out registrationError
                ),
                registrationError
            );

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    ServerContext(2),
                    new CommandInput(
                        "admin",
                        new string[0]
                    )
                );

            False(
                handlerCalled,
                "Handler ran before permission approval."
            );

            False(
                result.IsSuccess,
                "Permission denial unexpectedly succeeded."
            );

            Equal(
                "Permission denied",
                result.Title,
                "title"
            );

            Equal(
                "You do not have permission to use 'admin'.",
                result.Summary,
                "summary"
            );

            Equal(
                CommandSeverity.Error,
                result.Severity,
                "severity"
            );
        }

        [Fact]
        public void RejectServerCommandOnClient()
        {
            bool handlerCalled = false;

            var registry =
                new CommandRegistry();

            string registrationError;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "status",
                        CommandExecutionLocation.Server,
                        0,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            handlerCalled = true;
                            return Success("executed");
                        }
                    ),
                    out registrationError
                ),
                registrationError
            );

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    ClientContext(0),
                    new CommandInput(
                        "status",
                        new string[0]
                    )
                );

            False(
                handlerCalled,
                "Server-only handler ran on the client."
            );

            False(
                result.IsSuccess,
                "Location rejection unexpectedly succeeded."
            );

            Equal(
                "Command unavailable",
                result.Title,
                "title"
            );

            Equal(
                "Command 'status' must execute on the server.",
                result.Summary,
                "summary"
            );
        }

        [Fact]
        public void IsolateHandlerException()
        {
            var registry =
                new CommandRegistry();

            string registrationError;

            True(
                registry.TryRegister(
                    CreateDefinition(
                        "explode",
                        CommandExecutionLocation.Either,
                        0,
                        delegate(
                            CommandExecutionContext context,
                            CommandInput input
                        )
                        {
                            throw new InvalidOperationException(
                                "Sensitive server detail."
                            );
                        }
                    ),
                    out registrationError
                ),
                registrationError
            );

            var executor =
                new CommandExecutor(registry);

            CommandResult result =
                executor.Execute(
                    ServerContext(0),
                    new CommandInput(
                        "explode",
                        new string[0]
                    )
                );

            False(
                result.IsSuccess,
                "Throwing handler unexpectedly succeeded."
            );

            Equal(
                "Command failed",
                result.Title,
                "title"
            );

            Equal(
                "The command could not be completed.",
                result.Summary,
                "summary"
            );

            Equal(
                CommandSeverity.Error,
                result.Severity,
                "severity"
            );

            False(
                result.Summary.Contains("Sensitive"),
                "Handler exception detail leaked to the result."
            );
        }

        private static CommandDefinition CreateDefinition(
            string canonicalName,
            CommandExecutionLocation executionLocation,
            int permissionRequirement,
            CommandHandler handler
        )
        {
            return new CommandDefinition(
                canonicalName,
                new string[0],
                canonicalName + " command",
                canonicalName + " help",
                canonicalName,
                "Tests",
                executionLocation,
                permissionRequirement,
                "CommandAPI.Tests",
                handler
            );
        }

        private static CommandExecutionContext ServerContext(
            int permissionLevel
        )
        {
            return new CommandExecutionContext(
                "request-server",
                76561198000000042UL,
                42L,
                "Server Player",
                permissionLevel,
                true
            );
        }

        private static CommandExecutionContext ClientContext(
            int permissionLevel
        )
        {
            return new CommandExecutionContext(
                "request-client",
                76561198000000043UL,
                43L,
                "Client Player",
                permissionLevel,
                false
            );
        }

        private static CommandResult Success(
            string detail
        )
        {
            return new CommandResult(
                true,
                "Completed",
                "Command completed.",
                new[] { detail },
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
