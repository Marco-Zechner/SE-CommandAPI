namespace Mz.SemanticVersioning
{
    /// <summary>
    /// Defines the released version of this library package.
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
        public const int Patch = 0;

        /// <summary>
        /// Gets the version string.
        /// </summary>
        public static string VersionString => $"{Major}.{Minor}.{Patch}";
    }
}
