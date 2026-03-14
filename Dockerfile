FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "StradigBlog/StradigBlog.csproj"
RUN dotnet build "StradigBlog/StradigBlog.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "StradigBlog/StradigBlog.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StradigBlog.dll"]