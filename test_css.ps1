[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy

# Test http instead
try {
    $r = Invoke-WebRequest -Uri 'http://localhost:55353/_content/TrackerGanadero.Shared/css/app.css' -UseBasicParsing
    Write-Host "app.css Status: $($r.StatusCode)"
    Write-Host "app.css Length: $($r.Content.Length) chars"
    Write-Host "First 80 chars: $($r.Content.Substring(0, [Math]::Min(80, $r.Content.Length)))"
} catch {
    Write-Host "app.css Error: $($_.Exception.Message)"
}

try {
    $r2 = Invoke-WebRequest -Uri 'http://localhost:55353/_content/TrackerGanadero.Shared/css/bootstrap/bootstrap.min.css' -UseBasicParsing
    Write-Host "`nBootstrap Status: $($r2.StatusCode)"
    Write-Host "Bootstrap Length: $($r2.Content.Length) chars"
} catch {
    Write-Host "`nBootstrap Error: $($_.Exception.Message)"
}

try {
    $r3 = Invoke-WebRequest -Uri 'http://localhost:55353/TrackerGanadero.Web.styles.css' -UseBasicParsing
    Write-Host "`nStyles bundle Status: $($r3.StatusCode)"
    Write-Host "Styles content: $($r3.Content.Substring(0, [Math]::Min(200, $r3.Content.Length)))"
} catch {
    Write-Host "`nStyles bundle Error: $($_.Exception.Message)"
}
