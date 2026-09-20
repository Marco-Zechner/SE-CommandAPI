[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Utf8WithoutBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Text
    )

    $encoding =
        New-Object System.Text.UTF8Encoding($false)

    $normalized =
        $Text.Replace("`r`n", "`n").Replace("`r", "`n")

    [System.IO.File]::WriteAllText(
        $Path,
        $normalized,
        $encoding
    )
}

function Write-GitHubOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
        return
    }

    [System.IO.File]::AppendAllText(
        $env:GITHUB_OUTPUT,
        $Name + "=" + $Value + [Environment]::NewLine
    )
}

function ConvertFrom-CSharpStringLiteral {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    try {
        return [regex]::Unescape($Value)
    }
    catch {
        throw (
            "Consumer changelog in '$Path' contains an unsupported " +
            "C# string escape."
        )
    }
}

function Read-ConsumerReleaseMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionFilePath
    )

    if (
        -not (
            Test-Path `
                -LiteralPath $VersionFilePath `
                -PathType Leaf
        )
    ) {
        throw "Consumer version file not found: $VersionFilePath"
    }

    $text =
        Get-Content `
            -LiteralPath $VersionFilePath `
            -Raw

    $parts =
        [ordered]@{}

    foreach ($name in @("Major", "Minor", "Patch")) {
        $match =
            [regex]::Match(
                $text,
                (
                    'public\s+const\s+int\s+' +
                    [regex]::Escape($name) +
                    '\s*=\s*(?<value>[0-9]+)\s*;'
                )
            )

        if (-not $match.Success) {
            throw (
                "Consumer version file '$VersionFilePath' does not " +
                "declare numeric $name."
            )
        }

        $parts[$name] =
            $match.Groups["value"].Value
    }

    $version =
        (
            "$($parts["Major"])." +
            "$($parts["Minor"])." +
            "$($parts["Patch"])"
        )

    $entryPattern =
        (
            '(?s)new\s+ChangelogEntry\s*\(\s*' +
            '"(?<version>(?:\\.|[^"\\])*)"\s*,\s*' +
            'new\s*\[\]\s*\{(?<changes>.*?)\}\s*\)'
        )

    $entryMatches =
        @(
            [regex]::Matches(
                $text,
                $entryPattern
            )
        )

    if ($entryMatches.Count -eq 0) {
        throw (
            "Consumer version file '$VersionFilePath' does not " +
            "declare any ChangelogEntry values."
        )
    }

    $changelog =
        New-Object System.Collections.ArrayList

    $seenVersions =
        @{}

    $previousVersion =
        $null

    foreach ($entryMatch in $entryMatches) {
        $entryVersion =
            ConvertFrom-CSharpStringLiteral `
                -Value $entryMatch.Groups["version"].Value `
                -Path $VersionFilePath

        if ($entryVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
            throw (
                "Consumer changelog contains invalid version " +
                "'$entryVersion'."
            )
        }

        if ($seenVersions.ContainsKey($entryVersion)) {
            throw (
                "Consumer changelog declares version " +
                "'$entryVersion' more than once."
            )
        }

        $parsedVersion =
            [version]::Parse($entryVersion)

        if (
            $null -ne $previousVersion `
            -and $parsedVersion.CompareTo($previousVersion) -ge 0
        ) {
            throw (
                "Consumer changelog must be ordered from newest " +
                "to oldest."
            )
        }

        $changesText =
            $entryMatch.Groups["changes"].Value

        $changeMatches =
            @(
                [regex]::Matches(
                    $changesText,
                    '"(?<value>(?:\\.|[^"\\])*)"'
                )
            )

        if ($changeMatches.Count -eq 0) {
            throw (
                "Consumer changelog version '$entryVersion' " +
                "does not contain any changes."
            )
        }

        $residue =
            [regex]::Replace(
                $changesText,
                '"(?:\\.|[^"\\])*"',
                ""
            )

        $residue =
            [regex]::Replace(
                $residue,
                '[\s,]',
                ""
            )

        if (-not [string]::IsNullOrEmpty($residue)) {
            throw (
                "Consumer changelog version '$entryVersion' " +
                "uses unsupported change-list syntax."
            )
        }

        $changes =
            New-Object System.Collections.ArrayList

        foreach ($changeMatch in $changeMatches) {
            $change =
                ConvertFrom-CSharpStringLiteral `
                    -Value $changeMatch.Groups["value"].Value `
                    -Path $VersionFilePath

            if ([string]::IsNullOrWhiteSpace($change)) {
                throw (
                    "Consumer changelog version '$entryVersion' " +
                    "contains an empty change."
                )
            }

            [void]$changes.Add($change)
        }

        [void]$changelog.Add(
            [pscustomobject]@{
                Version = $entryVersion
                Changes = @($changes)
            }
        )

        $seenVersions[$entryVersion] =
            $true

        $previousVersion =
            $parsedVersion
    }

    if ([string]$changelog[0].Version -ne $version) {
        throw (
            "Consumer changelog must begin with current package " +
            "version '$version'."
        )
    }

    return [pscustomobject]@{
        Version = $version
        Changelog = @($changelog)
    }
}

$scriptDirectory =
    Split-Path -Parent $MyInvocation.MyCommand.Path

$repoRoot =
    [System.IO.Path]::GetFullPath(
        (Join-Path $scriptDirectory "..\..")
    )

$packageId =
    "Mz.CommandAPI.Consumer"

$tagPattern =
    (
        '^release/' +
        [regex]::Escape($packageId) +
        '/(?<version>[0-9]+\.[0-9]+\.[0-9]+)$'
    )

if ($Tag -notmatch $tagPattern) {
    throw (
        "Invalid release tag '$Tag'. Expected " +
        "release/Mz.CommandAPI.Consumer/<major.minor.patch>."
    )
}

$tagVersion =
    $Matches["version"]

$sourceDirectory =
    Join-Path `
        $repoRoot `
        "Consumer\Mz.CommandAPI.Consumer"

$versionFilePath =
    Join-Path `
        $sourceDirectory `
        "ApiVersionFile.cs"

$metadata =
    Read-ConsumerReleaseMetadata `
        -VersionFilePath $versionFilePath

$version =
    [string]$metadata.Version

$changelog =
    @($metadata.Changelog)

if ($tagVersion -ne $version) {
    throw (
        "Release tag version '$tagVersion' does not match " +
        "ApiVersionFile version '$version' for '$packageId'."
    )
}

$lockPath =
    Join-Path `
        $repoRoot `
        "selibs.lock.json"

if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
    throw "SELibs lock file not found: $lockPath"
}

$lock =
    Get-Content `
        -LiteralPath $lockPath `
        -Raw |
    ConvertFrom-Json

$apiProtocolProperty =
    $lock.packages.PSObject.Properties["Mz.ApiProtocol"]

if ($null -eq $apiProtocolProperty) {
    throw (
        "selibs.lock.json does not contain the required " +
        "Mz.ApiProtocol package."
    )
}

$apiProtocolVersion =
    [string]$apiProtocolProperty.Value.version

if (
    [string]::IsNullOrWhiteSpace($apiProtocolVersion) `
    -or $apiProtocolVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$'
) {
    throw (
        "selibs.lock.json contains an invalid Mz.ApiProtocol version."
    )
}

$testProject =
    Join-Path `
        $repoRoot `
        "Tests\CommandAPI.Consumer.Tests\CommandAPI.Consumer.Tests.csproj"

if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) {
    throw "Consumer test project not found: $testProject"
}

Write-Output "Package: $packageId"
Write-Output "Version: $version"
Write-Output "Tag: $Tag"
Write-Output "Mz.ApiProtocol dependency: $apiProtocolVersion"

if (-not $SkipTests) {
    Write-Output ""
    Write-Output "Running CommandAPI.Consumer.Tests"

    & dotnet test `
        $testProject `
        --configuration Release `
        --nologo `
        --verbosity minimal

    if ($LASTEXITCODE -ne 0) {
        throw "CommandAPI.Consumer.Tests failed."
    }
}
else {
    Write-Output ""
    Write-Output "Portable tests were skipped by request."
}

$outputFull =
    [System.IO.Path]::GetFullPath($OutputDirectory)

if (Test-Path -LiteralPath $outputFull) {
    Remove-Item `
        -LiteralPath $outputFull `
        -Recurse `
        -Force
}

New-Item `
    -ItemType Directory `
    -Path $outputFull `
    -Force |
    Out-Null

$stagingRoot =
    Join-Path $outputFull ".staging"

$librariesRoot =
    Join-Path $stagingRoot "Libraries"

$destinationDirectory =
    Join-Path $librariesRoot $packageId

New-Item `
    -ItemType Directory `
    -Path $destinationDirectory `
    -Force |
    Out-Null

try {
    $sourceFiles =
        @(
            Get-ChildItem `
                -LiteralPath $sourceDirectory `
                -Recurse `
                -File |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]' `
                    -and (
                        $_.Extension.Equals(
                            ".cs",
                            [System.StringComparison]::OrdinalIgnoreCase
                        ) `
                        -or $_.Name.Equals(
                            "README.md",
                            [System.StringComparison]::OrdinalIgnoreCase
                        ) `
                        -or $_.Name.Equals(
                            "Guide.md",
                            [System.StringComparison]::OrdinalIgnoreCase
                        )
                    )
            } |
            Sort-Object FullName
        )

    $csharpCount =
        @(
            $sourceFiles |
            Where-Object {
                $_.Extension.Equals(
                    ".cs",
                    [System.StringComparison]::OrdinalIgnoreCase
                )
            }
        ).Count

    $readmeCount =
        @(
            $sourceFiles |
            Where-Object {
                $_.Name.Equals(
                    "README.md",
                    [System.StringComparison]::OrdinalIgnoreCase
                )
            }
        ).Count

    if ($csharpCount -eq 0) {
        throw "Consumer package contains no C# source files."
    }

    if ($readmeCount -ne 1) {
        throw (
            "Consumer package must contain exactly one README.md; " +
            "found $readmeCount."
        )
    }

    foreach ($sourceFile in $sourceFiles) {
        $relativePath =
            $sourceFile.FullName.Substring(
                $sourceDirectory.Length
            ).TrimStart(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar
            )

        $destinationPath =
            Join-Path `
                $destinationDirectory `
                $relativePath

        $destinationParent =
            Split-Path -Parent $destinationPath

        New-Item `
            -ItemType Directory `
            -Path $destinationParent `
            -Force |
            Out-Null

        Copy-Item `
            -LiteralPath $sourceFile.FullName `
            -Destination $destinationPath
    }

    $assetBaseName =
        "$packageId-$version"

    $componentName =
        "$assetBaseName-component.zip"

    $manifestName =
        "$assetBaseName-package.json"

    $componentPath =
        Join-Path $outputFull $componentName

    $manifestPath =
        Join-Path $outputFull $manifestName

    Compress-Archive `
        -LiteralPath $librariesRoot `
        -DestinationPath $componentPath `
        -CompressionLevel Optimal

    $componentHash =
        (
            Get-FileHash `
                -LiteralPath $componentPath `
                -Algorithm SHA256
        ).Hash.ToLowerInvariant()

    $dependencies =
        [ordered]@{
            "Mz.ApiProtocol" = $apiProtocolVersion
        }

    $packageManifest =
        [ordered]@{
            schemaVersion = 1
            id = $packageId
            version = $version
            changelog = @(
                $changelog |
                ForEach-Object {
                    [ordered]@{
                        version = [string]$_.Version
                        changes = @($_.Changes)
                    }
                }
            )
            dependencies = $dependencies
            folders = @($packageId)
            component = [ordered]@{
                asset = $componentName
                sha256 = $componentHash
            }
        }

    Write-Utf8WithoutBom `
        -Path $manifestPath `
        -Text (
            ($packageManifest | ConvertTo-Json -Depth 10) +
            "`n"
        )

    $commit =
        (& git -C $repoRoot rev-parse HEAD)

    if ($LASTEXITCODE -ne 0) {
        throw "Could not determine the release commit."
    }

    $notesPath =
        Join-Path $outputFull "release-notes.md"

    $notesLines =
        @(
            "# $packageId $version"
            ""
            "SELibs source package generated from commit ``$commit``."
            ""
            "## Changes"
            ""
        )

    foreach ($change in @($changelog[0].Changes)) {
        $notesLines +=
            "- $change"
    }

    $notesLines +=
        @(
            ""
            "## Package"
            ""
            "- ID: ``$packageId``"
            "- Version: ``$version``"
            "- Component: ``$componentName``"
            "- SHA-256: ``$componentHash``"
            ""
            "## Included folders"
            ""
            "- ``Libraries/$packageId``"
            ""
            "## Exact dependencies"
            ""
            "- ``Mz.ApiProtocol`` ``$apiProtocolVersion``"
            ""
            "## Validation"
            ""
            "- ``CommandAPI.Consumer.Tests``"
        )

    Write-Utf8WithoutBom `
        -Path $notesPath `
        -Text (($notesLines -join "`n") + "`n")

    Write-GitHubOutput `
        -Name "component_path" `
        -Value $componentPath

    Write-GitHubOutput `
        -Name "manifest_path" `
        -Value $manifestPath

    Write-GitHubOutput `
        -Name "release_notes_path" `
        -Value $notesPath

    Write-GitHubOutput `
        -Name "release_title" `
        -Value "$packageId $version"

    Write-Output ""
    Write-Output "SELibs consumer release assets created:"
    Write-Output "  Manifest: $manifestPath"
    Write-Output "  Component: $componentPath"
    Write-Output "  SHA256: $componentHash"
    Write-Output "  Package files: $($sourceFiles.Count)"
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item `
            -LiteralPath $stagingRoot `
            -Recurse `
            -Force
    }
}
