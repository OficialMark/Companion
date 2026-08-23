param(
    [string]$BaseUrl = "http://localhost:3000"
)

$ErrorActionPreference = "Stop"

Write-Host "Verificando frontend em $BaseUrl"
$frontend = Invoke-WebRequest -Uri $BaseUrl -UseBasicParsing -TimeoutSec 10
if ($frontend.StatusCode -ne 200) { throw "Frontend retornou $($frontend.StatusCode)." }

Write-Host "Verificando API pelo proxy"
$health = Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing -TimeoutSec 10
if ($health.StatusCode -ne 200) { throw "Health check retornou $($health.StatusCode)." }

Write-Host "Smoke test concluído com sucesso."
