# repository-tree-selection Specification

## Purpose

Permite selecionar vários repositórios e grupos na árvore da tela inicial e movê-los de uma vez, em vez de reorganizar a árvore um item por vez.

## Requirements

### Requirement: Seleção múltipla com Ctrl e Shift
A árvore de repositórios da tela inicial SHALL aceitar seleção de vários itens. Clique sem modificador SHALL selecionar só o item clicado. Ctrl+clique SHALL adicionar o item à seleção ou tirá-lo dela. Shift+clique SHALL selecionar o intervalo visível entre o último item clicado e o item clicado.

#### Scenario: Ctrl+clique
- **GIVEN** "meeds-bff" está selecionado
- **WHEN** o usuário dá Ctrl+clique em "meeds-ms-auth"
- **THEN** "meeds-bff" e "meeds-ms-auth" ficam selecionados

#### Scenario: Ctrl+clique em item já selecionado
- **GIVEN** "meeds-bff" e "meeds-ms-auth" estão selecionados
- **WHEN** o usuário dá Ctrl+clique em "meeds-ms-auth"
- **THEN** só "meeds-bff" continua selecionado

#### Scenario: Shift+clique
- **GIVEN** "meeds-front-admin" está selecionado e a lista mostra, em ordem, "meeds-front-admin", "meeds-front-doctor", "meeds-front-documentos", "meeds-front-web"
- **WHEN** o usuário dá Shift+clique em "meeds-front-web"
- **THEN** os quatro repositórios ficam selecionados

#### Scenario: Clique simples desfaz a seleção múltipla
- **GIVEN** três repositórios estão selecionados
- **WHEN** o usuário clica sem modificador em um quarto repositório
- **THEN** só o quarto repositório fica selecionado

### Requirement: Seleção em retângulo na área vazia
Apertar o botão esquerdo na área vazia da árvore e arrastar SHALL desenhar um retângulo e selecionar todos os itens que ele cobre, atualizando a seleção enquanto o ponteiro se move. Sem modificador, o retângulo SHALL substituir a seleção; com Ctrl (Meta no macOS), SHALL somar à seleção que existia antes. Soltar o botão SHALL esconder o retângulo e manter a seleção.

#### Scenario: Retângulo sobre vários repositórios
- **GIVEN** a árvore mostra "meeds-bff", "meeds-front-admin" e "meeds-front-doctor" em sequência, com área vazia abaixo
- **WHEN** o usuário aperta o botão na área vazia abaixo da lista e arrasta para cima até cobrir os três
- **THEN** os três ficam selecionados

#### Scenario: Retângulo com Ctrl soma à seleção
- **GIVEN** "meeds-ms-auth" está selecionado
- **WHEN** o usuário segura Ctrl e desenha um retângulo sobre "meeds-bff" e "meeds-front-admin"
- **THEN** os três ficam selecionados

#### Scenario: Gesto sobre um item continua sendo arrastar
- **WHEN** o usuário aperta o botão sobre um item e arrasta
- **THEN** nenhum retângulo é desenhado e o item é arrastado para mover

### Requirement: Arrastar a seleção
Começar a arrastar um item que faz parte da seleção SHALL arrastar todos os itens selecionados. Começar a arrastar um item fora da seleção SHALL arrastar só esse item. Apertar o botão sobre um item já selecionado, sem modificador, SHALL manter a seleção até o usuário soltar sem ter arrastado.

#### Scenario: Arrastar vários para um grupo
- **GIVEN** "meeds-integracao-betim" e "meeds-integracao-gmus" estão selecionados
- **WHEN** o usuário arrasta um dos dois e solta sobre o grupo "Integrações"
- **THEN** os dois repositórios passam a ficar dentro de "Integrações"

#### Scenario: Soltar sobre um repositório
- **GIVEN** dois repositórios estão selecionados
- **WHEN** o usuário arrasta a seleção e solta sobre um repositório que está no grupo "MEEDS"
- **THEN** os dois repositórios vão para o grupo "MEEDS", que é o grupo do repositório de destino

#### Scenario: Arrastar item fora da seleção
- **GIVEN** "meeds-bff" e "meeds-ms-auth" estão selecionados
- **WHEN** o usuário arrasta "meeds-ms-bpa", que não está selecionado, e solta sobre o grupo "Antigos"
- **THEN** só "meeds-ms-bpa" é movido

