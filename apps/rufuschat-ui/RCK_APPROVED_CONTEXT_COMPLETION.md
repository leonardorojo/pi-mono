# RCK approved context completion

- Approved RCK context is included in the next chat completion after an explicit Confirm injection.
- The behavior is one-shot: the confirmed context is consumed for the next request and must not be reused automatically.
- The loaded ContextPack preview must expose `exactTextInjected` before confirmation.
- This phase does not persist approved context in `.data`.
- This phase does not modify the RCK DAG.
- This phase does not register Anchor yet.
- The approved context lives in memory only and does not survive a hard refresh.
- The reverse write-back direction is documented in [`RCK_WRITEBACK_DESIGN.md`](./RCK_WRITEBACK_DESIGN.md).

Expected completion metadata:
- `rckContextIncluded`
- `rckInjectionId`
- `sourceTraceSliceHashesCount`
