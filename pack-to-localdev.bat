@echo off

dotnet new tool-manifest --force
dotnet tool install inedo.extensionpackager

cd InedoCore\InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\InedoCore.upack --build=Debug -o
cd ..\..