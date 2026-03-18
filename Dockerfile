# Stage 1: Build frontend
FROM node:22-alpine AS frontend-build
WORKDIR /app
COPY frontend/weight-tracker-ui/package*.json ./
RUN npm ci
COPY frontend/weight-tracker-ui/ ./
RUN npm run build

# Stage 2: Build and publish backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /app
COPY backend/WeightTracker.Api/WeightTracker.Api.csproj ./
RUN dotnet restore
COPY backend/WeightTracker.Api/ ./
COPY --from=frontend-build /app/dist ./wwwroot
RUN dotnet publish -c Release -o /publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend-build /publish ./
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "WeightTracker.Api.dll"]
