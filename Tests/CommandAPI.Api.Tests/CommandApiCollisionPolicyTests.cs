using System;
using System.Collections.Generic;
using MarcoZechner.CommandApi.Api;
using MarcoZechner.CommandApi.Core;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class CommandApiCollisionPolicyTests
    {
        [Fact]
        public void QualifiedCommandsSurvivePreferredNameCollision()
        {
            var registry =
                new CommandRegistry();

            var service =
                new CommandRegistrationService(
                    registry
                );

            Action alphaQualified =
                service.RegisterCommand(
                    Metadata(
                        "Smoke.Alpha",
                        "smoke.alpha"
                    ),
                    Success
                );

            Action betaQualified =
                service.RegisterCommand(
                    Metadata(
                        "Smoke.Beta",
                        "smoke.beta"
                    ),
                    Success
                );

            Action alphaPreferred =
                service.RegisterCommand(
                    Metadata(
                        "Smoke.Alpha",
                        "smoke"
                    ),
                    Success
                );

            InvalidOperationException collision =
                Assert.Throws<InvalidOperationException>(
                    delegate
                    {
                        service.RegisterCommand(
                            Metadata(
                                "Smoke.Beta",
                                "smoke"
                            ),
                            Success
                        );
                    }
                );

            Assert.Equal(
                "Command name 'smoke' is already registered.",
                collision.Message
            );

            AssertOwner(
                registry,
                "smoke.alpha",
                "Smoke.Alpha"
            );

            AssertOwner(
                registry,
                "smoke.beta",
                "Smoke.Beta"
            );

            AssertOwner(
                registry,
                "smoke",
                "Smoke.Alpha"
            );

            alphaPreferred();

            Action betaPreferred =
                service.RegisterCommand(
                    Metadata(
                        "Smoke.Beta",
                        "smoke"
                    ),
                    Success
                );

            AssertOwner(
                registry,
                "smoke",
                "Smoke.Beta"
            );

            betaPreferred();
            betaQualified();
            alphaQualified();

            Assert.Equal(
                0,
                registry.Count
            );
        }

        private static IDictionary<string, object>
            Metadata(
                string ownerId,
                string canonicalName
            )
        {
            return new Dictionary<string, object>
            {
                {
                    "OwnerId",
                    ownerId
                },
                {
                    "CanonicalName",
                    canonicalName
                }
            };
        }

        private static IDictionary<string, object>
            Success(
                IDictionary<string, object> request
            )
        {
            return new Dictionary<string, object>
            {
                {
                    "IsSuccess",
                    true
                },
                {
                    "Title",
                    "Smoke"
                },
                {
                    "Summary",
                    "Completed."
                }
            };
        }

        private static void AssertOwner(
            CommandRegistry registry,
            string commandName,
            string expectedOwnerId
        )
        {
            CommandDefinition definition;

            Assert.True(
                registry.TryResolve(
                    commandName,
                    out definition
                )
            );

            Assert.Equal(
                expectedOwnerId,
                definition.OwnerId
            );
        }
    }
}
