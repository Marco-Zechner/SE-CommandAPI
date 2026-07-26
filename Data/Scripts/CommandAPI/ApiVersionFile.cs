using Mz.SemanticVersioning;

namespace MarcoZechner.CommandApi
{
    /// <summary>
    /// Defines the public CommandAPI provider contract version and changelog.
    /// </summary>
    public static class ApiVersionFile
    {
        /// <summary>
        /// Gets the major API version number.
        /// </summary>
        public const int Major = 1;

        /// <summary>
        /// Gets the minor API version number.
        /// </summary>
        public const int Minor = 1;

        /// <summary>
        /// Gets the patch API version number.
        /// </summary>
        public const int Patch = 0;

        /// <summary>
        /// Gets the normalized API version string.
        /// </summary>
        public static string VersionString =>
            $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Gets the complete API changelog ordered from newest to oldest.
        /// </summary>
        public static Changelog Changelog { get; } =
            new Changelog(
                VersionString,
                new[]
                {
                    new ChangelogEntry(
                        "1.1.0",
                        new[]
                        {
                            "Added optional Prefix metadata for provider-specific top-level command prefixes.",
                            "Scoped command and alias collisions to their registered prefix.",
                            "Preserved the RegisterCommand endpoint signature and defaulted omitted Prefix values to /cmd."
                        }
                    ),
                    new ChangelogEntry(
                        "1.0.0",
                        new[]
                        {
                            "Introduced the RegisterCommand endpoint with BCL-only metadata, request, result, and unregister contracts."
                        }
                    )
                }
            );
    }
}
