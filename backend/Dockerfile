# Build the React frontend
FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
ARG VITE_GOOGLE_MAPS_API_KEY=""
ARG VITE_API_BASE_URL=""
ENV VITE_GOOGLE_MAPS_API_KEY=${VITE_GOOGLE_MAPS_API_KEY}
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

# Build the .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY backend/CarLease.Api.csproj ./backend/
RUN dotnet restore ./backend/CarLease.Api.csproj
COPY backend/ ./backend/
RUN dotnet publish ./backend/CarLease.Api.csproj --configuration Release --output /app/publish --no-restore

# Run the combined application
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=api-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot/
ENTRYPOINT ["dotnet", "CarLease.Api.dll"]
