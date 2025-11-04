param(
    [switch] $Complete
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$todoFile = Join-Path $repoRoot "todo.md"
$stateFile = Join-Path $repoRoot ".todo_state.json"

if (-not (Test-Path $todoFile)) {
    Write-Error "todo.md not found at $todoFile"
    exit 1
}

function Read-TodoLines {
    Get-Content -LiteralPath $todoFile -Encoding utf8
}

function Parse-TodoItems {
    param([string[]] $Lines)

    $pattern = '^- \[(?<state>[ xX])\] (?<text>.+)$'
    $items = @()
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i].Trim()
        if ($line -match $pattern) {
            $items += [pscustomobject]@{
                Index = $i
                State = $Matches.state
                Text  = $Matches.text
            }
        }
    }
    return $items
}

function Load-State {
    if (-not (Test-Path $stateFile)) {
        return @{}
    }
    try {
        return Get-Content -LiteralPath $stateFile -Raw -Encoding utf8 | ConvertFrom-Json
    } catch {
        return @{}
    }
}

function Save-State {
    param($State)
    ($State | ConvertTo-Json -Depth 2) | Set-Content -LiteralPath $stateFile -Encoding utf8
}

function Find-Current {
    param($State, $Items)
    if (-not $State.PSObject.Properties.Name.Contains("current_index")) {
        return $null
    }
    $idx = [int]$State.current_index
    foreach ($item in $Items) {
        if ($item.Index -eq $idx -and $item.State -ne 'x' -and $item.State -ne 'X') {
            return $item
        }
    }
    return $null
}

function Find-FirstIncomplete {
    param($Items)
    foreach ($item in $Items) {
        if ($item.State -ne 'x' -and $item.State -ne 'X') {
            return $item
        }
    }
    return $null
}

function Mark-Complete {
    param([string[]] $Lines, $Item)
    $Lines[$Item.Index] = "- [x] $($Item.Text)"
    $content = ($Lines -join "`n") + "`n"
    [System.IO.File]::WriteAllText($todoFile, $content, [System.Text.Encoding]::UTF8)
}

$lines = Read-TodoLines
$items = Parse-TodoItems -Lines $lines
if ($items.Count -eq 0) {
    Write-Output "todo.md has no actionable items."
    exit 0
}

$state = Load-State
$current = Find-Current -State $state -Items $items

if ($Complete) {
    if (-not $current) {
        $current = Find-FirstIncomplete -Items $items
        if (-not $current) {
            Write-Output "All todo items already complete."
            if ($state.current_index) {
                $state.PSObject.Properties.Remove("current_index") | Out-Null
                Save-State -State $state
            }
            exit 0
        }
    }
    Mark-Complete -Lines $lines -Item $current
    Write-Output "Marked complete: $($current.Text)"

    $refreshedLines = Read-TodoLines
    $refreshedItems = Parse-TodoItems -Lines $refreshedLines
    $nextItem = Find-FirstIncomplete -Items $refreshedItems
    if ($nextItem) {
        $state.current_index = $nextItem.Index
        Write-Output "Next up: $($nextItem.Text)"
    } else {
        if ($state.current_index) {
            $state.PSObject.Properties.Remove("current_index") | Out-Null
        }
        Write-Output "No remaining todo items."
    }
    Save-State -State $state
    exit 0
}

if (-not $current) {
    $current = Find-FirstIncomplete -Items $items
    if (-not $current) {
        Write-Output "All todo items complete."
        if ($state.current_index) {
            $state.PSObject.Properties.Remove("current_index") | Out-Null
            Save-State -State $state
        }
        exit 0
    }
    $state.current_index = $current.Index
    Save-State -State $state
}

Write-Output "Current todo: $($current.Text)"
