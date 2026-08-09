# EffortHours Governance

## Project direction

EffortHours is an MIT-licensed reference implementation for evidence-backed
Equivalent Human Effort estimation. Its core semantic boundary is documented in
`docs/PRODUCT.md` and `docs/ESTIMATION_MODEL.md`: EHE is counterfactual
replacement effort, not reconstructed time worked.

The current lead maintainer is the repository owner, Victor Zakharov
([`@VictorZakharov`](https://github.com/VictorZakharov)). The lead maintainer has
final responsibility for releases, security response, repository administration,
and decisions that cannot reach consensus.

## Contributions and decisions

Small fixes may proceed through an ordinary pull request. Material changes to
estimation semantics, public contracts, calibration admission, licensing, privacy,
or attribution safeguards should first be discussed in an issue and update the
relevant decision document explicitly.

Decisions favor:

1. user safety and honest claims;
2. reproducible evidence and calculation lineage;
3. deterministic offline behavior;
4. independently reviewable calibration;
5. compatibility for published contracts; and
6. implementation simplicity and maintainability.

Pull requests require the checks described in `CONTRIBUTING.md`. A maintainer may
decline work that is correct but outside the current product boundary. Declining a
change does not prevent anyone from using the MIT-licensed code in a fork or a
different implementation.

## Calibration independence

Teacher labels, independent reviews, validation results, and held-out test results
must retain their recorded identities and maturity. The same reviewer must not be
silently presented as independent, and release pressure must not weaken the frozen
admission boundary.

## Releases

The lead maintainer approves version numbers and public releases. Preview releases
may intentionally ship experimental, uncalibrated estimators when those limits are
prominent. Production-readiness or billing-accuracy claims require the independent
evidence and gates documented in the model-review policies.

The mechanical and authorization steps are in `docs/RELEASING.md`. Repository
visibility, GitHub releases, and NuGet publication are distinct actions.

## Succession

As the contributor base grows, maintainers may be added based on sustained,
trustworthy contributions and sound judgment around privacy and estimation claims.
If the lead maintainer steps away, they should transfer repository and NuGet
ownership to an active maintainer and record the change here.
