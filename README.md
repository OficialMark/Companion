# Companion

Mesa web responsiva para campanhas de D&D, com campanhas por código, rolagem pública de dados, histórico, iniciativa e turnos sincronizados em tempo real.

## Executar com Docker Compose

Pré-requisitos:

- Docker Desktop no Windows ou Docker Engine com Compose
- Porta TCP 3000 livre
- Pelo menos 2 GB de memória disponíveis para o SQL Server

No PowerShell, a partir da raiz do projeto:

```powershell
Copy-Item .env.example .env
```

Edite `.env` e substitua `MSSQL_SA_PASSWORD` por uma senha forte. Depois execute:

```powershell
docker compose up --build -d
docker compose ps
```

Quando os três serviços estiverem saudáveis, abra [http://localhost:3000](http://localhost:3000). O banco e a API ficam isolados na rede Docker; somente a aplicação web é publicada.

Para acompanhar a inicialização:

```powershell
docker compose logs -f api frontend sqlserver
```

## Acesso por outros dispositivos da LAN

Descubra o IPv4 do computador host:

```powershell
ipconfig
```

Procure o adaptador Wi-Fi ou Ethernet em uso e anote o endereço, por exemplo `192.168.1.50`. No Android ou em outro computador conectado à mesma rede, abra:

```text
http://192.168.1.50:3000
```

O frontend utiliza caminhos relativos para API e SignalR. Portanto, não é necessário recompilar ao trocar `localhost` pelo IP LAN.

Se a página não abrir, permita uma regra de entrada TCP para a porta 3000 no Firewall do Windows, limitada ao perfil de rede Privada. Não exponha a porta diretamente à internet; o MVP usa HTTP por ter como alvo redes privadas.

## Hamachi ou Radmin VPN

Depois de instalar e conectar os dispositivos à mesma rede virtual:

1. Execute `ipconfig` e encontre o IPv4 do adaptador Hamachi ou Radmin.
2. Confirme que os dispositivos conseguem alcançar esse IP.
3. Permita TCP 3000 no firewall também para o perfil atribuído ao adaptador VPN.
4. Acesse `http://<ip-da-vpn>:3000`.

O Compose continua igual porque publica em `0.0.0.0`. A segurança, o roteamento e o controle de membros da VPN permanecem sob responsabilidade do administrador da rede.

## Comandos operacionais

```powershell
# Verificar frontend e API pelo mesmo endereço
.\scripts\smoke-test.ps1

# Reiniciar sem perder dados
docker compose restart

# Parar sem perder dados
docker compose down

# ATENÇÃO: parar e remover definitivamente o volume de desenvolvimento
docker compose down -v
```

Os dados persistem no volume nomeado `companion-sql-data` durante `restart` e `down` normal.

## Desenvolvimento

Backend:

```powershell
dotnet restore .\backend\Companion.slnx
dotnet test .\backend\Companion.slnx
```

Frontend, usando Node.js 22 ou superior:

```powershell
cd frontend
npm install
npm run dev
```

O servidor de desenvolvimento Nuxt encaminha `/api` e `/hubs` para uma API local na porta 8080.

## Documentação Spec Kit

A feature do MVP está em [`specs/001-campaign-mvp`](specs/001-campaign-mvp):

- `spec.md`: jornadas, requisitos e critérios de sucesso
- `plan.md`: arquitetura e decisões técnicas
- `data-model.md`: entidades, regras e transições
- `contracts/`: contratos REST e SignalR
- `tasks.md`: tarefas rastreáveis por funcionalidade
- `quickstart.md`: roteiro de validação ponta a ponta

Para evoluir uma funcionalidade com Codex, use as skills `$speckit-specify`, `$speckit-plan`, `$speckit-tasks`, `$speckit-analyze` e `$speckit-implement`.
## Battlemap interativo

Durante um encontro, o mestre pode publicar uma imagem PNG, JPEG ou WebP de até 20 MB e definir um grid de 1×1 a 100×100. Cada jogador recebe um pino próprio; jogadores movem somente seu pino e o mestre pode arbitrar qualquer posição.

O visualizador mantém uma área estável em desktop e Android, usa células com escala inicial próxima de 40 px e oferece pan, zoom, ajuste total, arraste, seleção por toque e navegação por teclado. O estado confirmado é persistido no SQL Server e ressincronizado por snapshot após eventos SignalR ou reconexão.
