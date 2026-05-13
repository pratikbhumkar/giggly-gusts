# syntax=docker/dockerfile:1.7
# Multi-stage build for the GigglyGusts.Host ASP.NET Core API.
#
# - Stage 1 (build): .NET 8 SDK to restore + publish.
# - Stage 2 (runtime): ASP.NET 8 runtime image, augmented with the
#   AWS Lambda Web Adapter so the same image is "Lambda-compatible"
#   per AWS guidance while preserving the local HTTP surface
#   (Kestrel on :8080) that `docker run -p` exposes.
#
# Why ASP.NET runtime + Lambda Web Adapter instead of
# `public.ecr.aws/lambda/dotnet:8`:
#   - The Web Adapter pattern keeps controllers/middleware running
#     identically locally (curl http://localhost:8080/weather) and
#     inside Lambda (event -> HTTP via the adapter extension).
#   - The Lambda dotnet base image would otherwise require an
#     Amazon.Lambda.AspNetCoreServer.Hosting handler entrypoint
#     that does NOT serve plain HTTP locally without RIE shims.
#   - This deviation is documented in README and ADR 0001.
#
# Note: Lambda ignores EXPOSE; `EXPOSE 8080` is for local `docker run -p`.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GigglyGusts.sln ./
COPY src/GigglyGusts.Host/GigglyGusts.Host.csproj src/GigglyGusts.Host/
RUN dotnet restore src/GigglyGusts.Host/GigglyGusts.Host.csproj

COPY src/ src/
RUN dotnet publish src/GigglyGusts.Host/GigglyGusts.Host.csproj \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# AWS Lambda Web Adapter (only activates when AWS_LAMBDA_RUNTIME_API is set,
# i.e. inside the Lambda runtime). Outside Lambda this binary is a no-op and
# Kestrel handles requests directly. See https://github.com/awslabs/aws-lambda-web-adapter
COPY --from=public.ecr.aws/awsguru/aws-lambda-adapter:0.8.4 /lambda-adapter /opt/extensions/lambda-adapter

WORKDIR /var/task
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
# AWS_LWA_PORT defaults to 8080; keep ASPNETCORE_URLS aligned.

EXPOSE 8080

ENTRYPOINT ["dotnet", "GigglyGusts.Host.dll"]
