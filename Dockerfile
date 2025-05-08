# Use the .NET 8 SDK for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

# Copy solution and restore dependencies
COPY lookout.sln ./
COPY Runner/*.csproj ./Runner/
RUN dotnet restore

# Copy the entire project and build the application
COPY . .
RUN dotnet publish ./Runner -c Release -o /app/out

# Use the .NET runtime for the final image
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app
COPY --from=build /app/out .

# Set the default command
ENTRYPOINT ["dotnet", "Runner.dll"]