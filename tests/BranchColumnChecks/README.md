# Branch column checks

Run on a Windows desktop with .NET 10 and Git available:

```powershell
dotnet run --project tests/BranchColumnChecks -p:UsedAvaloniaProducts=
```

The executable uses an isolated temporary Git repository and configuration directory. It checks grouped branch labels, Unicode truncation, hover behavior, tooltips, graph connections, actual header dragging, saved column settings, and restoring the original message labels. Pass `--render` to save `preview-branches.png` beside the executable.
