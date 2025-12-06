## Tech stack

This is a .Net 10 solution.
The Mutation.Ui is a WinUI 3 project (NOT WPF).

## Build \& test

* Restore: dotnet restore
* Build: dotnet build --configuration Release > logs/build_output.txt
* Test: dotnet test --configuration Release --logger "trx;LogFileName=test-results.trx" > logs/test_output.txt

Note: Output files are redirected to the `logs/` directory to keep the root clean.


## Conventions

* Use the .NET 10 SDK already installed in the environment
* Prefer 'dotnet build' over 'msbuild'
* Use tabs and not spaces.
