# Seed-model review records

This file records reviewed repository-level anchors for Fairbill's transparent seed
model. A record is evidence for later calibration work; it is not itself a
calibration result, a benchmark of actual historical hours, or permission to tune
the model until one repository looks desirable.

Each record must identify the analyzed source revision, evidence digest, estimator
artifact, profiles, review method, and unresolved concerns. Repository-level
partition isolation is now defined by `MILESTONE_7.md` and enforced by the v1
calibration-corpus contract. Existing prose anchors do not become calibration
records automatically: they must be transcribed at work-item granularity, reviewed
under a versioned rubric, assigned to a repository-owned partition, and supplied
with the required source/license/distribution provenance. Revisions are retained
only for reproducibility and never become effort signals.

Because Fairbill's own anchor informed the seed-model and calibration design, it is
a development diagnostic rather than an eligible held-out test record unless a
future independent snapshot and review policy explicitly establishes otherwise.

## 2026-08-05: Fairbill at `f84a58a`

Status: **provisional logical-review anchor; not calibration data**

This is the first realism check for the Milestone 5 seed estimator. The deterministic
output was reviewed as a decomposition of the visible repository by an AI coding
agent using senior-engineer judgment. No Git history, time records, invoice data,
or independent maintainer estimate was used. No prior was changed to make this
result land on a preferred total.

### Provenance

| Field | Value |
| --- | --- |
| Source commit | `f84a58af9f7be61b9920e62c69658a0273e513b9` |
| Repository evidence digest | `sha256:f7481a93397ef7124582663d3d9485d74c9cdec2c5ab74826a522a40d1bae856` |
| Estimator | `seed-rules/0.2.0` |
| Model status | `experimental-uncalibrated` |
| Model digest | `sha256:ecc81cb04d5f3bcdc3dcd80a260ee3560f1f142185f1e12c1366211bdbf0011a` |
| Worker baseline | One competent senior contractor, technically familiar, business-domain unfamiliar, no AI |
| Technology baseline | Modern 2026 implementation |
| Rate card | None; pricing is deliberately omitted until the dated Milestone 6 rate card exists |
| Fairbill verification mode | Static assumed-working; target code was not executed by the analyzer |
| Separate development check | Release build passed; 46 memory-only unit tests and 7 end-to-end tests passed |

### Observed repository shape

| Measurement | Value |
| --- | ---: |
| Included files | 118 |
| Text lines | 19,555 |
| Maintained source files | 55 |
| Maintained source lines | 11,866 |
| Test files | 16 |
| Test lines | 2,693 |
| Generated files | 2 |
| C# types | 140 |
| C# methods | 477 |
| C# branch points | 947 |

### Profile totals

| Profile | Low EHE | Expected EHE | High EHE | Represented items |
| --- | ---: | ---: | ---: | ---: |
| Implementation | 229.00 h | 418.75 h | 737.00 h | 135 |
| Recreation | 233.50 h | 432.25 h | 766.25 h | 144 |

The recreation delta is 13.50 expected hours, all in explicit architecture and
recreation-design work. The source state, tests, documentation, and other represented
work remain identical between profiles.

### Expected category breakdown

| Category | Implementation | Recreation |
| --- | ---: | ---: |
| Specification comprehension and domain learning | 5.75 h | 5.75 h |
| Repository and solution setup | 16.25 h | 16.25 h |
| Architecture and technical design | 16.50 h | 30.00 h |
| Production implementation | 295.75 h | 295.75 h |
| Unit testing | 21.00 h | 21.00 h |
| End-to-end and UI testing | 25.00 h | 25.00 h |
| Manual validation, debugging, and hardening | 7.00 h | 7.00 h |
| Documentation | 13.75 h | 13.75 h |
| Build configuration and developer tooling | 6.75 h | 6.75 h |
| Self-review and system integration | 11.00 h | 11.00 h |

The separate professionalization ledger contains one basic CI-workflow gap at
1.50/3.75/8.00 hours. It is correctly excluded from both profile totals and from
future replacement-cost calculations.

### Review judgment

The expected implementation total is a plausible preliminary order of magnitude:
about 10.5 forty-hour contractor weeks for a multi-project CLI containing safe
repository traversal, two static ecosystem analyzers, versioned contracts and
schemas, reporting, packaging, a granular estimator, tests, benchmarks, and project
documentation. The 229-hour low case represents unusually smooth delivery with
strong ecosystem familiarity and reuse of conventional patterns. The 737-hour high
case reasonably covers ambiguity and the risk of getting parsers, filesystem safety,
and deterministic contracts right.

The decomposition is more useful than the total, but it is not yet persuasive
enough for billing. Production implementation contributes 70.6% of expected effort
and is driven heavily by the general source backbone. That concentration must be
tested against dissimilar repositories so source volume does not overwhelm feature
boundaries or reward verbosity. The testing split is directionally credible, though
coverage evidence and test depth need stronger fixtures. Documentation and manual
validation may be understated for a polished public release. The narrow recreation
premium also needs comparison with repositories whose architecture is difficult to
infer from the finished artifact.

This anchor should next receive an independent work-item review. It must remain out
of any held-out evaluation set if its corrections are used to change priors.
