@echo off
dotnet run --project "%~dp0src\DependencyVisualizer.csproj" -- %*
exit /b %errorlevel%
