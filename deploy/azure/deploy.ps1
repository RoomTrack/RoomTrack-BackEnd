#Requires -Version 5.1
<#
.SYNOPSIS
    Deploys RoomTrack to Azure Container Apps (budget setup for Azure for Students).

.DESCRIPTION
    Creates (or updates) in one resource group:
      - Azure Database for MySQL Flexible Server (Burstable B1ms) with one schema and one user per service
        (same layout as deploy/mysql/init/01-databases.sql), public access limited to Azure services, TLS required.
      - A Container Apps environment (consumption) with its Log Analytics workspace.
      - 7 Container Apps from the GHCR images of docker-publish.yml:
          gateway                   external ingress, scales to zero
          identity/accommodations/  internal ingress (http://<app>), bookings/profiles/accommodations scale to zero;
          bookings/profiles/        identity and analytics keep 1 replica because they consume RabbitMQ events
          analytics-service
          notifications-worker      no ingress, 1 replica (consumes RabbitMQ)
    RabbitMQ (CloudAMQP), Brevo and Cloudinary stay external, as on Render. EF migrations run on service start.

    Settings and secrets come from roomtrack.azure.env (see roomtrack.azure.env.example). Missing generated values
    (MySQL server name and passwords, JWT/internal/cron keys) are created and written back to that file.

    Needs the Azure CLI (az login) and, for the first run, Docker (creates the MySQL users with the mysql client).

.PARAMETER UpdateImages
    Only rolls out a new revision of every app with the current images (after docker-publish.yml published new ones).

.EXAMPLE
    .\deploy\azure\deploy.ps1
    .\deploy\azure\deploy.ps1 -UpdateImages
#>
param(
    [string]$EnvFile = (Join-Path $PSScriptRoot 'roomtrack.azure.env'),
    [switch]$UpdateImages
)

$ErrorActionPreference = 'Stop'

# ── Helpers ────────────────────────────────────────────────────────────────────────────────────────────────────────

function Find-AzCli {
    $command = Get-Command az -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $default = Join-Path $env:ProgramFiles 'Microsoft SDKs\Azure\CLI2\wbin\az.cmd'
    if (Test-Path $default) { return $default }
    throw 'Azure CLI not found. Install it with: winget install Microsoft.AzureCLI'
}

$script:Az = Find-AzCli

# Simple function (no param block) so that az flags such as -o or -n are passed through instead of being bound
# as PowerShell parameters (-o would clash with -OutVariable).
function Invoke-Az {
    $output = & $script:Az @args --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw "az $($args[0..2] -join ' ') failed (exit code $LASTEXITCODE)" }
    return ($output -join "`n").Trim()
}

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

function Read-EnvFile([string]$Path) {
    $settings = @{}
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ($line -match '^\s*#' -or $line.IndexOf('=') -lt 1) { continue }
        $index = $line.IndexOf('=')
        $settings[$line.Substring(0, $index).Trim()] = $line.Substring($index + 1).Trim()
    }
    return $settings
}

function Set-EnvFileValue([string]$Path, [string]$Key, [string]$Value) {
    $text = [IO.File]::ReadAllText($Path)
    $pattern = "(?m)^$([regex]::Escape($Key))=[^\r\n]*"
    if ([regex]::IsMatch($text, $pattern)) {
        $text = [regex]::Replace($text, $pattern, { param($m) "$Key=$Value" })
    } else {
        $text = $text.TrimEnd() + [Environment]::NewLine + "$Key=$Value" + [Environment]::NewLine
    }
    [IO.File]::WriteAllText($Path, $text, (New-Object Text.UTF8Encoding $false))
}

# Alphanumeric only: values go through az.cmd (cmd.exe) and connection strings, where & ^ % ; " break things.
function New-RandomString([int]$Length, [string]$Alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789') {
    $bytes = New-Object byte[] $Length
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return -join ($bytes | ForEach-Object { $Alphabet[$_ % $Alphabet.Length] })
}

function Get-Setting([string]$Key, [scriptblock]$Generate) {
    $value = $script:Settings[$Key]
    if ([string]::IsNullOrWhiteSpace($value)) {
        if (-not $Generate) { throw "Missing $Key in $EnvFile" }
        $value = & $Generate
        Set-EnvFileValue $EnvFile $Key $value
        $script:Settings[$Key] = $value
        Write-Host "    generated $Key (saved in $(Split-Path $EnvFile -Leaf))"
    }
    return $value
}

