---
name: efforthours
description: Use the installed EffortHours (`eh`) CLI for EHE estimates, today-to-date engineering work, period trends, portfolios, and normalized X factors. Use whenever the user explicitly asks for `eh`, EffortHours, or EHE; do not use it for generic time tracking.
metadata:
  efforthours-integration-contract: efforthours-codex/1.0.0
---

# EffortHours

Use the highest-level native EH command that matches the request. Treat EH output as the calculation of record; do not recreate its selection or arithmetic externally.

For a today-to-date GitHub estimate:

- Run one `eh change today` command from the user's home folder with the requested owner, identity, timezone, open-PR policy, scope, capacity, format, and output.
- GitHub-assisted modes invoke authenticated `gh`, access its user configuration, use the network, and write an EH-managed cache. In a sandboxed environment, request sufficient permission for the EH command on the first invocation; when supported, use the narrow reusable prefix `eh change today`.
- Do not enumerate a workspace, scan repository folders, construct manifests, call `gh` separately, clone repositories, write helper scripts, inspect EH source, or manually aggregate repositories for native today mode.
- Allow the native command to finish while surfacing its progress. Do not treat a quiet network or acquisition phase as a hang before EH's declared timeout or failure signal.
- On success, read the EH-produced report and summarize EHE, normalized X, coverage, and EH's internal timing. Keep EH end-to-end time distinct from total conversation latency.
- Preserve every requested `--author` Git alias. For a single contributor with open PRs, pass `--provider-login <login|@me>` only when the caller supplies that account. If EH returns `github-contributor-identity-unresolved`, ask for the provider account; do not guess it, drop email aliases, or disable open PRs to obtain a complete report.
- On failure, use EH's structured failure code and suggested action. Retry the exact command at most once when EH identifies missing sandbox permission and the user or environment permits it. Otherwise report the incomplete result; do not silently replace it with a manual calculation.

EHE is replacement effort, not actual labor. Capacity is only the requested denominator; do not infer actual hours worked.

For a teammate's offline daily graph:

- Run one `eh calendar` with explicit flags for unattended use; do not use interactive mode from an agent. Bare `eh calendar` is the human terminal setup flow.
- Supply the requested date range, project/workspace scope, identity, format, and external output/checkpoint paths. Unspecified dates default to the last complete local month; reference hours default to eight per calendar day.
- Use repeated `--project id=path` for exact local inputs, `--all-repos --workspace <folder>` for scoped native discovery, or `--repo id=owner/name --author <identity>` for checkout-free inputs. Only explicit `--fetch-missing` authorizes provider acquisition.
- Let EH build/reconcile the portfolio and render its HTML/text/JSON. Surface native progress and preserve incomplete status; do not author a website, construct manifests, sum daily estimates, infer actual hours, or label the X reference ratio as an AI skill score.
