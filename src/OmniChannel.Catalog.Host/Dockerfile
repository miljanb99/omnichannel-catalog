FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/OmniChannel.Catalog.Host/OmniChannel.Catalog.Host.csproj
RUN dotnet publish src/OmniChannel.Catalog.Host/OmniChannel.Catalog.Host.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OmniChannel.Catalog.Host.dll"]