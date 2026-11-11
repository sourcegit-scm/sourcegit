# Spec Delta

## Purpose

Dá a cada workspace a sua própria árvore de repositórios e grupos, para que trocar de workspace também troque a lista de repositórios da tela inicial, além das abas abertas.

## ADDED Requirements

### Requirement: Árvore própria por workspace
Cada workspace SHALL ter a sua própria árvore de repositórios e grupos, cuja raiz é o próprio workspace. A tela inicial SHALL mostrar só a árvore do workspace ativo, sem nenhum nó que represente o workspace. Um workspace criado SHALL começar com a árvore vazia.

#### Scenario: Tela inicial do workspace ativo
- **GIVEN** o workspace "MEEDS" contém "meeds-bff" e o grupo "Integrações", e o workspace "Conecta" contém "conecta_front"
- **WHEN** o usuário está no workspace "MEEDS" e abre uma aba nova
- **THEN** a tela inicial lista "Integrações" e "meeds-bff" no primeiro nível e não lista "conecta_front"

#### Scenario: Trocar de workspace troca a árvore
- **GIVEN** a tela inicial mostra a árvore do workspace "MEEDS"
- **WHEN** o usuário troca para o workspace "Conecta"
- **THEN** a tela inicial passa a mostrar a árvore do workspace "Conecta"

#### Scenario: Workspace novo
- **WHEN** o usuário cria um workspace e o ativa
- **THEN** a tela inicial mostra uma árvore vazia

### Requirement: Repositório em um único workspace
Um repositório SHALL estar na árvore de no máximo um workspace. Abrir, clonar, inicializar ou confiar em um repositório que já está na árvore de outro workspace SHALL abrir a aba normalmente e SHALL mantê-lo no workspace onde já está.

#### Scenario: Abrir repositório de outro workspace
- **GIVEN** o workspace ativo é "Conecta" e o repositório "meeds-bff" está no workspace "MEEDS"
- **WHEN** o usuário abre "meeds-bff" pelo menu "Abrir repositório local"
- **THEN** a aba de "meeds-bff" abre, e "meeds-bff" continua no workspace "MEEDS" e não aparece na tela inicial de "Conecta"

#### Scenario: Abrir repositório que não está em nenhum workspace
- **GIVEN** o workspace ativo é "Conecta" e o repositório "utilitarios" não está em nenhum workspace
- **WHEN** o usuário abre "utilitarios" pelo menu "Abrir repositório local" sem escolher grupo
- **THEN** "utilitarios" é adicionado à raiz do workspace "Conecta"

### Requirement: Raiz do workspace como destino
Toda ação que coloca um item na raiz SHALL colocá-lo na raiz do workspace ativo: adicionar, clonar, inicializar ou confiar em um repositório, abrir um repositório recebido pela linha de comando, escanear a pasta padrão de clone, criar grupo na raiz, soltar itens na área vazia da lista e escolher a raiz em "Mover para outro grupo".

#### Scenario: Clonar sem escolher grupo
- **GIVEN** o workspace ativo é "MEEDS"
- **WHEN** o usuário clona um repositório novo escolhendo "Sem grupo"
- **THEN** o repositório aparece na raiz do workspace "MEEDS"

#### Scenario: Criar grupo na raiz
- **GIVEN** o workspace ativo é "MEEDS"
- **WHEN** o usuário cria o grupo "Microsserviços" pelo menu da área vazia
- **THEN** o grupo "Microsserviços" aparece na raiz do workspace "MEEDS"

#### Scenario: Soltar na área vazia
- **GIVEN** o workspace ativo é "MEEDS" e existe o grupo "Antigos" dentro dele
- **WHEN** o usuário arrasta um repositório de "Antigos" e solta na área vazia da lista
- **THEN** o repositório passa a ficar na raiz do workspace "MEEDS"

#### Scenario: Escanear a pasta padrão de clone
- **GIVEN** o workspace ativo é "MEEDS" e a pasta padrão de clone tem a subpasta "api" com um repositório novo
- **WHEN** o usuário reescaneia a pasta padrão de clone
- **THEN** o grupo "api" é criado dentro do workspace "MEEDS" com o repositório novo, e repositórios que já estão em outros workspaces não são movidos

### Requirement: Busca restrita ao workspace
A busca da tela inicial SHALL procurar só na árvore do workspace ativo.

