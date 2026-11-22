# Imagen de la API: se compila con el SDK de .NET 11 y corre sobre la imagen de ASP.NET Core 11.
# El contexto de construcción es la raíz del repositorio (docker-compose.yml lo fija así) porque los proyectos
# incrustan archivos de sat/: los catálogos en CSV y, más adelante, los esquemas, los XSLT y los CSD de prueba.

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Primero solo lo que decide el restore: mientras no cambien los paquetes, Docker reutiliza esta capa.
COPY global.json ./
COPY backend/Directory.Build.props backend/
COPY backend/src/Comprobante.Core/Comprobante.Core.csproj backend/src/Comprobante.Core/
COPY backend/src/Comprobante.Catalogos/Comprobante.Catalogos.csproj backend/src/Comprobante.Catalogos/
COPY backend/src/Comprobante.Pdf/Comprobante.Pdf.csproj backend/src/Comprobante.Pdf/
COPY backend/src/Comprobante.Api/Comprobante.Api.csproj backend/src/Comprobante.Api/
RUN dotnet restore backend/src/Comprobante.Api/Comprobante.Api.csproj

COPY backend/src/ backend/src/
COPY sat/ sat/
RUN dotnet publish backend/src/Comprobante.Api/Comprobante.Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0

# curl lo usa el healthcheck de docker-compose.yml. tzdata hace falta para que la zona horaria de cada código
# postal (America/Mazatlan, America/Tijuana...) se resuelva con TimeZoneInfo.
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Usuario sin privilegios que traen las imágenes de .NET.
USER $APP_UID

COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Comprobante.Api.dll"]
