# Migrar o Companion local de PostgreSQL para SQLite

Este roteiro vale para o Compose da raiz, que publica o Companion na porta 3636. Instalações existentes continuam usando o PostgreSQL até que o provedor seja alterado no `.env` da raiz.

## Prepare a importação

Mantenha o `.env` atual e confirme que a API usa PostgreSQL com `docker compose exec -T api printenv Database__Provider`. Reserve uma janela de manutenção para bloquear gravações durante o backup final e a cópia.

Monte o volume SQLite com as permissões do usuário da API e construa o importador antes de parar a API.

```powershell
docker compose run --rm --no-deps sqlite-volume
docker compose --profile migration build sqlite-import
```

## Pause a API e verifique o backup final

Execute no PowerShell na raiz do repositório. A API fica parada e o PostgreSQL continua disponível durante o backup e a importação.

```powershell
docker compose stop api
if ($LASTEXITCODE -ne 0) { throw 'Falha ao parar a API.' }

$pastaBackup = Join-Path $env:LOCALAPPDATA 'Companion\Backups'
New-Item -ItemType Directory -Force -Path $pastaBackup | Out-Null
$arquivoBackup = 'companion-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.dump'
$backupTemporario = "/tmp/$arquivoBackup"
$backupLocal = Join-Path $pastaBackup $arquivoBackup
$idPostgres = docker compose ps -q postgres
if ($LASTEXITCODE -ne 0 -or !$idPostgres) { throw 'PostgreSQL indisponível para backup.' }

docker compose exec -T postgres pg_dump -U companion -d companion -Fc -f $backupTemporario
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o backup do PostgreSQL.' }
docker compose exec -T postgres pg_restore --file=/dev/null $backupTemporario
if ($LASTEXITCODE -ne 0) { throw 'Falha na leitura integral do backup.' }

docker cp "${idPostgres}:$backupTemporario" $backupLocal
if ($LASTEXITCODE -ne 0) { throw 'Falha ao copiar o backup para o host.' }
$hashOrigem = docker compose exec -T postgres sha256sum $backupTemporario
if ($LASTEXITCODE -ne 0 -or !$hashOrigem) { throw 'Falha ao calcular o hash do backup original.' }
$hashCopia = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupLocal).Hash
if ($hashCopia -ne ($hashOrigem -split '\s+')[0]) { throw 'O backup copiado diverge do original.' }
docker compose exec -T postgres rm $backupTemporario
```

## Importe os dados e troque o provedor

Com a API ainda parada, execute o importador.

```powershell
docker compose --profile migration run --rm --no-deps sqlite-import
```

O importador lê todas as tabelas de aplicação em uma transação PostgreSQL, confere os dados por tabela e publica o arquivo no volume `companion-sqlite-data` depois da verificação. O arquivo de destino é `/data/companion.db`. O importador recusa sobrescrever um arquivo existente.

Depois da mensagem `Arquivo SQLite pronto`, acrescente estas linhas ao `.env`. Mantenha a senha do PostgreSQL disponível para eventual retorno.

```dotenv
COMPANION_DATABASE_PROVIDER=Sqlite
COMPANION_DATABASE_CONNECTION='Data Source=/data/companion.db;Mode=ReadWrite;Foreign Keys=True;Default Timeout=20'
COMPANION_POSTGRES_REPLICAS=0
```

O modo `ReadWrite` exige que o arquivo importado exista e impede a criação acidental de um banco vazio. A escala zero desativa o serviço PostgreSQL, preservando o volume legado.

```powershell
docker compose config --quiet
docker compose up -d --no-build --wait api frontend
docker compose exec -T api printenv Database__Provider
Invoke-WebRequest http://localhost:3636/health
```

Confirme o provedor `Sqlite` e abra uma mesa antiga para verificar participantes, ficha, mapa, imagens ou anexos. Guarde o PostgreSQL antigo pelo período de retenção do backup.

## Backup e retorno

Para criar um backup consistente com a API em execução, use o [comando SQLite de backup](https://sqlite.org/backup.html). Para copiar o arquivo diretamente, pare a API, confirme que nenhum outro processo está usando o banco e copie o volume para armazenamento privado.

Se o backup ou a importação falhar antes de alterar o `.env`, reative a API com `docker compose up -d --no-build --wait api`. Se o `.env` já apontar para SQLite e ele ainda estiver sem novas gravações, pare a API, retire as três linhas adicionadas e execute `docker compose up -d --no-build --wait postgres api frontend`.

Se o SQLite já recebeu novas gravações, pare a API e guarde uma cópia consistente do SQLite. Preserve também o PostgreSQL e o backup final. Retome o serviço em SQLite quando corrigir a falha ou reconcilie as gravações novas no PostgreSQL e confira os dados antes de voltar a usá-lo.
