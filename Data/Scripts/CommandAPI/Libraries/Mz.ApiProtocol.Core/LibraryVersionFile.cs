using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Defines the released version and ordered changelog of this library
    /// package.
    /// </summary>
    public static class LibraryVersionFile
    {
        /// <summary>
        /// Gets the major version number.
        /// </summary>
        public const int Major = 0;

        /// <summary>
        /// Gets the minor version number.
        /// </summary>
        public const int Minor = 2;

        /// <summary>
        /// Gets the patch version number.
        /// </summary>
        public const int Patch = 2;

        /// <summary>
        /// Gets the version string.
        /// </summary>
        public static string VersionString => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Gets the complete changelog ordered from newest to oldest.
        /// </summary>
        public static Changelog Changelog { get; } = new Changelog(
            VersionString,
            new[]
            {
                new ChangelogEntry(
                    "0.2.2",
                    new[]
                    {
                        "Added compiled provider, consumer-facade, and downstream usage examples.",
                        "Included the copy-paste guide in release bundles."
                    }
                ),
                new ChangelogEntry(
                    "0.2.1",
                    new[]
                    {
                        "Added a complete package usage guide and installation examples.",
                        "Adopted the shared SemanticVersioning changelog model."
                    }
                ),
                new ChangelogEntry(
                    "0.2.0",
                    new[]
                    {
                        "Published the initial SELibs package release."
                    }
                )
            }
        );
    }
}
