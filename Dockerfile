# Use the official .NET 10 SDK image to build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["wanderer-api.csproj", "./"]
RUN dotnet restore "wanderer-api.csproj"

# Copy everything and publish
COPY . .
RUN dotnet publish "wanderer-api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Let the service listen on the port Render provides at runtime
EXPOSE 80
ENTRYPOINT ["sh", "-c", "dotnet wanderer-api.dll --urls http://*:$PORT"]
