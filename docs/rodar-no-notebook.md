# Rodar o NEXO em outra máquina (notebook)

O banco (Neon) e a IA (Gemini) ficam na nuvem, então o notebook só precisa rodar a API e um túnel público. O script `scripts/iniciar-demo.ps1` faz isso e reaponta o webhook da Meta sozinho.

## 1. O que instalar no notebook (uma vez, com internet)

Abra o PowerShell e rode:

```powershell
winget install --id Microsoft.DotNet.SDK.10
winget install --id Git.Git
winget install --id Cloudflare.cloudflared
```

Feche e abra o PowerShell depois da instalação. Confira com `dotnet --version`, `git --version` e `cloudflared --version`.

Se o `winget` não achar o .NET 10, baixe o SDK em https://dotnet.microsoft.com/download.

## 2. Levar o código

Escolha uma opção.

**A. Pelo GitHub**, se o código já tiver sido enviado para `github.com/Andreezera/nexo-api`:

```powershell
git clone https://github.com/Andreezera/nexo-api.git nexo
```

**B. Por pendrive**, sem depender do GitHub. No computador atual, na pasta do projeto:

```powershell
git bundle create E:\nexo.bundle --all
```

No notebook:

```powershell
git clone E:\nexo.bundle nexo
```

## 3. Levar os segredos

Os segredos (chave do Gemini, senha do banco, tokens da Meta) **não estão no repositório**. Para recadastrar sem digitar:

No computador atual, com o pendrive conectado (use o caminho do pendrive, nunca o OneDrive):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\exportar-segredos.ps1 -Destino E:\segredos-nexo.ps1
```

No notebook, copie `segredos-nexo.ps1` para a raiz da pasta `nexo` e rode:

```powershell
powershell -ExecutionPolicy Bypass -File .\segredos-nexo.ps1
```

**Depois apague o arquivo do pendrive e do notebook.** Ele contém as chaves em texto puro.

## 4. Conferir que compila e passa nos testes

```powershell
cd nexo
dotnet test tests/Nexo.Tests
```

Deve terminar com 300 testes aprovados.

## 5. Subir o NEXO para a demonstração

```powershell
powershell -ExecutionPolicy Bypass -File scripts\iniciar-demo.ps1
```

O script sobe a API, abre o túnel, mostra o endereço público e aponta o webhook da Meta para ele. Termina com `PRONTO`. Em seguida, mande uma mensagem pelo WhatsApp para o número do NEXO (+55 19 99273-2397) e confira a resposta.

Para encerrar:

```powershell
Get-Process Nexo.Api, cloudflared | Stop-Process
```

## 6. Regras que evitam susto

- **Só uma máquina recebe as mensagens por vez.** Quem rodou o script por último fica com o webhook. Se rodar no notebook, o computador de casa para de receber.
- **Rode o script de novo a cada vez** que reiniciar a máquina, fechar o terminal ou trocar de rede. O endereço do túnel muda a cada subida, e o script cuida disso.
- **Rede de universidade pode bloquear o túnel.** O script tenta o protocolo normal e, se não achar o endereço, tenta `http2`. Se ainda falhar, ligue o notebook no hotspot do celular.
- **O banco pode demorar na primeira mensagem**, porque o plano gratuito do Neon "dorme". Mande uma mensagem de teste uns 10 minutos antes.
- **O token da Meta é permanente**, então não precisa renová-lo no dia.

## 7. Plano B, sem WhatsApp

Se a Meta ou o túnel falharem na hora, a demonstração continua pelo Swagger, que não depende da Meta. Na primeira vez no notebook, confie no certificado de desenvolvimento (aparece uma janela, aceite):

```powershell
dotnet dev-certs https --trust
```

Depois, com a API parada (`Get-Process Nexo.Api | Stop-Process`):

```powershell
dotnet run --project src/Nexo.Api --launch-profile https
```

Abra https://localhost:7117/index.html e use o endpoint `POST /api/webhook/whatsapp` (texto) ou `POST /api/webhook/whatsapp/media` (áudio e foto). Isso só precisa do Gemini e do Neon.

## 8. Checklist de 10 minutos antes

1. Notebook carregado, na rede certa, com o PowerShell aberto na pasta `nexo`.
2. `scripts\iniciar-demo.ps1` terminou com `PRONTO`.
3. Mensagem de texto de teste respondida no celular.
4. Um áudio de teste respondido.
5. Uma foto de nota fiscal de teste respondida.
6. Swagger aberto em outra aba, como plano B.

## Problemas comuns

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| `Segredo ausente` | Segredos não cadastrados nesta máquina | Rodar o passo 3 |
| `A API nao subiu` | .NET não instalado, ou porta 5161 ocupada | Ver `scripts\logs\api.err.log` e rodar `Get-Process Nexo.Api \| Stop-Process` |
| `Nao foi possivel abrir o tunel` | Rede bloqueando o Cloudflare | Hotspot do celular |
| `A Meta nao aceitou o webhook` | App Secret ou token de verificação errados | Refazer o passo 3 |
| Mensagem enviada, sem resposta | Webhook apontando para outra máquina | Rodar o script nesta máquina |
