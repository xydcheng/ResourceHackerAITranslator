Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root "dist\ResourceHackerAITranslator.exe"
$config = Join-Path $root "dist\translator.config.json"

if (-not (Test-Path -LiteralPath $exe)) {
    throw "EXE not found: $exe"
}

if (-not (Test-Path -LiteralPath $config)) {
    throw "Config not found: $config"
}

$json = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
$required = "openai","deepseek","doubao","qwen","microsoft","google","youdao","custom-openai-compatible"
foreach ($name in $required) {
    if (-not $json.providers.PSObject.Properties[$name]) {
        throw "Missing provider: $name"
    }
}

$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 2
if ($p.HasExited) {
    throw "EXE exited during smoke test with code $($p.ExitCode)"
}
$p.CloseMainWindow() | Out-Null
Start-Sleep -Seconds 1
if (-not $p.HasExited) {
    Stop-Process -Id $p.Id -Force
}

"Config and EXE smoke test passed."
