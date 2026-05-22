# RFS Pi programmatic integration audit

This document records programmatic integration choices when RFS calls Pi programmatically.

## P7 - Pi JSON Event Stream agent prototype

Goal: evaluate whether `pi --mode json` can be used to run a read-only agent safely without enabling editing or shell tools.

Findings:

- The prototype command `rfs agent-json` (implemented as `PiJsonEventRunner.RunAgentAsync`) launches `pi --mode json` with a restricted `--tools` argument set to: `read,grep,find,ls`.
- The implementation attempts to avoid write-capable tools. It explicitly does not include `edit`, `write`, `bash`, or any tool that would allow repository modification.
- This approach relies on Pi's CLI `--tools` flag to restrict available tools. If Pi's `--tools` is honored and enforces read-only semantics, this provides a reasonable safety boundary comparable to the legacy bridge's read-only custom tools (`list_directory`, `read_file`).
- If Pi's `--tools` flag is not a secure enforcement mechanism (for example, if `pi` still allows arbitrary tool invocation or file writes via other vectors), then this prototype is not safe for automatic migration.

Recommendation / next steps:

- Treat this as an experimental prototype. Do not migrate `rfs agent` or `rfs agent --record` to Pi JSON mode until an audit from Pi or tests prove `--tools` enforces a strict capability set.
- Add runtime checks (or an explicit user-visible warning) when `rfs agent-json` is used, clarifying this is an experimental mode and may not be strictly read-only depending on Pi's implementation.
- If Pi offers an API to further lock filesystem paths (e.g., a `--tool-root` or sandbox option), prefer that over `--tools`.

