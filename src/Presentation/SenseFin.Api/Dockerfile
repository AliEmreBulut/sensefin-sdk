# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Proje dosyalarını kopyala ve restore et
COPY ["src/Presentation/SenseFin.Api/SenseFin.Api.csproj", "Presentation/SenseFin.Api/"]
COPY ["src/Infrastructure/SenseFin.Infrastructure/SenseFin.Infrastructure.csproj", "Infrastructure/SenseFin.Infrastructure/"]
COPY ["src/Core/SenseFin.Application/SenseFin.Application.csproj", "Core/SenseFin.Application/"]
COPY ["src/Core/SenseFin.Domain/SenseFin.Domain.csproj", "Core/SenseFin.Domain/"]

RUN dotnet restore "Presentation/SenseFin.Api/SenseFin.Api.csproj"

# Tüm kodları kopyala ve publish et
COPY src/ .
RUN dotnet publish "Presentation/SenseFin.Api/SenseFin.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SenseFin.Api.dll"]