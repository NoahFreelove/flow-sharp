# Language-only host

This small CLI and embedding example references only `flow-language`. Its output
contains the host, the language assembly, `core.flow` and `collections.flow`; it
installs no music bindings or audio services.

```bash
dotnet run --project scripts/LanguageHost -- examples/language/collections.flow
dotnet run --project scripts/LanguageHost -- -e '(print (add 20 22))'
dotnet run --project scripts/LanguageHost -- --repl
dotnet run --project scripts/LanguageHost -- --json /tmp/closure.json examples/language/dict-aggregation.flow
dotnet publish scripts/LanguageHost -c Release -o /tmp/flow-language-host
```

Scripts and piped stdin require explicit imports. `-e` and the line-oriented REPL
import `@core`; REPL definitions persist until `:quit` or EOF. The host is a minimal
architecture/embedding example, not the full music CLI or a multiline editor.
`LanguageSession` demonstrates injected output/diagnostic sinks and explicit
lexer/parser/interpreter composition. Errors produce a nonzero exit status.

`--json` records the language assembly references, loaded process assemblies,
and native shared objects (Linux `/proc/self/maps`; empty on other platforms).
`LanguageHostTests` runs all seven tutorial programs in fresh processes and checks
their exact output and closure. It also exercises embedding, REPL state and the
absence of an implicit script prelude or music modules.
