using System;

namespace Mz.RichHudChatApi
{
    public sealed class ChatRouteRegistration
    {
        public string RouteId { get; }

        public string Prefix { get; }

        public string ActivationPrefix { get; }

        public string Description { get; }

        public bool IsDefault { get; }

        public ChatRouteRegistration(
            string routeId,
            string prefix = null,
            string activationPrefix = null,
            string description = null,
            bool isDefault = false
        )
        {
            if (string.IsNullOrWhiteSpace(routeId))
                throw new ArgumentException(
                    "A route identifier is required.",
                    nameof(routeId)
                );

            if (
                !isDefault
                && string.IsNullOrWhiteSpace(prefix)
            )
            {
                throw new ArgumentException(
                    "A non-default route requires a prefix.",
                    nameof(prefix)
                );
            }

            RouteId = routeId.Trim();

            Prefix =
                string.IsNullOrWhiteSpace(prefix)
                    ? null
                    : prefix.Trim();

            ActivationPrefix =
                string.IsNullOrWhiteSpace(activationPrefix)
                    ? null
                    : activationPrefix.Trim();

            Description =
                string.IsNullOrWhiteSpace(description)
                    ? RouteId
                    : description.Trim();

            IsDefault = isDefault;
        }
    }
}