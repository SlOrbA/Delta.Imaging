@echo off
rem Builds every configuration and platform of Delta.Wsq.sln: the native nbis
rem DLLs and the .NET projects. Run it from a "Developer Command Prompt for
rem VS 2022" (msbuild and the C++ toolset must be available); the .NET SDK
rem alone cannot build nbis.vcxproj.
setlocal
cd /d "%~dp0"

for %%C in (Debug Release) do (
	for %%P in (Win32 x64) do (
		echo ***************** Building %%C / %%P *****************
		msbuild Delta.Wsq.sln -restore -m -nologo -v:minimal -p:Configuration=%%C -p:Platform=%%P
		if errorlevel 1 (
			echo ***************** %%C / %%P build failed! *****************
			exit /b 1
		)
	)
)

echo ***************** All builds succeeded *****************
