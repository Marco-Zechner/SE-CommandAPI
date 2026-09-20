using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.CommandApi;
using Mz.SemanticVersioning;
using Xunit;

namespace Mz.CommandApi.Tests
{
    public sealed class CommandApiClientTests
    {
        [Fact]
        public void FacadeVersionMatchesCurrentChangelog()
        {
            Assert.Equal(
                ApiVersionFile.VersionString,
                ApiVersionFile.Changelog
                    .CurrentVersion
                    .ToString()
            );

            Assert.Equal(
                ApiVersionFile.VersionString,
                ApiVersionFile.Changelog
                    .Current
                    .Version
                    .ToString()
            );
        }

        [Fact]
        public void AcceptsNewerProviderWithoutUpperCeiling()
        {
            var bus =
                new RecordingModMessageBus();

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(9, 0, 0),
                    ValidEndpoints(
                        delegate(
                            IDictionary<string, object> metadata,
                            Func<
                                IDictionary<string, object>,
                                IDictionary<string, object>
                            > handler
                        )
                        {
                            return delegate { };
                        }
                    )
                );

            provider.Start();

            var client =
                CreateClient(bus);

            client.Start();

            Assert.True(client.IsConnected);

            Assert.Equal(
                "9.0.0",
                client.ProviderApiVersion.ToString()
            );

            client.Dispose();
            provider.Dispose();
        }

        [Fact]
        public void MapsTypedRegistrationRequestAndResponse()
        {
            var bus =
                new RecordingModMessageBus();

            IDictionary<string, object> observedMetadata =
                null;

            Func<
                IDictionary<string, object>,
                IDictionary<string, object>
            > observedHandler =
                null;

            int unregisterCount =
                0;

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(1, 1, 0),
                    ValidEndpoints(
                        delegate(
                            IDictionary<string, object> metadata,
                            Func<
                                IDictionary<string, object>,
                                IDictionary<string, object>
                            > handler
                        )
                        {
                            observedMetadata = metadata;
                            observedHandler = handler;

                            return delegate
                            {
                                unregisterCount++;
                            };
                        }
                    )
                );

            provider.Start();

            var client =
                CreateClient(bus);

            client.Start();

            CommandRequest observedRequest =
                null;

            CommandRegistrationHandle handle =
                client.Register(
                    new CommandRegistration(
                        "/EXAMPLE",
                        "echo",
                        CommandExecutionLocation.Client,
                        new[] { "say" },
                        "Echoes supplied text.",
                        "Returns the first argument.",
                        "echo <text>",
                        "Utility",
                        2
                    ),
                    delegate(CommandRequest request)
                    {
                        observedRequest = request;

                        return new CommandResponse(
                            true,
                            "Echo",
                            "Command completed.",
                            new[] { request.Arguments[0] },
                            CommandSeverity.Success,
                            null
                        );
                    }
                );

            Assert.True(handle.IsActive);
            Assert.NotNull(observedMetadata);
            Assert.NotNull(observedHandler);

            Assert.Equal(
                "Example.Mod",
                observedMetadata["OwnerId"]
            );

            Assert.Equal(
                "/example",
                observedMetadata["Prefix"]
            );

            Assert.Equal(
                "Client",
                observedMetadata["ExecutionLocation"]
            );

            Assert.Equal(
                new[] { "say" },
                (string[])observedMetadata["Aliases"]
            );

            IDictionary<string, object> response =
                observedHandler(
                    new Dictionary<string, object>
                    {
                        { "RequestId", "request-42" },
                        {
                            "RequesterSteamId",
                            76561198000000042UL
                        },
                        { "RequesterIdentityId", 42L },
                        {
                            "RequesterDisplayName",
                            "Marco"
                        },
                        { "PermissionLevel", 3 },
                        { "IsServer", false },
                        { "Prefix", "/example" },
                        { "CommandName", "echo" },
                        {
                            "Arguments",
                            new[] { "MiXeD" }
                        }
                    }
                );

            Assert.NotNull(observedRequest);

            Assert.Equal(
                "request-42",
                observedRequest.RequestId
            );

            Assert.Equal(
                76561198000000042UL,
                observedRequest.RequesterSteamId
            );

            Assert.Equal(
                new[] { "MiXeD" },
                observedRequest.Arguments
            );

            Assert.Equal(true, response["IsSuccess"]);
            Assert.Equal("Echo", response["Title"]);
            Assert.Equal("Success", response["Severity"]);

            Assert.Equal(
                new[] { "MiXeD" },
                (string[])response["DetailLines"]
            );

            handle.Dispose();
            handle.Dispose();

            Assert.False(handle.IsActive);
            Assert.True(handle.IsDisposed);
            Assert.Equal(1, unregisterCount);

