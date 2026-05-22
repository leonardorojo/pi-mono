# Rufus.Cli.ParserChecks

Minimal console-based regression checks for `PiJsonEventRunner`.

It uses a temporary fake `pi` executable on `PATH` to validate:

- structured final answer extraction
- fallback to accumulated `text_delta` when no structured final answer is present
- invalid JSONL error handling
- stderr separation from stdout parsing
