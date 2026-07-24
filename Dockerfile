FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY Gridifier.slnx ./

COPY Gridifier.Shared/Gridifier.Shared.csproj Gridifier.Shared/
COPY Gridifier.Worker/Gridifier.Worker.csproj Gridifier.Worker/

RUN dotnet restore Gridifier.Worker/Gridifier.Worker.csproj

COPY Gridifier.Shared/ Gridifier.Shared/
COPY Gridifier.Worker/ Gridifier.Worker/

RUN dotnet publish Gridifier.Worker/Gridifier.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libsqlite3-0 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

VOLUME /data
ENV ConnectionStrings__Gridifier="Data Source=/data/gridifier.db"

ENTRYPOINT ["dotnet", "Gridifier.Worker.dll"]