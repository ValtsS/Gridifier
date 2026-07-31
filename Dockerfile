FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY Gridifier.slnx ./

COPY Gridifier.Shared/Gridifier.Shared.csproj Gridifier.Shared/
COPY Gridifier.Worker/Gridifier.Worker.csproj Gridifier.Worker/
COPY Gridifier.Api/Gridifier.Api.csproj Gridifier.Api/

RUN dotnet restore Gridifier.Api/Gridifier.Api.csproj

COPY Gridifier.Shared/ Gridifier.Shared/
COPY Gridifier.Worker/ Gridifier.Worker/
COPY Gridifier.Api/ Gridifier.Api/

RUN dotnet publish Gridifier.Api/Gridifier.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libsqlite3-0 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 10001 appuser \
    && mkdir -p /data \
    && chown -R appuser:appuser /app /data

COPY --from=build /app .

VOLUME /data
ENV ConnectionStrings__Gridifier="Data Source=/data/gridifier.db"

USER appuser
ENTRYPOINT ["dotnet", "Gridifier.Api.dll"]