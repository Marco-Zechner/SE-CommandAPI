using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Api;
using MarcoZechner.CommandApi.Core;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandRegistrationServiceTests
    {
        [Fact]
        public void RegisterMapsBclPayloadAndReturnsUnregisterAction()
        {
            var registry =
                new CommandRegistry();

            var service =
                new CommandRegistrationService(
                    registry
                );

            IDictionary<string, object> observedRequest =
                null;

            var metadata =
                new Dictionary<string, object>
                {
                    { "OwnerId", "Example.Mod" },
                    { "Prefix", "/IME" },
                    { "ExecutionLocation", "Client" },
                    { "CanonicalName", "echo" },
                    { "Aliases", new[] { "say" } },
                    {
                        "ShortDescription",
                        "Echoes supplied text."
                    },
                    {
                        "HelpText",
                        "Returns the supplied argument."
                    },
                    { "Usage", "echo <text>" },
                    { "Category", "Utility" },
                    { "PermissionRequirement", 2 }
                };

            Action unregister =
                service.RegisterCommand(
                    metadata,
                    delegate(
                        IDictionary<string, object> request
                    )
                    {
                        observedRequest = request;

                        return new Dictionary<string, object>
                        {
                            { "IsSuccess", true },
                            { "Title", "Echo" },
                            {
                                "Summary",
                                "Command completed."
                            },
                            {
                                "DetailLines",
                                new[]
                                {
                                    (
                                        (string[])request[
                                            "Arguments"
                                        ]
                                    )[0]
                                }
                            },
                            { "Severity", "Success" },
                            { "UsageHint", null }
                        };
                    }
                );

            Assert.NotNull(unregister);
            Assert.Equal(1, registry.Count);
            Assert.Equal(1, service.ExternalProviderCount);

            CommandDefinition definition;

            Assert.True(
                registry.TryResolve(
                    "/ime",
                    "SAY",
                    out definition
                )
            );

            Assert.Equal(
                "echo",
                definition.CanonicalName
            );

            Assert.Equal(
                "Example.Mod",
                definition.OwnerId
            );

            Assert.Equal(
                CommandExecutionLocation.Client,
                definition.ExecutionLocation
            );

            Assert.Equal(
                2,
                definition.PermissionRequirement
            );

            var context =
                new CommandExecutionContext(
                    "request-42",
                    76561198000000042UL,
                    42L,
                    "Marco",
                    3,
                    true
                );

            CommandResult result =
                definition.Handler(
                    context,
                    new CommandInput(
                        "/ime",
                        "echo",
                        new[] { "MiXeD" }
                    )
                );

            Assert.NotNull(observedRequest);

            Assert.Equal(
                "request-42",
                observedRequest["RequestId"]
            );

            Assert.Equal(
                76561198000000042UL,
                observedRequest["RequesterSteamId"]
            );

            Assert.Equal(
                42L,
                observedRequest["RequesterIdentityId"]
            );

            Assert.Equal(
                "Marco",
                observedRequest["RequesterDisplayName"]
            );

            Assert.Equal(
                3,
                observedRequest["PermissionLevel"]
            );

            Assert.Equal(
                true,
                observedRequest["IsServer"]
            );

            Assert.Equal(
                "/ime",
                observedRequest["Prefix"]
            );

            Assert.Equal(
                "echo",
                observedRequest["CommandName"]
            );

            Assert.Equal(
                new[] { "MiXeD" },
                (string[])observedRequest["Arguments"]
            );

            Assert.True(result.IsSuccess);
            Assert.Equal("Echo", result.Title);

            Assert.Equal(
                "Command completed.",
                result.Summary
            );

            Assert.Equal(
                CommandSeverity.Success,
                result.Severity
            );

            Assert.Equal(
                new[] { "MiXeD" },
                result.DetailLines
            );

            unregister();
            unregister();

            Assert.Equal(0, registry.Count);
            Assert.Equal(0, service.ExternalProviderCount);

            Assert.False(
                registry.TryResolve(
                    "echo",
                    out definition
                )
            );
        }

        [Fact]
        public void ExternalProviderCountTracksDistinctOwnersAndIdempotentUnregister()
        {
            var registry = new CommandRegistry();
            var service = new CommandRegistrationService(registry);

            Action first = service.RegisterCommand(MinimalMetadata("Example.One", "first"), SuccessHandler);
            Action second = service.RegisterCommand(MinimalMetadata("Example.One", "second"), SuccessHandler);
            Action third = service.RegisterCommand(MinimalMetadata("Example.Two", "third"), SuccessHandler);

            Assert.Equal(3, registry.Count);
            Assert.Equal(2, service.ExternalProviderCount);

            second();
            Assert.Equal(2, registry.Count);
            Assert.Equal(2, service.ExternalProviderCount);

            first();
            first();
            Assert.Equal(1, registry.Count);
            Assert.Equal(1, service.ExternalProviderCount);

            third();
            third();
            Assert.Equal(0, registry.Count);
            Assert.Equal(0, service.ExternalProviderCount);
        }

        [Fact]
        public void RegisterRejectsInternalExecutionLocationWithoutMutation()
        {
            var registry = new CommandRegistry();
            var service = new CommandRegistrationService(registry);
            IDictionary<string, object> metadata = MinimalMetadata("internal");
            metadata["ExecutionLocation"] = "Internal";

            Assert.Throws<ArgumentException>(delegate { service.RegisterCommand(metadata, SuccessHandler); });
            Assert.Equal(0, registry.Count);
            Assert.Equal(0, service.ExternalProviderCount);
        }

        [Fact]
        public void RegisterRejectsDuplicateCommandName()
        {
            var registry =
                new CommandRegistry();

            var service =
                new CommandRegistrationService(
                    registry
                );

            IDictionary<string, object> metadata =
                MinimalMetadata("echo");

            Action first =
                service.RegisterCommand(
                    metadata,
                    SuccessHandler
                );

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    delegate
                    {
                        service.RegisterCommand(
                            MinimalMetadata("ECHO"),
                            SuccessHandler
                        );
                    }
                );

            Assert.Contains(
                "already registered",
                exception.Message
            );

            Assert.Equal(1, registry.Count);

            first();
        }

        [Fact]
        public void RegisterRejectsMalformedMetadataWithoutMutation()
        {
            var registry =
                new CommandRegistry();

            var service =
                new CommandRegistrationService(
                    registry
                );

            var metadata =
                new Dictionary<string, object>
                {
                    { "OwnerId", "Example.Mod" },
                    { "Prefix", "/IME" },
                    { "ExecutionLocation", "Client" },
                    { "CanonicalName", "echo" },
                    {
                        "PermissionRequirement",
                        "administrator"
                    }
                };

            Assert.Throws<ArgumentException>(
                delegate
                {
                    service.RegisterCommand(
                        metadata,
                        SuccessHandler
                    );
                }
            );

            Assert.Equal(0, registry.Count);
        }

        private static IDictionary<string, object> MinimalMetadata(string commandName)
        {
            return MinimalMetadata("Example.Mod", commandName);
        }

        private static IDictionary<string, object> MinimalMetadata(string ownerId, string commandName)
        {
            return new Dictionary<string, object>
            {
                { "OwnerId", ownerId },
                { "CanonicalName", commandName }
            };
        }

        private static IDictionary<string, object>
            SuccessHandler(
                IDictionary<string, object> request
            )
        {
            return new Dictionary<string, object>
            {
                { "IsSuccess", true },
                { "Title", "Completed" },
                { "Summary", "Command completed." },
                { "DetailLines", new string[0] },
                { "Severity", "Success" },
                { "UsageHint", null }
            };
        }
    }
}
