#!/bin/bash
set -a             
source .env
set +a
dotnet run -v d --project Runner/Runner.csproj