$ErrorActionPreference = "Stop"

$sourceRoot =
    Split-Path -Parent $MyInvocation.MyCommand.Path

$modsRoot =
    Split-Path -Parent $sourceRoot

$commonSource =
    Join-Path `
        $sourceRoot `
        "SmokeMods\Common\CommandApiSmokeConsumer.cs"

$sourceGroups =
    @(
        @{
            Name = "Mz.CommandAPI.Consumer"
            Source = Join-Path `
                $sourceRoot `
                "Consumer\Mz.CommandAPI.Consumer"
            Target = "Libraries\Mz.CommandAPI.Consumer"
        },
        @{
            Name = "Mz.ApiProtocol.Core"
            Source = Join-Path `
                $sourceRoot `
                "Data\Scripts\CommandAPI\Libraries\Mz.ApiProtocol.Core"
            Target = "Libraries\Mz.ApiProtocol.Core"
        },
        @{
            Name = "Mz.ApiProtocol.SpaceEngineers"
            Source = Join-Path `
                $sourceRoot `
                "Data\Scripts\CommandAPI\Libraries\Mz.ApiProtocol.SpaceEngineers"
            Target = "Libraries\Mz.ApiProtocol.SpaceEngineers"
        },
        @{
            Name = "Mz.SemanticVersioning"
            Source = Join-Path `
                $sourceRoot `
                "Data\Scripts\CommandAPI\Libraries\Mz.SemanticVersioning"
            Target = "Libraries\Mz.SemanticVersioning"
        }
    )

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

if (-not (Test-Path -LiteralPath $commonSource -PathType Leaf)) {
    throw "Required source was not found: $commonSource"
}

foreach ($group in $sourceGroups) {
    if (
        -not (
            Test-Path `
                -LiteralPath $group.Source `
                -PathType Container
        )
    ) {
        throw "Required source was not found: $($group.Source)"
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

    New-Item `
        -ItemType Directory `
        -Path $scriptTarget `
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

    foreach ($group in $sourceGroups) {
        $groupTarget =
            Join-Path `
                $scriptTarget `
                $group.Target

        New-Item `
            -ItemType Directory `
            -Path $groupTarget `
            -Force |
            Out-Null

        Copy-Item `
            -Path (Join-Path $group.Source "*") `
            -Destination $groupTarget `
            -Recurse `
            -Force

        $sourceCount =
            @(
                Get-ChildItem `
                    -LiteralPath $group.Source `
                    -Recurse `
                    -File `
                    -Filter "*.cs"
            ).Count

        $targetCount =
            @(
                Get-ChildItem `
                    -LiteralPath $groupTarget `
                    -Recurse `
                    -File `
                    -Filter "*.cs"
            ).Count

        if ($targetCount -ne $sourceCount) {
            throw (
                "{0} received {1} {2} source files instead of {3}." -f
                $name,
                $targetCount,
                $group.Name,
                $sourceCount
            )
        }
    }

    Write-Output "Synchronized: $target"
}

Write-Output ""
Write-Output "Load these local mods with CommandAPI:"
Write-Output "- CommandApiSmokeAlpha"
Write-Output "- CommandApiSmokeBeta"
Write-Output ""
Write-Output "Run:"
Write-Output "/smoke smoke"
Write-Output "/smoke smoke.alpha"
Write-Output "/smoke smoke.beta"
Write-Output "/cmd help"
