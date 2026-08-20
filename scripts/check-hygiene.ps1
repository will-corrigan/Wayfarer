# Scans tracked files and commit messages for disallowed content.
# The pattern list is intentionally NOT stored in this repository. Supply it via:
#   - the WAYFARER_HYGIENE_PATTERNS environment variable (semicolon-separated regexes), or
#   - a git-ignored scripts/hygiene.local.txt (one regex per line).
# Matched patterns are never echoed; output shows only file locations.
$sources = @()
if ($env:WAYFARER_HYGIENE_PATTERNS) {
    $sources += ($env:WAYFARER_HYGIENE_PATTERNS -split ';')
}
$localFile = Join-Path $PSScriptRoot 'hygiene.local.txt'
if (Test-Path $localFile) {
    $sources += Get-Content $localFile
}
$patterns = $sources | Where-Object { $_ -and $_.Trim() } | ForEach-Object { $_.Trim() } | Select-Object -Unique
if (-not $patterns) {
    Write-Host 'hygiene: no pattern source available, skipping'
    exit 0
}

$failed = $false
$files = git ls-files | Where-Object { $_ -notmatch '\.png$' }
foreach ($p in $patterns) {
    $hits = $files | ForEach-Object { Select-String -Path $_ -Pattern $p -CaseSensitive:$false } | Where-Object { $_ }
    if ($hits) {
        $hits | ForEach-Object { Write-Host "HYGIENE VIOLATION: $($_.Path):$($_.LineNumber)" }
        $failed = $true
    }
}

$log = git log --format='%H %B'
foreach ($p in $patterns) {
    $hit = $log | Select-String -Pattern $p -CaseSensitive:$false | Select-Object -First 1
    if ($hit) {
        $sha = ($hit.Line -split ' ')[0]
        Write-Host "HYGIENE VIOLATION: commit message in $sha matches a disallowed pattern"
        $failed = $true
    }
}

if ($failed) { exit 1 } else { Write-Host 'hygiene: clean' }
