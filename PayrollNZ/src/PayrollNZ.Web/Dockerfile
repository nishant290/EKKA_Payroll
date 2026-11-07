FROM mcr.microsoft.com/dotnet/aspnet:8.0-nanoserver-ltsc2022 AS base
WORKDIR /app
EXPOSE 5179

ENV ASPNETCORE_URLS=http://+:5179

FROM mcr.microsoft.com/dotnet/sdk:8.0-nanoserver-ltsc2022 AS build
ARG configuration=Release
WORKDIR /src
COPY ["PayrollNZ/src/PayrollNZ.Web/PayrollNZ.Web.csproj", "PayrollNZ/src/PayrollNZ.Web/"]
RUN dotnet restore "PayrollNZ\src\PayrollNZ.Web\PayrollNZ.Web.csproj"
COPY . .
WORKDIR "/src/PayrollNZ/src/PayrollNZ.Web"
RUN dotnet build "PayrollNZ.Web.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "PayrollNZ.Web.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PayrollNZ.Web.dll"]
