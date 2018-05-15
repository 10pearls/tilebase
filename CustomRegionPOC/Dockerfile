FROM microsoft/aspnetcore-build:2.0 AS build-env
WORKDIR /app

# copy csproj and restore as distinct layers
#COPY *.csproj ./

# copy everything else and build
COPY . ./
WORKDIR /app/CustomRegionPOC.API
RUN dotnet restore
RUN dotnet publish -c Release -o out

# build runtime image
FROM microsoft/aspnetcore:2.0
WORKDIR /app/CustomRegionPOC.API
COPY --from=build-env /app/CustomRegionPOC.API/out .
ENTRYPOINT ["dotnet", "CustomRegionPOC.API.dll"]