#### Scenario: Busca no workspace
- **GIVEN** o workspace ativo é "MEEDS" e o workspace "ManyLinks" tem "manylinks-auth-front"
- **WHEN** o usuário busca "front"
- **THEN** aparecem só os repositórios do workspace "MEEDS" cujo nome contém "front"

### Requirement: Seletores restritos ao workspace
Os seletores de grupo de "Clonar" e "Abrir repositório local" e a paleta de repositórios SHALL listar só os grupos e repositórios do workspace ativo.

#### Scenario: Grupos ao clonar
- **GIVEN** o workspace ativo é "MEEDS", que tem o grupo "Integrações", e o workspace "ManyLinks" tem o grupo "Front"
- **WHEN** o usuário abre "Clonar repositório"
- **THEN** a lista de grupos mostra "Integrações" e não mostra "Front"

#### Scenario: Paleta de repositórios
- **GIVEN** o workspace ativo é "Conecta"
- **WHEN** o usuário abre a paleta de troca de repositórios
- **THEN** a paleta lista só os repositórios do workspace "Conecta"

### Requirement: Migração da árvore global
Na primeira carga, cada workspace SHALL receber como árvore o conteúdo do grupo de mesmo nome, ignorando maiúsculas e minúsculas, quando há exatamente um grupo com esse nome; havendo vários, SHALL valer o único com a grafia exata do workspace. Todo o resto da árvore global SHALL ir para o workspace "Default", ou para o primeiro workspace se não houver um "Default". A migração SHALL acontecer uma única vez e SHALL não perder nenhum repositório nem grupo.

#### Scenario: Grupo com o nome do workspace
- **GIVEN** a árvore global tem o grupo "MEEDS" com 19 repositórios e existe o workspace "MEEDS"
- **WHEN** o app carrega as preferências pela primeira vez com esta versão
- **THEN** a árvore do workspace "MEEDS" tem os 19 repositórios no primeiro nível, sem um grupo "MEEDS" no meio

#### Scenario: Grupos com grafias diferentes
- **GIVEN** a árvore global tem os grupos "MEEDS" e "Meeds" e existe o workspace "MEEDS"
- **WHEN** o app carrega as preferências pela primeira vez com esta versão
- **THEN** o conteúdo do grupo "MEEDS" vira a árvore do workspace "MEEDS", e o grupo "Meeds" vai para o workspace "Default"

#### Scenario: Itens sem workspace correspondente
- **GIVEN** a árvore global tem o repositório solto "utilitarios" e o grupo "Estudos", e não existe workspace "Estudos"
- **WHEN** o app carrega as preferências pela primeira vez com esta versão
- **THEN** "utilitarios" e "Estudos" ficam na árvore do workspace "Default"

#### Scenario: Migração não se repete
- **GIVEN** a migração já aconteceu e o usuário criou depois um grupo "Conecta" dentro do workspace "MEEDS"
- **WHEN** o app é reiniciado
- **THEN** o grupo "Conecta" continua dentro do workspace "MEEDS"

### Requirement: Renomear e apagar workspaces
Renomear um workspace SHALL manter a árvore dele. Apagar um workspace SHALL levar toda a árvore dele para a raiz do workspace ativo.

#### Scenario: Renomear workspace
- **WHEN** o usuário renomeia o workspace "MEEDS" para "Meeds Saúde"
- **THEN** o workspace "Meeds Saúde" continua com os mesmos repositórios e grupos

#### Scenario: Apagar workspace
- **GIVEN** o workspace ativo é "Default" e o workspace "Conecta" tem "conecta_front" e "conecta-back"
- **WHEN** o usuário apaga o workspace "Conecta"
- **THEN** "conecta_front" e "conecta-back" aparecem na raiz do workspace "Default"

### Requirement: Abas do workspace mantidas
Trocar de workspace SHALL continuar fechando as abas do workspace anterior e restaurando as abas salvas do novo workspace.

#### Scenario: Restaurar abas ao trocar
- **GIVEN** o workspace "Conecta" tinha as abas "conecta_front" e "conecta-back" abertas
- **WHEN** o usuário troca para "Conecta"
- **THEN** as duas abas são reabertas e a tela inicial mostra a árvore do workspace "Conecta"
