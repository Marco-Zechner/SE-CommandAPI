$ErrorActionPreference = "Stop"

$workshopId = "3768390977"
$serviceName = "MzNetworkingSmoke"

$sourceRoot =
    Split-Path -Parent $MyInvocation.MyCommand.Path

$modsRoot =
    Split-Path -Parent $sourceRoot

$stubRoot =
    Join-Path $modsRoot "StubModForDS"

$sourceData =
    Join-Path $sourceRoot "Data"

$clientTarget =
    "E:\SteamLibrary\steamapps\workshop\content\244850\$workshopId"

$serverTarget =
    "C:\ProgramData\SpaceEngineersDedicated\MzNetworkingSmoke\content\244850\$workshopId"

$timestamp =
    Get-Date -Format "yyyyMMdd-HHmmss"

$backupRoot =
    Join-Path `
        $modsRoot `
        "Backups\CommandAPISync-$timestamp"

$stagingRoot =
    Join-Path `
        $env:TEMP `
        "CommandAPISync-$timestamp"

function Section {
    param(
        [string] $Label
    )

    ""
    "===== $Label ====="
}

function Get-RelativeHashMap {
    param(
        [string] $Root
    )

    $result = @{}

    Get-ChildItem `
        -LiteralPath $Root `
        -Recurse `
        -File |
    Sort-Object FullName |
    ForEach-Object {
        $relative =
            $_.FullName.Substring(
                $Root.Length
            ).TrimStart('\')

        $result[$relative] =
            (Get-FileHash `
                -LiteralPath $_.FullName `
                -Algorithm SHA256
            ).Hash
    }

    return $result
}

function Assert-EqualTrees {
    param(
        [string] $ExpectedRoot,
        [string] $ActualRoot,
        [string] $Label
    )

    $expected =
        Get-RelativeHashMap -Root $ExpectedRoot

    $actual =
        Get-RelativeHashMap -Root $ActualRoot

    $expectedKeys =
        @($expected.Keys | Sort-Object)

    $actualKeys =
        @($actual.Keys | Sort-Object)

    if (($expectedKeys -join "`n") -ne ($actualKeys -join "`n")) {
        throw "$Label file list differs from staging."
    }

    foreach ($relative in $expectedKeys) {
        if ($expected[$relative] -ne $actual[$relative]) {
            throw "$Label hash mismatch: $relative"
        }
    }
}

function Copy-DeployableData {
    param(
        [string] $Source,
        [string] $Destination
    )

    New-Item `
        -ItemType Directory `
        -Path $Destination `
        -Force |
        Out-Null

    Get-ChildItem `
        -LiteralPath $Source `
        -Recurse `
        -File |
    ForEach-Object {
        $relative =
            $_.FullName.Substring(
                $Source.Length
            ).TrimStart('\')

        $isBuildArtifact =
            $relative.StartsWith(
                "bin\",
                [StringComparison]::OrdinalIgnoreCase
            ) -or
            $relative.StartsWith(
                "obj\",
                [StringComparison]::OrdinalIgnoreCase
            ) -or
            $relative.Equals(
                "CommandAPI.csproj",
                [StringComparison]::OrdinalIgnoreCase
            )

        if (-not $isBuildArtifact) {
            $destinationFile =
                Join-Path $Destination $relative

            $destinationDirectory =
                Split-Path -Parent $destinationFile

            New-Item `
                -ItemType Directory `
                -Path $destinationDirectory `
                -Force |
                Out-Null

            Copy-Item `
                -LiteralPath $_.FullName `
                -Destination $destinationFile `
                -Force
        }
    }
}

Section "Guard elevation and processes"

$currentIdentity =
    [Security.Principal.WindowsIdentity]::GetCurrent()

$currentPrincipal =
    New-Object Security.Principal.WindowsPrincipal(
        $currentIdentity
    )

$isAdministrator =
    $currentPrincipal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )

if (-not $isAdministrator) {
    throw "Run 'sync to DS and client.bat' as Administrator."
}

$service =
    Get-Service -Name $serviceName -ErrorAction Stop

if ($service.Status -ne "Stopped") {
    throw "Stop the $serviceName dedicated server before syncing."
}

$dedicatedProcesses =
    @(
        Get-Process `
            -Name "SpaceEngineersDedicated" `
            -ErrorAction SilentlyContinue
    )

if ($dedicatedProcesses.Count -gt 0) {
    throw "Close the Space Engineers Dedicated Server GUI and all dedicated-server processes before syncing."
}

$gameProcesses =
    @(
        Get-Process `
            -Name "SpaceEngineers" `
            -ErrorAction SilentlyContinue
    )

if ($gameProcesses.Count -gt 0) {
    throw "Close the Space Engineers game client before syncing."
}

Section "Guard source and destinations"

foreach ($path in @(
    $sourceData,
    $clientTarget,
    $serverTarget
)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required path was not found: $path"
    }
}

$sessionSource =
    Join-Path `
        $sourceData `
        "Scripts\CommandAPI\CommandApiSession.cs"

if (-not (Test-Path -LiteralPath $sessionSource -PathType Leaf)) {
    throw "CommandAPI session source was not found: $sessionSource"
}

$metadataCandidates = @(
    (Join-Path $stubRoot "metadata.mod"),
    (Join-Path $clientTarget "metadata.mod"),
    (Join-Path $serverTarget "metadata.mod")
)

$metadataSource =
    $metadataCandidates |
    Where-Object {
        Test-Path -LiteralPath $_ -PathType Leaf
    } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($metadataSource)) {
    throw "No metadata.mod source was found."
}

try {
    Section "Prepare staging package"

    New-Item `
        -ItemType Directory `
        -Path $stagingRoot `
        -Force |
        Out-Null

    Copy-DeployableData `
        -Source $sourceData `
        -Destination (Join-Path $stagingRoot "Data")

    Copy-Item `
        -LiteralPath $metadataSource `
        -Destination (Join-Path $stagingRoot "metadata.mod")

    $stagedSession =
        Join-Path `
            $stagingRoot `
            "Data\Scripts\CommandAPI\CommandApiSession.cs"

    if (-not (Test-Path -LiteralPath $stagedSession -PathType Leaf)) {
        throw "CommandAPI session is missing from staging: $stagedSession"
    }

    Section "Back up current Workshop caches"

    New-Item `
        -ItemType Directory `
        -Path $backupRoot `
        -Force |
        Out-Null

    Copy-Item `
        -LiteralPath $clientTarget `
        -Destination (Join-Path $backupRoot "client") `
        -Recurse

    Copy-Item `
        -LiteralPath $serverTarget `
        -Destination (Join-Path $backupRoot "server") `
        -Recurse

    Section "Replace client Workshop cache"

    Remove-Item `
        -LiteralPath $clientTarget `
        -Recurse `
        -Force

    New-Item `
        -ItemType Directory `
        -Path $clientTarget `
        -Force |
        Out-Null

    Copy-Item `
        -Path (Join-Path $stagingRoot "*") `
        -Destination $clientTarget `
        -Recurse `
        -Force

    Section "Replace dedicated-server Workshop cache"

    Remove-Item `
        -LiteralPath $serverTarget `
        -Recurse `
        -Force

    New-Item `
        -ItemType Directory `
        -Path $serverTarget `
        -Force |
        Out-Null

    Copy-Item `
        -Path (Join-Path $stagingRoot "*") `
        -Destination $serverTarget `
        -Recurse `
        -Force

    Section "Verify synchronized content"

    Assert-EqualTrees `
        -ExpectedRoot $stagingRoot `
        -ActualRoot $clientTarget `
        -Label "Client cache"

    Assert-EqualTrees `
        -ExpectedRoot $stagingRoot `
        -ActualRoot $serverTarget `
        -Label "Dedicated-server cache"

    foreach ($target in @(
        $clientTarget,
        $serverTarget
    )) {
        $expectedSession =
            Join-Path `
                $target `
                "Data\Scripts\CommandAPI\CommandApiSession.cs"

        $oldSmokeSource =
            Join-Path `
                $target `
                "Data\Scripts\MzNetworkingSmoke"

        $oldStub =
            Join-Path `
                $target `
                "Data\Scripts\StubModForDS\Stub.cs"

        if (-not (Test-Path -LiteralPath $expectedSession -PathType Leaf)) {
            throw "CommandAPI session is missing after sync: $expectedSession"
        }

        if (Test-Path -LiteralPath $oldSmokeSource) {
            throw "Old MzNetworkingSmoke source remains after sync: $oldSmokeSource"
        }

        if (Test-Path -LiteralPath $oldStub) {
            throw "Old stub source remains after sync: $oldStub"
        }
    }
}
finally {
    Section "Cleanup staging"

    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item `
            -LiteralPath $stagingRoot `
            -Recurse `
            -Force
    }
}

Section "Sync completed"

"Workshop ID: $workshopId"
"Client cache: $clientTarget"
"Server cache: $serverTarget"
"Backup: $backupRoot"
"Start the MzNetworkingSmoke server through the Dedicated Server GUI."
"Then start Space Engineers, connect, and run:"
"/cmd help"
"/cmd ping"
"/cmd whoami"
"/cmd status"