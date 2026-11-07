# Installation

## Status

PCWord has no end-user installer or supported installation package. It is not ready for consumer installation. The current application is a controlled-editor development prototype; the TSF DLL is diagnostic-only and must not be registered as a production text service.

## Build and run for development

Validated build instructions are maintained in the root [README](../README.md). The current project targets Windows and .NET 10. Use a Windows machine with the .NET 10 SDK and Windows Desktop targeting/runtime components available.

From a PowerShell prompt at the repository root:

```powershell
dotnet restore .\PCWord.sln
dotnet build .\PCWord.sln --configuration Release --no-restore
dotnet test .\PCWord.sln --configuration Release --no-restore --verbosity minimal
dotnet run --project .\src\PCWord.Stage0\PCWord.Stage0.csproj --configuration Release
```

Use only synthetic text. This launches PCWord's owned editor; it does not enable system-wide suggestions.

## TSF diagnostic probe

The TSF probe is an optional feasibility tool, not part of normal installation or startup. Do not run `regsvr32` on it as part of installing PCWord for everyday use. Its manual test registration and cleanup procedure is documented in [testing-guide.md](testing-guide.md), and registration state must be removed after that experiment.

## User configuration

The application stores settings under `%LOCALAPPDATA%\PCWord\settings.json`. Do not delete this file as part of ordinary source/build cleanup if settings should be preserved. No upgrade migration or settings backup/restore tool is available.

## Release prerequisites not yet implemented

A release install requires an architecture-specific distribution, verified runtime prerequisites, versioning, asset/license manifest, registration design if production TSF is introduced, rollback behavior, and a tested uninstaller. None is currently provided. See [release-readiness.md](release-readiness.md).