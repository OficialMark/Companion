# Companion na Azure com Bastion e Cloudflare Tunnel

Este roteiro usa uma VM Ubuntu AMD64 sem IPv4 público, o PostgreSQL Flexible Server e um domínio gerenciado pela Cloudflare. Você administra a VM pelo Bastion, compila o Companion nela e usa o Cloudflare Tunnel para entregar HTTPS e SignalR no mesmo endereço público.

Se já tem a aplicação rodando e quer tirar a latência entre a VM e o PostgreSQL, veja a [migração para SQLite](deploy-azure-sqlite.md).

Se ainda estiver criando os recursos pela oferta gratuita, confira a elegibilidade da VM Linux `Standard_B2ats_v2` e do disco Premium SSD P6 de 64 GiB. Para o banco, use PostgreSQL 17 na mesma região da VM, com **Burstable B1MS**, **32 GiB**, backup de **7 dias**, sem alta disponibilidade, redundância geográfica ou aumento automático do disco. [Criar recursos gratuitos na Azure](https://learn.microsoft.com/en-us/azure/cost-management-billing/manage/create-free-services)

## 1. Abra a VM pelo Bastion e confira a saída para a internet

No Azure, abra a VM e acesse **Conectar → Bastion**. Selecione a edição **Developer** se for necessário criar o Bastion. Ela é gratuita onde está disponível, inclusive em South Africa North. Entre como `azureuser` com a chave privada `.pem` da VM. Os comandos Bash deste tutorial são executados no terminal da VM aberto no navegador.

A VM precisa acessar GitHub e os repositórios de pacotes e imagens. Ela também precisa alcançar o Cloudflare Tunnel na porta **7844 de saída** e o PostgreSQL na **5432 de saída**. Uma regra de segurança que permite saída ainda depende de conectividade de rede. Em uma sub-rede privada da Azure, confirme que existe um meio de saída configurado. O Bastion permite entrar na VM mesmo quando ela não consegue sair para a internet.

No terminal do Bastion, teste o acesso ao GitHub e ao túnel:

```bash
curl -4fsSI --max-time 10 https://github.com/
timeout 8 bash -c 'true < /dev/tcp/region1.v2.argotunnel.com/7844' && echo 'TCP 7844 OK'
```

Se os testes falharem, confira primeiro **Rede virtual → Sub-redes → sub-rede da VM** e o meio de saída efetivo. Uma sub-rede não privada pode usar o acesso de saída padrão da Azure, quando disponível. Esse IP pode mudar. Em uma sub-rede privada, avalie o custo do meio de saída antes de provisioná-lo. As portas **80, 443 e 7844 de entrada** na VM são dispensáveis para o túnel. Preserve o acesso SSH pela rede do Bastion.

[Azure Bastion Developer](https://learn.microsoft.com/en-us/azure/bastion/bastion-sku-comparison) · [Saída da VM na Azure](https://learn.microsoft.com/en-us/azure/virtual-network/ip-services/default-outbound-access) · [Portas do Cloudflare Tunnel](https://developers.cloudflare.com/tunnel/configuration/)

## 2. Instale Git e Docker no Ubuntu

Na VM, instale as ferramentas e configure o repositório oficial do Docker:

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl git nano openssl

sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

. /etc/os-release
printf 'Types: deb\nURIs: https://download.docker.com/linux/ubuntu\nSuites: %s\nComponents: stable\nArchitectures: %s\nSigned-By: /etc/apt/keyrings/docker.asc\n' \
  "${UBUNTU_CODENAME:-$VERSION_CODENAME}" "$(dpkg --print-architecture)" \
  | sudo tee /etc/apt/sources.list.d/docker.sources >/dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
git --version
sudo docker compose version
```

Os Dockerfiles já instalam o SDK .NET e o Node.js nas etapas de compilação. A VM precisa apenas de Git e Docker para construir as imagens. [Instalar Docker Engine no Ubuntu](https://docs.docker.com/engine/install/ubuntu/)

## 3. Clone o projeto e prepare a compilação

Na VM:

```bash
git clone https://github.com/OficialMark/Companion.git ~/Companion
cd ~/Companion/deploy/azure
git log -1 --oneline
```

Use uma revisão do repositório que contenha este tutorial e o serviço `tunnel` no arquivo `deploy/azure/compose.yaml`.

Na `Standard_B2ats_v2`, a RAM de 1 GiB pode apertar durante a compilação. Confira espaço e swap com `df -h /` e `swapon --show`. Se não houver swap e o disco tiver pelo menos 3 GiB livres além do espaço para as imagens, crie um arquivo de swap:

```bash
if [ -z "$(swapon --show --noheadings)" ] && [ ! -e /swapfile-companion ]; then
  sudo install -m 0600 /dev/null /swapfile-companion &&
  sudo fallocate -l 3G /swapfile-companion &&
  sudo mkswap /swapfile-companion &&
  sudo swapon /swapfile-companion &&
  printf '%s\n' '/swapfile-companion none swap sw 0 0' | sudo tee -a /etc/fstab
fi
free -h
```

Compile a API e o frontend em etapas separadas, conforme a seção 5.

## 4. Prepare o PostgreSQL e restaure os dados pelo Windows

Use o **Nome do servidor** mostrado em **Visão geral** no portal do Azure. No PostgreSQL, libere por IP o computador Windows usado para a restauração e a origem de saída da VM usada pela API. Mantenha desmarcado o acesso de **qualquer serviço do Azure**. Se a VM estiver usando saída dinâmica, confirme qual IP o banco vê e revise a regra se esse IP mudar.

No PowerShell do Windows, com Docker Desktop aberto, conecte ao banco padrão usando o administrador do Azure. Substitua o endereço em `$pgHost` pelo endereço copiado do portal.

```powershell
$pgHost = 'companion-postre-db-ca.postgres.database.azure.com'
docker run --rm -it postgres:17-alpine psql "host=$pgHost port=5432 dbname=postgres user=companionadmin sslmode=verify-full sslrootcert=system"
```

Se você ainda não criou o login e o banco, execute os comandos a seguir no `psql`. Quando `\password` pedir uma senha, informe uma nova senha exclusiva para `companion_app`. Conclua os comandos e saia com `\q`.

```sql
CREATE ROLE companion_app LOGIN;
\password companion_app
CREATE DATABASE companion;
\connect companion
GRANT CONNECT ON DATABASE companion TO companion_app;
GRANT USAGE, CREATE ON SCHEMA public TO companion_app;
```

Para levar os dados atuais, pause a API local antes do backup. Na raiz do repositório **no Windows**, execute o bloco abaixo no PowerShell 7. O cliente `pg_restore` envia o arquivo diretamente do PostgreSQL local para o banco vazio na Azure. O dump também fica salvo fora do repositório. Informe a senha de `companion_app` quando solicitada.

```powershell
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$dir = Join-Path $env:LOCALAPPDATA 'Companion/Backups'
New-Item -ItemType Directory -Force $dir | Out-Null
$name = "companion-postgres-$(Get-Date -Format 'yyyyMMdd-HHmmss').dump"
$dumpInContainer = "/tmp/$name"
$dump = Join-Path $dir $name

docker compose stop api
docker compose exec -T postgres pg_dump -U companion -d companion -Fc -f $dumpInContainer
docker compose cp "postgres:${dumpInContainer}" $dump
Get-FileHash $dump

docker compose exec postgres pg_restore --no-owner --no-privileges --single-transaction --password --dbname "host=$pgHost port=5432 dbname=companion user=companion_app sslmode=verify-full sslrootcert=system" $dumpInContainer
```

Restaure antes de iniciar a API na VM, que aplica migrações e semeia catálogos ao iniciar. A opção `--single-transaction` reverte a restauração em caso de erro. Se sua origem ainda for o SQL Server, o [importador legado](../backend/tools/Companion.DatabaseTransfer/Program.cs) aceita um banco SQL Server acessível e um PostgreSQL vazio pelas variáveis `COMPANION_SQLSERVER_SOURCE` e `COMPANION_POSTGRES_TARGET`.

[Firewall do PostgreSQL](https://learn.microsoft.com/en-us/azure/postgresql/security/security-firewall-rules) · [Criar usuários no PostgreSQL da Azure](https://learn.microsoft.com/en-us/azure/postgresql/security/security-manage-database-users) · [Restauração com pg_restore](https://www.postgresql.org/docs/17/app-pgrestore.html)

## 5. Configure o túnel, compile e suba o Companion

No painel da **Cloudflare**, abra **Networking → Tunnels** e crie um túnel chamado `companion`. Copie o token do conector. Em **Routes**, adicione uma **Published application** com o subdomínio desejado, por exemplo `mesa.seudominio.com`, e o serviço **HTTP** em `proxy:80` (a URL completa é `http://proxy:80`). O nome `proxy` é o serviço Caddy na mesma rede Docker que o conector. A Cloudflare configura o DNS do subdomínio pela rota do túnel. Se esse hostname já tiver um registro DNS, escolha outro ou substitua o registro existente se ele estiver reservado para o Companion. [Criar e publicar uma rota de túnel](https://developers.cloudflare.com/tunnel/get-started/)

Na VM, em `~/Companion/deploy/azure`, prepare o arquivo de configuração:

```bash
cd ~/Companion/deploy/azure
umask 077
cp -n .env.example .env
chmod 600 .env
openssl rand -hex 32
nano .env
```

Em `COMPANION_POSTGRES_TARGET`, informe o endereço completo do servidor, o banco `companion`, o usuário `companion_app` e sua senha. Mantenha `SSL Mode=VerifyFull`. Use uma chave exclusiva em `ADMIN_ACCESS_KEY` (você pode usar a saída do `openssl`) e cole o token do conector em `CLOUDFLARE_TUNNEL_TOKEN`. Os valores ficam no `.env` privado da VM.

Com o banco pronto, ainda na VM:

```bash
sudo docker run --rm -it postgres:17-alpine psql \
  'host=SEU_SERVIDOR.postgres.database.azure.com port=5432 dbname=companion user=companion_app sslmode=verify-full sslrootcert=system connect_timeout=8' \
  -c 'SELECT inet_client_addr();'
```

Substitua o nome do servidor e informe a senha de `companion_app`. Se conectar, o comando mostra o endereço que o PostgreSQL vê para a VM. Se houver erro de firewall com indicação do IP de origem, use esse IP para corrigir a regra do PostgreSQL.

Depois compile e inicie os serviços:

```bash
sudo docker compose config --quiet
sudo docker compose build api
sudo docker compose build frontend
sudo docker compose up -d --no-build
sudo docker compose ps
sudo docker compose logs --tail=100 api proxy tunnel
```

A Cloudflare entrega HTTPS ao navegador. Se o hostname também aceitar HTTP, configure na Cloudflare um redirecionamento para HTTPS específico para esse subdomínio. O Caddy encaminha frontend, API e SignalR na rede Docker. O Compose mantém todas as portas dos contêineres na rede interna. [WebSockets na Cloudflare](https://developers.cloudflare.com/network/websockets/)

## 6. Confira a mesa e faça a virada

Do seu computador, abra `https://mesa.seudominio.com/health`. A resposta esperada é `Healthy`. No painel Cloudflare, confira se o túnel está conectado. Abra o Companion em dois navegadores, verifique uma campanha restaurada e faça uma rolagem para confirmar a atualização em tempo real. Se a administração do banco passar a ser feita pela VM, remova a regra temporária do IP do Windows.

Se precisar voltar antes de liberar gravações na Azure, execute `sudo docker compose stop` em `~/Companion/deploy/azure` na VM e `docker compose start api` na raiz do projeto no Windows. Depois de novas gravações na Azure, reconcilie os dados antes de voltar à instância local.

Para atualizar a aplicação, rode `git pull --ff-only` na VM e repita a compilação sequencial e a subida do Compose. Confira na assinatura se as cotas gratuitas da VM e do PostgreSQL continuam ativas e acompanhe custos de rede separadamente. [Acompanhar as cotas gratuitas da Azure](https://learn.microsoft.com/en-us/azure/cost-management-billing/manage/check-free-service-usage)
