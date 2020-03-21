<p align="center">
  <a href="https://github.com/lanzorg/wslsetup">
    <img height="200" src="https://image.flaticon.com/icons/svg/1069/1069101.svg" alt="logo">
  </a>
</p>

<h1 align="center">WslSetup</h1>

<p align="center">Setup WSL2 with Ubuntu 18.04 on Windows automatically.</p>

## Requirements

* Administrator rights.
* Windows 10 x64 build 18917 or higher.

## Build

```shell
git clone https://github.com/lanzorg/wslsetup.git
cd .\wslsetup\src\WslSetup\
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true
```

## Run

```shell
.\bin\Release\netcoreapp*\win-x64\publish\WslSetup.exe
```