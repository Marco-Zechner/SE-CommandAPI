using Mz.SemanticVersioning;

namespace Mz.Networking
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
        public const int Minor = 1;

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
                    "0.1.2",
                    new[]
                    {
                        "Added compiled server and client copy-paste examples for a multiplayer ping flow.",
                        "Included the copy-paste guide in release bundles."
                    }
                ),
                new ChangelogEntry(
                    "0.1.1",
                    new[]
                    {
                        "Organized Core and Space Engineers sources by responsibility.",
                        "Preserved nested source folders in validation and release archives.",
                        "Added a complete package usage guide and installation examples.",
                        "Added the shared Mz.SemanticVersioning dependency.",
                        "Adopted the shared SemanticVersioning changelog model."
                    }
                ),
                new ChangelogEntry(
                    "0.1.0",
                    new[]
                    {
                        "Published the initial SELibs package release."
                    }
                )
            }
        );
    }
}
