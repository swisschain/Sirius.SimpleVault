FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim
WORKDIR /app
EXPOSE 5101
EXPOSE 5100
COPY . .
ENTRYPOINT ["dotnet", "Swisschain.Sirius.SimpleVault.Worker.dll"]
