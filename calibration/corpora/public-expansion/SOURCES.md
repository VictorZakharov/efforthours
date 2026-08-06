# Public expansion source manifest

This manifest records the immutable release snapshots used by
`fairbill-public-expansion/0.1.0`. Release identifiers and content checksums are
retained only for provenance and reproduction. Age, authorship, churn, and Git
history are not effort signals and were not inspected.

The source archives are not copied into Fairbill. The committed corpus contains
derived labels, stable source-work-item and evidence identifiers, and review
reasoning without source excerpts.

| Repository | Shape | Partition | Release | Source archive SHA256 | License provenance | Fairbill source digest |
|---|---|---|---|---|---|---|
| [developit/mitt](https://github.com/developit/mitt) | TypeScript event-emitter library, runtime/type tests, and multi-format package tooling | development | [`3.0.1`](https://github.com/developit/mitt/releases/tag/3.0.1) | `A362926811CB6D28E52FA7F1276C9189595081FFA18FD3CEB87A022D26BA3A2D` | MIT; in-archive `LICENSE` SHA256 `1CAB22F196264195A4CAEC8CA5630170FDDE76EE8F43346E47021D087332D3B0` | `sha256:135d31133a5cff5924e53febea07eee67c7fb059156ba225cccb262b1abd5d7d` |
| [Tyrrrz/CliWrap](https://github.com/Tyrrrz/CliWrap) | Multi-project .NET process library, subprocess fixtures, benchmarks, and cross-platform test suite | validation | [`3.10.4`](https://github.com/Tyrrrz/CliWrap/releases/tag/3.10.4) | `C4ADC4F3E8526058D941DFB9DE2E79285A2C2AAFE91298BF14AEE57C98DB9A39` | MIT; in-archive `License.txt` SHA256 `974FBF83EF71EF9B9C24BF59453B0E05323338193192EC7D49E14A37F11BA57E` | `sha256:de315d5bf4c4bfef5fc3c9ed498358a7041aff9456490c83b80e0bb48a885c9a` |
| [nanostores/nanostores](https://github.com/nanostores/nanostores) | JavaScript reactive-state library, extensive TypeScript declarations, tests, documentation, and release automation | test | [`1.4.2`](https://github.com/nanostores/nanostores/releases/tag/1.4.2) | `4420B10AB3F508F902E98F08F12E76CF3C78319440A967495A4CC47CF3BEAFEF` | MIT; in-archive `LICENSE` SHA256 `181BC2685F0CEA9D1BEB28352420E3A7FB382B38895D2B430ED1B3BC39572AF6` | `sha256:8dddc951643957bc7ce762f6e9db4982e95138c8b983071a95674b961ad83432` |

Public status, release identity, and MIT license metadata were checked through the
GitHub repository and release APIs on 2026-08-06. Exact release archives were
retrieved from GitHub's codeload service, checked for unsafe archive paths, hashed,
and analyzed locally in default static mode. Fairbill did not execute repository
code, install dependencies, or query commit objects or history.

This remains a small expansion rather than a representative software population.
Each partition contains one new repository family, and all records share the same
host-AI teacher. Partition assignments were frozen before numerical source review:

- mitt is development data;
- CliWrap is validation data; and
- nanostores is test data.

Additional releases, forks, or profiles from these repository families must remain
in their assigned partitions.
