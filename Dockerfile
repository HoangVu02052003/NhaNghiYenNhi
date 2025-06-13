# Use the official ASP.NET Core runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 10000
ENV ASPNETCORE_URLS=http://+:10000

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["NhaNghiYenNhi.csproj", "."]
RUN dotnet restore "NhaNghiYenNhi.csproj"

# Copy the rest of the application code
COPY . .
WORKDIR "/src"
RUN dotnet build "NhaNghiYenNhi.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "NhaNghiYenNhi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build the runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NhaNghiYenNhi.dll"] 