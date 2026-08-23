$ErrorActionPreference = 'Stop'

$rawInput = [Console]::In.ReadToEnd()

$denialReason = 'Existing EF Core migration files are protected. Create a new migration instead of modifying or deleting an existing migration.'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Deny-ProtectedMigration {
    [ordered]@{
        hookSpecificOutput = [ordered]@{
            hookEventName = 'PreToolUse'
            permissionDecision = 'deny'
            permissionDecisionReason = $denialReason
        }
    } | ConvertTo-Json -Compress

    exit 0
}

function Test-ProtectedMigrationPath {
    param(
        [AllowNull()]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $normalizedPath = ($Path.Trim().Trim('"') -replace '\\', '/')
    if ([IO.Path]::GetExtension($normalizedPath) -ine '.cs' -or $normalizedPath -notmatch '(^|/)Migrations/') {
        return $false
    }

    $candidatePath = if ([IO.Path]::IsPathRooted($normalizedPath)) {
        $normalizedPath
    }
    else {
        Join-Path $repositoryRoot $normalizedPath
    }

    return Test-Path -LiteralPath $candidatePath -PathType Leaf
}

if ([string]::IsNullOrWhiteSpace($rawInput)) {
    exit 0
}

try {
    $payload = $rawInput | ConvertFrom-Json
}
catch {
    exit 0
}

$toolName = [string]$payload.tool_name
$toolInput = $payload.tool_input

if ($toolName -eq 'apply_patch') {
    $patch = [string]$toolInput.input
    foreach ($line in ($patch -split "`r?`n")) {
        if ($line -match '^\s*\*\*\*\s+(Update|Delete) File:\s*(.+?)\s*$' -and (Test-ProtectedMigrationPath $Matches[2])) {
            Deny-ProtectedMigration
        }
    }
}

if ($toolName -match '^(Edit|Write)$') {
    foreach ($path in @([string]$toolInput.filePath, [string]$toolInput.path, [string]$toolInput.target_file)) {
        if (Test-ProtectedMigrationPath $path) {
            Deny-ProtectedMigration
        }
    }
}

if ($toolName -match '^(Bash|Terminal|run_in_terminal)$') {
    $command = [string]$toolInput.command
    foreach ($match in [regex]::Matches($command, '(?i)(?:[A-Za-z]:[\\/]|\.?[\\/])[^\s"''`]+\.cs\b')) {
        if (Test-ProtectedMigrationPath $match.Value.TrimEnd(',;)]}')) {
            Deny-ProtectedMigration
        }
    }
}

exit 0