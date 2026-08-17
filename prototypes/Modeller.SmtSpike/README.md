# Solver-backed rule assurance prototype

This throwaway prototype answers one question: can a narrow canonical Truth/`And` projection use an SMT solver to find useful rule and decision defects without becoming a second semantic authority?

Run the Child Care claim example with one command:

```powershell
dotnet run --project prototypes/Modeller.SmtSpike/Modeller.SmtSpike.csproj
```

Run the exhaustive interpreter cross-check and defect examples with:

```powershell
dotnet test prototypes/Modeller.SmtSpike.Tests/Modeller.SmtSpike.Tests.csproj
```

The prototype is not part of `Modeller.Rules`. It does not change binding or evaluation.
