# Gera um arquivo .ps1 que recadastra os segredos do NEXO (user-secrets) em OUTRA maquina.
# O arquivo gerado contem chaves e senhas em texto puro. Grave num pendrive, rode no notebook e APAGUE depois.
# Uso: powershell -ExecutionPolicy Bypass -File scripts\exportar-segredos.ps1 -Destino E:\segredos-nexo.ps1

param(
    [Parameter(Mandatory = $true)][string]$Destino
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$caminho = [System.IO.Path]::GetFullPath($Destino)

if ($caminho.StartsWith($raiz, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Nao grave os segredos dentro do repositorio ($raiz). Use um pendrive."
}
if ($caminho -match "OneDrive|Dropbox|Google Drive") {
    throw "Esse caminho e sincronizado com a nuvem. Use um pendrive."
}

Push-Location $raiz
$linhas = dotnet user-secrets list --project src/Nexo.Api
Pop-Location

$saida = New-Object System.Collections.Generic.List[string]
$saida.Add("# Segredos do NEXO. Rode na raiz do repositorio e apague este arquivo depois.")
$saida.Add('Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path)')
$n = 0
foreach ($l in $linhas) {
    $i = $l.IndexOf(" = ")
    if ($i -le 0) { continue }
    $chave = $l.Substring(0, $i)
    $valor = $l.Substring($i + 3).Trim().Replace("'", "''")
    $saida.Add("dotnet user-secrets set '$chave' '$valor' --project src/Nexo.Api | Out-Null")
    $n++
}
$saida.Add('Write-Host "Segredos cadastrados."')
[System.IO.File]::WriteAllLines($caminho, $saida, (New-Object System.Text.UTF8Encoding($true)))
Write-Host "$n segredos gravados em $caminho"
Write-Host "No notebook, copie o arquivo para a raiz do repositorio, rode e apague."
