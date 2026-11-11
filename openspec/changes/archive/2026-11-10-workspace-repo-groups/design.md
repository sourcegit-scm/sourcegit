# Design

## Context

Motivação: ver `proposal.md` (seção Why). Requisitos: `specs/workspace-repositories/spec.md` e `specs/repository-tree-selection/spec.md`.

Estado atual que molda a abordagem:

- `Preferences` guarda duas listas independentes: `Workspaces` (cada uma com as abas em `Repositories` e `ActiveIdx`) e `RepositoryNodes`, a árvore global de grupos e repositórios. Os grupos têm um `Id` GUID estável; os repositórios usam o caminho como `Id`.
- A serialização do `preference.json` é feita por código gerado (`App.JsonCodeGen.cs`, `[JsonSerializable(typeof(ViewModels.Preferences))]`). Propriedades públicas novas em `Workspace` são salvas automaticamente; propriedades só de leitura e as marcadas com `[JsonIgnore]` não são. Campos desconhecidos no arquivo são ignorados ao ler.
- Muitos pontos procuram nós na árvore global inteira: `Preferences.FindNode`, usado ao abrir abas, apagar e renomear, a detecção de repositórios já gerenciados no `ScanRepositories` e a limpeza de nós inválidos.
- `Welcome` é um singleton compartilhado por todas as abas vazias. `Refresh()` monta `Rows` a partir de uma coleção-base, com profundidade inicial 0, e a busca marca `IsVisible` percorrendo a mesma coleção.
- Em toda a base, `parent == null` significa "raiz". Ele aparece em `FindOrAddNodeByRepositoryPath`, chamado por `Welcome`, `Init`, `Clone`, `OpenLocalRepository`, `TrustRepository`, `ScanRepositories` e na abertura pela linha de comando do `Launcher` (`TryOpenRepositoryFromPath`). Aparece também em `AddNode` (usado por `CreateGroup`) e em `MoveNode` (soltar na área vazia e "ROOT" em `MoveRepositoryNode`). As abas restauradas de um workspace não passam por ele: um repositório fora da árvore abre como aba "unmanaged" e não é adicionado.
- Os seletores de grupo de `Clone` e `OpenLocalRepository` e a `LauncherPagesCommandPalette` percorrem `Preferences.RepositoryNodes` inteira.
- `Views/BranchTree.axaml.cs` já implementa uma árvore com `SelectionMode="Multiple"`, com Ctrl (Meta no macOS) e Shift e ações sobre `SelectedItems`.

## Goals / Non-Goals

**Goals:**
- Cada workspace com a sua árvore, sem reescrever os pontos que procuram nós globalmente.
- Um ponto único que decide qual é a raiz atual, usado por todos os fluxos que recebem `parent == null`.
- Migrar a árvore existente sem perder nada e sem exigir passo manual.
- Seleção múltipla e mover em lote reaproveitando o padrão do `BranchTree`.

**Non-Goals:**
- Um repositório em mais de um workspace.
- Ver os repositórios de todos os workspaces juntos.
- Mover itens entre workspaces pela interface. Hoje isso só acontece apagando um workspace; arrastar entre workspaces fica para depois.
- Apagar, abrir ou editar vários itens de uma vez. Só mover entra em lote nesta change.
- Manter a seleção depois de mover: a lista é reconstruída e a seleção é limpa.
- Traduzir os textos novos para línguas além de en_US e pt_BR.

## Decisions

### D1. A árvore de cada workspace é um grupo-raiz oculto na árvore global
`Workspace` ganha `RootGroupId` (string, persistido). Ele aponta para um grupo no primeiro nível de `Preferences.RepositoryNodes`. Esse grupo é a raiz da árvore do workspace e nunca aparece na interface. No modelo novo, o primeiro nível da árvore global só tem esses grupos-raiz, um por workspace. O nome do grupo-raiz acompanha o nome do workspace, só para o JSON ficar legível e para uma versão antiga do SourceGit mostrar algo que faça sentido.

