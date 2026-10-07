# build

> [!WARNING]
> The files under the `build` folder is used for `Github Action` only, **NOT** for end users.

## How to build this project manually

1. Make sure [.NET SDK 10](https://dotnet.microsoft.com/en-us/download) is installed on your machine.
2. Clone this project
3. Run the follow command under the project root dir
```sh
dotnet publish -c Release -r $RUNTIME_IDENTIFIER -o $DESTINATION_FOLDER src/SourceGit.csproj
```
> [!NOTE]
> Please replace the `$RUNTIME_IDENTIFIER` with one of `win-x64`,`win-arm64`,`linux-x64`,`linux-arm64`,`osx-x64`,`osx-arm64`, and replace the `$DESTINATION_FOLDER` with the real path that will store the output executable files.


## Native AOT diagnostics

Release builds publish as Native AOT. A successful publish can still report AOT
analysis warnings from dependencies. To expand a third-party assembly warning
such as `IL3053`, publish with:

```sh
dotnet publish src/SourceGit.csproj -c Release -r linux-x64 -p:TrimmerSingleWarn=false
```

`SuppressTrimAnalysisWarnings` currently suppresses trimming diagnostics; it does
not suppress AOT diagnostics. The command above expands the existing AOT warnings
without disabling the analysis or changing their severity.

### DataGrid warnings

With Avalonia.Controls.DataGrid 12.1.2, the expanded `IL3053` consists of three
`IL3050` warnings:

* `TypeHelper.GetPropertyOrIndexer()` in `Utils/ReflectionHelper.cs` dynamically
  constructs `IList<T>` and `IReadOnlyList<T>` with `Type.MakeGenericType`. These
  two warnings also occur with DataGrid 11.3.13. This fallback is reached only for
  an indexer path when no suitable default indexer was found and an element type
  can be inferred.
* `DataGrid.GenerateColumn()` in `DataGridColumns.cs` creates a reflection-based
  `Binding` for an automatically generated column. Avalonia 12 marks that
  constructor as requiring dynamic code.

SourceGit's `HistoriesCommitList` sets `AutoGenerateColumns` and
`CanUserSortColumns` to `false`, sets `IsReadOnly` to `true`, and defines its
columns explicitly as `DataGridTemplateColumn` instances. It does not configure
indexed property paths for DataGrid's bound-column, sorting, or grouping helpers.
These usage constraints are the basis for accepting the audited warnings; they
are not proof of unreachability available to the AOT compiler.

Revisit this assessment when changing the DataGrid version, adding automatically
generated or bound columns, introducing indexed property paths, or enabling
property-based sorting or grouping. Setting a runtime DataGrid property does not
remove the corresponding implementation from AOT analysis. The current DataGrid
package has no feature switch to disable its indexer fallback.

Keep AOT warnings enabled. A global `NoWarn=IL3050` would also hide unrelated
warnings introduced by future changes. An `UnconditionalSuppressMessage` on a
SourceGit caller does not suppress warnings emitted inside a dependency method.
A dependency-level fix or suppression would require changes to that dependency;
suppression alone would not change its runtime behavior.

References:

* [IL3053: assembly produced AOT warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/warnings/il3053)
* [IL3050: calls requiring dynamic code](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/warnings/il3050)
* [DataGrid 12.1.2 indexer fallback](https://github.com/AvaloniaUI/Avalonia.Controls.DataGrid/blob/1e12211eb0c52a09e314ca57a344147c37d0a26c/src/Avalonia.Controls.DataGrid/Utils/ReflectionHelper.cs#L342-L416)
* [DataGrid 12.1.2 automatic column generation](https://github.com/AvaloniaUI/Avalonia.Controls.DataGrid/blob/1e12211eb0c52a09e314ca57a344147c37d0a26c/src/Avalonia.Controls.DataGrid/DataGridColumns.cs#L1703-L1784)
