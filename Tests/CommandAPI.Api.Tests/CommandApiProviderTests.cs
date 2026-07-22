using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Api;
using MarcoZechner.CommandApi.Core;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandApiProviderTests
    {
        private const long DiscoveryChannelId =
            1154638390L;

        [Fact]
        public void StartPublishesRegistrationEndpoint()
        {
            var registry =
                new CommandRegistry();

            var bus =
                new RecordingModMessageBus();

            var provider =
                new CommandApiProvider(
                    bus,
                    registry
                );

            provider.Start();

            Assert.Equal(
                1,
                bus.RegistrationCount
            );

            Assert.Equal(
                DiscoveryChannelId,
                bus.LastChannelId
            );

            ApiAnnouncement announcement;

            Assert.True(
                ApiDiscoveryWireProtocol
                    .TryParseAnnouncement(
                        bus.SentPayloads[0],
                        out announcement
                    )
            );

            Assert.Equal(
                "MarcoZechner.CommandAPI",
                announcement.Provider.Id
            );

            Assert.Equal(
                "CommandAPI",
                announcement.Provider.DisplayName
            );

            Assert.Equal(
                "0.1.0",
                announcement.Provider.Version.ToString()
            );

            Assert.Equal(
                "MarcoZechner.CommandAPI",
                announcement.Descriptor.ApiId
            );

            Assert.Equal(
                "1.1.0",
                announcement.Descriptor.Version.ToString()
            );

            Delegate endpoint =
                announcement.Endpoints["RegisterCommand"];

            var register =
                Assert.IsType<
                    Func<
                        IDictionary<string, object>,
                        Func<
                            IDictionary<string, object>,
                            IDictionary<string, object>
                        >,
                        Action
                    >
                >(endpoint);

            Action unregister =
                register(
                    new Dictionary<string, object>
                    {
                        { "OwnerId", "Example.Mod" },
                        { "CanonicalName", "echo" }
                    },
                    SuccessHandler
                );

            Assert.Equal(1, registry.Count);

            CommandDefinition definition;

            Assert.True(
                registry.TryResolve(
                    "echo",
                    out definition
                )
            );

            unregister();

            Assert.Equal(0, registry.Count);

            provider.Dispose();

            Assert.Equal(
                1,
                bus.UnregistrationCount
            );
        }

        [Fact]
        public void StartAndDisposeAreIdempotent()
        {
            var bus =
                new RecordingModMessageBus();

            var provider =
                new CommandApiProvider(
                    bus,
                    new CommandRegistry()
                );

            provider.Start();
            provider.Start();

            Assert.Equal(
                1,
                bus.RegistrationCount
            );

            provider.Dispose();
            provider.Dispose();

            Assert.Equal(
                1,
                bus.UnregistrationCount
            );
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

            public int RegistrationCount
            {
                get;
                private set;
            }

            public int UnregistrationCount
            {
                get;
                private set;
            }

            public long LastChannelId
            {
                get;
                private set;
            }

            public List<object> SentPayloads
            {
                get;
            } = new List<object>();

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
                RegistrationCount++;
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
                    && handlers.Remove(handler)
                )
                {
                    UnregistrationCount++;
                }
            }

            public void Send(
                long channelId,
                object payload
            )
            {
                LastChannelId = channelId;
                SentPayloads.Add(payload);

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
