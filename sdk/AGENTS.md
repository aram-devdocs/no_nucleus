# sdk/ — distribution

`Nucleus.Sdk` is the metapackage a third-party mod references; `templates/Nucleus.ModTemplate` backs
`dotnet new nucleus-mod` (a thin `[BepInPlugin]` that registers an `IMod`). Keep the template minimal and
current with the `IMod`/`IModContext` contract in `Nucleus.Abstractions`; a scaffolded mod must build against
the `Nucleus.Sdk` NuGet packages with the consumer's own game DLLs in `lib/` (never shipped).
