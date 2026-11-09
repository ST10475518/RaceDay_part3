# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RaceDay.Api.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
# Writable folder for the local image-upload fallback (used when Azure Blob Storage is not configured)
RUN mkdir -p /app/uploads && chown -R $APP_UID /app/uploads
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
