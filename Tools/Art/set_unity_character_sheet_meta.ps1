param(
    [Parameter(Mandatory = $true)]
    [string]$MetaPath,

    [Parameter(Mandatory = $true)]
    [string]$CharacterId,

    [Parameter(Mandatory = $true)]
    [ValidateSet("idle", "walk")]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [int]$FramesPerDirection
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$cellSize = 128
$pivotY = 12.0 / $cellSize
$directions = @("down", "left", "right", "up")
$resolvedMetaPath = (Resolve-Path -LiteralPath $MetaPath).Path
$meta = Get-Content -Raw -LiteralPath $resolvedMetaPath

$internalTable = [System.Text.StringBuilder]::new()
$sprites = [System.Text.StringBuilder]::new()
$nameTable = [System.Text.StringBuilder]::new()
$index = 0

foreach ($direction in $directions)
{
    $rowIndex = [Array]::IndexOf($directions, $direction)
    $sourceY = ($directions.Count - $rowIndex - 1) * $cellSize

    for ($frameIndex = 0; $frameIndex -lt $FramesPerDirection; $frameIndex++)
    {
        $spriteName = "{0}_{1}_{2}_{3:D2}" -f $CharacterId, $Action, $direction, $frameIndex
        $internalId = 21300000 + ($index * 2)
        $hashBytes = [System.Security.Cryptography.SHA256]::HashData(
            [System.Text.Encoding]::UTF8.GetBytes($spriteName))
        $spriteId = [Convert]::ToHexString($hashBytes).Substring(0, 32).ToLowerInvariant()
        $sourceX = $frameIndex * $cellSize

        [void]$internalTable.AppendLine("  - first:")
        [void]$internalTable.AppendLine("      213: $internalId")
        [void]$internalTable.AppendLine("    second: $spriteName")

        [void]$sprites.AppendLine("    - serializedVersion: 2")
        [void]$sprites.AppendLine("      name: $spriteName")
        [void]$sprites.AppendLine("      rect:")
        [void]$sprites.AppendLine("        serializedVersion: 2")
        [void]$sprites.AppendLine("        x: $sourceX")
        [void]$sprites.AppendLine("        y: $sourceY")
        [void]$sprites.AppendLine("        width: $cellSize")
        [void]$sprites.AppendLine("        height: $cellSize")
        [void]$sprites.AppendLine("      alignment: 9")
        [void]$sprites.AppendLine("      pivot: {x: 0.5, y: $pivotY}")
        [void]$sprites.AppendLine("      border: {x: 0, y: 0, z: 0, w: 0}")
        [void]$sprites.AppendLine("      outline: []")
        [void]$sprites.AppendLine("      physicsShape: []")
        [void]$sprites.AppendLine("      tessellationDetail: 0")
        [void]$sprites.AppendLine("      bones: []")
        [void]$sprites.AppendLine("      spriteID: $spriteId")
        [void]$sprites.AppendLine("      internalID: $internalId")
        [void]$sprites.AppendLine("      vertices: []")
        [void]$sprites.AppendLine("      indices: ")
        [void]$sprites.AppendLine("      edges: []")
        [void]$sprites.AppendLine("      weights: []")

        [void]$nameTable.AppendLine("      ${spriteName}: $internalId")
        $index++
    }
}

$internalBlock = "  internalIDToNameTable:`r`n" + $internalTable.ToString() + "  externalObjects:"
$meta = [regex]::Replace(
    $meta,
    "(?ms)^  internalIDToNameTable:.*?^  externalObjects:",
    $internalBlock)

$spriteSheet = [System.Text.StringBuilder]::new()
[void]$spriteSheet.AppendLine("  spriteSheet:")
[void]$spriteSheet.AppendLine("    serializedVersion: 2")
[void]$spriteSheet.AppendLine("    sprites:")
[void]$spriteSheet.Append($sprites.ToString())
[void]$spriteSheet.AppendLine("    outline: []")
[void]$spriteSheet.AppendLine("    customData: ")
[void]$spriteSheet.AppendLine("    physicsShape: []")
[void]$spriteSheet.AppendLine("    bones: []")
[void]$spriteSheet.AppendLine("    spriteID: ")
[void]$spriteSheet.AppendLine("    internalID: 0")
[void]$spriteSheet.AppendLine("    vertices: []")
[void]$spriteSheet.AppendLine("    indices: ")
[void]$spriteSheet.AppendLine("    edges: []")
[void]$spriteSheet.AppendLine("    weights: []")
[void]$spriteSheet.AppendLine("    secondaryTextures: []")
[void]$spriteSheet.AppendLine("    spriteCustomMetadata:")
[void]$spriteSheet.AppendLine("      entries: []")
[void]$spriteSheet.AppendLine("    nameFileIdTable:")
[void]$spriteSheet.Append($nameTable.ToString())
[void]$spriteSheet.Append("  mipmapLimitGroupName:")

$meta = [regex]::Replace(
    $meta,
    "(?ms)^  spriteSheet:.*?^  mipmapLimitGroupName:",
    $spriteSheet.ToString())

[System.IO.File]::WriteAllText(
    $resolvedMetaPath,
    $meta,
    [System.Text.UTF8Encoding]::new($false))

Write-Output "Configured $index sprite rects in $resolvedMetaPath"
