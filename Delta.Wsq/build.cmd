@echo off

dotnet build Delta.Wsq.sln --configuration Debug --arch x86
if %ERRORLEVEL% equ 0 (	
	echo ***************** Debug/x86 Build succeeded ***************** 
) else (
	echo ***************** Debug/x86 Build failed! *****************
	echo Error level: %ERRORLEVEL%
	pause
)

dotnet build Delta.Wsq.sln --configuration Release --arch x86
if %ERRORLEVEL% equ 0 (	
	echo ***************** Release/x86 Build succeeded ***************** 
) else (
	echo ***************** Release/x86 Build failed! *****************
	echo Error level: %ERRORLEVEL%
	pause
)

dotnet build Delta.Wsq.sln --configuration Debug --arch x64
if %ERRORLEVEL% equ 0 (	
	echo ***************** Debug/x64 Build succeeded ***************** 
) else (
	echo ***************** Debug/x64 Build failed! *****************
	echo Error level: %ERRORLEVEL%
	pause
)

dotnet build Delta.Wsq.sln --configuration Release --arch x64
if %ERRORLEVEL% equ 0 (	
	echo ***************** Release/x64 Build succeeded ***************** 
) else (
	echo ***************** Release/x64 Build failed! *****************
	echo Error level: %ERRORLEVEL%
	pause
)