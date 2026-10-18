FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY .editorconfig global.json Directory.Build.props Directory.Packages.props ./
COPY src/Mita.Domain/Mita.Domain.csproj src/Mita.Domain/
COPY src/Mita.Infrastructure/Mita.Infrastructure.csproj src/Mita.Infrastructure/
COPY src/Mita.Api/Mita.Api.csproj src/Mita.Api/
RUN dotnet restore src/Mita.Api/Mita.Api.csproj
COPY src/ src/
RUN dotnet publish src/Mita.Api/Mita.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
  CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"]
ENTRYPOINT ["dotnet", "Mita.Api.dll"]
