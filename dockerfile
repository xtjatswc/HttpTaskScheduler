# ʹ�� .NET 9.0 SDK ��Ϊ����������������Ŀ
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# ��¡��Ŀ����
RUN git clone https://github.com/xtjatswc/HttpTaskScheduler.git .

# ��ԭ��Ŀ����
RUN dotnet restore "HttpTaskScheduler.csproj"

# ������Ŀ
RUN dotnet publish "HttpTaskScheduler.csproj" -c Release -o /app/publish

# ʹ�� .NET 9.0 ����ʱ��Ϊ��������������Ӧ�ó���
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# ��¶Ӧ�ó���˿ڣ��������Ӧ�ó���ʹ�� 8080 �˿�
EXPOSE 8080

# ����Ӧ�ó���
ENTRYPOINT ["dotnet", "HttpTaskScheduler.dll"]