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

The transition build targets .NET 8 and requires PowerShell 7.4. Before the first PSDataverse 2 stable release, move the production target to .NET 10 and the minimum host to PowerShell 7.6 LTS unless real user compatibility evidence justifies one final 7.4 build. PowerShell 5.1 is not supported by the current binary architecture.

## Milestone 1: executable specification

- [x] Add golden MIME tests for successful and failed multi-operation batch responses, malformed boundaries, duplicate headers, CRLF/LF variants, 429 responses, and unexpected proxy bodies.
- [x] Add a reusable fake HTTP transport and use it for authorization headers, retry timing, cross-environment isolation, pagination, and cancellation tests. The same harness is ready for the Milestone 2 token-refresh and Milestone 3 DOP-discovery scenarios when those seams exist.
- [x] Add Pester tests for packaged-module imports, parameter sets, pipeline input, output modes, command exports, and error categories.
- [x] Add Windows/Linux/macOS CI for the current and minimum supported PowerShell hosts, dependency audit, static analysis, and deterministic package creation.

Milestone 1 is complete when the C# and Pester suites pass, two clean builds produce identical file hashes, the package imports in PowerShell 7.4 and the current host, static analysis reports no errors, and the dependency audit reports no known vulnerabilities.

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
- Benchmark parallel individual requests, small `$batch` payloads, and Dataverse bulk APIs before selecting defaults.

The read-only parallel-request benchmark harness is available in `tools/Measure-DataverseRequestPerformance.ps1`. Selecting batch and bulk defaults remains open until the same environment/data shape can be measured without risking production data.

## Compatibility policy

- Preserve concepts and common scripts where doing so does not retain a defect or insecure behavior.
- Prefer aliases and deprecation warnings over duplicate implementations.
- Do not silently reinterpret ambiguous old arguments; fail with a migration-oriented message.
- Maintain a single migration guide from the latest 0.x release and update it in every milestone.
- Keep the transparent low-level Web API escape hatch even as higher-level commands are added.
