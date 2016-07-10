@ECHO off
SETLOCAL
	SET xProjectName=ctr_PatcherConsole
	SET msBuildDirPath="%programfiles(x86)%\MSBuild\12.0\Bin"
	SET WorkDirPath=%~dp0
	SET ContribPath=%WorkDirPath%\contrib
	SET OutPath=%WorkDirPath%build
	SET TargetFilePath=%OutPath%\%xProjectName%.exe
	SET PatchFile=%WorkDirPath%%xProjectName%\Resources\Patch.3ps
	IF NOT EXIST "%PatchFile%" (
	ECHO %PatchFile% Doesn't Exist.
	ECHO Exit.
	pause
	Exit
	)
	
	RD "%OutPath%" /S /Q
	
	CALL %msBuildDirPath%\msbuild.exe "%WorkDirPath%\%xProjectName%\%xProjectName%.csproj" /p:Configuration=Release,Platform="Any CPU",OutputPath="%OutPath%" /t:Clean,Build
	FOR /F "delims=" %%G IN ('dir /b /s "%OutPath%\*.dll"') DO ("%ContribPath%\ILRepack.exe" /internalize /out:"%TargetFilePath%" "%TargetFilePath%" "%%G")
	FOR /F "delims=" %%G IN ('dir /b /s "%OutPath%\*.dll"') DO (DEL "%%G")