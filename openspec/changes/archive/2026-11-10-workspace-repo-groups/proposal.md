# Proposal

## Why

Hoje um workspace guarda só as abas de repositório abertas. A árvore de repositórios e grupos da tela inicial (Welcome) é global e não muda quando o workspace muda. Quem separa os projetos em workspaces (por exemplo MEEDS, ManyLinks, Conecta) troca de workspace e continua vendo todos os projetos misturados, então os workspaces parecem não ter efeito. Além disso, reorganizar a árvore é lento: só dá para mover um repositório ou grupo por vez.

## What Changes

- **Cada workspace passa a ter a própria árvore de repositórios.** O workspace é a raiz: dentro dele ficam repositórios e grupos (pastas organizacionais), em quantos níveis o usuário quiser. Um workspace novo começa vazio.
- **Cada repositório pertence a um único workspace.** Adicionar ou abrir um repositório que já está em outro workspace não o tira de lá.
- A tela inicial mostra **só a árvore do workspace ativo**, e trocar de workspace troca a árvore. A busca procura só nela.
- Tudo o que hoje cai na "raiz" passa a cair na raiz do workspace ativo: abrir, clonar, inicializar, confiar, escanear, abrir pela linha de comando, criar grupo, soltar na área vazia e mover para a raiz.
- Os seletores de grupo de "Clonar" e "Abrir repositório local", a janela "Mover para outro grupo" e a paleta de repositórios passam a mostrar só o workspace ativo.
- Apagar um workspace leva a árvore dele para o workspace ativo, para não perder nada.
- **Migração da árvore global existente:** um grupo com o mesmo nome de um workspace vira a árvore desse workspace. O que não corresponder a nenhum workspace vai para o workspace "Default" (ou para o primeiro, se não houver um "Default").
- **Seleção múltipla na árvore da tela inicial** com Ctrl+clique e Shift+clique. Os itens selecionados podem ser movidos juntos arrastando a seleção ou pelo menu "Mover para outro grupo".
- **Compatibilidade:** o `preference.json` existente é migrado na primeira carga. Uma versão antiga do SourceGit que ler o arquivo migrado mostra cada workspace como um grupo de mesmo nome na árvore global, sem perder repositórios.

## Capabilities

### New Capabilities
- `workspace-repositories`: árvore de repositórios própria de cada workspace. Cobre a migração da árvore global, o que a tela inicial e a busca mostram, para onde vão os itens novos, os seletores restritos ao workspace e o que acontece ao criar, renomear e apagar workspaces.
- `repository-tree-selection`: seleção de um ou vários itens na árvore de repositórios da tela inicial e as ações em lote sobre essa seleção (mover arrastando e pelo menu).

### Modified Capabilities
<!-- Nenhuma: o projeto ainda não tem specs em openspec/specs/. -->

## Impact

- **Modelo e persistência:** `ViewModels/Workspace.cs` ganha o ID da raiz da sua árvore. `ViewModels/Preferences.cs` ganha a migração em `PrepareWorkspaces` e a regra "raiz → raiz do workspace ativo" em `FindOrAddNodeByRepositoryPath`, `AddNode`, `MoveNode` e `FindOrCreateGroupRecursive`.
- **Tela inicial:** `ViewModels/Welcome.cs` (`Refresh`, busca, mover em lote), `Views/Welcome.axaml` e `Views/Welcome.axaml.cs` (seleção múltipla, arrastar vários itens, menu de contexto).
- **Seletores restritos ao workspace:** `ViewModels/Clone.cs`, `ViewModels/OpenLocalRepository.cs`, `ViewModels/MoveRepositoryNode.cs` (com `Views/MoveRepositoryNode.axaml`) e `ViewModels/LauncherPagesCommandPalette.cs`.
- **Workspaces:** `ViewModels/Launcher.cs` (`SwitchWorkspace` atualiza a tela inicial) e `ViewModels/ConfigureWorkspace.cs` (criar, renomear e apagar mantêm a árvore de cada workspace).
- **Textos:** chaves novas em `Resources/Locales/en_US.axaml` e `pt_BR.axaml`. As outras línguas caem no texto em inglês.
- **Sem dependências novas.**
