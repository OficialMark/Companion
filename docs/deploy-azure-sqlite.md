# Migrar o Companion da Azure para SQLite

O Compose usa o SQLite no disco da VM quando `COMPANION_DATABASE_PROVIDER=Sqlite`. O arquivo fica em `deploy/azure/data/companion.db` e é montado como `/data/companion.db` na API. O build da imagem não acessa o banco nem carrega os dados. Use uma instância da API com este arquivo e copie os backups para fora da VM.

## 1. Atualize o código e prepare a imagem

No Bastion:

```bash
cd ~/Companion
git pull --ff-only
cd deploy/azure
sudo docker compose build api
sudo install -d -m 0700 -o 1654 -g 1654 data
```

A pasta e o banco precisam ser graváveis pelo usuário `app` do contêiner (UID 1654), inclusive para os arquivos de trabalho do SQLite. A API em execução continua usando o PostgreSQL até a troca do `.env`.

## 2. Coloque o banco populado na VM

Escolha uma das opções abaixo. Preserve o PostgreSQL até verificar que os dados aparecem no site.

### Usar um arquivo SQLite preparado a partir do backup

Copie o arquivo por um canal privado que você controle, por exemplo um blob privado com uma URL SAS de leitura de curta duração ou um canal SSH protegido. O banco contém campanhas, arquivos e dados de sessão. Guarde-o fora do Git e desabilite o link de transferência quando terminar.

Dentro de `~/Companion/deploy/azure`, compare o SHA-256 do arquivo recebido com o fornecido na origem. Instale a cópia com nome `companion.db`, preservando qualquer arquivo anterior em separado:

```bash
sha256sum /caminho/privado/companion.db
sudo test ! -e data/companion.db &&
  sudo install -m 0600 -o 1654 -g 1654 /caminho/privado/companion.db data/companion.db
```

Um arquivo produzido a partir de um backup só inclui gravações feitas até a data do backup. Para incluir o uso mais recente da Azure, use a opção a seguir.

### Criar o arquivo com os dados atuais do PostgreSQL na Azure

Essa opção dispensa transferir um arquivo do Windows para a VM. O serviço `sqlite-import` usa a conexão `COMPANION_POSTGRES_TARGET` já configurada. Ele lê o PostgreSQL em uma transação, cria o SQLite, confere o conteúdo de cada tabela e só então coloca o arquivo no caminho final. O importador recusa sobrescrever um arquivo existente.

```bash
sudo docker compose --profile migration build sqlite-import
```

Quando estiver pronto para fazer a virada, pause a API para que nenhuma gravação posterior à cópia fique no PostgreSQL:

```bash
sudo docker compose stop api
sudo docker compose --profile migration run --rm --no-deps sqlite-import
```

Espere a mensagem `Arquivo SQLite pronto`. Se ocorrer um erro, aponte a aplicação para a conexão anterior e suba a API enquanto corrige a importação.

## 3. Ative o SQLite

Se usou um arquivo já preparado, pause a API antes de editar o `.env`. Abra `nano .env` e acrescente as duas linhas, mantendo a configuração anterior do PostgreSQL para facilitar o retorno:

```dotenv
COMPANION_DATABASE_PROVIDER=Sqlite
COMPANION_DATABASE_CONNECTION='Data Source=/data/companion.db;Mode=ReadWrite;Foreign Keys=True;Default Timeout=20'
```

O modo `ReadWrite` exige que o banco já exista. Suba a API e confira a rota pública:

```bash
sudo docker compose config --quiet
sudo docker compose up -d --no-build --wait api
curl -fsS https://companion.marcos-tech.loan/health
```

Abra uma campanha antiga, confira imagens ou anexos e faça uma rolagem com dois dispositivos. O proxy e o túnel continuam atendendo o mesmo domínio.

## 4. Backup e retorno ao PostgreSQL

Para um backup com a API em execução, use o comando `.backup` do `sqlite3` e transfira a cópia para fora da VM. Se preferir copiar o arquivo diretamente, pare a API primeiro e confira que nenhum outro processo está usando o banco. [Backup de um SQLite em execução](https://sqlite.org/backup.html)

Para voltar antes de aceitar gravações no SQLite, retire as duas variáveis novas do `.env` e rode `sudo docker compose up -d --no-build --wait api`. O Compose volta a usar `COMPANION_POSTGRES_TARGET`. Se o SQLite já tiver recebido novas gravações, reconcilie os dados antes de voltar.