            client.Dispose();
            provider.Dispose();
        }

        [Fact]
        public void RegistrationRejectsInternalExecutionLocation()
        {
            Assert.Throws<ArgumentException>(
                delegate
                {
                    new CommandRegistration("/example", "internal", CommandExecutionLocation.Internal);
                }
            );
        }

        [Fact]
        public void PendingRegistrationActivatesWhenProviderAppears()
        {
            var bus =
                new RecordingModMessageBus();

            int registrationCount =
                0;

            int unregisterCount =
                0;

            var client =
                CreateClient(bus);

            client.Start();

            CommandRegistrationHandle handle =
                client.Register(
                    new CommandRegistration(
                        "/late",
                        "ping"
                    ),
                    delegate(CommandRequest request)
                    {
                        return new CommandResponse(
                            true,
                            "Pong",
                            "Late provider connected."
                        );
                    }
                );

            Assert.False(handle.IsActive);

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(1, 1, 0),
                    ValidEndpoints(
                        delegate(
                            IDictionary<string, object> metadata,
                            Func<
                                IDictionary<string, object>,
                                IDictionary<string, object>
                            > handler
                        )
                        {
                            registrationCount++;

                            return delegate
                            {
                                unregisterCount++;
                            };
                        }
                    )
                );

            provider.Start();

            Assert.True(client.IsConnected);
            Assert.True(handle.IsActive);
            Assert.Equal(1, registrationCount);

            provider.Dispose();

            Assert.False(client.IsConnected);
            Assert.False(handle.IsActive);
            Assert.Equal(1, unregisterCount);

            client.Dispose();
        }

        [Fact]
        public void RejectsProviderMissingExactEndpoint()
        {
            var bus =
                new RecordingModMessageBus();

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(1, 1, 0),
                    new Dictionary<string, Delegate>(
                        StringComparer.Ordinal
                    )
                );

            provider.Start();

            var client =
                CreateClient(bus);

            client.Start();

            Assert.False(client.IsConnected);
            Assert.NotNull(client.LastError);

            Assert.Contains(
                "RegisterCommand",
                client.LastError.Message
            );

            client.Dispose();
            provider.Dispose();
        }

        private static CommandApiClient CreateClient(
            IModMessageBus bus
        )
        {
            return new CommandApiClient(
                bus,
                "Example.Mod",
                "Example Mod",
                new SemanticVersion(2, 3, 4),
                true,
                "Registers Example Mod commands."
            );
        }

        private static ApiDiscoveryProvider CreateProvider(
            IModMessageBus bus,
            SemanticVersion apiVersion,
            IDictionary<string, Delegate> endpoints
        )
        {
            return new ApiDiscoveryProvider(
                bus,
                new ApiModIdentity(
                    "MarcoZechner.CommandAPI",
                    "CommandAPI",
                    new SemanticVersion(0, 2, 0)
                ),
                new ApiDescriptor(
                    "MarcoZechner.CommandAPI",
                    apiVersion
                ),
                endpoints
            );
        }

        private static IDictionary<string, Delegate>
            ValidEndpoints(
                Func<
                    IDictionary<string, object>,
                    Func<
                        IDictionary<string, object>,
                        IDictionary<string, object>
                    >,
                    Action
                > registerCommand
            )
        {
            return new Dictionary<string, Delegate>(
                StringComparer.Ordinal
            )
            {
                {
                    "RegisterCommand",
                    registerCommand
                }
            };
        }

        private sealed class RecordingModMessageBus :
            IModMessageBus
        {
            private readonly Dictionary<
                long,
                List<Action<object>>
            > _handlers =
                new Dictionary<
                    long,
                    List<Action<object>>
                >();

            public void RegisterHandler(
                long channelId,
                Action<object> handler
            )
            {
                List<Action<object>> handlers;

                if (
                    !_handlers.TryGetValue(
                        channelId,
                        out handlers
                    )
                )
                {
                    handlers =
                        new List<Action<object>>();

                    _handlers.Add(
                        channelId,
                        handlers
                    );
                }

                handlers.Add(handler);
            }

            public void UnregisterHandler(
                long channelId,
                Action<object> handler
            )
            {
                List<Action<object>> handlers;

                if (
                    _handlers.TryGetValue(
                        channelId,
                        out handlers
                    )
                )
                {
                    handlers.Remove(handler);
                }
            }

            public void Send(
                long channelId,
                object payload
            )
            {
                List<Action<object>> handlers;

                if (
                    !_handlers.TryGetValue(
                        channelId,
                        out handlers
                    )
                )
                {
                    return;
                }

                Action<object>[] snapshot =
                    handlers.ToArray();

                for (
                    int index = 0;
                    index < snapshot.Length;
                    index++
                )
                {
                    snapshot[index](payload);
                }
            }
        }
    }
}
