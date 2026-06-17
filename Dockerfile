FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files first to leverage Docker layer caching for restore
COPY AIStudyHub.API/AIStudyHub.API.csproj        AIStudyHub.API/
COPY AIStudyHub.Business/AIStudyHub.Business.csproj AIStudyHub.Business/
COPY AIStudyHub.Data/AIStudyHub.Data.csproj       AIStudyHub.Data/

RUN dotnet restore AIStudyHub.API/AIStudyHub.API.csproj

COPY AIStudyHub.API/        AIStudyHub.API/
COPY AIStudyHub.Business/   AIStudyHub.Business/
COPY AIStudyHub.Data/       AIStudyHub.Data/

RUN dotnet publish AIStudyHub.API/AIStudyHub.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "AIStudyHub.API.dll"]