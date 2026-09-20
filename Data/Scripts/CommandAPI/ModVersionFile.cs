using Mz.SemanticVersioning;

namespace MarcoZechner.CommandApi
{
    /// <summary>
    /// Defines the installed CommandAPI mod version and ordered changelog.
    /// </summary>
    public static class ModVersionFile
    {
        /// <summary>
        /// Gets the major mod version number.
        /// </summary>
        public const int Major = 0;

        /// <summary>
        /// Gets the minor mod version number.
        /// </summary>
        public const int Minor = 3;

        /// <summary>
        /// Gets the patch mod version number.
        /// </summary>
        public const int Patch = 0;

        /// <summary>
        /// Gets the normalized mod version string.
        /// </summary>
        public static string VersionString =>
            $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Gets the complete mod changelog ordered from newest to oldest.
        /// </summary>
        public static Changelog Changelog { get; } =
            new Changelog(
                VersionString,
                new[]
                {
                    new ChangelogEntry(
                        "0.3.0",
                        new[]
                        {
                            "Simplified the runtime to standalone vanilla Space Engineers chat and removed RichHudChatAPI integration.",
                            "Improved live external-provider status and client help visibility for server-routed commands.",
                            "Reserved the Internal execution location from public registrations.",
                            "Updated SELibs dependencies to Mz.ApiProtocol 0.3.0, Mz.Networking 0.2.1, and Mz.SemanticVersioning 0.2.0.",
                            "Removed disposable smoke mods and obsolete deployment tooling."
                        }
                    ),
                    new ChangelogEntry(
                        "0.2.0",
                        new[]
                        {
                            "Added prefix-scoped external command registration and routing.",
                            "Added optional RichHudChatAPI command input, suggestions, completion, and result presentation.",
                            "Added authoritative client/server command execution with structured command results.",
                            "Migrated shared ApiProtocol and Networking dependencies to SELibs."
                        }
                    ),
                    new ChangelogEntry(
                        "0.1.0",
                        new[]
                        {
                            "Established the initial CommandAPI vertical slice with built-in commands and external command registration."
                        }
                    )
                }
            );
    }
}
