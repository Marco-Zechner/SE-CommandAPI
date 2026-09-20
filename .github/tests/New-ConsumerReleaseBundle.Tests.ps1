$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot =
    [System.IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot "..\..")
    )

$bundleScript =
    Join-Path `
        $repoRoot `
        ".github\scripts\New-ConsumerReleaseBundle.ps1"

$versionFilePath =
    Join-Path `
        $repoRoot `
        "Consumer\Mz.CommandAPI.Consumer\ApiVersionFile.cs"

$lockPath =
    Join-Path `
        $repoRoot `
        "selibs.lock.json"

$script:Passed =
    0

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }

    $script:Passed++
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]
        $Expected,

        [Parameter(Mandatory = $true)]
        $Actual,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw (
            "$Message`n" +
            "Expected: '$Expected'`n" +
            "Actual:   '$Actual'"
        )
    }

    $script:Passed++
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedMessagePart
    )

    try {
        & $Action
    }
    catch {
        if (
            $_.Exception.Message -notlike
            "*$ExpectedMessagePart*"
        ) {
            throw (
                "Expected error containing '$ExpectedMessagePart', " +
                "but received '$($_.Exception.Message)'."
            )
        }

        $script:Passed++
        return
    }

    throw (
        "Expected an exception containing " +
        "'$ExpectedMessagePart'."
    )
}

function Read-CurrentVersion {
    $text =
        Get-Content `
            -LiteralPath $versionFilePath `
            -Raw

    $parts =
        @()

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
            throw "Could not read consumer $name version."
        }

        $parts +=
            $match.Groups["value"].Value
    }

    return $parts -join "."
}