- *Alternativa descartada: uma `List<RepositoryNode>` dentro de cada `Workspace`.* É o modelo mais "puro", mas obriga a reescrever todos os pontos que procuram nós globalmente (Context) para varrer todos os workspaces. Também deixa a regra "um repositório num só workspace" para ser garantida à mão. Com a árvore global única, um `Id` só pode existir uma vez, então a regra vem de graça.
- *Alternativa descartada: manter a ligação opcional a um grupo visível (versão anterior desta change).* Foi rejeitada pelo usuário: o workspace deve ser a raiz, sem depender de um grupo ligado.

### D2. Migração em `PrepareWorkspaces`, idempotente
Roda em toda carga, mas só faz algo quando encontra o que corrigir:
1. Para cada workspace com `RootGroupId` vazio ou apontando para um grupo inexistente: se há exatamente um grupo com o mesmo nome (sem diferenciar maiúsculas) em qualquer nível, ou, havendo vários, exatamente um com a grafia exata, esse grupo vira o grupo-raiz e, se estiver aninhado, sobe para o primeiro nível. Se não há, um grupo-raiz vazio é criado.
2. Todo nó do primeiro nível que não é grupo-raiz de algum workspace é movido para dentro do grupo-raiz do workspace "Default" (ou do primeiro workspace, se não houver um "Default").
3. O nome de cada grupo-raiz é sincronizado com o nome do workspace.

Na carga seguinte, os workspaces já têm `RootGroupId` válido, então o passo 1 não religa nada. Isso atende ao cenário "Migração não se repete". O passo 2 só age se outra versão do app tiver deixado itens soltos no primeiro nível.

Os campos `LinkedGroupId` e `IsGroupLinkResolved`, gravados pela versão anterior desta change, deixam de existir. O leitor ignora os dois, e a ligação pelo nome refaz a associação.

### D3. Raiz efetiva centralizada em `Preferences`
`Preferences.GetScopeRoot()` retorna o grupo-raiz do workspace ativo e o recria se tiver sumido, então nunca retorna `null` depois da carga. `GetScopeNodes()` retorna os `SubNodes` dele.

Os três pontos de entrada traduzem `null` para essa raiz:
- `AddNode(node, to: null)` → `to = GetScopeRoot()`.
- `MoveNode(node, to: null)` → `to = GetScopeRoot()`.
- `FindOrAddNodeByRepositoryPath(repo, parent: null, shouldMoveNode)`: se o nó não existe, é criado na raiz do workspace ativo. Se já existe em algum lugar, ou seja, em algum workspace, e o `parent` original era `null`, ele **não** é movido. Com `parent` explícito, o comportamento atual continua igual.

`FindOrCreateGroupRecursive`, usado pelo `ScanRepositories`, passa a partir de `GetScopeNodes()`. A detecção de repositórios já gerenciados no `ScanRepositories` continua global, para que escanear não tire repositórios de outros workspaces.

Rotinas internas que usam `RepositoryNodes` diretamente (busca por ID, remoção, limpeza de inválidos, ordenação após renomear) continuam operando na árvore inteira.

### D4. Telas que listam a árvore usam a raiz efetiva
- `Welcome.Refresh()`, a busca e `UpdateStatusAsync` usam `GetScopeNodes()`.
- `Clone` e `OpenLocalRepository` montam o seletor de grupos a partir de `GetScopeNodes()`. A opção "Sem grupo" já passa `null`, que vira a raiz do workspace.
- `LauncherPagesCommandPalette` lista os repositórios de `GetScopeNodes()`.
- `MoveRepositoryNode` mostra como raiz uma linha com o nome do workspace, que aponta para o grupo-raiz, e lista só os grupos dele.

`Launcher.SwitchWorkspace` chama `Welcome.Instance.Refresh()` depois de trocar o workspace ativo.

### D5. Ciclo de vida do workspace em `ConfigureWorkspace`
- **Criar:** o workspace novo recebe um grupo-raiz vazio na hora.
- **Apagar:** os `SubNodes` do grupo-raiz vão para o grupo-raiz do workspace ativo (o apagado nunca é o ativo, pela regra que já existe). Depois o grupo-raiz é removido.
- **Renomear:** ao fechar a janela, o nome dos grupos-raiz é sincronizado e a tela inicial é atualizada.

