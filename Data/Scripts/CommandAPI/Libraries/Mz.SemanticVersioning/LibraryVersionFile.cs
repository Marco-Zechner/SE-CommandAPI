namespace Mz.SemanticVersioning
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
        public const int Patch = 1;

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
                    "0.1.1",
                    new[]
                    {
                        "Added a complete package usage guide and installation examples.",
                        "Added shared immutable changelog value types."
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
