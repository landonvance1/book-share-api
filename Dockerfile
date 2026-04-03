# Use the official .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory
WORKDIR /src

# Copy project files and restore dependencies
COPY BookSharingApp.csproj .
COPY BookSharingApp.Cli/BookSharingApp.Cli.csproj BookSharingApp.Cli/
RUN dotnet restore BookSharingApp.csproj
RUN dotnet restore BookSharingApp.Cli/BookSharingApp.Cli.csproj

# Copy the rest of the source code
COPY . .

# Build the application
RUN dotnet build BookSharingApp.csproj -c Release -o /app/build

# Publish the application
RUN dotnet publish BookSharingApp.csproj -c Release -o /app/publish

# Publish the CLI tool
RUN dotnet publish BookSharingApp.Cli/BookSharingApp.Cli.csproj -c Release -o /app/cli

# Use the official .NET 8 runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set the working directory
WORKDIR /app

# Copy the published application from the build stage
COPY --from=build /app/publish .
COPY --from=build /app/cli ./cli

# Add CLI shortcut
RUN printf '#!/bin/sh\nexec dotnet /app/cli/BookSharingApp.Cli.dll "$@"\n' > /usr/local/bin/admin \
    && chmod +x /usr/local/bin/admin

# Expose port 8080
EXPOSE 8080

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Run the application
ENTRYPOINT ["dotnet", "BookSharingApp.dll"]