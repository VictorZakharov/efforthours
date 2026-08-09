# Public pilot source manifest

This manifest records the external source snapshots used by
`efforthours-public-pilot/0.1.0`. Git metadata is retained only for provenance and
reproduction. Commit age, author, churn, and history are not effort signals.

The repository archives themselves are not copied into EffortHours. The committed
corpus contains derived labels, stable source-work-item/evidence identifiers, and
review reasoning without source excerpts.

| Repository | Shape | Partition | Revision | Commit date | Tree | License provenance | EffortHours source digest |
|---|---|---|---|---|---|---|---|
| [ardalis/GuardClauses](https://github.com/ardalis/GuardClauses) | .NET library and xUnit suite | development | [`41162c4`](https://github.com/ardalis/GuardClauses/commit/41162c46946214600a1f5a55b0abc94b0744691a) | 2026-05-27 | `a270ed1a37135600e62deb6a261f3ffa7c9b6260` | MIT; [`LICENSE`](https://github.com/ardalis/GuardClauses/blob/41162c46946214600a1f5a55b0abc94b0744691a/LICENSE) blob `45b51efe1c6e720d3d45bbacdeebb7db9311a977` | `sha256:8000fcacf968cde7b06d2b4a17fd979c89df9ade30a3dbcbf0a9dbe02a39b374` |
| [sindresorhus/p-queue](https://github.com/sindresorhus/p-queue) | TypeScript concurrency library and behavior/type tests | validation | [`180ab9e`](https://github.com/sindresorhus/p-queue/commit/180ab9e25cd10b6f548767d7176076b50d25e188) | 2026-07-22 | `e8e63896c7368b45ead03441d007c76f2b2591e5` | MIT; [`license`](https://github.com/sindresorhus/p-queue/blob/180ab9e25cd10b6f548767d7176076b50d25e188/license) blob `fa7ceba3eb4a9657a9db7f3ffca4e4e97a9019de` | `sha256:3073a4209d8325e0d0692a519caa6992f8851ba96908a1295c56e26b2a2e6edf` |
| [KristofferStrube/Blazor.FileSystemAccess](https://github.com/KristofferStrube/Blazor.FileSystemAccess) | .NET/JavaScript interop library and two Blazor samples | test | [`a318303`](https://github.com/KristofferStrube/Blazor.FileSystemAccess/commit/a318303142cbec91e7c82b3d6dd69685adcfbac1) | 2026-04-14 | `b1bda3e99bc3a194d20bf7c056e18cb614cd42ea` | MIT; [`LICENSE`](https://github.com/KristofferStrube/Blazor.FileSystemAccess/blob/a318303142cbec91e7c82b3d6dd69685adcfbac1/LICENSE) blob `e9302c54553450a17884483313969a72d02c8993` | `sha256:555a7b525db2fc835870d231d3df86dc9cb43aea663fce0bb93f6dcf9f645896` |

License metadata, default branches, commit objects, trees, and language summaries
were checked through the GitHub API on 2026-08-06. Fixed source archives were
retrieved from GitHub's codeload service and analyzed locally in default static
mode. EffortHours did not execute repository code, install dependencies, or inspect
history beyond selecting and recording the exact public revision.

This is a deliberately small pilot, not a representative population of software.
Each partition contains only one repository family, and all three records share one
teacher. Partition assignments were frozen before any rule tuning:

- GuardClauses is development data;
- p-queue is validation data; and
- Blazor.FileSystemAccess is test data.

Additional revisions or profiles of these repository identities must remain in the
same partitions.
