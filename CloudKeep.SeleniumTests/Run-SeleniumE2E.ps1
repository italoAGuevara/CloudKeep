param(
    [string]$BaseUrl = "http://localhost:5271",
    [string]$Password = "admin",
    [string]$FfmpegPath = $env:CLOUDKEEP_FFMPEG_PATH,
    [string]$AzureContainerName = $env:CLOUDKEEP_AZURE_CONTAINER_NAME,
    [string]$AzureConnectionString = $env:CLOUDKEEP_AZURE_CONNECTION_STRING,
    [string]$AwsBucketName = $env:CLOUDKEEP_AWS_BUCKET_NAME,
    [string]$AwsRegion = $env:CLOUDKEEP_AWS_REGION,
    [string]$AwsAccessKeyId = $env:CLOUDKEEP_AWS_ACCESS_KEY_ID,
    [string]$AwsSecretAccessKey = $env:CLOUDKEEP_AWS_SECRET_ACCESS_KEY
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$apiProject = Join-Path $repoRoot "API\CloudKeep.csproj"
$testProject = Join-Path $repoRoot "CloudKeep.SeleniumTests\CloudKeep.SeleniumTests.csproj"
$publishDir = Join-Path $repoRoot "API\bin\Release\net10.0-windows\win-x64\publish"
$appExe = Join-Path $publishDir "CloudKeep.exe"
$reportDir = Join-Path $repoRoot "TestResults\Selenium\Evidence"
$port = ([Uri]$BaseUrl).Port
$startedProcess = $null

function Assert-FfmpegAvailable {
    if ($FfmpegPath) {
        $configuredFfmpeg = Get-Command $FfmpegPath -ErrorAction SilentlyContinue
        if ($configuredFfmpeg) {
            return $configuredFfmpeg.Source
        }

        if (Test-Path $FfmpegPath) {
            return (Resolve-Path $FfmpegPath).Path
        }
    }

    $pathFfmpeg = Get-Command "ffmpeg" -ErrorAction SilentlyContinue
    if ($pathFfmpeg) {
        return $pathFfmpeg.Source
    }

    $candidateFiles = @(
        (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links\ffmpeg.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\ffmpeg\bin\ffmpeg.exe"),
        (Join-Path $env:ProgramFiles "ffmpeg\bin\ffmpeg.exe"),
        (Join-Path $env:ProgramFiles "Gyan\FFmpeg\bin\ffmpeg.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "ffmpeg\bin\ffmpeg.exe")
    )

    foreach ($candidate in $candidateFiles) {
        if ($candidate -and (Test-Path $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    $candidateRoots = @(
        (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"),
        $env:LOCALAPPDATA,
        $env:ProgramFiles
    )

    foreach ($root in $candidateRoots) {
        if (-not $root -or -not (Test-Path $root)) {
            continue
        }

        $found = Get-ChildItem -Path $root -Filter "ffmpeg.exe" -Recurse -File -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if ($found) {
            return $found.FullName
        }
    }

    throw "ffmpeg esta instalado segun winget, pero PowerShell no encontro ffmpeg.exe. Ejecute este script con -FfmpegPath 'C:\ruta\ffmpeg.exe'."
}

function Stop-CloudKeepOnPort {
    param([int]$Port)

    $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue |
        Where-Object { $_.OwningProcess -gt 0 } |
        Select-Object -ExpandProperty OwningProcess -Unique

    foreach ($processId in $connections) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "Deteniendo CloudKeep/proceso en puerto $Port (PID $processId)..."
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}

function Wait-CloudKeepReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    } while ((Get-Date) -lt $deadline)

    throw "CloudKeep no respondio en $Url despues de $TimeoutSeconds segundos."
}

function Set-EnvIfValue {
    param(
        [string]$Name,
        [string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        Set-Item -Path "Env:$Name" -Value $Value
    }
}

function Convert-SecureStringToPlainText {
    param([securestring]$Value)

    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Read-RequiredValue {
    param(
        [string]$CurrentValue,
        [string]$Prompt,
        [switch]$Secret
    )

    if (-not [string]::IsNullOrWhiteSpace($CurrentValue)) {
        return $CurrentValue
    }

    if ($Secret) {
        return Convert-SecureStringToPlainText (Read-Host $Prompt -AsSecureString)
    }

    return Read-Host $Prompt
}

function Resolve-DestinationCredentials {
    $script:AzureContainerName = Read-RequiredValue -CurrentValue $AzureContainerName -Prompt "Nombre del contenedor Azure"
    $script:AzureConnectionString = Read-RequiredValue -CurrentValue $AzureConnectionString -Prompt "Cadena de conexion Azure" -Secret
    $script:AwsBucketName = Read-RequiredValue -CurrentValue $AwsBucketName -Prompt "Bucket AWS S3"
    $script:AwsRegion = Read-RequiredValue -CurrentValue $AwsRegion -Prompt "Region AWS S3"
    $script:AwsAccessKeyId = Read-RequiredValue -CurrentValue $AwsAccessKeyId -Prompt "AWS Access Key ID"
    $script:AwsSecretAccessKey = Read-RequiredValue -CurrentValue $AwsSecretAccessKey -Prompt "AWS Secret Access Key" -Secret

    $missing = @()
    if ([string]::IsNullOrWhiteSpace($AzureContainerName)) { $missing += "AzureContainerName" }
    if ([string]::IsNullOrWhiteSpace($AzureConnectionString)) { $missing += "AzureConnectionString" }
    if ([string]::IsNullOrWhiteSpace($AwsBucketName)) { $missing += "AwsBucketName" }
    if ([string]::IsNullOrWhiteSpace($AwsRegion)) { $missing += "AwsRegion" }
    if ([string]::IsNullOrWhiteSpace($AwsAccessKeyId)) { $missing += "AwsAccessKeyId" }
    if ([string]::IsNullOrWhiteSpace($AwsSecretAccessKey)) { $missing += "AwsSecretAccessKey" }

    if ($missing.Count -gt 0) {
        throw "Faltan credenciales para las pruebas Selenium de destinos: $($missing -join ', ')."
    }
}

try {
    $resolvedFfmpegPath = Assert-FfmpegAvailable
    Resolve-DestinationCredentials
    Stop-CloudKeepOnPort -Port $port

    Write-Host "Publicando CloudKeep..."
    dotnet publish $apiProject -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) {
        throw "La publicacion de CloudKeep fallo con codigo $LASTEXITCODE."
    }

    Write-Host "Iniciando CloudKeep..."
    $startedProcess = Start-Process -FilePath $appExe -WorkingDirectory $publishDir -PassThru
    Wait-CloudKeepReady -Url $BaseUrl

    Write-Host "Ejecutando pruebas Selenium..."
    $env:CLOUDKEEP_E2E = "1"
    $env:CLOUDKEEP_BASE_URL = $BaseUrl
    $env:CLOUDKEEP_PASSWORD = $Password
    $env:CLOUDKEEP_REPORT_DIR = $reportDir
    $env:CLOUDKEEP_APP_EXE = $appExe
    $env:CLOUDKEEP_HEADLESS = "false"
    $env:CLOUDKEEP_FFMPEG_PATH = $resolvedFfmpegPath
    Set-EnvIfValue -Name "CLOUDKEEP_AZURE_CONTAINER_NAME" -Value $AzureContainerName
    Set-EnvIfValue -Name "CLOUDKEEP_AZURE_CONNECTION_STRING" -Value $AzureConnectionString
    Set-EnvIfValue -Name "CLOUDKEEP_AWS_BUCKET_NAME" -Value $AwsBucketName
    Set-EnvIfValue -Name "CLOUDKEEP_AWS_REGION" -Value $AwsRegion
    Set-EnvIfValue -Name "CLOUDKEEP_AWS_ACCESS_KEY_ID" -Value $AwsAccessKeyId
    Set-EnvIfValue -Name "CLOUDKEEP_AWS_SECRET_ACCESS_KEY" -Value $AwsSecretAccessKey

    dotnet test $testProject --logger "trx;LogFileName=selenium-report.trx" --results-directory (Join-Path $repoRoot "TestResults\Selenium")
    if ($LASTEXITCODE -ne 0) {
        throw "Las pruebas Selenium fallaron con codigo $LASTEXITCODE."
    }
}
finally {
    if ($startedProcess -and -not $startedProcess.HasExited) {
        Write-Host "Cerrando CloudKeep iniciado por las pruebas..."
        Stop-Process -Id $startedProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
