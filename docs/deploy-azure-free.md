# Companion na Azure com PostgreSQL

Este roteiro usa uma VM Ubuntu `Standard_B2ats_v2` (AMD64), um Azure Database for PostgreSQL Flexible Server e Docker Compose. O Caddy publica o frontend, a API e o SignalR no mesmo endereço HTTPS.

## 1. Confira a oferta antes de criar recursos

- Crie a VM Linux pela seção **Serviços gratuitos**. Selecione `Standard_B2ats_v2`, uma imagem Ubuntu LTS x64 sem licença extra e um disco **Premium SSD P6 de 64 GiB** se o portal indicar cobertura. Na oferta exibida na sua assinatura, VM e disco têm cotas separadas.
- Crie o PostgreSQL Flexible Server no plano **Burstable B1MS**, com **32 GiB** de armazenamento, sem alta disponibilidade nem backup com redundância geográfica. Confira no portal as cotas de horas, armazenamento e backup e o preço dos recursos auxiliares. Escolha **PostgreSQL 17** em uma região que ofereça essa versão para restaurar o backup do Compose local.
- Um IPv4 público Standard, um domínio e outros recursos fora das cotas podem gerar custo. Use um grupo de recursos próprio para o Companion e confira o uso em **Assinaturas → sua assinatura → Serviços gratuitos** e em **Gerenciamento de Custos**. Após a validade da oferta, recursos ativos passam a seguir a cobrança normal da assinatura.

