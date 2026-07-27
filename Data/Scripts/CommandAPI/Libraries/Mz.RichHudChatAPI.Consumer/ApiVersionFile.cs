using Mz.SemanticVersioning;

namespace Mz.RichHudChatApi
{
    public static class ApiVersionFile
    {
        public const int Major = 1;
        public const int Minor = 0;
        public const int Patch = 0;

        public static SemanticVersion MinimumProviderApiVersion { get; } =
            new SemanticVersion(
                1,
                4,
                0
            );

        public static string VersionString =>
            $"{Major}.{Minor}.{Patch}";

        public static Changelog Changelog { get; } =
            new Changelog(
                VersionString,
                new[]
                {
                    new ChangelogEntry(
                        "1.0.0",
                        new[]
                        {
                            "Added the initial typed RichHudChatAPI consumer facade.",
                            "Added typed participant, route, transcript, companion, interaction, and input operations.",
                            "Added automatic discovery, exact endpoint validation, reconnection, and cleanup.",
                            "Accepted newer provider API versions while retaining the 1.4.0 contract floor."
                        }
                    )
                }
            );
    }
}