# CI phase checkpoint - 2026-10-03

Public successful-run API timestamps. Durations are seconds, rounded to the API's one-second resolution. Start/end offsets are relative to run creation, not pure queue time. No runner identifiers or logs are retained.

| Run | Head | Event | Created UTC | Gate seconds |
| --- | --- | --- | --- | ---: |
| [37037008980](https://github.com/VictorZakharov/efforthours/actions/runs/37037008980) | `97877bad60dbf5d854185f61bdfad274af896b28` | pull_request | 2026-10-02T16:53:36Z | 442 |
| [37042428595](https://github.com/VictorZakharov/efforthours/actions/runs/37042428595) | `9febaaf6be736cbe8a27e9e6c82789339bf5e278` | pull_request | 2026-10-02T17:41:31Z | 412 |
| [37053877905](https://github.com/VictorZakharov/efforthours/actions/runs/37053877905) | `e208b597a222e92fd9b12e029f66af6a5c10d856` | pull_request | 2026-10-02T19:24:12Z | 390 |
| [37077694007](https://github.com/VictorZakharov/efforthours/actions/runs/37077694007) | `9edc7804016d748a7ae00bb7dedd7b4d394e75fe` | pull_request | 2026-10-02T23:27:52Z | 436 |
| [37080666742](https://github.com/VictorZakharov/efforthours/actions/runs/37080666742) | `905314057c97c0d5ab1c83001c3eb71b3d150530` | pull_request | 2026-10-03T00:07:01Z | 512 |
| [37083387417](https://github.com/VictorZakharov/efforthours/actions/runs/37083387417) | `c29a77527f0872f976ce17b9a41c4a1f656cfa7f` | pull_request | 2026-10-03T00:45:27Z | 480 |
| [37083938212](https://github.com/VictorZakharov/efforthours/actions/runs/37083938212) | `c18e1b7aba337caa42746e85cc481457c18336d8` | push | 2026-10-03T00:53:38Z | 27 |
| [37118742097](https://github.com/VictorZakharov/efforthours/actions/runs/37118742097) | `c51bc0bd1019e80c76830acb50eeb3b2545dcba2` | pull_request | 2026-10-03T11:09:13Z | 334 |
| [37119576713](https://github.com/VictorZakharov/efforthours/actions/runs/37119576713) | `be480fa741d1983cd00e69b6897ca7ab9fe7b459` | push | 2026-10-03T11:24:55Z | 28 |
| [37119893698](https://github.com/VictorZakharov/efforthours/actions/runs/37119893698) | `4cf3ad2a12aa4104a4912dd2fb390c462c71da77` | pull_request | 2026-10-03T11:30:48Z | 508 |
| [37122898201](https://github.com/VictorZakharov/efforthours/actions/runs/37122898201) | `99bea7f48058a9a8533a093a66f782a3477bdebc` | pull_request | 2026-10-03T12:26:49Z | 517 |
| [37123489676](https://github.com/VictorZakharov/efforthours/actions/runs/37123489676) | `73b1b83ab5a731a3987e92f810bbb62b3a6fb010` | push | 2026-10-03T12:37:48Z | 29 |
| [37123657295](https://github.com/VictorZakharov/efforthours/actions/runs/37123657295) | `9084966753a8a38e5eaabeef6ad923890a1f122b` | pull_request | 2026-10-03T12:40:54Z | 362 |
| [37124032373](https://github.com/VictorZakharov/efforthours/actions/runs/37124032373) | `3896f800c4766c08196f184d493383c666d4fd21` | push | 2026-10-03T12:47:50Z | 21 |

All recorded jobs concluded success except expected skipped Formatting and package jobs in unchanged post-merge runs.

| Run | Job | Start offset | Job duration | End offset |
| --- | --- | ---: | ---: | ---: |
| 37037008980 | Pull request commits are linear (success) | 3 | 4 | 7 |
| 37037008980 | Formatting (success) | 10 | 101 | 111 |
| 37037008980 | Build preview package candidate (success) | 10 | 69 | 79 |
| 37037008980 | End-to-end (windows-latest) (success) | 10 | 421 | 431 |
| 37037008980 | Quality (windows-latest) (success) | 9 | 89 | 98 |
| 37037008980 | End-to-end (ubuntu-latest) (success) | 9 | 240 | 249 |
| 37037008980 | Quality (ubuntu-latest) (success) | 11 | 107 | 118 |
| 37037008980 | Quality (macos-latest) (success) | 17 | 124 | 141 |
| 37037008980 | End-to-end (macos-latest) (success) | 16 | 333 | 349 |
| 37037008980 | Pack preview artifact (success) | 434 | 8 | 442 |
| 37042428595 | Pull request commits are linear (success) | 2 | 5 | 7 |
| 37042428595 | Build preview package candidate (success) | 9 | 66 | 75 |
| 37042428595 | Quality (macos-latest) (success) | 14 | 106 | 120 |
| 37042428595 | End-to-end (ubuntu-latest) (success) | 45 | 242 | 287 |
| 37042428595 | Formatting (success) | 9 | 98 | 107 |
| 37042428595 | End-to-end (windows-latest) (success) | 10 | 339 | 349 |
| 37042428595 | End-to-end (macos-latest) (success) | 15 | 390 | 405 |
| 37042428595 | Quality (ubuntu-latest) (success) | 9 | 99 | 108 |
| 37042428595 | Quality (windows-latest) (success) | 11 | 118 | 129 |
| 37042428595 | Pack preview artifact (success) | 407 | 5 | 412 |
| 37053877905 | Pull request commits are linear (success) | 3 | 4 | 7 |
| 37053877905 | Formatting (success) | 10 | 85 | 95 |
| 37053877905 | Build preview package candidate (success) | 10 | 57 | 67 |
| 37053877905 | Quality (ubuntu-latest) (success) | 9 | 95 | 104 |
| 37053877905 | End-to-end (windows-latest) (success) | 10 | 371 | 381 |
| 37053877905 | Quality (windows-latest) (success) | 9 | 120 | 129 |
| 37053877905 | Quality (macos-latest) (success) | 17 | 105 | 122 |
| 37053877905 | End-to-end (macos-latest) (success) | 14 | 237 | 251 |
| 37053877905 | End-to-end (ubuntu-latest) (success) | 10 | 240 | 250 |
| 37053877905 | Pack preview artifact (success) | 384 | 6 | 390 |
| 37077694007 | Pull request commits are linear (success) | 2 | 5 | 7 |
| 37077694007 | End-to-end (windows-latest) (success) | 9 | 416 | 425 |
| 37077694007 | Build preview package candidate (success) | 9 | 67 | 76 |
| 37077694007 | Quality (ubuntu-latest) (success) | 9 | 104 | 113 |
| 37077694007 | Quality (windows-latest) (success) | 10 | 126 | 136 |
| 37077694007 | End-to-end (macos-latest) (success) | 14 | 348 | 362 |
| 37077694007 | Formatting (success) | 9 | 90 | 99 |
| 37077694007 | Quality (macos-latest) (success) | 16 | 117 | 133 |
| 37077694007 | End-to-end (ubuntu-latest) (success) | 9 | 263 | 272 |
| 37077694007 | Pack preview artifact (success) | 427 | 9 | 436 |
| 37080666742 | Pull request commits are linear (success) | 9 | 7 | 16 |
| 37080666742 | Build preview package candidate (success) | 19 | 86 | 105 |
| 37080666742 | End-to-end (macos-latest) (success) | 23 | 296 | 319 |
| 37080666742 | Quality (ubuntu-latest) (success) | 18 | 110 | 128 |
| 37080666742 | Quality (macos-latest) (success) | 21 | 114 | 135 |
| 37080666742 | End-to-end (ubuntu-latest) (success) | 18 | 219 | 237 |
| 37080666742 | Quality (windows-latest) (success) | 20 | 133 | 153 |
| 37080666742 | End-to-end (windows-latest) (success) | 18 | 483 | 501 |
| 37080666742 | Formatting (success) | 18 | 109 | 127 |
| 37080666742 | Pack preview artifact (success) | 503 | 9 | 512 |
| 37083387417 | Pull request commits are linear (success) | 2 | 4 | 6 |
| 37083387417 | Build preview package candidate (success) | 9 | 67 | 76 |
| 37083387417 | Quality (macos-latest) (success) | 13 | 79 | 92 |
| 37083387417 | Quality (windows-latest) (success) | 8 | 108 | 116 |
| 37083387417 | Quality (ubuntu-latest) (success) | 9 | 108 | 117 |
| 37083387417 | End-to-end (ubuntu-latest) (success) | 8 | 261 | 269 |
| 37083387417 | End-to-end (windows-latest) (success) | 12 | 461 | 473 |
| 37083387417 | Formatting (success) | 10 | 109 | 119 |
| 37083387417 | End-to-end (macos-latest) (success) | 17 | 300 | 317 |
| 37083387417 | Pack preview artifact (success) | 475 | 5 | 480 |
| 37083938212 | Pull request commits are linear (success) | 2 | 6 | 8 |
| 37083938212 | End-to-end (macos-latest) (success) | 15 | 5 | 20 |
| 37083938212 | End-to-end (ubuntu-latest) (success) | 10 | 4 | 14 |
| 37083938212 | End-to-end (windows-latest) (success) | 10 | 5 | 15 |
| 37083938212 | Quality (macos-latest) (success) | 20 | 7 | 27 |
| 37083938212 | Quality (windows-latest) (success) | 11 | 4 | 15 |
| 37083938212 | Quality (ubuntu-latest) (success) | 10 | 2 | 12 |
| 37083938212 | Build preview package candidate (skipped) | 9 | -1 | 8 |
| 37083938212 | Formatting (skipped) | 9 | -1 | 8 |
| 37083938212 | Pack preview artifact (skipped) | 27 | 0 | 27 |
| 37118742097 | Pull request commits are linear (success) | 2 | 4 | 6 |
| 37118742097 | Formatting (success) | 8 | 104 | 112 |
| 37118742097 | End-to-end (macos-latest) (success) | 11 | 251 | 262 |
| 37118742097 | Quality (macos-latest) (success) | 15 | 111 | 126 |
| 37118742097 | Build preview package candidate (success) | 8 | 60 | 68 |
| 37118742097 | Quality (ubuntu-latest) (success) | 8 | 55 | 63 |
| 37118742097 | Quality (windows-latest) (success) | 9 | 123 | 132 |
| 37118742097 | End-to-end (ubuntu-latest) (success) | 10 | 281 | 291 |
| 37118742097 | End-to-end (windows-latest) (success) | 8 | 318 | 326 |
| 37118742097 | Pack preview artifact (success) | 328 | 6 | 334 |
| 37119576713 | Pull request commits are linear (success) | 2 | 11 | 13 |
| 37119576713 | End-to-end (macos-latest) (success) | 19 | 4 | 23 |
| 37119576713 | Quality (windows-latest) (success) | 15 | 4 | 19 |
| 37119576713 | Quality (ubuntu-latest) (success) | 15 | 4 | 19 |
| 37119576713 | Quality (macos-latest) (success) | 22 | 5 | 27 |
| 37119576713 | End-to-end (ubuntu-latest) (success) | 16 | 4 | 20 |
| 37119576713 | End-to-end (windows-latest) (success) | 16 | 4 | 20 |
| 37119576713 | Formatting (skipped) | 14 | -1 | 13 |
| 37119576713 | Build preview package candidate (skipped) | 14 | -1 | 13 |
| 37119576713 | Pack preview artifact (skipped) | 28 | 0 | 28 |
| 37119893698 | Pull request commits are linear (success) | 4 | 6 | 10 |
| 37119893698 | Formatting (success) | 12 | 64 | 76 |
| 37119893698 | Build preview package candidate (success) | 12 | 59 | 71 |
| 37119893698 | Quality (ubuntu-latest) (success) | 12 | 101 | 113 |
| 37119893698 | Quality (macos-latest) (success) | 20 | 92 | 112 |
| 37119893698 | End-to-end (macos-latest) (success) | 19 | 265 | 284 |
| 37119893698 | Quality (windows-latest) (success) | 13 | 138 | 151 |
| 37119893698 | End-to-end (ubuntu-latest) (success) | 13 | 277 | 290 |
| 37119893698 | End-to-end (windows-latest) (success) | 13 | 487 | 500 |
| 37119893698 | Pack preview artifact (success) | 502 | 6 | 508 |
| 37122898201 | Pull request commits are linear (success) | 2 | 5 | 7 |
| 37122898201 | Formatting (success) | 9 | 93 | 102 |
| 37122898201 | Build preview package candidate (success) | 10 | 62 | 72 |
| 37122898201 | End-to-end (ubuntu-latest) (success) | 9 | 270 | 279 |
| 37122898201 | Quality (macos-latest) (success) | 16 | 97 | 113 |
| 37122898201 | Quality (ubuntu-latest) (success) | 11 | 104 | 115 |
| 37122898201 | Quality (windows-latest) (success) | 9 | 119 | 128 |
| 37122898201 | End-to-end (macos-latest) (success) | 14 | 371 | 385 |
| 37122898201 | End-to-end (windows-latest) (success) | 10 | 499 | 509 |
| 37122898201 | Pack preview artifact (success) | 512 | 5 | 517 |
| 37123489676 | Pull request commits are linear (success) | 3 | 9 | 12 |
| 37123489676 | Quality (windows-latest) (success) | 14 | 4 | 18 |
| 37123489676 | Quality (ubuntu-latest) (success) | 14 | 4 | 18 |
| 37123489676 | End-to-end (ubuntu-latest) (success) | 14 | 2 | 16 |
| 37123489676 | Quality (macos-latest) (success) | 23 | 6 | 29 |
| 37123489676 | End-to-end (macos-latest) (success) | 20 | 4 | 24 |
| 37123489676 | End-to-end (windows-latest) (success) | 15 | 4 | 19 |
| 37123489676 | Formatting (skipped) | 13 | -1 | 12 |
| 37123489676 | Build preview package candidate (skipped) | 13 | -1 | 12 |
| 37123489676 | Pack preview artifact (skipped) | 29 | 0 | 29 |
| 37123657295 | Pull request commits are linear (success) | 2 | 6 | 8 |
| 37123657295 | Build preview package candidate (success) | 10 | 64 | 74 |
| 37123657295 | Formatting (success) | 10 | 88 | 98 |
| 37123657295 | Quality (windows-latest) (success) | 10 | 75 | 85 |
| 37123657295 | Quality (ubuntu-latest) (success) | 10 | 82 | 92 |
| 37123657295 | End-to-end (macos-latest) (success) | 17 | 337 | 354 |
| 37123657295 | End-to-end (windows-latest) (success) | 10 | 306 | 316 |
| 37123657295 | Quality (macos-latest) (success) | 15 | 110 | 125 |
| 37123657295 | End-to-end (ubuntu-latest) (success) | 10 | 283 | 293 |
| 37123657295 | Pack preview artifact (success) | 356 | 6 | 362 |
| 37124032373 | Pull request commits are linear (success) | 2 | 6 | 8 |
| 37124032373 | End-to-end (ubuntu-latest) (success) | 10 | 4 | 14 |
| 37124032373 | End-to-end (macos-latest) (success) | 17 | 4 | 21 |
| 37124032373 | Quality (macos-latest) (success) | 16 | 3 | 19 |
| 37124032373 | Quality (windows-latest) (success) | 11 | 3 | 14 |
| 37124032373 | End-to-end (windows-latest) (success) | 12 | 6 | 18 |
| 37124032373 | Quality (ubuntu-latest) (success) | 10 | 3 | 13 |
| 37124032373 | Formatting (skipped) | 9 | -1 | 8 |
| 37124032373 | Build preview package candidate (skipped) | 9 | -1 | 8 |
| 37124032373 | Pack preview artifact (skipped) | 22 | -1 | 21 |

Successful E2E steps only; skipped steps are excluded.

| Run | Platform | Job setup | Checkout | SDK | Restore | Build | Test | Job cleanup |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 37037008980 | End-to-end (windows-latest) | 1 | 5 | 3 | 10 | 63 | 334 | 0 |
| 37037008980 | End-to-end (ubuntu-latest) | 1 | 2 | 1 | 5 | 63 | 164 | 0 |
| 37037008980 | End-to-end (macos-latest) | 2 | 4 | 14 | 8 | 58 | 240 | 3 |
| 37042428595 | End-to-end (ubuntu-latest) | 1 | 2 | 0 | 5 | 64 | 168 | 0 |
| 37042428595 | End-to-end (windows-latest) | 1 | 6 | 2 | 10 | 46 | 270 | 1 |
| 37042428595 | End-to-end (macos-latest) | 2 | 4 | 17 | 12 | 60 | 287 | 2 |
| 37053877905 | End-to-end (windows-latest) | 1 | 8 | 2 | 22 | 41 | 292 | 0 |
| 37053877905 | End-to-end (macos-latest) | 3 | 3 | 9 | 7 | 36 | 173 | 2 |
| 37053877905 | End-to-end (ubuntu-latest) | 1 | 2 | 1 | 4 | 63 | 166 | 0 |
| 37077694007 | End-to-end (windows-latest) | 1 | 7 | 3 | 12 | 57 | 332 | 0 |
| 37077694007 | End-to-end (macos-latest) | 1 | 3 | 15 | 5 | 51 | 266 | 2 |
| 37077694007 | End-to-end (ubuntu-latest) | 1 | 2 | 1 | 9 | 63 | 183 | 0 |
| 37080666742 | End-to-end (macos-latest) | 1 | 3 | 9 | 4 | 43 | 231 | 1 |
| 37080666742 | End-to-end (ubuntu-latest) | 2 | 2 | 1 | 15 | 46 | 149 | 0 |
| 37080666742 | End-to-end (windows-latest) | 1 | 7 | 3 | 13 | 66 | 387 | 0 |
| 37083387417 | End-to-end (ubuntu-latest) | 2 | 2 | 0 | 5 | 60 | 189 | 0 |
| 37083387417 | End-to-end (windows-latest) | 1 | 9 | 3 | 13 | 66 | 364 | 0 |
| 37083387417 | End-to-end (macos-latest) | 1 | 3 | 13 | 7 | 58 | 213 | 1 |
| 37118742097 | End-to-end (macos-latest) | 2 | 2 | 13 | 6 | 38 | 188 | 1 |
| 37118742097 | End-to-end (ubuntu-latest) | 1 | 1 | 1 | 12 | 63 | 198 | 0 |
| 37118742097 | End-to-end (windows-latest) | 1 | 7 | 2 | 12 | 41 | 250 | 0 |
| 37119893698 | End-to-end (macos-latest) | 1 | 2 | 9 | 6 | 39 | 203 | 1 |
| 37119893698 | End-to-end (ubuntu-latest) | 1 | 1 | 1 | 4 | 63 | 203 | 0 |
| 37119893698 | End-to-end (windows-latest) | 1 | 6 | 3 | 12 | 67 | 394 | 0 |
| 37122898201 | End-to-end (ubuntu-latest) | 0 | 2 | 0 | 10 | 64 | 191 | 0 |
| 37122898201 | End-to-end (macos-latest) | 2 | 2 | 17 | 9 | 56 | 281 | 2 |
| 37122898201 | End-to-end (windows-latest) | 1 | 7 | 2 | 12 | 69 | 404 | 0 |
| 37123657295 | End-to-end (macos-latest) | 2 | 4 | 9 | 5 | 43 | 268 | 3 |
| 37123657295 | End-to-end (windows-latest) | 1 | 5 | 2 | 10 | 35 | 250 | 0 |
| 37123657295 | End-to-end (ubuntu-latest) | 1 | 2 | 0 | 9 | 66 | 203 | 0 |