function ConvertTo-YamlString([string]$Value) { return "'" + ($Value -replace "'", "''") + "'" }

# ── Settings ───────────────────────────────────────────────────────────────────────────────────────────────────────

if (-not (Test-Path $EnvFile)) {
    throw "Missing $EnvFile. Copy roomtrack.azure.env.example to roomtrack.azure.env and fill in the REQUIRED values."
}
$script:Settings = Read-EnvFile $EnvFile

$newPassword = { 'Aa1' + (New-RandomString 29) }   # upper + lower + digit, as Azure MySQL requires
$newKey = { New-RandomString 48 }

$location = Get-Setting 'AZURE_LOCATION'
$group = Get-Setting 'AZURE_RESOURCE_GROUP'
$environmentName = Get-Setting 'AZURE_ENVIRONMENT'
$imagePrefix = Get-Setting 'IMAGE_PREFIX'
$imageTag = Get-Setting 'IMAGE_TAG'

$apps = @(
    @{ Name = 'gateway';                Ingress = 'external'; MinReplicas = 0 },
    @{ Name = 'identity-service';       Ingress = 'internal'; MinReplicas = 1; Schema = 'identity' },
    @{ Name = 'accommodations-service'; Ingress = 'internal'; MinReplicas = 0; Schema = 'accommodations' },
    @{ Name = 'bookings-service';       Ingress = 'internal'; MinReplicas = 0; Schema = 'bookings' },
    @{ Name = 'profiles-service';       Ingress = 'internal'; MinReplicas = 0; Schema = 'profiles' },
    @{ Name = 'analytics-service';      Ingress = 'internal'; MinReplicas = 1; Schema = 'analytics' },
    @{ Name = 'notifications-worker';   Ingress = 'none';     MinReplicas = 1 }
)

Write-Step "Azure subscription: $(Invoke-Az account show --query name -o tsv)"
Write-Step 'Installing/updating the containerapp extension of the Azure CLI'
Invoke-Az extension add --name containerapp --upgrade | Out-Null

if ($UpdateImages) {
    $suffix = 'r' + (Get-Date -Format 'yyyyMMddHHmmss')
    foreach ($app in $apps) {
        Write-Step "New revision of $($app.Name)"
        Invoke-Az containerapp update -g $group -n $app.Name --image "$imagePrefix-$($app.Name):$imageTag" --revision-suffix $suffix | Out-Null
    }
    Write-Step 'Done'
    return
}

$required = 'RABBITMQ_HOST', 'RABBITMQ_VHOST', 'RABBITMQ_USER', 'RABBITMQ_PASSWORD', 'INITIAL_CHAIN_ADMIN_EMAIL',
    'INITIAL_CHAIN_ADMIN_PASSWORD', 'CLOUDINARY_CLOUD_NAME', 'CLOUDINARY_API_KEY', 'CLOUDINARY_API_SECRET',
    'SALES_NOTIFICATION_EMAIL', 'BREVO_API_KEY', 'EMAIL_FROM_ADDRESS'
$missing = $required | Where-Object { [string]::IsNullOrWhiteSpace($script:Settings[$_]) }
if ($missing) { throw "Fill in these values in $(Split-Path $EnvFile -Leaf): $($missing -join ', ')" }

$mysqlServer = Get-Setting 'MYSQL_SERVER_NAME' { 'roomtrack-mysql-' + (New-RandomString 5 'abcdefghijkmnpqrstuvwxyz23456789') }
$mysqlAdminUser = Get-Setting 'MYSQL_ADMIN_USER'
$mysqlAdminPassword = Get-Setting 'MYSQL_ADMIN_PASSWORD' $newPassword
$schemaPasswords = @{}
foreach ($schema in 'identity', 'accommodations', 'bookings', 'profiles', 'analytics') {
    $schemaPasswords[$schema] = Get-Setting "MYSQL_$($schema.ToUpper())_PASSWORD" $newPassword
}
$tokenSecret = Get-Setting 'TOKEN_SECRET' $newKey
$internalApiKey = Get-Setting 'INTERNAL_API_KEY' $newKey
$cronApiKey = Get-Setting 'CRON_API_KEY' $newKey
$mysqlHost = "$mysqlServer.mysql.database.azure.com"

