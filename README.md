# Green Expanses / Зелёные просторы

Economic farm management simulator.

## Bootstrap commands

```bash
dotnet restore GreenExpanses.sln
dotnet build GreenExpanses.sln --configuration Release --no-restore
dotnet test GreenExpanses.sln --configuration Release --no-build
dotnet run --project src/GreenExpanses.Tools -- --seed 42 --days 365
```

Product source-of-truth documents live in the project Google Drive. This repository contains implementation code and implementation-local technical notes.
