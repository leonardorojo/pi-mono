# Rufus.Cli.ParserChecks

Parser and contract regression checks for the RFS CLI, TUI, agenting,
and RCK workspace layers.

Suites are partitioned by flag:

- `--core-only`       — deterministic parser, codec, and agent contract checks
- `--tui-only`        — deterministic TUI parser and renderer checks
- `--integration-only` — CLI integration paths with mock LLM fixtures
- `--long-paste-only` — long paste capture guard (≥1200 chars)
- `--legacy-only`     — full TUI session checks with mocked Pi (all modes)

Default (no flags) runs `--core-only` + `--tui-only`.

No suite requires real Pi. All Pi-backed paths use mock `pi` bash scripts
with canned JSONL fixtures.

Canonical reference: [`docs/RFS_TEST_BASELINE.md`](../docs/RFS_TEST_BASELINE.md)
