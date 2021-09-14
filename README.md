# Biohazrd

[![MIT Licensed](https://img.shields.io/github/license/infectedlibraries/biohazrd?style=flat-square)](LICENSE.txt)
[![CI Status](https://img.shields.io/github/workflow/status/infectedlibraries/biohazrd/Biohazrd/main?style=flat-square)](https://github.com/InfectedLibraries/Biohazrd/actions?query=workflow%3ABiohazrd+branch%3Amain)
[![NuGet Version](https://img.shields.io/nuget/v/Biohazrd?style=flat-square)](https://www.nuget.org/packages/Biohazrd/)
[![Sponsor](https://img.shields.io/badge/sponsor-%E2%9D%A4-lightgrey?logo=github&style=flat-square)](https://github.com/sponsors/PathogenDavid)

Biohazrd is a framework for creating binding generators for C **and** C++ libraries. It aims to lower the amount of ongoing boilerplate maintenance required to use native libraries from .NET as well as allow direct interoperation with C++ libraries without a C translation later.

Biohazrd's API is now stable, but the documentation and overall developer experience is still improving. If you're interested in this project, consider [sponsoring development](https://github.com/sponsors/PathogenDavid).

Interested in seeing how Biohazrd can be used in practice? Check out [InfectedImGui](https://github.com/InfectedLibraries/InfectedImGui) or [InfectedPhysX](https://github.com/InfectedLibraries/InfectedPhysX).

We also have peliminary documentation available in [the docs folder](docs/).

## License

This project is licensed under the MIT License. [See the license file for details](LICENSE.txt).

Additionally, this project has some third-party dependencies. [See the third-party notice listing for details](THIRD-PARTY-NOTICES.md).

## Quick Overview

Here's a quick overview of the individual components of this repository:

| Project | Description |
|---------|-------------|
| [`Biohazrd`](https://www.nuget.org/packages/Biohazrd.Core/) | The core of Biohazrd. This is the code is primarily responsible for parsing the Cursor tree of libclang` and translating it into a simplified model that's easier to work with.
| [`Biohazrd.Transformation`](https://www.nuget.org/packages/Biohazrd.Transformation/) | Language-agnostic functionality for transforming the immutable object model output by the core. (As well as a [some common transformations you might need](docs/BuiltInTransformations/))
| [`Biohazrd.OutputGeneration`](https://www.nuget.org/packages/Biohazrd.OutputGeneration/) | Language-agnostic functionality for writing out code and other files.
| [`Biohazrd.CSharp`](https://www.nuget.org/packages/Biohazrd.CSharp/) | Transformations, output generation, and other infrastructure for supporting emitting a C# interop layer.
| [`Biohazrd.Utilities`](https://www.nuget.org/packages/Biohazrd.Utilities/) | Optional helpers that don't fit anywhere else.
| [`Biohazrd.AllInOne`](https://www.nuget.org/packages/Biohazrd/) | A convenience package which brings in all of the other components of Biohazrd.
| `Tests` | Automated tests for Biohazrd.
