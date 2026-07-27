# Wolverine static code generation

Gateway and Admin run Wolverine in strict `TypeLoadMode.Static`. Their generated
handler adapters under `Services/ConduitLLM.Gateway/Internal/Generated` and
`Services/ConduitLLM.Admin/Internal/Generated` are authoritative source files
and must be committed. The services do not fall back to runtime compilation.

Regenerate the adapters after changing a message handler, handler signature,
middleware, bridge registration, or Wolverine policy:

```powershell
./scripts/generate-wolverine-code.ps1
```

The script generates both hosts in the Production environment with the
PostgreSQL topology. It expects PostgreSQL and Redis on the standard local
development ports; `DATABASE_URL` and `REDIS_URL` can override those defaults.
The generation-only `WolverineFx.RuntimeCompilation` dependency is restored into
an isolated artifacts tree below the system temporary directory, so normal
restores and production publishes remain Roslyn-free.

To reproduce the CI drift check locally:

```powershell
./scripts/generate-wolverine-code.ps1 -Verify
```

Review generated changes before committing them. Do not hand-edit files below
either `Internal/Generated/WolverineHandlers` directory.
