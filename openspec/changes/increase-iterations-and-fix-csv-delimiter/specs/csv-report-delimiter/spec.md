## ADDED Requirements

### Requirement: Summary CSV uses a fixed comma delimiter

The summary report `summary.csv` SHALL always use a comma (`,`) as the field delimiter, regardless of the host machine's locale or the process culture.

#### Scenario: report written on a machine with semicolon locale

- **WHEN** the summary report is generated on a machine whose culture uses `;` as the list separator
- **THEN** the `summary.csv` file still uses `,` as the field delimiter

#### Scenario: report written on a machine with comma locale

- **WHEN** the summary report is generated on a machine whose culture uses `,` as the list separator
- **THEN** the `summary.csv` file uses `,` as the field delimiter