### D6. Seleção múltipla
`RepositoryListBox` usa `SelectionMode="Multiple"`. Ctrl (Meta no macOS) e Shift vêm do próprio `ListBox` do Avalonia.

**Problema conhecido:** o `ListBox` muda a seleção quando o botão é *apertado*, então apertar sobre um item já selecionado colapsaria a seleção antes do arrasto. Solução: no `PointerPressed` do item, se ele já está selecionado, não há modificador e `SelectedItems.Count > 1`, o evento é marcado como tratado e fica guardado um "clique pendente". No `PointerReleased` sem arrasto, só aquele item fica selecionado.

As teclas que usam `SelectedItem` (Enter, Esquerda/Direita, Delete) só agem quando `SelectedItems.Count == 1`.

### D6b. Seleção em retângulo
O `ListBox` do Avalonia não tem seleção em retângulo, então ela é implementada na view da tela inicial:
- Um `Canvas` sobreposto à árvore, que não recebe cliques, mostra o retângulo.
- O gesto só começa quando o botão é apertado fora de qualquer `ListBoxItem` e fora da barra de rolagem. O ponteiro é capturado pela lista.
- A cada movimento, os itens são calculados pelos contêineres realizados cujos limites cruzam o retângulo, e a seleção é aplicada em lote pelo `Selection` do `ListBox`. Com Ctrl ao começar, a seleção anterior é somada.
- *Limitação aceita:* não há rolagem automática ao encostar na borda. Com a virtualização, só os itens visíveis podem ser cobertos, o que já é o que o retângulo mostra.

### D7. Arrasto de vários itens
O formato `sourcegit-dnd-repo-node` continua o mesmo, mas o conteúdo passa a ser uma lista de IDs separados por `\n`. Se o item sob o cursor está na seleção, vão todos os selecionados; se não está, vai só ele.

### D8. Mover em lote no modelo
`Welcome.MoveNodes(nodes, to)`, usado pelo arrasto e pela janela:
1. **Normalizar:** remover da lista todo nó que é descendente de outro grupo também selecionado.
2. **Validar:** se `to` é um dos grupos selecionados ou descendente de um deles, não mover nada.
3. Chamar `Preferences.MoveNode(n, to, save: false)` para cada nó, depois `Save()` uma vez e `Refresh()`.

- *Por que tudo ou nada:* a janela já esconde os destinos inválidos, então o caso só acontece por arrasto, e não fazer nada é o retorno mais claro.

### D9. `MoveRepositoryNode` para vários itens
Recebe `IReadOnlyList<RepositoryNode> targets`, mostra o nome do item ou "N itens", pula os grupos selecionados e seus descendentes, e delega para `Welcome.MoveNodes`. O construtor de um único item continua existindo para `RepositoryNode.Move()`.

## Risks / Trade-offs

- **[Algum fluxo passa `null` esperando a raiz real]** → A tradução fica só nos três métodos de D3. As tarefas incluem conferir cada chamador listado em Context.
- **[A seleção colapsa ao começar o arrasto]** → Tratado em D6. Precisa de teste manual no Linux, porque o comportamento de pressão do `ListBox` é do Avalonia.
- **[Um repositório aberto em aba não aparece na tela inicial]** porque está em outro workspace → É intencional (um repositório, um workspace). Para movê-lo, por enquanto o caminho é trocar para o workspace dele.
- **[A migração associa o grupo errado]** quando há nomes parecidos → Só associa com nome igual (sem diferenciar maiúsculas) e com um único candidato. O resto vai para o Default, de onde o usuário move com a seleção múltipla.
- **[O `preference.json` migrado aberto numa versão antiga do SourceGit]** → A versão antiga mostra cada workspace como um grupo de mesmo nome no primeiro nível e descarta `RootGroupId` ao salvar. Ao voltar para esta versão, o passo 1 da migração religa os grupos pelo nome.

## Migration Plan

- Não há passo manual. Na primeira carga com esta versão, `PrepareWorkspaces` executa D2 e o resultado é salvo no próximo `Save()`.
- **Rollback:** voltar para a versão anterior. A árvore continua íntegra, com um grupo por workspace no primeiro nível.
