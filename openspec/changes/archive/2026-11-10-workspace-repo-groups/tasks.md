# Tasks

> O projeto não tem testes automatizados. Cada tarefa é verificada com a build (`dotnet build src/SourceGit.csproj` sem erros nem avisos) e com as checagens manuais descritas. Os cenários citados estão em `specs/`.

## 1. Modelo: árvore própria por workspace

- [x] 1.1 Trocar em `Workspace` os campos da versão anterior (`LinkedGroupId`, `IsGroupLinkResolved`, `ShowAllRepositories`) por `RootGroupId` (string, persistido), conforme design D1. Verificar: build ok e, depois de rodar e fechar o app, o `preference.json` tem `RootGroupId` em cada workspace e não tem os campos antigos.
- [x] 1.2 Implementar a migração idempotente em `Preferences.PrepareWorkspaces()` (design D2): grupo de mesmo nome vira o grupo-raiz e sobe para o primeiro nível; sem grupo, cria um vazio; itens soltos no primeiro nível vão para o Default (ou o primeiro workspace); nomes sincronizados. Verificar com o `preference.json` real: o primeiro nível tem só os quatro grupos-raiz, MEEDS/ManyLinks/Conecta têm os repositórios no primeiro nível da sua árvore, e nenhum repositório some (contar antes e depois). Reiniciar não muda nada (cenários de "Migração da árvore global").
- [x] 1.3 Implementar `GetScopeRoot()` (sempre o grupo-raiz do workspace ativo, recriando se faltar) e `GetScopeNodes()` (design D3). Verificar: build ok e cenário "Workspace novo".

## 2. Modelo: raiz do workspace como destino

- [x] 2.1 Traduzir `to == null` para `GetScopeRoot()` em `Preferences.AddNode` e `Preferences.MoveNode`. Verificar: "Criar grupo" pela área vazia cria o grupo na raiz do workspace ativo (cenário "Criar grupo na raiz").
- [x] 2.2 Ajustar `FindOrAddNodeByRepositoryPath`: criar na raiz do workspace ativo e, com `parent` original `null`, nunca mover um nó que já existe (design D3). Verificar: os cenários "Abrir repositório de outro workspace" e "Abrir repositório que não está em nenhum workspace".
- [x] 2.3 Fazer `FindOrCreateGroupRecursive` partir de `GetScopeNodes()`, mantendo global a detecção de repositórios gerenciados no `ScanRepositories`. Verificar: o cenário "Escanear a pasta padrão de clone".
- [x] 2.4 Conferir cada chamador listado no design (Context) e confirmar que nenhum precisa da raiz global. Verificar: o cenário "Clonar sem escolher grupo"; abrir pela linha de comando (`SourceGit <caminho>`) um repositório fora da árvore o põe na raiz do workspace ativo.

## 3. Telas restritas ao workspace

- [x] 3.1 Fazer `Welcome.Refresh()`, a busca e `UpdateStatusAsync` usarem `GetScopeNodes()`, e remover a faixa e o "Ver todos" (design D4). Verificar: os cenários "Tela inicial do workspace ativo" e "Busca no workspace".
- [x] 3.2 Em `Launcher.SwitchWorkspace`, chamar `Welcome.Instance.Refresh()`. Verificar: os cenários "Trocar de workspace troca a árvore" e "Restaurar abas ao trocar".
- [x] 3.3 Restringir ao workspace ativo os seletores de grupo de `Clone` e `OpenLocalRepository` e a `LauncherPagesCommandPalette` (design D4). Verificar: os cenários "Grupos ao clonar" e "Paleta de repositórios".
- [x] 3.4 Remover as chaves de texto que deixaram de ser usadas (faixa, "Ver todos", seletor de grupo) de `en_US.axaml` e `pt_BR.axaml`. Verificar: `grep` sem ocorrências no código e build ok.

## 4. Ciclo de vida do workspace

- [x] 4.1 Em `ConfigureWorkspace`, remover o seletor de grupo, criar o grupo-raiz ao criar um workspace e, ao apagar, levar a árvore para o workspace ativo (design D5). Verificar: os cenários "Workspace novo" e "Apagar workspace".
- [x] 4.2 Sincronizar o nome dos grupos-raiz e atualizar a tela inicial ao fechar a configuração. Verificar: o cenário "Renomear workspace".

## 5. Seleção múltipla na árvore

- [x] 5.1 Usar `SelectionMode="Multiple"` no `RepositoryListBox` (design D6). Verificar: os cenários de "Seleção múltipla com Ctrl e Shift".
- [x] 5.2 Adiar a seleção única para o `PointerReleased` sem arrasto quando o item apertado já está numa seleção múltipla (design D6). Verificar: o cenário "Clique sem arrastar em item selecionado", e arrastar mantém todos selecionados.
- [x] 5.3 Fazer Enter, Esquerda/Direita e Delete agirem só com exatamente um item selecionado. Verificar: o cenário "Delete com seleção múltipla"; com um item, tudo funciona como antes.
- [x] 5.4 Implementar a seleção em retângulo na área vazia (design D6b). Verificar: os cenários "Retângulo sobre vários repositórios", "Retângulo com Ctrl soma à seleção" e "Gesto sobre um item continua sendo arrastar".

## 6. Mover vários itens

- [x] 6.1 Implementar `Welcome.MoveNodes(nodes, to)` (design D8). Verificar: o cenário "Grupo e filho selecionados".
- [x] 6.2 Levar no arrasto todos os IDs selecionados, ou só o item arrastado quando ele está fora da seleção, e usar `MoveNodes` em `DropOnTreeNode` e `DropOnTreeView` (design D7). Verificar: os cenários "Arrastar vários para um grupo", "Soltar sobre um repositório", "Arrastar item fora da seleção", "Soltar sobre o próprio descendente" e "Soltar na área vazia".
- [x] 6.3 `MoveRepositoryNode` com vários itens, com a raiz mostrando o nome do workspace e só os grupos dele (design D9). Verificar: os cenários "Mover vários pelo menu", "Janela de destino com grupo selecionado", "Mover para a raiz do workspace" e "Destinos do workspace"; mover um item só continua igual a antes.
- [x] 6.4 Menu de contexto com seleção múltipla: só "Mover N itens para outro grupo"; clique fora da seleção volta ao menu de um item. Verificar: os cenários "Menu com seleção múltipla" e "Menu sobre item fora da seleção".
- [x] 6.5 Manter em `en_US.axaml` e `pt_BR.axaml` as chaves do menu e da janela para vários itens. Verificar: os textos aparecem traduzidos.

## 7. Integração

- [x] 7.1 Rodar a build em Release (`dotnet build src/SourceGit.csproj -c Release`) e a checagem de formatação do CI (`dotnet format --verify-no-changes src/SourceGit.csproj`). Verificar: as duas passam sem erros.
- [x] 7.2 Percorrer de ponta a ponta com o `preference.json` real: abrir o app, conferir a migração, trocar entre workspaces, buscar, clonar ou abrir um repositório, criar e apagar um workspace de teste, selecionar vários repositórios e movê-los arrastando e pelo menu, reiniciar e confirmar que tudo persistiu.