function Get-ZipEntries {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $archive =
        [System.IO.Compression.ZipFile]::OpenRead($Path)

    try {
        return @(
            $archive.Entries |
            ForEach-Object {
                $_.FullName.Replace("\", "/")
            } |
            Sort-Object
        )
    }
    finally {
        $archive.Dispose()
    }
}

$version =
    Read-CurrentVersion

$tag =
    "release/Mz.CommandAPI.Consumer/$version"

$lock =
    Get-Content `
        -LiteralPath $lockPath `
        -Raw |
    ConvertFrom-Json

$apiProtocolVersion =
    [string]$lock.packages."Mz.ApiProtocol".version

$semanticVersioningVersion =
    [string]$lock.packages."Mz.SemanticVersioning".version

$testRoot =
    Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        (
            "commandapi-consumer-package-" +
            [Guid]::NewGuid().ToString("N")
        )

New-Item `
    -ItemType Directory `
    -Path $testRoot `
    -Force |
    Out-Null

try {
    & $bundleScript `
        -Tag $tag `
        -OutputDirectory $testRoot `
        -SkipTests |
        Out-Null

    $manifestPath =
        Join-Path `
            $testRoot `
            (
                "Mz.CommandAPI.Consumer-" +
                $version +
                "-package.json"
            )

    $componentPath =
        Join-Path `
            $testRoot `
            (
                "Mz.CommandAPI.Consumer-" +
                $version +
                "-component.zip"
            )

    Assert-True `
        -Condition (
            Test-Path `
                -LiteralPath $manifestPath `
                -PathType Leaf
        ) `
        -Message "Consumer package manifest is missing."

    Assert-True `
        -Condition (
            Test-Path `
                -LiteralPath $componentPath `
                -PathType Leaf
        ) `
        -Message "Consumer component archive is missing."

    $manifest =
        Get-Content `
            -LiteralPath $manifestPath `
            -Raw |
        ConvertFrom-Json

    Assert-Equal `
        -Expected 1 `
        -Actual ([int]$manifest.schemaVersion) `
        -Message "Consumer manifest has the wrong schema version."

    Assert-Equal `
        -Expected "Mz.CommandAPI.Consumer" `
        -Actual ([string]$manifest.id) `
        -Message "Consumer manifest has the wrong package ID."

    Assert-Equal `
        -Expected $version `
        -Actual ([string]$manifest.version) `
        -Message "Consumer manifest has the wrong version."

    Assert-Equal `
        -Expected $version `
        -Actual ([string]$manifest.changelog[0].version) `
        -Message "Consumer changelog does not begin with the current version."

    Assert-True `
        -Condition (
            @($manifest.changelog[0].changes).Count -gt 0
        ) `
        -Message "Consumer current changelog is empty."

    Assert-Equal `
        -Expected 2 `
        -Actual @(
            $manifest.dependencies.PSObject.Properties
        ).Count `
        -Message "Consumer manifest declares the wrong dependency count."

    Assert-Equal `
        -Expected $apiProtocolVersion `
        -Actual (
            [string]$manifest.dependencies."Mz.ApiProtocol"
        ) `
        -Message "Consumer manifest has the wrong ApiProtocol dependency."

    Assert-Equal `
        -Expected $semanticVersioningVersion `
        -Actual (
            [string]$manifest.dependencies."Mz.SemanticVersioning"
        ) `
        -Message "Consumer manifest has the wrong SemanticVersioning dependency."

    Assert-Equal `
        -Expected 1 `
        -Actual @($manifest.folders).Count `
        -Message "Consumer manifest declares the wrong folder count."

    Assert-Equal `
        -Expected "Mz.CommandAPI.Consumer" `
        -Actual ([string]$manifest.folders[0]) `
        -Message "Consumer manifest declares the wrong owned folder."

    $actualHash =
        (
            Get-FileHash `
                -LiteralPath $componentPath `
                -Algorithm SHA256
        ).Hash.ToLowerInvariant()

    Assert-Equal `
        -Expected $actualHash `
        -Actual ([string]$manifest.component.sha256) `
        -Message "Consumer manifest checksum is incorrect."

    $entries =
        @(Get-ZipEntries -Path $componentPath)

    Assert-True `
        -Condition ($entries.Count -gt 0) `
        -Message "Consumer archive is empty."

    Assert-True `
        -Condition (
            @(
                $entries |
                Where-Object {
                    -not $_.StartsWith(
                        "Libraries/Mz.CommandAPI.Consumer/",
                        [System.StringComparison]::Ordinal
                    )
                }
            ).Count -eq 0
        ) `
        -Message "Consumer archive contains undeclared paths."

    foreach ($expectedEntry in @(
        "Libraries/Mz.CommandAPI.Consumer/ApiVersionFile.cs"
        "Libraries/Mz.CommandAPI.Consumer/CommandApiClient.cs"
        "Libraries/Mz.CommandAPI.Consumer/CommandRegistration.cs"
        "Libraries/Mz.CommandAPI.Consumer/CommandRequest.cs"
        "Libraries/Mz.CommandAPI.Consumer/CommandResponse.cs"
        "Libraries/Mz.CommandAPI.Consumer/Guide.md"
        "Libraries/Mz.CommandAPI.Consumer/README.md"
    )) {
        Assert-True `
            -Condition ($entries -contains $expectedEntry) `
            -Message "Consumer archive is missing '$expectedEntry'."
    }

    Assert-Equal `
        -Expected 0 `
        -Actual @(
            $entries |
            Where-Object {
                $_ -like "Libraries/Mz.ApiProtocol*"
            }
        ).Count `
        -Message "Consumer archive embeds its ApiProtocol dependency."

    Assert-Equal `
        -Expected 0 `
        -Actual @(
            $entries |
            Where-Object {
                $_ -like "Libraries/Mz.SemanticVersioning*"
            }
        ).Count `
        -Message "Consumer archive embeds its SemanticVersioning dependency."

    Assert-Throws `
        -Action {
            & $bundleScript `
                -Tag "consumer/v$version" `
                -OutputDirectory (
                    Join-Path $testRoot "bad-tag"
                ) `
                -SkipTests |
                Out-Null
        } `
        -ExpectedMessagePart "Invalid release tag"

    Assert-Throws `
        -Action {
            & $bundleScript `
                -Tag "release/Mz.CommandAPI.Consumer/9.9.9" `
                -OutputDirectory (
                    Join-Path $testRoot "bad-version"
                ) `
                -SkipTests |
                Out-Null
        } `
        -ExpectedMessagePart "does not match ApiVersionFile version"

    Write-Output (
        "OK CommandAPI consumer package tests passed: " +
        "$script:Passed assertions"
    )
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item `
            -LiteralPath $testRoot `
            -Recurse `
            -Force
    }
}
