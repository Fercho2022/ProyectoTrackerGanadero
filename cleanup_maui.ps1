$servicesDir = 'c:\ProyectoTrackerGanadero\TrackerGanaderoBlazorhibridMaui\Services'
$keepFiles = @('MauiTokenStorageService.cs', 'MauiGeolocationService.cs', 'MauiTextToSpeechService.cs')

Get-ChildItem $servicesDir -Filter '*.cs' | Where-Object { $keepFiles -notcontains $_.Name } | ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "Deleted: $($_.Name)"
}
Write-Host "Done!"
