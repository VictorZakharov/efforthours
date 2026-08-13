# Pricing and rate cards

## Separation from effort

EffortHours estimates EHE before applying a rate:

```text
Equivalent Replacement Cost = Equivalent Human Effort x hourly market rate
```

Pricing is a replaceable projection. It must never affect observed evidence,
capabilities, work-item hours, uncertainty, or model admission. Equivalent
Replacement Cost is not historical pay, an invoice, or a billing determination.

Callers can use the bundled rate, disable pricing, or provide an exact override:

```text
eh estimate . --no-rate
eh estimate . --hourly-rate 175 --currency USD
eh rate info
eh rate show
```

No currency conversion occurs. `--currency` requires `--hourly-rate`, and rate
options cannot be combined with `--no-rate`.

## Bundled 2026 US rate

The current bundled card is `us-senior-software-contractor/2026.1`:

| Point | USD/hour |
| --- | ---: |
| Low market reference | 125 |
| Default expected rate | 160 |
| High market reference | 200 |

It models a nationwide US independent senior software contractor. The three
market points describe rate-card provenance; they are not cross-multiplied with
the estimate's low/expected/high uncertainty. `TotalCost` multiplies each EHE
point by the one selected hourly rate.

## Public-data derivation

The card starts with the May 2025 US Bureau of Labor Statistics Occupational
Employment and Wage Statistics release for Software Developers, SOC 15-1252:

| OEWS point | Series | Wage |
| --- | --- | ---: |
| Median | `OEUN000000000000015125208` | $65.38/hour |
| 75th percentile | `OEUN000000000000015125209` | $82.68/hour |
| 90th percentile | `OEUN000000000000015125210` | $103.21/hour |

OEWS excludes self-employed workers, so these observations are employee wage
anchors rather than measured contractor bill rates.

The March 2026 BLS Employer Costs for Employee Compensation table reports $72.99
total compensation and $50.53 wages and salaries per hour for private-industry
professional and related occupations. Their ratio is `1.44448842`.

EffortHours applies an explicit 75% billable-utilization assumption to represent
ordinary independent-contractor nonbillable time such as administration, business
development, leave, and bench time. That utilization factor is an EffortHours
policy assumption, not a BLS measurement.

```text
raw bill rate = OEWS wage x (72.99 / 50.53) / 0.75
published rate = raw bill rate rounded to the nearest $5/hour
```

The unrounded results are $125.9209, $159.2404, and $198.7809. The 75th-percentile
point becomes the $160 default because the modeled worker is a competent senior
contractor rather than the median software developer.

Primary sources:

- [BLS OEWS tables](https://www.bls.gov/oes/tables.htm)
- [BLS OEWS time-series data](https://download.bls.gov/pub/time.series/oe/oe.txt)
- [BLS ECEC professional occupations table](https://www.bls.gov/news.release/ecec.t04.htm)
- [BLS copyright and link policy](https://www.bls.gov/bls/linksite.htm)

BLS-published material is public domain apart from previously copyrighted
photographs and illustrations. EffortHours redistributes only the cited numeric
observations, series IDs, formula, and provenance.

## Versioning and storage

The complete card is checked in at
`rates/us-senior-contractor/2026.1.json`, schema-validated, and embedded for
deterministic offline use. It records:

- schema and semantic version;
- effective date and currency;
- market scope and worker description;
- source release dates and series IDs;
- formula inputs and utilization assumption;
- published low, expected, and high market references;
- provenance and public-domain notes; and
- a content digest.

The public v1 `RateCard` remains the report-facing contract. The richer bundled
artifact maps into it. Caller overrides create an explicit caller-supplied rate
identity and do not rewrite the bundled artifact.

Future regional or updated cards require new dated identities and reproducible
provenance. Geography and rate-card choice must remain independent from the
underlying effort estimate.
