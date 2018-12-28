FROM microsoft/dotnet:2.2.100-sdk-alpine3.8 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore -r alpine.3.7-x64

# Copy everything else and build
COPY . ./
RUN dotnet publish -r alpine.3.7-x64 -c Release -o out

# Build runtime image
FROM microsoft/dotnet:2.2.0-runtime-deps-alpine3.8
WORKDIR /app
COPY --from=build-env /app/out .
EXPOSE 5000
ENV ASPNETCORE_URLS http://*:5000
ENTRYPOINT ["./WebApiHost"]
