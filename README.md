# Companion

Mesa web responsiva para campanhas de D&D, com campanhas por código, rolagem pública de dados, histórico, iniciativa e turnos sincronizados em tempo real.

## Executar com Docker Compose

Pré-requisitos:

- Docker Desktop no Windows ou Docker Engine com Compose
- Portas TCP 3636 (frontend) e 8080 (backend) livres
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

Quando os três serviços estiverem saudáveis, abra [http://localhost:3636](http://localhost:3636). A API também fica disponível diretamente em `http://localhost:8080`; o banco permanece isolado na rede Docker.

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
http://192.168.1.50:3636
```

O frontend utiliza caminhos relativos para API e SignalR. Portanto, não é necessário recompilar ao trocar `localhost` pelo IP LAN.

Se a página não abrir, permita uma regra de entrada TCP para a porta 3636 no Firewall do Windows, limitada ao perfil de rede Privada. Libere também a porta 8080 somente se precisar acessar a API diretamente de outro dispositivo. Não exponha essas portas diretamente à internet; o MVP usa HTTP por ter como alvo redes privadas.

## Hamachi ou Radmin VPN

Depois de instalar e conectar os dispositivos à mesma rede virtual:

1. Execute `ipconfig` e encontre o IPv4 do adaptador Hamachi ou Radmin.
2. Confirme que os dispositivos conseguem alcançar esse IP.
3. Permita TCP 3636 no firewall também para o perfil atribuído ao adaptador VPN.
4. Acesse `http://<ip-da-vpn>:3636`.

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

Ao publicar ou substituir a imagem, a prévia já traz o editor de névoa: um mapa novo começa todo coberto, e a névoa desenhada vai junto com a imagem, então os jogadores nunca recebem o mapa descoberto. No mapa publicado, o botão **Névoa** permite cobrir e revelar com pincel ou retângulo; criaturas totalmente cobertas não são enviadas aos jogadores.

## Biblioteca de criaturas

A aba **Criaturas** (só para o mestre) traz o compêndio em português de 725 criaturas, com busca por nome em PT ou EN e filtros por tamanho, tipo, ND e origem. O mestre pode criar criaturas próprias e editar qualquer uma. Editar uma criatura do compêndio cria a versão daquela mesa, e é possível restaurar o original. A imagem enviada aparece nos tokens.

O mestre adiciona de 1 a 20 cópias ao combate pela ficha, pelo formulário "Combatentes" (modo Do catálogo) ou pelo botão "Do catálogo" ao lado de "Inserir token" no mapa, com nomes numerados, PV, iniciativa e tamanho do token. Com o combate em andamento, elas entram como reforços com iniciativa rolada, sem mudar o turno. Ele também escreve anotações pessoais e rola para a criatura escolhendo quantidade, dado, atributo e um adicional. No mapa, o menu de um token de criatura tem **Ficha e rolagens**, e a última configuração de rolagem de cada token volta pré-selecionada.

O compêndio é semeado no primeiro startup da API a partir de `backend/src/Companion.Infrastructure/Persistence/Seed/creatures.pocketdm.json`, de forma idempotente. As imagens do compêndio são buscadas pelo servidor sob demanda e guardadas no banco.

**Créditos:** tradução e curadoria © [Pocket DM](https://pocketdm.com.br/monstros), usadas com autorização. Conteúdo SRD sob [Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/) (Wizards of the Coast). Para reimportar, veja [`scripts/import-creatures/README.md`](scripts/import-creatures/README.md).
