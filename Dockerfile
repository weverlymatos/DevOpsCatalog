FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["src/DevOpsCatalog.Api/DevOpsCatalog.Api.csproj", "src/DevOpsCatalog.Api/"]

RUN dotnet restore "src/DevOpsCatalog.Api/DevOpsCatalog.Api.csproj"

COPY . .

WORKDIR "/src/src/DevOpsCatalog.Api"

RUN dotnet publish "DevOpsCatalog.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DevOpsCatalog.Api.dll"]