#### Scenario: Clique sem arrastar em item selecionado
- **GIVEN** três repositórios estão selecionados
- **WHEN** o usuário aperta e solta o botão sobre um deles sem arrastar e sem modificador
- **THEN** só esse repositório fica selecionado

### Requirement: Mover a seleção pelo menu
Com mais de um item selecionado, o menu de contexto aberto sobre um item da seleção SHALL oferecer mover todos os selecionados, mostrando quantos são. A janela de destino SHALL mover todos os itens para o grupo escolhido.

#### Scenario: Mover vários pelo menu
- **GIVEN** quatro repositórios estão selecionados
- **WHEN** o usuário clica com o botão direito em um deles, escolhe mover e seleciona o grupo "Front"
- **THEN** os quatro repositórios passam a ficar dentro de "Front"

#### Scenario: Menu sobre item fora da seleção
- **GIVEN** dois repositórios estão selecionados
- **WHEN** o usuário clica com o botão direito em um terceiro repositório, fora da seleção
- **THEN** a seleção passa a ser só esse repositório e o menu mostra as ações de um único item

### Requirement: Ações de item único com seleção múltipla
Com mais de um item selecionado, o menu de contexto SHALL mostrar só ações que valem para todos os selecionados. Ações de um único item (editar, abrir, abrir no gerenciador de arquivos, abrir terminal, apagar) SHALL ficar fora desse menu. A tecla Delete SHALL não apagar nada quando há mais de um item selecionado.

#### Scenario: Menu com seleção múltipla
- **GIVEN** dois repositórios estão selecionados
- **WHEN** o usuário abre o menu de contexto sobre um deles
- **THEN** o menu mostra a ação de mover os selecionados e não mostra editar, abrir nem apagar

#### Scenario: Delete com seleção múltipla
- **GIVEN** dois repositórios estão selecionados
- **WHEN** o usuário aperta Delete
- **THEN** nenhum item é apagado e a janela de confirmação de exclusão não abre

### Requirement: Grupo e descendentes selecionados juntos
Quando a seleção contém um grupo e itens que estão dentro dele, mover a seleção SHALL mover só o grupo, e os itens internos SHALL ir junto com ele, mantendo a estrutura.

#### Scenario: Grupo e filho selecionados
- **GIVEN** o grupo "Antigos" e o repositório "Antigos/legado-api" estão selecionados
- **WHEN** o usuário move a seleção para o grupo "Arquivo"
- **THEN** "Antigos" fica dentro de "Arquivo" e "legado-api" continua dentro de "Antigos"

### Requirement: Destinos inválidos
Um grupo selecionado SHALL não poder ser movido para dentro de si mesmo nem de um descendente. A janela de destino SHALL esconder esses grupos, e soltar a seleção sobre um deles SHALL não mover nenhum item.

#### Scenario: Janela de destino com grupo selecionado
- **GIVEN** o grupo "Antigos", que tem o subgrupo "Antigos/2024", está selecionado
- **WHEN** o usuário abre "Mover para outro grupo"
- **THEN** a lista de destinos não mostra "Antigos" nem "Antigos/2024"

#### Scenario: Soltar sobre o próprio descendente
- **GIVEN** o grupo "Antigos" e o repositório "outro-repo" estão selecionados
- **WHEN** o usuário arrasta a seleção e solta sobre o subgrupo "Antigos/2024"
- **THEN** nenhum item é movido

### Requirement: Seleção e destinos dentro do workspace
A seleção SHALL conter só itens visíveis na tela inicial, que pertencem ao workspace ativo. Mover para a raiz SHALL levar os itens para a raiz do workspace ativo, e a janela de destino SHALL mostrar como raiz o workspace ativo e só os grupos dele.

#### Scenario: Mover para a raiz do workspace
- **GIVEN** o workspace ativo é "MEEDS" e dois repositórios do grupo "Antigos" estão selecionados
- **WHEN** o usuário move a seleção para a raiz pela janela de destino
- **THEN** os dois repositórios passam a ficar na raiz do workspace "MEEDS"

#### Scenario: Destinos do workspace
- **GIVEN** o workspace ativo é "MEEDS" e o workspace "ManyLinks" tem o grupo "Front"
- **WHEN** o usuário abre "Mover para outro grupo" com itens selecionados
- **THEN** a janela mostra "MEEDS" como raiz e só os grupos de "MEEDS" como destinos, sem "Front"
