# PSDataverse 2 plan

PSDataverse 2 is a deliberate modernization rather than a promise of strict behavioral compatibility with every 0.x detail. The low-level Web API model remains recognizable, but correctness, secure defaults, testability, and a PowerShell-native experience take precedence. Existing users should receive documented replacements, compatibility aliases where they are inexpensive and unambiguous, and actionable errors for removed behavior.

## Milestone 0: safety and truthfulness

Goal: establish a dependable baseline before introducing connection objects, new authentication flows, or a new request scheduler.

### Scope and current status

- [x] Remove the unexported Scriban-based `ConvertTo-CustomText` feature and its Humanizer/Scriban dependencies.
- [x] Stop writing bearer access tokens to debug output.
- [x] Recreate the service provider and HTTP client when connecting to a different environment.
- [x] Dispose connection resources on reconnect and disconnect.
- [x] Refresh authentication only near token expiry rather than for every pipeline item.
- [x] Make the default batch parallelism consistently use an effective DOP of 20.
- [x] Carry PowerShell cancellation through single requests, pagination, batch sends, retry waits, and semaphore waits.
- [x] Make retry delay calculation safe for exception-based failures and both forms of `Retry-After`.
- [x] Make batch errors deterministic for empty, non-JSON, malformed, and throttled responses.
- [x] Preserve response bodies and content headers for all successful HTTP status codes, not only 200.
- [x] Fix `ChangeSet.RemoveOperation`, table row count, and multiple option-set export defects.
- [x] Remove unused `-Retry` parameters and the unexported experimental `Connect-DataverseNew` implementation.
- [x] Correct the manifest's supported PowerShell version and exported command surface.
- [x] Add regression coverage for the highest-risk corrected paths.
- [x] Add packaged-module smoke tests for the current and minimum supported PowerShell hosts.
- [x] Add dependency vulnerability auditing to CI and align the PowerShell/xUnit dependency graph to remove current high-severity transitive advisories.

### Completion criteria

Milestone 0 is complete when:

1. The C# tests pass, including one test for each confirmed high-risk defect.
2. The module built by the repository build script imports in the minimum supported PowerShell version and exports exactly the manifest's command list.
3. A dependency vulnerability audit has no known high or critical findings, or an explicit reviewed exception exists.
4. The migration guide accurately describes every breaking change included in the milestone.

### Support contract for this milestone

The early transition build targeted .NET 8 and required PowerShell 7.4. Milestone 4 moves the production target to .NET 10 and the minimum host to PowerShell 7.6 LTS. PowerShell 5.1 and PowerShell 7.4 are not supported by the PSDataverse 2 binary architecture.

## Milestone 1: executable specification

- [x] Add golden MIME tests for successful and failed multi-operation batch responses, malformed boundaries, duplicate headers, CRLF/LF variants, 429 responses, and unexpected proxy bodies.
- [x] Add a reusable fake HTTP transport and use it for authorization headers, retry timing, cross-environment isolation, pagination, and cancellation tests. The same harness is ready for the Milestone 2 token-refresh and Milestone 3 DOP-discovery scenarios when those seams exist.
- [x] Add Pester tests for packaged-module imports, parameter sets, pipeline input, output modes, command exports, and error categories.
- [x] Add Windows/Linux/macOS CI for the current and minimum supported PowerShell hosts, dependency audit, static analysis, and deterministic package creation.

Milestone 1 was completed against the transitional PowerShell 7.4 host. Milestone 4 supersedes that runtime baseline with PowerShell 7.6 LTS and .NET 10.

## Milestone 2: connections and authentication

- [x] Introduce an explicit `DataverseConnection` object and a session-scoped registry with default and named connections.
- [x] Add PowerShell-native parameter sets for interactive, Integrated Windows Authentication, device code, client secret, certificate, access token, and token-provider authentication.
- [x] Prefer WAM on supported Windows systems and the system browser elsewhere. Passkeys are provided by the interactive identity experience, not modeled as a separate OAuth grant.
- [x] Keep connection-string input as a migration path and adopt familiar XRM tooling aliases.
- [x] Add optional secret resolvers without coupling the core to one secret store.

Milestone 2 is complete when named connections can be created, enumerated, selected by name or object, switched as the default, refreshed without duplicate concurrent token acquisition, and deterministically disposed; the packaged command metadata exposes every authentication flow; secrets and access tokens are absent from public connection properties; and the migration guide contains runnable examples.

## Milestone 3: request engine and convenient commands

