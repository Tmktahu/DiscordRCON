$folders = @(
  "$env:VRISING_SERVER_PATH/BepInEx/interop"
)

$outFile = "ReferenceItemGroup.txt"

'<ItemGroup>' | Set-Content -Path $outFile -Encoding UTF8
foreach ($folder in $folders) {
  Get-ChildItem -Path $folder -Filter *.dll | ForEach-Object {
    $dllName = $_.BaseName
    $dllPath = $_.FullName.Replace('\', '/')
    $refLine1 = "  <Reference Include='" + $dllName + "'>"
    $refLine2 = "    <HintPath>$(VRisingServerPath)/BepInEx/interop/" + $dllName + ".dll</HintPath>"
    $refLine3 = "    <Private>False</Private>"
    $refLine4 = "  </Reference>"
    [System.IO.File]::AppendAllText($outFile, $refLine1 + "`r`n")
    [System.IO.File]::AppendAllText($outFile, $refLine2 + "`r`n")
    [System.IO.File]::AppendAllText($outFile, $refLine3 + "`r`n")
    [System.IO.File]::AppendAllText($outFile, $refLine4 + "`r`n")
  }
}
[System.IO.File]::AppendAllText($outFile, '</ItemGroup>`r`n')
Write-Output "ReferenceItemGroup.txt generated. Copy its contents into your .csproj file."
