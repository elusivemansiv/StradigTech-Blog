# Use the official Microsoft .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the solution file and restore dependencies
COPY *.sln .
COPY StradigBlog/*.csproj ./StradigBlog/
RUN dotnet restore

# Copy the remaining files and build the app
COPY StradigBlog/. ./StradigBlog/
WORKDIR /app/StradigBlog
RUN dotnet publish -c Release -o /app/publish

# Use the official Microsoft .NET runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Set environment variables for Railway
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# Expose the port
EXPOSE 8080

# Start the application
ENTRYPOINT ["dotnet", "StradigBlog.dll"]
