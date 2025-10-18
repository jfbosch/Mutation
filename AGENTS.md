## Tech stack

This is a .Net 9 solution.
The Mutation.Ui is a WinUI 3 project (NOT WPF).

## Build \& test

* Restore: dotnet restore
* Build: dotnet build --configuration Release
* Test: dotnet test --configuration Release --logger "trx;LogFileName=test-results.trx"

## Conventions

* Use the .NET 9 SDK already installed in the environment
* Prefer 'dotnet build' over 'msbuild'
* Use tabs and not spaces.
