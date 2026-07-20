$ErrorActionPreference = "Stop"

$sourceRoot =
    Split-Path -Parent $MyInvocation.MyCommand.Path

$modsRoot =
    Split-Path -Parent $sourceRoot

$protocolSource =
    Join-Path `
        $sourceRoot `
        "Data\Scripts\CommandAPI\Libraries\Mz.ApiProtocol"

$commonSource =
    Join-Path `
        $sourceRoot `
        "SmokeMods\Common\CommandApiSmokeConsumer.cs"

$timestamp =
    Get-Date -Format "yyyyMMdd-HHmmss"

$backupRoot =
    Join-Path `
        $modsRoot `
        "Backups\CommandApiSmokeConsumers-$timestamp"

$gameProcesses =
    @(
        Get-Process `
            -Name "SpaceEngineers" `
            -ErrorAction SilentlyContinue
    )

if ($gameProcesses.Count -gt 0) {
    throw "Close Space Engineers before syncing the smoke mods."
}

foreach ($required in @(
    $protocolSource,
    $commonSource
)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required source was not found: $required"
    }
}

$specifications =
    @(
        @{
            Name = "CommandApiSmokeAlpha"
            Project = "SmokeMods\CommandApiSmokeAlpha\CommandApiSmokeAlpha.csproj"
            Session = "SmokeMods\CommandApiSmokeAlpha\Data\Scripts\CommandApiSmokeAlpha\CommandApiSmokeAlphaSession.cs"
        },
        @{
            Name = "CommandApiSmokeBeta"
            Project = "SmokeMods\CommandApiSmokeBeta\CommandApiSmokeBeta.csproj"
            Session = "SmokeMods\CommandApiSmokeBeta\Data\Scripts\CommandApiSmokeBeta\CommandApiSmokeBetaSession.cs"
        }
    )

foreach ($specification in $specifications) {
    $project =
        Join-Path $sourceRoot $specification.Project

    & dotnet build $project --nologo

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $project"
    }
}

foreach ($specification in $specifications) {
    $name =
        $specification.Name

    $target =
        Join-Path $modsRoot $name

    $sessionSource =
        Join-Path `
            $sourceRoot `
            $specification.Session

    if (-not (Test-Path -LiteralPath $sessionSource -PathType Leaf)) {
        throw "Session source was not found: $sessionSource"
    }

    if (Test-Path -LiteralPath $target) {
        New-Item `
            -ItemType Directory `
            -Path $backupRoot `
            -Force |
            Out-Null

        Copy-Item `
            -LiteralPath $target `
            -Destination (Join-Path $backupRoot $name) `
            -Recurse
    }

    Remove-Item `
        -LiteralPath $target `
        -Recurse `
        -Force `
        -ErrorAction SilentlyContinue

    $scriptTarget =
        Join-Path `
            $target `
            ("Data\Scripts\" + $name)

    $protocolTarget =
        Join-Path `
            $scriptTarget `
            "Libraries\Mz.ApiProtocol"

    New-Item `
        -ItemType Directory `
        -Path $protocolTarget `
        -Force |
        Out-Null

    Copy-Item `
        -LiteralPath $sessionSource `
        -Destination (
            Join-Path `
                $scriptTarget `
                ($name + "Session.cs")
        )

    Copy-Item `
        -LiteralPath $commonSource `
        -Destination (
            Join-Path `
                $scriptTarget `
                "CommandApiSmokeConsumer.cs"
        )

    Copy-Item `
        -Path (Join-Path $protocolSource "*") `
        -Destination $protocolTarget `
        -Recurse `
        -Force

    $protocolCount =
        @(
            Get-ChildItem `
                -LiteralPath $protocolTarget `
                -Recurse `
                -File `
                -Filter "*.cs"
        ).Count

    if ($protocolCount -ne 36) {
        throw "$name received $protocolCount protocol files instead of 36."
    }

    "Synchronized: $target"
}

""
"Load these local mods with CommandAPI:"
"- CommandApiSmokeAlpha"
"- CommandApiSmokeBeta"
""
"Run:"
"/cmd smoke"
"/cmd smoke.alpha"
"/cmd smoke.beta"
"/cmd help"
