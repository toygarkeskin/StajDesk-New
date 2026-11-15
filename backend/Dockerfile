# Çok aşamalı build: SDK imajında derle, küçük runtime imajında çalıştır.
# Aynı Dockerfile hem API hem Worker için kullanılır (PROJECT argümanı).
ARG PROJECT=StajDesk.Api

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG PROJECT
WORKDIR /src
COPY Directory.Build.props ./
COPY src/StajDesk.Domain/StajDesk.Domain.csproj src/StajDesk.Domain/
COPY src/StajDesk.Infrastructure/StajDesk.Infrastructure.csproj src/StajDesk.Infrastructure/
COPY src/StajDesk.Api/StajDesk.Api.csproj src/StajDesk.Api/
COPY src/StajDesk.Worker/StajDesk.Worker.csproj src/StajDesk.Worker/
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj
COPY src/ src/
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
ARG PROJECT
ENV APP_DLL=${PROJECT}.dll \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Europe/Istanbul
# icu-data-full: tr-TR gibi tam kültür verisi (para birimi biçimi, "6.280,54" ayrıştırma). krb5-libs: Npgsql GSSAPI uyarısını önler.
RUN apk add --no-cache icu-libs icu-data-full krb5-libs tzdata && mkdir -p /app/data/uploads /app/data/keys && chown -R app /app/data
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet $APP_DLL"]