- [x] Replace the polling task list with a bounded `System.Threading.Channels` scheduler with backpressure.
- [x] Honor Dataverse DOP hints, `Retry-After`, cancellation, completion/input ordering modes, and idempotency-aware replay rules.
- [x] Introduce `Invoke-DataverseRequest` and keep `Send-DataverseOperation` as a compatibility alias for at least one major release.
- [x] Add connection inspection, WhoAmI/test, CRUD, metadata, action/function, and bulk import/export commands.
- [x] Generate and publish command-reference help for the expanded command surface.
- [x] Add a guarded disposable-table harness that independently measures POST, PATCH, and DELETE with parallel individual requests, `$batch`, and the bulk APIs supported by custom standard tables.
- [x] Run the guarded write harness as a 30-row smoke benchmark in the test environment and record the verb-specific results.
- [x] Extend the harness with concurrent batch and bulk envelopes, warm-up operations, repeated samples, randomized scenario order, envelope counts, and median summaries.
- [x] Run the warmed, repeated 100-row benchmark with concurrent `$batch` and bulk envelopes.
- [x] Keep individual, `$batch`, and multiple-operation APIs explicit on imports; add chunking and concurrent-envelope controls to CreateMultiple, UpdateMultiple, and UpsertMultiple commands.
- Document measured starting points rather than silently selecting one transport because transactional, failure, ordering, and response semantics differ.

The read-only and disposable-table write results are recorded in `docs/benchmarks/2026-09-org8848d2a1.md`. The guarded write harness is `tools/Measure-DataverseWritePerformance.ps1`. It creates and removes a uniquely named custom table, verifies counts between phases, and deliberately treats GET, POST, PATCH, and DELETE as different workloads. Its second revision can run multiple `$batch` and bulk envelopes concurrently and reduces warm-up and ordering bias. The repeated run supports retaining 20 as the general individual-request ceiling. It also demonstrates that concurrent envelopes can substantially outperform individual requests, but transport selection remains explicit because it changes semantics.

## Milestone 4: runtime modernization and release hardening

- [x] Retain Integrated Windows Authentication for federated and compatible Active Directory identities; keep WAM interactive authentication as the recommended Windows flow for managed Entra identities.
- [x] Target .NET 10 and require PowerShell 7.6 LTS across the project, manifest, documentation, and CI.
- [x] Review and resolve compiler warnings and obsolete APIs, documenting intentional compatibility exceptions such as the retained MSAL IWA call.
- [x] Add guarded live integration coverage for CRUD, pagination, `$batch`, CreateMultiple, UpdateMultiple, UpsertMultiple, throttling behavior, and cleanup.
- [x] Improve multiple-operation failures so errors identify the failed chunk and its input rows.
- [ ] Detect and report whether a table supports each multiple-operation message before sending a large workload.
- [ ] Complete command help, measured transport recommendations, and the 0.x-to-2.x migration guide.
- [ ] Produce and verify deterministic prerelease packages, including installation from a local PowerShell repository.
- [ ] Run the release-candidate package on Windows, Linux, and macOS with PowerShell 7.6.

Milestone 4 is complete when the .NET 10 package passes unit, packaged-module,
integration, vulnerability, deterministic-build, and cross-platform PowerShell 7.6
checks; intentional compatibility warnings are documented; migration guidance is
complete; and a release-candidate package can be installed without using the source
tree.

The warning review completed with a clean .NET 10 build. Legacy formatter-based
exception serialization was removed, source-generated native interop and compiled
logging messages replaced analyzer-warning implementations, and smaller API and
allocation warnings were resolved. The sole obsolete behavior retained by design is
MSAL's Integrated Windows Authentication builder; its compiler suppression is scoped
to that call, while WAM remains the recommended Windows flow.

The guarded live suite in `tests/Invoke-PSDataverseLiveIntegration.ps1` passed against
the disposable custom table it created in the org8848d2a1 test environment. It covers
convenience CRUD, concurrent `$batch` envelopes, CreateMultiple, UpdateMultiple,
UpsertMultiple, and forced multi-page reads, and removes the generated table in a
`finally` block. Service-protection headers are captured without intentionally
overloading the tenant; HTTP 429 `Retry-After` handling is covered deterministically
by the transport test suite.

CreateMultiple, UpdateMultiple, and UpsertMultiple failures now use `DVERR-1020` and
carry a client-only `MultipleOperationFailureContext`. The context identifies the
failed chunk, its one-based source row range, retained input objects, failed content
IDs, and successful sibling chunks without exposing row contents in the displayed
error message. Unit, packaged-module, and guarded live tests verify the mapping; the
live negative test deliberately fails the second CreateMultiple chunk and confirms
that callers receive exactly one structured error while the first chunk succeeds.

## Compatibility policy

- Preserve concepts and common scripts where doing so does not retain a defect or insecure behavior.
- Prefer aliases and deprecation warnings over duplicate implementations.
- Do not silently reinterpret ambiguous old arguments; fail with a migration-oriented message.
- Maintain a single migration guide from the latest 0.x release and update it in every milestone.
- Keep the transparent low-level Web API escape hatch even as higher-level commands are added.
