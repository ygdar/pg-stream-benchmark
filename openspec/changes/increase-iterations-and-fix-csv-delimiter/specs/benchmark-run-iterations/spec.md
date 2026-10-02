## ADDED Requirements

### Requirement: Deviation in measurement iterations for dev/full modes

The benchmark runner SHALL run 12 measurement iterations per benchmark for the `dev` and `full` run modes.

#### Scenario: dev/full run uses 12 measurement iterations

- **WHEN** a benchmark is launched with mode `dev` or `full`
- **THEN** the BenchmarkDotNet job config uses an `IterationCount` of 12 for that benchmark

#### Scenario: validation mode keeps default iteration behaviour

- **WHEN** a benchmark is launched with mode `validation`
- **THEN** no explicit `IterationCount` is set, preserving the harness's `ShortRun` defaults