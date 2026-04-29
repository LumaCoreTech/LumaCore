# Third-Party Notices

LumaCore is licensed under the MIT License (see [LICENSE](LICENSE)).

LumaCore incorporates third-party libraries and assets listed below. We are grateful to the
authors and contributors of these projects for making their work available to the open-source
community. Inclusion in this file is an attribution requirement of the respective upstream
license; it does **not** imply that the listed projects endorse LumaCore.

The file is organized into two parts:

1. **[Source-Adapted Code](#source-adapted-code)** — third-party source code that has been
   copied into the LumaCore repository and modified. These entries carry the full original
   license text, as required by the respective licenses.
2. **[Binary Dependencies](#binary-dependencies)** — third-party software consumed as
   compiled artifacts (NuGet packages, JavaScript libraries). License texts for these
   are distributed by the upstream projects themselves; this section provides attribution
   and license identifiers.

---

## Source-Adapted Code

### Nito.AsyncEx

- **Author:** Stephen Cleary
- **Source:** <https://github.com/StephenCleary/AsyncEx>
- **License:** MIT

The async coordination primitives in `LumaCore.Core.Threading` were originally adapted from
Nito.AsyncEx. They have since been modified, restructured, and selectively re-synced with
upstream changes; the exact upstream revision is no longer tracked. The following files
contain code derived from Nito.AsyncEx:

- `src/LumaCore.Core/Threading/AsyncAutoResetEvent.cs`
- `src/LumaCore.Core/Threading/AsyncManualResetEvent.cs`
- `src/LumaCore.Core/Threading/CancellationTokenTaskSource.cs`
- `src/LumaCore.Core/Threading/DefaultAsyncWaitQueue.cs`
- `src/LumaCore.Core/Threading/IAsyncWaitQueue.cs`
- `src/LumaCore.Core/Threading/TaskCompletionSourceExtensions.cs`
- `src/LumaCore.Core/Threading/TaskExtensions.cs`

The motivation for adapting (rather than depending on) Nito.AsyncEx is documented in
[ADR-0002 — Custom async primitives](docs/architecture/decisions/0002-custom-async-primitives.md).

#### Original License Text

```
The MIT License (MIT)

Copyright (c) 2014 StephenCleary

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

---

## Binary Dependencies

For consumed NuGet packages, see also the package metadata in
[`Directory.Packages.props`](Directory.Packages.props) and the respective package licenses
on [nuget.org](https://www.nuget.org/).

### NuGet Packages

#### Asp.Versioning (Http, Mvc.ApiExplorer)

- **Author:** .NET API Versioning Contributors
- **License:** MIT
- **URL:** <https://github.com/dotnet/aspnet-api-versioning>

#### coverlet.msbuild

- **Author:** coverlet-coverage Contributors
- **License:** MIT
- **URL:** <https://github.com/coverlet-coverage/coverlet>

#### Markdig

- **Author:** Alexandre Mutel (xoofx)
- **License:** BSD-2-Clause
- **URL:** <https://github.com/xoofx/markdig>

#### Microsoft.AspNetCore.SignalR.Client

- **Author:** Microsoft
- **License:** MIT
- **URL:** <https://github.com/dotnet/aspnetcore>

#### MinVer

- **Author:** Adam Ralph and Contributors
- **License:** MIT
- **URL:** <https://github.com/adamralph/minver>

#### MudBlazor

- **Author:** Garderoben, Henon and Contributors
- **License:** MIT
- **URL:** <https://github.com/MudBlazor/MudBlazor>

#### Npgsql.EntityFrameworkCore.PostgreSQL

- **Author:** Shay Rojansky and the Npgsql Contributors
- **License:** PostgreSQL License
- **URL:** <https://github.com/npgsql/efcore.pg>

#### Serilog (AspNetCore, Settings.Configuration, Sinks.Console, Sinks.File, Enrichers.Environment, Enrichers.Span, Enrichers.Thread)

- **Author:** Serilog Contributors
- **License:** Apache-2.0
- **URL:** <https://github.com/serilog/serilog>

#### Swashbuckle.AspNetCore.SwaggerUI

- **Author:** Richard Morris and Contributors
- **License:** MIT
- **URL:** <https://github.com/domaindrivendev/Swashbuckle.AspNetCore>

#### xUnit.net v3 (xunit.v3, xunit.v3.assert)

- **Author:** .NET Foundation and Contributors
- **License:** Apache-2.0
- **URL:** <https://github.com/xunit/xunit>

### JavaScript Libraries

The following libraries are vendored under `src/LumaCore.Ui.Web/wwwroot/lib/` and ship
as-is with the LumaCore application. The original `LICENSE` file is included next to the
vendored copy in each respective folder.

#### Cropper.js

- **Author:** Chen Fengyuan
- **Source:** <https://github.com/fengyuanchen/cropperjs>
- **License:** MIT — see [`src/LumaCore.Ui.Web/wwwroot/lib/cropper/LICENSE`](src/LumaCore.Ui.Web/wwwroot/lib/cropper/LICENSE)

#### Prism.js

- **Author:** Lea Verou and contributors
- **Source:** <https://github.com/PrismJS/prism>
- **License:** MIT — see [`src/LumaCore.Ui.Web/wwwroot/lib/prism/LICENSE`](src/LumaCore.Ui.Web/wwwroot/lib/prism/LICENSE)

### .NET Platform

LumaCore is built on the [.NET platform](https://github.com/dotnet/runtime) by Microsoft,
licensed under the MIT License. The following Microsoft packages are used throughout the
project and are listed here for completeness:

- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.AspNetCore.Components.Authorization
- Microsoft.AspNetCore.Components.WebAssembly
- Microsoft.AspNetCore.Components.WebAssembly.DevServer
- Microsoft.AspNetCore.Components.WebAssembly.Server
- Microsoft.AspNetCore.OpenApi
- Microsoft.AspNetCore.TestHost
- Microsoft.EntityFrameworkCore (Core, Design, Sqlite, SqlServer)
- Microsoft.Extensions.ApiDescription.Server
- Microsoft.Extensions.Configuration (Core, EnvironmentVariables, Json)
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Diagnostics.Testing
- Microsoft.Extensions.Hosting.Abstractions
- Microsoft.Extensions.Http
- Microsoft.Extensions.Localization
- Microsoft.Extensions.Logging (Core, Abstractions)
- Microsoft.Extensions.Options (Core, ConfigurationExtensions, DataAnnotations)
- Microsoft.Extensions.TimeProvider.Testing
- Microsoft.NET.Test.Sdk
- Microsoft.SourceLink.GitHub
- System.ComponentModel.Annotations
- System.IdentityModel.Tokens.Jwt

All Microsoft packages are licensed under the **MIT License**.
See <https://github.com/dotnet/runtime/blob/main/LICENSE.TXT> for details.

### Build Infrastructure

#### build.net

- **Author:** LumaCoreTech
- **License:** MIT
- **URL:** <https://github.com/LumaCoreTech/build.net>
- **Usage:** Git submodule providing shared build infrastructure (Directory.Build.props, CI helpers).
