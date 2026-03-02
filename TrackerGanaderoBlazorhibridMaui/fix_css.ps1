$base = 'c:\ProyectoTrackerGanadero\TrackerGanaderoBlazorhibridMaui\TrackerGanadero.Shared\wwwroot\css'
$src = Join-Path $base 'css'

if (Test-Path $src) {
    # Copy app.css
    Copy-Item -Path (Join-Path $src 'app.css') -Destination $base -Force

    # Copy bootstrap folder
    Copy-Item -Path (Join-Path $src 'bootstrap') -Destination $base -Recurse -Force

    # Copy open-iconic folder
    Copy-Item -Path (Join-Path $src 'open-iconic') -Destination $base -Recurse -Force

    # Remove the duplicate css/css directory
    Remove-Item -Path $src -Recurse -Force

    Write-Host "CSS files moved successfully"
} else {
    Write-Host "Source directory not found: $src"
}
