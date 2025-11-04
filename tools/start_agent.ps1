param()

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$agentsFile = Join-Path $repoRoot "agents.md"
$nextFile = Join-Path $repoRoot "next.md"
$todoFile = Join-Path $repoRoot "todo.md"

if (-not (Test-Path $agentsFile)) {
    Write-Error "agents.md not found at $agentsFile"
    exit 1
}

Write-Output "=== agents.md ==="
Get-Content -LiteralPath $agentsFile -Encoding utf8

if (Test-Path $nextFile) {
    Write-Output ""
    Write-Output "=== next.md ==="
    Get-Content -LiteralPath $nextFile -Encoding utf8
}

if (Test-Path $todoFile) {
    Write-Output ""
    Write-Output "=== First TODO Item ==="
    $todoOutput = & (Join-Path $scriptRoot "manage_todo.ps1")
    Write-Output $todoOutput
}
