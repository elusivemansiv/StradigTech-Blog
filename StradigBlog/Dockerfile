FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["StradigBlog.csproj", "."]
RUN dotnet restore "StradigBlog.csproj"
COPY . .
RUN dotnet build "StradigBlog.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "StradigBlog.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StradigBlog.dll"]