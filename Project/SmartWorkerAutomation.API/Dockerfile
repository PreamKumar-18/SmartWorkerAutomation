# Runtime-only image - matches how this app actually gets deployed on
# srv1788202: a separate `dotnet publish` step (not run inside Docker at
# all) drops its output straight into the directory this Dockerfile lives
# in (DLLs, Config/, appsettings.json, wwwroot/, etc., all flat), and this
# just packages that already-published output into a runtime container.
# Confirmed live on the server - `ls ~/api` shows SmartWorkerAutomation.API.dll
# and everything else `dotnet publish` produces sitting right next to this
# file. Not a multi-stage source build (an earlier version of this file
# tried COPY Project/... + dotnet restore/publish from source, which fails
# on this server with "lstat /root/Project: no such file or directory" -
# there's no Project/ source tree here, only the flat publish output).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Microsoft.Data.SqlClient's native SNI layer probes for libgssapi_krb5.so.2
# (Kerberos/GSSAPI) at startup - pulled in transitively via
# Microsoft.EntityFrameworkCore.SqlServer even though this app's actual
# database traffic goes through Npgsql/Postgres, not SQL Server. The aspnet
# runtime base image doesn't ship this library by default (trimmed for
# size), so without it the app crashes immediately on startup with
# "libgssapi_krb5.so.2: cannot open shared object file: No such file or
# directory" before it reaches any application code.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY . .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SmartWorkerAutomation.API.dll"]
