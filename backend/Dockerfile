
# Stage 1: Build React frontend
FROM node:22-alpine AS frontend-build

WORKDIR /src/frontend

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./

# Empty API base URL means the browser calls /api/... on the same host.
ARG VITE_API_BASE_URL=""
ARG VITE_GOOGLE_MAPS_API_KEY=""

ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
ENV VITE_GOOGLE_MAPS_API_KEY=${VITE_GOOGLE_MAPS_API_KEY}

RUN npm run build


# Stage 2: Publish .NET API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build

WORKDIR /src/backend

COPY backend/*.csproj ./
RUN dotnet restore CarLease.Api.csproj

COPY backend/ ./
RUN dotnet publish CarLease.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=api-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot

ENTRYPOINT ["dotnet", "CarLease.Api.dll"]
