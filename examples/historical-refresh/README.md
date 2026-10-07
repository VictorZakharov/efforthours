# Synthetic offline historical refresh

All JSON here was generated from the repository's in-memory synthetic comparison
fixture (not a real person's history). Files and example C# code are MIT licensed
under the root LICENSE. No credentials, private repository evidence or live writer
are included. `comparison.json` is a complete five-day single-repository report;
its operational runtime/OS/processor fields are fixed synthetic values, not host
measurements. Its optional capacity is deliberately 12 hours while entry multipliers still use 8.
Two implementation entries share a day with different logged durations, another
uses a blank retained date, and meeting/PTO records are excluded from allocation.

Installed users can obtain this complete example offline with `eh docs export
<new-directory>`. Then run from `examples/historical-refresh` inside that export,
using the same installed CLI version and new output paths. These commands do not
contact providers or change entries:

```text
eh change review-days comparison.json --work-records work-records.json --workdays declared-days.json --workday-policy equal-declared-days/1.0.0 --entry-policy equal-declared-day-entries/1.0.0 --output new-review.json
eh change plan-refresh comparison.json --work-records work-records.json --entries entries.json --workdays declared-days.json --workday-policy equal-declared-days/1.0.0 --entry-policy equal-declared-day-entries/1.0.0 --fields both --output new-plan.json
eh change check-refresh new-plan.json --entries entries.json --output new-check.json
```

The saved `review.json`, `plan.json` and `check-ready.json` are exact expected
semantic outputs. Here `entries.json` represents unchanged freshly observed
snapshots; a real consumer must re-export them. The entry snapshot permissions are
synthetic facts only. `check-blocked.json` is a valid blocked receipt with entry-a
invoiced; it must never authorize a partial application. To reproduce it, use its
`current` object as a fresh manifest: `check-refresh` emits the receipt and exits 3.
It remains blocked even though a receipt file exists.

`OfflineRefreshAdapter.cs` is a compiled, tested example consumer. `Prepare`
validates EH semantics and a separately confirmed plan digest, rejects blocked
receipts, and returns note mutations and analytics proposals separately.
`MemoryNoteStore` demonstrates complete-snapshot conditional note updates under
an in-memory lock, independent live note permissions/restrictions, reread, retry
idempotence and target digest checking. It cannot access timeinv, does not write
files, does not map numeric fields and does not implement a batch transaction.
The E2E project links this file and executes the example against these fixtures.

Run `eh docs show historical-refresh-integration` or read the exported
[integration contract](../../docs/HISTORICAL_REFRESH_INTEGRATION.md)
for native process exits, required receipt fields, atomic API requirements and
recovery boundaries. Schema validation alone, an exit zero or a ready proposal
alone never substitutes for confirmation and a conditional external update.
