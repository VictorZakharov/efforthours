# Third-party notices

This file records the third-party packages resolved by the checked-in NuGet lock
files as of 2026-08-05. Package versions are centrally pinned in
`Directory.Packages.props`.

Fairbill is licensed under the MIT License. Third-party components remain under
their respective licenses. This notice is included in the global-tool package
alongside Fairbill's `LICENSE` file.

## Runtime dependencies

| Package | Version | License | Project |
| --- | --- | --- | --- |
| JsonSchema.Net | 7.4.0 | MIT | <https://github.com/gregsdennis/json-everything> |
| JsonPointer.Net | 5.3.1 | MIT | <https://github.com/gregsdennis/json-everything> |
| Json.More.Net | 2.1.1 | MIT | <https://github.com/gregsdennis/json-everything> |
| Humanizer.Core | 2.14.1 | MIT | <https://github.com/Humanizr/Humanizer> |
| Microsoft.CodeAnalysis.CSharp | 5.6.0 | MIT | <https://github.com/dotnet/roslyn> |
| Microsoft.CodeAnalysis.Common | 5.6.0 | MIT | <https://github.com/dotnet/roslyn> |

JsonSchema.Net is intentionally pinned to a release whose NuGet package declares
the standard MIT license. Any upgrade requires a fresh review of the binary package
terms as well as API compatibility.

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
