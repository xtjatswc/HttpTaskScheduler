# 使用 .NET 9.0 SDK 作为基础镜像来构建项目
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 克隆项目代码
RUN git clone https://github.com/xtjatswc/HttpTaskScheduler.git .

# 还原项目依赖
RUN dotnet restore "HttpTaskScheduler.csproj"

# 发布项目
RUN dotnet publish "HttpTaskScheduler.csproj" -c Release -o /app/publish

# 使用 .NET 9.0 运行时作为基础镜像来运行应用程序
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 暴露应用程序端口，这里假设应用程序使用 8080 端口
EXPOSE 8080

# 运行应用程序
ENTRYPOINT ["dotnet", "HttpTaskScheduler.dll"]