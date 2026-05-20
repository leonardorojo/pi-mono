# Rufus CLI Direction

Rufus CLI (`rfs`) is an operational governance layer, not a blank-slate agent.

## Direction

- Rufus CLI should stay conceptually independent from Pi.
- Rufus CLI may wrap or invoke Pi, Codex, or other engines when that is useful.
- The initial goal is design-first: clear contracts, explicit boundaries, and no premature coupling.
- Preferred core implementation: C# / .NET.
- If adapters or extensions are needed, Pi-facing integration can live in TypeScript.
- The v0 goal is to define the shape of the system before implementation expands.
- Integration with RCK Core should be progressive and explicit.

Rufus no ES Pi.
Rufus USA Pi cuando conviene.

## v0 principles

- design first
- contracts before code
- zero premature coupling
- replaceable backends
- operational clarity over agent complexity