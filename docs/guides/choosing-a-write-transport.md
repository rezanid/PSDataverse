# Choosing a Dataverse write transport

PSDataverse keeps individual requests, Web API `$batch`, and Dataverse's
multiple-operation messages explicit because they do not have interchangeable
transaction, ordering, response, or failure behavior. Choose for correctness first,
then tune chunk size and concurrency against the target environment.

## Quick decision guide

| Requirement | Recommended starting point |
|---|---|
| Homogeneous creates, updates, or upserts on one supported table | `CreateMultiple`, `UpdateMultiple`, or `UpsertMultiple` |
| Mixed tables, HTTP verbs, or request shapes | `$batch` |
| A group of writes must commit or roll back together | One `$batch` change set |
| Every row must succeed or fail independently | Individual requests, or separate batch change sets designed around that boundary |
| Downstream processing must follow input order | `-OutputOrder Input` |
| Maximum streaming throughput matters | Keep the default completion order |
| Delete from a standard table | Start with individual requests at DOP 20 |
| Delete from an elastic table | Consider `DeleteMultiple`; PSDataverse does not yet wrap it |
| The table does not support a multiple-operation message | Use `$batch` or individual requests |

## The three transports

### Individual requests

Individual mode sends one HTTP request per row. It supports any table and ordinary
Web API operation, gives each row its own response or error, and is the simplest
choice when rows must be retried or corrected independently.

The cost is one request envelope per row. Bounded concurrency hides much of that
latency, but every request still counts separately toward Dataverse service-protection
limits. PSDataverse uses a client-side ceiling of 20 when `-MaxDop` is zero or
omitted, and lowers active concurrency when Dataverse returns a smaller DOP hint.

### `$batch`

Batch mode groups operations into multipart HTTP envelopes. PSDataverse places each
group in a transactional change set, so operations in that change set commit or roll
back together. Responses retain operation content IDs, which makes a failed operation
identifiable. Separate envelopes can run concurrently but have no global ordering or
transaction boundary.

Use `$batch` when operations differ by table or verb, when relationships require an
ordered atomic group, or when a table does not support a multiple-operation message.
Keep dependent operations in the same deliberately ordered change set; parallelize
only independent envelopes.

### Multiple-operation messages

`CreateMultiple`, `UpdateMultiple`, and `UpsertMultiple` submit homogeneous rows for
one table. PSDataverse checks support once per table and connection, caches the
result, splits rows with `-ChunkSize`, and submits independent chunks concurrently up
to `-MaxDop`.

For standard tables, an error rolls back the entire chunk. For elastic tables,
partial success is possible and Dataverse can return per-record error details. All
elastic tables support the multiple-operation messages, while standard-table support
must be detected. `UpsertMultiple` is available when both `CreateMultiple` and
`UpdateMultiple` are available. See Microsoft's [bulk-operation guidance](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/bulk-operations).

Use these messages when rows target the same table and verb, the chunk's failure
boundary is acceptable, and high throughput is more important than receiving an
independent HTTP response for every row.

## Measured starting points

The September 2026 test-environment benchmark used 100 small rows, randomized
scenario order, one warm-up, and three measured samples. These were the strongest
median results:

| Workload | Transport | Envelope concurrency | Median operations/second |
|---|---|---:|---:|
| Create | Five 20-operation `$batch` envelopes | 5 | 165.52 |
| Create | Two 50-row `CreateMultiple` envelopes | 2 | 139.40 |
| Create | Individual requests | DOP 20 | 57.76 |
| Update | Two 50-row `UpdateMultiple` envelopes | 2 | 131.85 |
| Update | Five 20-operation `$batch` envelopes | 5 | 96.03 |
| Update | Individual requests | DOP 20 | 51.12 |
| Delete | Five 20-operation `$batch` envelopes | 5 | 34.76 |
| Delete | Individual requests | DOP 20 | 33.35 |

The full measurements and variability are in the
[benchmark record](../benchmarks/2026-09-org8848d2a1.md). They demonstrate that
concurrent envelopes matter and that GET behavior cannot predict write behavior.
They are not universal defaults: plug-ins, payload size, alternate keys,
relationships, network latency, table type, and service-protection pressure can
change the result.

## Starting configuration

- For individual writes, begin with `-MaxDop 20` or leave it at zero to use the
  module default and server hint.
- For `$batch`, begin with 20 independent operations per envelope and approximately
  four to five concurrent envelopes when the operations are safe to parallelize.
- For standard-table multiple operations, begin around 100 rows per chunk and tune
  upward when rows are small and synchronous plug-in work is limited. Microsoft
  suggests 100–1,000 as an experimental range, subject to request-size and execution
  time limits.
- For elastic tables, Microsoft recommends approximately 100 operations per request
  and parallel requests rather than very large chunks.
- Keep `-OutputOrder Completion` for streaming throughput. Use `Input` only when the
  consumer requires stable ordering because it buffers out-of-order completions.

## Failure and retry design

PSDataverse automatically retries replay-safe reads when appropriate. It does not
blindly replay POST, PATCH, DELETE, or write batches: a lost response does not prove
that Dataverse failed to apply the write. Design an application-specific recovery
strategy using primary IDs, alternate keys, ETags, or a verification read.

Multiple-operation failures use `DVERR-1020`. Their error target identifies the
failed chunk, original one-based input row range, retained input objects, successful
sibling chunks, and failed content IDs without printing row contents. A definitive
unsupported-table preflight uses `DVERR-1021` before any rows are sent.

## Benchmark your own workload

Use the guarded harness only in a disposable test environment:

```powershell
$results = ./tools/Measure-DataverseWritePerformance.ps1 `
    -Count 100 `
    -MaxDop 1,8,20,32 `
    -BatchSize 20 -BatchMaxDop 1,4,8 `
    -BulkSize 50 -BulkMaxDop 1,4 `
    -RepeatCount 3 -SummaryOnly `
    -Confirm:$false

$results | Format-Table -AutoSize
```

The harness creates a uniquely named table, validates row counts between phases,
and removes the table in a `finally` block. Review the target environment and script
before suppressing confirmation.
