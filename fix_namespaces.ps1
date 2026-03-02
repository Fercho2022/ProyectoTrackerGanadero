$basePath = 'c:\ProyectoTrackerGanadero\TrackerGanaderoBlazorhibridMaui\TrackerGanadero.Shared'
$files = Get-ChildItem $basePath -Recurse -Include '*.cs','*.razor'

foreach ($f in $files) {
    $content = [System.IO.File]::ReadAllText($f.FullName)
    $original = $content

    $content = $content -replace 'TrackerGanaderoBlazorHibridMaui\.Services', 'TrackerGanadero.Shared.Services'
    $content = $content -replace 'TrackerGanaderoBlazorHibridMaui\.Models', 'TrackerGanadero.Shared.Models'
    $content = $content -replace 'TrackerGanaderoBlazorHibridMaui\.Shared', 'TrackerGanadero.Shared.Shared'
    $content = $content -replace 'TrackerGanaderoBlazorHibridMaui\.Data', 'TrackerGanadero.Shared.Models'
    $content = $content -replace 'namespace TrackerGanaderoBlazorHibridMaui', 'namespace TrackerGanadero.Shared'
    $content = $content -replace 'using TrackerGanaderoBlazorHibridMaui;', 'using TrackerGanadero.Shared;'
    $content = $content -replace '@using TrackerGanaderoBlazorHibridMaui', '@using TrackerGanadero.Shared'

    if ($content -ne $original) {
        [System.IO.File]::WriteAllText($f.FullName, $content)
        Write-Host "Updated: $($f.Name)"
    }
}
Write-Host "Done!"
