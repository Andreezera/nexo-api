# Sobe o NEXO para demonstracao com o WhatsApp real.
# 1) inicia a API  2) abre um tunel publico (Cloudflare)  3) aponta o webhook da Meta para o endereco novo.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\iniciar-demo.ps1
# Precisa dos segredos ja cadastrados no user-secrets (ver docs/rodar-no-notebook.md).

param(
    [int]$Porta = 5161,
    [string]$AppId = "1185763307470849",
    [string]$VersaoGraph = "v27.0"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$logs = Join-Path $raiz "scripts\logs"
New-Item -ItemType Directory -Force $logs | Out-Null

function Passo($texto) { Write-Host ""; Write-Host "==> $texto" -ForegroundColor Cyan }

# ---- 0. pre-requisitos ----
Passo "Conferindo pre-requisitos"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet nao encontrado. Instale o .NET 10 SDK." }
$cloudflared = (Get-Command cloudflared -ErrorAction SilentlyContinue).Source
if (-not $cloudflared) {
    $padrao = "C:\Program Files (x86)\cloudflared\cloudflared.exe"
    if (Test-Path $padrao) { $cloudflared = $padrao } else { throw "cloudflared nao encontrado. Instale com: winget install --id Cloudflare.cloudflared" }
}
Write-Host "dotnet e cloudflared encontrados."

# ---- 1. segredos ----
Passo "Lendo segredos do user-secrets"
Push-Location $raiz
$linhas = dotnet user-secrets list --project src/Nexo.Api
Pop-Location
$segredos = @{}
foreach ($l in $linhas) { $i = $l.IndexOf(" = "); if ($i -gt 0) { $segredos[$l.Substring(0, $i)] = $l.Substring($i + 3).Trim() } }
foreach ($k in "Gemini:ApiKey", "ConnectionStrings:Default", "WhatsApp:AccessToken", "WhatsApp:PhoneNumberId", "WhatsApp:VerifyToken", "WhatsApp:AppSecret") {
    if (-not $segredos.ContainsKey($k)) { throw "Segredo ausente: $k. Rode o script de segredos (docs/rodar-no-notebook.md)." }
}
Write-Host "Todos os segredos estao cadastrados."

# ---- 2. encerra instancias antigas ----
Passo "Encerrando instancias antigas"
Get-Process -Name "Nexo.Api", "cloudflared" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# ---- 3. API ----
Passo "Iniciando a API na porta $Porta"
$logApi = Join-Path $logs "api.log"
$logApiErr = Join-Path $logs "api.err.log"
Start-Process -FilePath "dotnet" -ArgumentList "run --project src/Nexo.Api --urls http://localhost:$Porta" `
    -WorkingDirectory $raiz -RedirectStandardOutput $logApi -RedirectStandardError $logApiErr -WindowStyle Hidden | Out-Null

$no_ar = $false
for ($t = 0; $t -lt 60; $t++) {
    Start-Sleep -Seconds 2
    try { Invoke-WebRequest "http://localhost:$Porta/privacy" -UseBasicParsing -TimeoutSec 3 | Out-Null; $no_ar = $true; break } catch { }
}
if (-not $no_ar) { throw "A API nao subiu em 2 minutos. Veja $logApi e $logApiErr" }
Write-Host "API no ar."

# ---- 4. tunel ----
function Abrir-Tunel($protocolo) {
    $log = Join-Path $logs "tunel.log"
    Remove-Item $log -ErrorAction SilentlyContinue
    $args_ = "tunnel --url http://localhost:$Porta"
    if ($protocolo) { $args_ += " --protocol $protocolo" }
    Start-Process -FilePath $cloudflared -ArgumentList $args_ -RedirectStandardError $log -WindowStyle Hidden | Out-Null
    for ($t = 0; $t -lt 20; $t++) {
        Start-Sleep -Seconds 2
        if (Test-Path $log) {
            $m = Select-String -Path $log -Pattern "https://[a-z0-9-]+\.trycloudflare\.com" | Select-Object -First 1
            if ($m) { return $m.Matches[0].Value }
        }
    }
    return $null
}

Passo "Abrindo o tunel publico"
$url = Abrir-Tunel $null
if (-not $url) {
    Write-Host "Sem endereco com o protocolo padrao. Tentando http2 (redes que bloqueiam UDP)..."
    Get-Process -Name "cloudflared" -ErrorAction SilentlyContinue | Stop-Process -Force
    $url = Abrir-Tunel "http2"
}
if (-not $url) { throw "Nao foi possivel abrir o tunel. Veja scripts\logs\tunel.log" }
Write-Host "Endereco publico: $url"

# ---- 5. webhook da Meta ----
Passo "Apontando o webhook da Meta para o endereco novo"
$callback = "$url/api/webhook/whatsapp/cloud"
$corpo = @{
    object       = "whatsapp_business_account"
    callback_url = $callback
    verify_token = $segredos["WhatsApp:VerifyToken"]
    fields       = "messages"
    access_token = "$AppId|$($segredos['WhatsApp:AppSecret'])"
}
$ok = $false
for ($t = 1; $t -le 8; $t++) {
    try {
        $r = Invoke-RestMethod -Method Post -Uri "https://graph.facebook.com/$VersaoGraph/$AppId/subscriptions" -Body $corpo
        if ($r.success) { $ok = $true; break }
    } catch { Write-Host "Tentativa $t falhou (o DNS do tunel pode demorar uns segundos). Aguardando..." }
    Start-Sleep -Seconds 5
}
if (-not $ok) { throw "A Meta nao aceitou o webhook. Confira o App Secret e o token de verificacao." }

Write-Host ""
Write-Host "PRONTO. Webhook da Meta aponta para: $callback" -ForegroundColor Green
Write-Host "Mande uma mensagem para o numero do NEXO no WhatsApp para testar."
Write-Host "Logs em $logs. Para encerrar: Get-Process Nexo.Api, cloudflared | Stop-Process"
