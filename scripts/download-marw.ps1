param([int]$Concurrency = 3)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $root 'data/mushaf/editions/marw-newMushaf/20260913-full'
New-Item -ItemType Directory -Force $destination | Out-Null
$records = 1..604 | ForEach-Object -Parallel {
    $n = $_
    foreach ($extension in @('png','json')) {
        $name = 'page-{0:D3}.{1}' -f $n,$extension
        $path = Join-Path $using:destination $name
        $endpoint = if ($extension -eq 'png') { "pages/$n.png" } else { "ayat-shadows/$n" }
        $url = "https://mushaf.marw.dz/web/api/v1/masahif/newMushaf/$endpoint"
        if (-not (Test-Path -LiteralPath $path)) {
            $success = $false
            for ($attempt = 0; $attempt -lt 5; $attempt++) {
                try {
                    Invoke-WebRequest -Uri $url -OutFile "$path.part" -TimeoutSec 30 -ErrorAction Stop
                    Move-Item -LiteralPath "$path.part" -Destination $path
                    $success = $true
                    break
                } catch {
                    if ($attempt -eq 4) { throw }
                    Start-Sleep -Seconds ([Math]::Pow(2,$attempt))
                }
            }
            if (-not $success) { throw "Download failed: $url" }
        }
        [pscustomobject]@{file=$name;url=$url;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();sizeBytes=(Get-Item -LiteralPath $path).Length}
    }
    if ($n % 25 -eq 0) { [Console]::WriteLine("Page $n downloaded") }
} -ThrottleLimit $Concurrency
if ($records.Count -ne 1208) { throw "Incomplete download: $($records.Count)/1208 files" }
$manifest = @{edition='newMushaf';revision='20260913-full';pageCount=604;source='https://mushaf.marw.dz/web/ar/mushaf/newMushaf/1';recordedAtUtc=[DateTime]::UtcNow.ToString('o');status='Local corpus; human review and audio mapping pending';files=@($records | Sort-Object file)}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8NoBOM (Join-Path $destination 'manifest.json.part')
Move-Item -LiteralPath (Join-Path $destination 'manifest.json.part') -Destination (Join-Path $destination 'manifest.json') -Force
'604 pages downloaded; manifest complete.'
