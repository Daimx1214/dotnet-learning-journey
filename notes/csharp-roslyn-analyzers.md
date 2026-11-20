# C# Roslyn Analyzers

A Roslyn analyzer is code that inspects your code at compile time and reports diagnostics — warnings and errors with squiggles in the IDE. The built-in analyzers catch common mistakes; writing your own lets you enforce your team's rules automatically.

## How an Analyzer Works

An analyzer derives from `DiagnosticAnalyzer`, declares the diagnostics it can report, and registers actions that Roslyn calls as it walks the syntax tree or semantic model. You inspect nodes and report a diagnostic when a rule is broken.

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoDateTimeNowAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "DEMO001",
        title: "Do not use DateTime.Now",
        messageFormat: "Use DateTime.UtcNow or TimeProvider instead of '{0}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess,
            SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberAccessExpressionSyntax)context.Node;
        if (member.Expression is IdentifierNameSyntax id
            && id.Identifier.Text == "DateTime"
            && member.Name.Identifier.Text is "Now" or "Today")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, member.GetLocation(), member.Name.Identifier.Text));
        }
    }
}
```

`ConfigureGeneratedCodeAnalysis(None)` skips generated files; `EnableConcurrentExecution` lets Roslyn run the analyzer on multiple threads, so your actions must be thread-safe.

## Syntax vs Semantic Analysis

Syntax actions see the shape of the code (tokens and node kinds) but know nothing about types. For type-aware rules — "don't call this method on that type" — register a symbol action or an operation action instead, and use the semantic model.

```csharp
// Operation actions see typed operations (invocations, assignments, ...)
context.RegisterOperationAction(ctx =>
{
    var invocation = (IInvocationOperation)ctx.Operation;
    if (invocation.TargetMethod.ContainingType?.Name == "DateTime"
        && invocation.TargetMethod.Name is "Now" or "Today")
    {
        ctx.ReportDiagnostic(Diagnostic.Create(Rule,
            invocation.Syntax.GetLocation()));
    }
}, OperationKind.Invocation);
```

Operation actions are usually the right level: they understand method calls, conversions, and control flow with full type information, without you re-implementing overload resolution from syntax.

## Shipping as a NuGet Analyzer Package

Analyzers ship as NuGet packages with the analyzer DLL under `analyzers/dotnet/cs/`. A `.nuspec` (or SDK-style project with the right items) places the assembly there, and consuming projects get the diagnostics automatically — no code reference is added.

```xml
<!-- In the analyzer .csproj: pack the DLL as an analyzer, not a library -->
<ItemGroup>
  <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true"
        PackagePath="analyzers/dotnet/cs" Visible="false" />
</ItemGroup>
```

Keep the analyzer assembly dependency-free (or IL-merge what you need): it loads into the compiler and the IDE, so heavy dependencies slow every build. Version the diagnostic IDs as a public contract — never reuse an ID for a different rule, and document breaking changes.

## Testing with Verify

The `Microsoft.CodeAnalysis.Testing` packages (or the `Verify` snapshot library) let you assert diagnostics on sample code. A typical test feeds source text in, runs the analyzer, and compares the reported diagnostics (and code fixes) against a verified snapshot file.

```csharp
[Fact]
public async Task DateTimeNow_ReportsWarning()
{
    var test = new CSharpAnalyzerTest<NoDateTimeNowAnalyzer, DefaultVerifier>
    {
        TestCode = """
            using System;
            class C { void M() { var x = DateTime.Now; } }
            """
    };
    test.ExpectedDiagnostics.Add(
        new DiagnosticResult("DEMO001", DiagnosticSeverity.Warning)
            .WithSpan(3, 34, 3, 46));
    await test.RunAsync();
}
```

Snapshot testing catches regressions in locations and messages, which hand-written assertions miss. Cover the true positives, the false positives (e.g. `DateTime.UtcNow` must not warn), and generated-code exclusion.

## Key Takeaways

- An analyzer is a `DiagnosticAnalyzer` that registers actions and reports `Diagnostic`s with a stable ID.
- Use syntax actions for shape-based rules and operation actions when you need type information.
- Call `ConfigureGeneratedCodeAnalysis(None)` and `EnableConcurrentExecution`; keep actions thread-safe.
- Ship analyzers in a NuGet package under `analyzers/dotnet/cs` so consumers get them automatically.
- Keep the analyzer assembly dependency-free — it loads into every build and every IDE session.
- Test with the `Microsoft.CodeAnalysis.Testing` harness; cover true positives and false positives.