# ── Resource group and providers ───────────────────────────────────────────────────────────────────────────────────

Write-Step "Resource group $group ($location)"
Invoke-Az group create -n $group -l $location | Out-Null
foreach ($provider in 'Microsoft.App', 'Microsoft.OperationalInsights', 'Microsoft.DBforMySQL') {
    Invoke-Az provider register -n $provider --wait | Out-Null
}

# ── MySQL ──────────────────────────────────────────────────────────────────────────────────────────────────────────

$existingServer = Invoke-Az mysql flexible-server list -g $group --query "[?name=='$mysqlServer'].name" -o tsv
if (-not $existingServer) {
    Write-Step "MySQL Flexible Server $mysqlServer (Burstable B1ms, takes ~5-10 minutes)"
    # --public-access 0.0.0.0 = only Azure services (the Container Apps) can connect; TLS is required by default.
    Invoke-Az mysql flexible-server create -g $group -n $mysqlServer -l $location `
        --tier Burstable --sku-name Standard_B1ms --storage-size 20 --version 8.0.21 `
        --admin-user $mysqlAdminUser --admin-password $mysqlAdminPassword `
        --public-access 0.0.0.0 --backup-retention 7 --yes | Out-Null
} else {
    Write-Step "MySQL Flexible Server $mysqlServer already exists"
}

Write-Step 'MySQL schemas and users (one per service)'
$sqlLines = @()
foreach ($schema in $schemaPasswords.Keys) {
    $user = "$($schema)_svc"
    $sqlLines += "CREATE DATABASE IF NOT EXISTS roomtrack_$schema;"
    $sqlLines += "CREATE USER IF NOT EXISTS '$user'@'%' IDENTIFIED BY '$($schemaPasswords[$schema])';"
    $sqlLines += "ALTER USER '$user'@'%' IDENTIFIED BY '$($schemaPasswords[$schema])';"
    $sqlLines += "GRANT ALL PRIVILEGES ON roomtrack_$schema.* TO '$user'@'%';"
}
$sqlLines += 'FLUSH PRIVILEGES;'

# The mysql client runs in a local Docker container, so this machine's public IP is allowed only while it runs.
$clientIp = (Invoke-RestMethod -Uri 'https://api.ipify.org').ToString().Trim()
$firewallRule = 'deploy-client'
Invoke-Az mysql flexible-server firewall-rule create -g $group -n $mysqlServer --rule-name $firewallRule `
    --start-ip-address $clientIp --end-ip-address $clientIp | Out-Null
try {
    ($sqlLines -join "`n") | & docker run --rm -i -e "MYSQL_PWD=$mysqlAdminPassword" mysql:8.0 `
        mysql -h $mysqlHost -u $mysqlAdminUser --ssl-mode=REQUIRED
    if ($LASTEXITCODE -ne 0) { throw "mysql client failed (exit code $LASTEXITCODE). Is Docker Desktop running?" }
} finally {
    Invoke-Az mysql flexible-server firewall-rule delete -g $group -n $mysqlServer --rule-name $firewallRule --yes | Out-Null
}

# ── Container Apps environment ─────────────────────────────────────────────────────────────────────────────────────

$environmentId = Invoke-Az containerapp env list -g $group --query "[?name=='$environmentName'].id" -o tsv
if (-not $environmentId) {
    Write-Step "Container Apps environment $environmentName (with Log Analytics)"
    # Explicit mode: recent containerapp extensions default to Express, which rejects ingress.allowInsecure (the plain
    # http://<app> calls between services). WorkloadProfiles includes the serverless Consumption profile.
    Invoke-Az containerapp env create -g $group -n $environmentName -l $location --logs-destination log-analytics `
        --environment-mode WorkloadProfiles | Out-Null
    $environmentId = Invoke-Az containerapp env show -g $group -n $environmentName --query id -o tsv
} else {
    Write-Step "Container Apps environment $environmentName already exists"
    $environmentMode = Invoke-Az containerapp env show -g $group -n $environmentName --query properties.environmentMode -o tsv
    if ($environmentMode -eq 'Express') {
        throw "Environment $environmentName is in Express mode (no allowInsecure for internal calls). Delete it (and its apps) and run this script again."
    }
}

# ── Container Apps ─────────────────────────────────────────────────────────────────────────────────────────────────

function Get-ConnectionString([string]$Schema) {
    return "server=$mysqlHost;port=3306;database=roomtrack_$Schema;user=$($Schema)_svc;password=$($schemaPasswords[$Schema]);SslMode=Required;"
}

$sharedEnv = [ordered]@{
    'ASPNETCORE_ENVIRONMENT'      = 'Production'
    'Database__MySqlServerVersion' = '8.0.21'
    'TokenSettings__Secret'       = @{ Secret = 'token-secret'; Value = $tokenSecret }
    'InternalApi__Key'            = @{ Secret = 'internal-api-key'; Value = $internalApiKey }
    'Cron__ApiKey'                = @{ Secret = 'cron-api-key'; Value = $cronApiKey }
    'App__WebBaseUrl'             = $script:Settings['APP_WEB_BASE_URL']
    'App__LandingBaseUrl'         = $script:Settings['APP_LANDING_BASE_URL']
    'RabbitMq__Host'              = $script:Settings['RABBITMQ_HOST']
    'RabbitMq__Port'              = $script:Settings['RABBITMQ_PORT']
    'RabbitMq__VirtualHost'       = $script:Settings['RABBITMQ_VHOST']
    'RabbitMq__Username'          = $script:Settings['RABBITMQ_USER']
    'RabbitMq__Password'          = @{ Secret = 'rabbitmq-password'; Value = $script:Settings['RABBITMQ_PASSWORD'] }
    # Internal ingress: the apps of the environment reach each other by name
    'Services__Identity__BaseUrl'       = 'http://identity-service'
    'Services__Accommodations__BaseUrl' = 'http://accommodations-service'
    'Services__Bookings__BaseUrl'       = 'http://bookings-service'
    'Services__Profiles__BaseUrl'       = 'http://profiles-service'
}

function Get-AppEnv([hashtable]$App) {
    $envVars = [ordered]@{}
    if ($App.Name -eq 'gateway') {
        $envVars['ASPNETCORE_ENVIRONMENT'] = 'Production'
        $envVars['Cors__AllowedOrigins'] = $script:Settings['CORS_ALLOWED_ORIGINS']
        foreach ($cluster in 'identity', 'accommodations', 'bookings', 'profiles', 'analytics') {
            $envVars["ReverseProxy__Clusters__$($cluster)__Destinations__primary__Address"] = "http://$cluster-service"
        }
        return $envVars
    }

    foreach ($entry in $sharedEnv.GetEnumerator()) { $envVars[$entry.Key] = $entry.Value }
    if ($App.Schema) {
        $envVars['ConnectionStrings__DefaultConnection'] = @{ Secret = 'db-connection'; Value = (Get-ConnectionString $App.Schema) }
    }
    switch ($App.Name) {
        'identity-service' {
            $envVars['InitialChainAdmin__Email'] = $script:Settings['INITIAL_CHAIN_ADMIN_EMAIL']
            $envVars['InitialChainAdmin__Password'] = @{ Secret = 'initial-admin-password'; Value = $script:Settings['INITIAL_CHAIN_ADMIN_PASSWORD'] }
        }
        'accommodations-service' {
            $envVars['Cloudinary__CloudName'] = $script:Settings['CLOUDINARY_CLOUD_NAME']
            $envVars['Cloudinary__ApiKey'] = $script:Settings['CLOUDINARY_API_KEY']
            $envVars['Cloudinary__ApiSecret'] = @{ Secret = 'cloudinary-api-secret'; Value = $script:Settings['CLOUDINARY_API_SECRET'] }
        }
        'bookings-service' {
            $envVars['Bookings__TimeZone'] = 'America/Lima'
        }
        'profiles-service' {
            $envVars['Sales__NotificationEmail'] = $script:Settings['SALES_NOTIFICATION_EMAIL']
            $envVars['DemoRequests__SchedulingUrl'] = $script:Settings['DEMO_SCHEDULING_URL']
        }
        'notifications-worker' {
            foreach ($name in @($envVars.Keys | Where-Object { $_ -like 'Services__*' })) { $envVars.Remove($name) }
            $envVars['DOTNET_ENVIRONMENT'] = 'Production'
            $envVars['Email__Transport'] = 'BrevoApi'
            $envVars['Email__Brevo__ApiKey'] = @{ Secret = 'brevo-api-key'; Value = $script:Settings['BREVO_API_KEY'] }
            $envVars['Email__From__Address'] = $script:Settings['EMAIL_FROM_ADDRESS']
            $envVars['Email__From__Name'] = $script:Settings['EMAIL_FROM_NAME']
            $envVars['Push__Transport'] = $script:Settings['PUSH_TRANSPORT']
        }
    }
    return $envVars
}

function New-AppYaml([hashtable]$App) {
    $envVars = Get-AppEnv $App
    $secrets = @()
    $envLines = @()
    foreach ($entry in $envVars.GetEnumerator()) {
        if ($entry.Value -is [hashtable]) {
            $secrets += "      - name: $($entry.Value.Secret)`n        value: $(ConvertTo-YamlString $entry.Value.Value)"
            $envLines += "          - name: $($entry.Key)`n            secretRef: $($entry.Value.Secret)"
        } else {
            $envLines += "          - name: $($entry.Key)`n            value: $(ConvertTo-YamlString ([string]$entry.Value))"
        }
    }

    $ingress = switch ($App.Ingress) {
        # The gateway only answers HTTPS from Internet; internal calls stay on plain http://<app> inside the environment
        'external' { "    ingress:`n      external: true`n      targetPort: 8080`n      transport: auto`n      allowInsecure: false" }
        'internal' { "    ingress:`n      external: false`n      targetPort: 8080`n      transport: auto`n      allowInsecure: true" }
        default { '' }
    }

    $lines = @(
        "location: $location",
        'properties:',
        "  managedEnvironmentId: $environmentId",
        '  configuration:',
        '    activeRevisionsMode: Single'
    )
    if ($ingress) { $lines += $ingress }
    if ($secrets) { $lines += '    secrets:'; $lines += $secrets }
    $lines += @(
        '  template:',
        '    containers:',
        "      - name: $($App.Name)",
        "        image: $imagePrefix-$($App.Name):$imageTag",
        '        resources:',
        '          cpu: 0.25',
        '          memory: 0.5Gi'
    )
    if ($envLines) { $lines += '        env:'; $lines += $envLines }
    $lines += @(
        '    scale:',
        "      minReplicas: $($App.MinReplicas)",
        '      maxReplicas: 2'
    )
    return ($lines -join "`n") + "`n"
}

$existingApps = @((Invoke-Az containerapp list -g $group --query '[].name' -o tsv) -split "`n" | Where-Object { $_ })
$yamlFile = Join-Path ([IO.Path]::GetTempPath()) "roomtrack-app-$([guid]::NewGuid().ToString('N')).yaml"
try {
    foreach ($app in $apps) {
        [IO.File]::WriteAllText($yamlFile, (New-AppYaml $app), (New-Object Text.UTF8Encoding $false))
        if ($existingApps -contains $app.Name) {
            Write-Step "Updating $($app.Name)"
            Invoke-Az containerapp update -g $group -n $app.Name --yaml $yamlFile | Out-Null
        } else {
            Write-Step "Creating $($app.Name)"
            Invoke-Az containerapp create -g $group -n $app.Name --yaml $yamlFile | Out-Null
        }
    }
} finally {
    Remove-Item -LiteralPath $yamlFile -Force -ErrorAction SilentlyContinue   # it holds secrets
}

$gatewayFqdn = Invoke-Az containerapp show -g $group -n gateway --query properties.configuration.ingress.fqdn -o tsv
Write-Step 'Done'
Write-Host ''
Write-Host "  API:      https://$gatewayFqdn/api/v1"
Write-Host "  Health:   https://$gatewayFqdn/health"
Write-Host "  Swagger:  https://$gatewayFqdn/docs"
Write-Host ''
Write-Host '  GitHub (Settings > Secrets and variables > Actions) for scheduler.yml:'
Write-Host "    variable API_BASE_URL = https://$gatewayFqdn/api/v1"
Write-Host "    secret   CRON_API_KEY = value of CRON_API_KEY in $(Split-Path $EnvFile -Leaf)"
Write-Host '  Front-end: point the API base URL of the web app and the landing to the URL above.'
