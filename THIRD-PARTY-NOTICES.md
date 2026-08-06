# Third-party notices

This file records the third-party packages resolved by the checked-in NuGet lock
files as of 2026-08-05. Package versions are centrally pinned in
`Directory.Packages.props`.

Fairbill is licensed under the MIT License. Third-party components remain under
their respective licenses. This notice is included in the global-tool package
alongside Fairbill's `LICENSE` file.

## Public data used by the bundled rate model

`rates/us-senior-contractor/2026.1.json` contains a small derived set of numeric
observations and series identifiers from the US Bureau of Labor Statistics:

- May 2025 Occupational Employment and Wage Statistics for Software Developers,
  SOC 15-1252, released May 15, 2026;
- March 2026 Employer Costs for Employee Compensation for private-industry
  professional and related occupations, released June 12, 2026; and
- BLS series and publication provenance needed to reproduce the calculation.

The source pages are <https://www.bls.gov/oes/tables.htm>,
<https://download.bls.gov/pub/time.series/oe/oe.txt>, and
<https://www.bls.gov/news.release/ecec.t04.htm>. BLS states at
<https://www.bls.gov/bls/linksite.htm> that its published information is in the
public domain except for specifically identified third-party material. Fairbill
does not redistribute BLS photographs, illustrations, branding, or bulk datasets.
The derived Fairbill artifact and calculation code are distributed under Fairbill's
MIT License.

## Public repositories used by the calibration pilot

`calibration/corpora/public-pilot` contains Fairbill-derived evidence identifiers,
teacher labels, and baseline measurements for fixed revisions of these MIT-licensed
repositories:

| Repository | Revision | License |
| --- | --- | --- |
| <https://github.com/ardalis/GuardClauses> | `41162c46946214600a1f5a55b0abc94b0744691a` | MIT |
| <https://github.com/sindresorhus/p-queue> | `180ab9e25cd10b6f548767d7176076b50d25e188` | MIT |
| <https://github.com/KristofferStrube/Blazor.FileSystemAccess> | `a318303142cbec91e7c82b3d6dd69685adcfbac1` | MIT |

Fairbill does not redistribute their source archives, images, or documentation.
The exact commit trees, license-file links and blob identifiers, partition choices,
and Fairbill source digests are recorded in
`calibration/corpora/public-pilot/SOURCES.md`. The project-authored review plan,
derived labels, and evaluation reports are distributed under Fairbill's MIT
License; the upstream projects remain under their own MIT licenses.

## Runtime dependencies

| Package | Version | License | Project |
| --- | --- | --- | --- |
| Acornima | 1.6.2 | BSD-3-Clause | <https://github.com/adams85/acornima> |
| Acornima.Extras | 1.6.2 | BSD-3-Clause | <https://github.com/adams85/acornima> |
| JsonSchema.Net | 7.4.0 | MIT | <https://github.com/gregsdennis/json-everything> |
| JsonPointer.Net | 5.3.1 | MIT | <https://github.com/gregsdennis/json-everything> |
| Json.More.Net | 2.1.1 | MIT | <https://github.com/gregsdennis/json-everything> |
| Humanizer.Core | 2.14.1 | MIT | <https://github.com/Humanizr/Humanizer> |
| Microsoft.CodeAnalysis.CSharp | 5.6.0 | MIT | <https://github.com/dotnet/roslyn> |
| Microsoft.CodeAnalysis.Common | 5.6.0 | MIT | <https://github.com/dotnet/roslyn> |

JsonSchema.Net is intentionally pinned to a release whose NuGet package declares
the standard MIT license. Any upgrade requires a fresh review of the binary package
terms as well as API compatibility.

### Acornima and Acornima.Extras license

The following license applies to Acornima and Acornima.Extras 1.6.2. Both NuGet
packages identify source commit `93b19e4e2ce0bd2c0fdca9deb92b39d1f5d9f53b`.

Copyright (c) Adam Simon. All rights reserved.

BSD 3-Clause License

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of Acornima nor the names of its contributors may be used to
   endorse or promote products derived from this software without specific prior
   written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

## Test and development dependencies

| Package | Version | License |
| --- | --- | --- |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| Microsoft.CodeCoverage | 17.14.1 | MIT |
| Microsoft.CodeAnalysis.Analyzers | 5.3.0 | MIT |
| Microsoft.TestPlatform.ObjectModel | 17.14.1 | MIT |
| Microsoft.TestPlatform.TestHost | 17.14.1 | MIT |
| Newtonsoft.Json | 13.0.3 | MIT |
| xunit | 2.9.3 | Apache-2.0 |
| xunit.abstractions | 2.0.3 | Apache-2.0, with MIT-licensed imported portions noted by the project |
| xunit.analyzers | 1.18.0 | Apache-2.0 |
| xunit.assert | 2.9.3 | Apache-2.0 |
| xunit.core | 2.9.3 | Apache-2.0 |
| xunit.extensibility.core | 2.9.3 | Apache-2.0 |
| xunit.extensibility.execution | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |

These packages are used to build or test Fairbill and are not shipped as runtime
dependencies of the Fairbill global-tool package unless a future package layout
explicitly changes that fact.

The authoritative license text and notices for each dependency are available in
its source repository and NuGet package metadata. The NuGet lock files are the
authoritative record of the versions resolved for this repository.
