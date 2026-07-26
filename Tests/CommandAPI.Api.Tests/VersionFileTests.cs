using Mz.SemanticVersioning;
using Xunit;

namespace MarcoZechner.CommandApi.Tests
{
    public sealed class VersionFileTests
    {
        [Fact]
        public void ModVersionMatchesCurrentChangelogEntry()
        {
            Assert.Equal(
                ModVersionFile.VersionString,
                ModVersionFile.Changelog
                    .CurrentVersion
                    .ToString()
            );

            Assert.Equal(
                ModVersionFile.VersionString,
                ModVersionFile.Changelog
                    .Current
                    .Version
                    .ToString()
            );

            Assert.Equal(
                new SemanticVersion(
                    ModVersionFile.Major,
                    ModVersionFile.Minor,
                    ModVersionFile.Patch
                ),
                ModVersionFile.Changelog.CurrentVersion
            );
        }

        [Fact]
        public void ApiVersionMatchesCurrentChangelogEntry()
        {
            Assert.Equal(
                ApiVersionFile.VersionString,
                ApiVersionFile.Changelog
                    .CurrentVersion
                    .ToString()
            );

            Assert.Equal(
                ApiVersionFile.VersionString,
                ApiVersionFile.Changelog
                    .Current
                    .Version
                    .ToString()
            );

            Assert.Equal(
                new SemanticVersion(
                    ApiVersionFile.Major,
                    ApiVersionFile.Minor,
                    ApiVersionFile.Patch
                ),
                ApiVersionFile.Changelog.CurrentVersion
            );
        }
    }
}