[Criar recursos gratuitos](https://learn.microsoft.com/en-us/azure/cost-management-billing/manage/create-free-services) · [PostgreSQL na conta gratuita](https://learn.microsoft.com/azure/postgresql/flexible-server/how-to-deploy-on-azure-free-account) · [Preços de endereços IP](https://azure.microsoft.com/en-us/pricing/details/ip-addresses/)

## 2. Prepare a rede e o banco

1. Na VM, configure autenticação SSH por chave. No grupo de segurança de rede, permita TCP 22 apenas do seu IP e 80/443 para o público da mesa. Aponte o registro DNS A do domínio para o IPv4 público da VM.
2. No PostgreSQL, para usar a transferência a partir da sua estação, selecione acesso público com firewall e autorize somente o IPv4 de saída da VM e o IPv4 da estação durante a migração. Se escolher integração com rede virtual, execute o importador de uma máquina que alcance o banco pela rede privada. Use o nome DNS do servidor na conexão TLS.
3. Crie um banco vazio chamado `companion` e um login próprio para a aplicação. No `psql`, conectado como administrador do servidor, execute:

   ```sql
   CREATE ROLE companion_app LOGIN;
   \password companion_app
   CREATE DATABASE companion;
   \connect companion
   GRANT CONNECT ON DATABASE companion TO companion_app;
   GRANT USAGE, CREATE ON SCHEMA public TO companion_app;
   ```

   O aplicativo e o importador usam o mesmo login para que ele seja dono das tabelas e possa aplicar migrações futuras. Use `SSL Mode=VerifyFull` com o FQDN do servidor. Se a estação ou VM não reconhecer a cadeia de certificados, configure a CA indicada pela Microsoft.

[Firewall do PostgreSQL](https://learn.microsoft.com/en-us/azure/postgresql/security/security-firewall-rules) · [TLS no PostgreSQL](https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/security-tls)

## 3. Gere as imagens na sua estação

Na raiz do repositório, com o Docker Desktop em modo de contêineres Linux:

```powershell
docker buildx build --platform linux/amd64 --load -t companion-api:latest ./backend
docker buildx build --platform linux/amd64 --load -t companion-frontend:latest ./frontend
$imagens = Join-Path $env:TEMP 'companion-images.tar'
docker save -o $imagens companion-api:latest companion-frontend:latest
ssh azureuser@IP_DA_VM 'mkdir -p ~/companion'
scp $imagens deploy/azure/compose.yaml deploy/azure/Caddyfile deploy/azure/.env.example azureuser@IP_DA_VM:companion/
```

O arquivo TAR contém as imagens da aplicação. Mantenha o arquivo de backup do SQL Server em armazenamento privado. Na VM, [instale o Docker Engine e o plug-in Compose para Ubuntu](https://docs.docker.com/engine/install/ubuntu/), depois rode:

```bash
cd ~/companion
sudo docker load -i companion-images.tar
umask 077
cp .env.example .env
chmod 600 .env
nano .env
sudo docker compose config --quiet
```

Preencha o domínio, a conexão PostgreSQL e a chave administrativa. A VM tem 1 GiB de RAM. Construir as imagens fora dela deixa a memória disponível para executar a aplicação.

## 4. Transfira o PostgreSQL local para a Azure

Quando for fazer a virada, pause a API local para que a cópia inclua todas as gravações. Na raiz do repositório, no PowerShell:

```powershell
$dir = Join-Path $env:LOCALAPPDATA 'Companion/Backups'
New-Item -ItemType Directory -Force $dir | Out-Null
$dump = Join-Path $dir 'companion-postgres.dump'
docker compose stop api
docker compose exec -T postgres pg_dump -U companion -d companion -Fc -f /tmp/companion-postgres.dump
docker compose cp postgres:/tmp/companion-postgres.dump $dump
Get-FileHash $dump
scp $dump azureuser@IP_DA_VM:companion/
```

Na VM, restaure no banco vazio antes de subir a API. Informe a senha do usuário `companion_app` quando solicitada:

```bash
cd ~/companion
chmod 600 companion-postgres.dump
sudo docker run --rm -it \
  --mount "type=bind,source=$PWD/companion-postgres.dump,target=/backup.dump,readonly" \
  postgres:17-alpine pg_restore --no-owner --no-privileges --single-transaction --password \
  --dbname 'host=SEU_SERVIDOR.postgres.database.azure.com dbname=companion user=companion_app sslmode=verify-full sslrootcert=system' \
  /backup.dump
```

Se a restauração falhar, confira o erro antes de continuar. A opção `--single-transaction` reverte a restauração inteira em caso de falha.

### Alternativa para uma instalação que ainda usa SQL Server

Se a origem ainda for SQL Server, avise a mesa e pause a API antiga para impedir novas gravações. Os comandos a seguir são para o Compose legado, com o serviço `sqlserver`. Se o Compose já foi atualizado, use o procedimento PostgreSQL acima ou use uma cópia SQL Server restaurada e acessível pelo importador.

```powershell
$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:LOCALAPPDATA 'Companion/Backups'
New-Item -ItemType Directory -Force $dir | Out-Null
$nome = 'Companion-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bak'
$origem = "/var/opt/mssql/data/$nome"
$container = docker compose ps -q sqlserver
docker compose stop api
docker exec $container sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "$1"' sh "BACKUP DATABASE [Companion] TO DISK = N'$origem' WITH COPY_ONLY, CHECKSUM, INIT; RESTORE VERIFYONLY FROM DISK = N'$origem' WITH CHECKSUM;"
docker cp "${container}:${origem}" (Join-Path $dir $nome)
Get-FileHash (Join-Path $dir $nome)
```

O importador aceita o SQL Server original com a API parada ou uma cópia restaurada desse backup. Para acessar o contêiner original sem publicar a porta dele na rede, execute o importador no Docker, na mesma rede Compose. Informe as duas strings de conexão quando solicitado. Na origem use `Server=sqlserver,1433;Database=Companion;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True`. No destino use a conexão PostgreSQL do arquivo `.env` da VM.

```powershell
$importador = Join-Path $env:TEMP 'companion-importador'
dotnet publish backend/tools/Companion.DatabaseTransfer -c Release -o $importador
$env:COMPANION_SQLSERVER_SOURCE = Read-Host 'Conexão SQL Server' -MaskInput
$env:COMPANION_POSTGRES_TARGET = Read-Host 'Conexão PostgreSQL' -MaskInput
try {
    docker run --rm --network companion_companion-internal `
      -e COMPANION_SQLSERVER_SOURCE -e COMPANION_POSTGRES_TARGET `
      --mount "type=bind,source=$importador,target=/app,readonly" `
      mcr.microsoft.com/dotnet/runtime:10.0 `
      dotnet /app/Companion.DatabaseTransfer.dll
}
finally {
    Remove-Item Env:COMPANION_SQLSERVER_SOURCE, Env:COMPANION_POSTGRES_TARGET
}
```

Se você usou outro nome de projeto Compose, substitua `companion_companion-internal` pela rede indicada em `docker network ls`. Se estiver usando uma cópia restaurada acessível por TCP, você também pode executar `dotnet run --project backend/tools/Companion.DatabaseTransfer` diretamente na estação, com as duas variáveis definidas e o SDK .NET 10 instalado.

O importador aplica o esquema PostgreSQL, compara tabelas e colunas, copia os dados em uma transação e confere as contagens. Use como origem o SQL Server com as mesmas migrações do código de origem da transferência. O histórico de migrações é próprio de cada banco e os tokens de revisão são gerados novamente. Datas são convertidas para UTC com a precisão de microssegundos do PostgreSQL. Aguarde a mensagem `Importação confirmada` antes de subir a API na VM. Se houver falha antes da confirmação, as linhas importadas são revertidas e o esquema preparado pode permanecer.

## 5. Suba o serviço e confira a mesa

Na VM:

```bash
cd ~/companion
sudo docker compose up -d
sudo docker compose ps
sudo docker compose logs --tail=100 api frontend proxy
curl --fail https://mesa.exemplo.com/health
```

Abra o endereço HTTPS em dois navegadores. Confira uma campanha já existente, abra um mapa e um PDF, salve uma ficha e faça uma rolagem para verificar a atualização pelo SignalR. Remova a regra temporária do IP da estação no firewall do PostgreSQL quando terminar.

Se precisar voltar antes de liberar gravações na Azure, pare a stack Azure e execute `docker compose start api` na estação original. Depois de novas gravações na Azure, reconcilie os dados criados desde a virada antes de voltar à instância local. Guarde o backup e faça backups periódicos do PostgreSQL após a publicação.
