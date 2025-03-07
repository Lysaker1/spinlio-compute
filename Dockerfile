FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/SpinlioCompute.csproj", "src/"]
RUN dotnet restore "src/SpinlioCompute.csproj"
COPY . .
WORKDIR "/src/src"
RUN dotnet build "SpinlioCompute.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SpinlioCompute.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SpinlioCompute.dll"] 