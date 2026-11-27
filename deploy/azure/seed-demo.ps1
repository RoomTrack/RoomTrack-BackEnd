<#
.SYNOPSIS
    Loads the RoomTrack demo data (hotel, rooms, staff, guests and bookings) into the Azure deployment.

.DESCRIPTION
    Runs tool/seed_backend.py of the mobile app (RoomTrack-App) against the Azure gateway. The demo e-mails
    (@roomtrack.pe) never receive their verification links, so verify-demo-email.py marks them verified in the
    identity database: a MySQL firewall rule for this machine's IP is opened for the run and always removed.

    Before the first run, as the chain administrator (Swagger, /docs):
      1. POST /users     { firstName, lastName, email: "admin@roomtrack.pe", password: "RoomTrack-Staff-2026!",
                           role: "admin" }  (a chain administrator cannot create another one)
      2. POST /hotels    the demo hotel, if admin@roomtrack.pe was created with a hotelId; its imageUrl must be a
                         Cloudinary image of the account (https://res.cloudinary.com/<cloud>/image/upload/...).
    The script is idempotent: it skips what already exists. The MFA secrets of the accounts it signs in with are
    written to the app's tool/demo_accounts.local.json (git-ignored).

.EXAMPLE
    az login
    pip install pymysql
    .\deploy\azure\seed-demo.ps1 -AppDir C:\Users\me\AndroidStudioProjects\RoomTrack-App
#>
param(
    [string]$AppDir = (Join-Path $HOME 'AndroidStudioProjects\RoomTrack-App'),
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 turns every stderr line of a native command into an error record, and with
# $ErrorActionPreference = 'Stop' the first one aborts the script although the command succeeds: az writes its
# "Running .." progress there, the mysql client its password warning. Native commands run with 'Continue' and are
# judged by their exit code; their stderr (without the progress lines) goes into the error message.
function Invoke-Native([scriptblock]$Command, [string]$What) {
    $ErrorActionPreference = 'Continue'
    $output = & $Command 2>&1
    $code = $LASTEXITCODE
    $stdout = @($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })
    $stderr = @($output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_".Trim() } |
        Where-Object { $_ -and $_ -notmatch 'Running \.\.' })
    if ($code -ne 0) { throw "$What failed (exit code $code): $($stderr -join ' ')" }
    return ($stdout -join "`n").Trim()
}

$here = $PSScriptRoot
$envFile = Join-Path $here 'roomtrack.azure.env'
$seed = Join-Path $AppDir 'tool\seed_backend.py'
if (-not (Test-Path $envFile)) { throw "Falta $envFile (créalo desde roomtrack.azure.env.example o con deploy.ps1)." }
if (-not (Test-Path $seed)) { throw "No encuentro $seed. Indica la carpeta de RoomTrack-App con -AppDir." }
try { Invoke-Native { & $Python -c 'import pymysql' } 'PyMySQL' | Out-Null }
catch { throw "$Python no tiene PyMySQL: pip install pymysql" }

$settings = @{}
Get-Content $envFile | Where-Object { $_ -match '^\s*[A-Z_]+\s*=' } | ForEach-Object {
    $key, $value = $_ -split '=', 2
    $settings[$key.Trim()] = $value.Trim()
}
$group = $settings['AZURE_RESOURCE_GROUP']
$server = $settings['MYSQL_SERVER_NAME']
$fqdn = Invoke-Native { az containerapp show -g $group -n gateway --query properties.configuration.ingress.fqdn -o tsv --only-show-errors } 'az containerapp show'
if (-not $fqdn) { throw "No encontré el gateway en el grupo $group (¿az login?)." }

$rule = 'seed-demo-temp'
$ip = (Invoke-RestMethod https://api.ipify.org).Trim()
try {
    # Opened inside the try: if anything fails from here on, the finally still removes the rule.
    Write-Host "Abriendo el firewall de MySQL ($server) para $ip..."
    Invoke-Native { az mysql flexible-server firewall-rule create -g $group -n $server --rule-name $rule `
            --start-ip-address $ip --end-ip-address $ip -o none --only-show-errors } 'az firewall-rule create' | Out-Null
    $env:API_BASE_URL = "https://$fqdn/api/v1"
    $env:SEED_SKIP_CHAIN_ADMIN = '1'
    $env:SEED_EMAIL_VERIFIER = "$Python $(Join-Path $here 'verify-demo-email.py')"
    $env:PYTHONIOENCODING = 'utf-8'
    # Its output is shown as it comes; a warning on stderr does not stop the seed, only its exit code counts.
    & {
        $ErrorActionPreference = 'Continue'
        & $Python -u $seed 2>&1 | ForEach-Object { Write-Host "$_" }
    }
    if ($LASTEXITCODE -ne 0) { throw "seed_backend.py terminó con código $LASTEXITCODE" }
}
finally {
    Write-Host 'Cerrando el firewall de MySQL...'
    try {
        Invoke-Native { az mysql flexible-server firewall-rule delete -g $group -n $server --rule-name $rule --yes -o none --only-show-errors } 'az firewall-rule delete' | Out-Null
    } catch {
        # Also when the rule was never created; the list below says whether it is still open.
    }
    $left = Invoke-Native { az mysql flexible-server firewall-rule list -g $group -n $server --query "[?name=='$rule'].name" -o tsv --only-show-errors } 'az firewall-rule list'
    if ($left) { Write-Warning "La regla $rule sigue abierta: bórrala a mano." }
}
