param($installPath, $toolsPath, $package, $project)

$publish = New-Object System.EnterpriseServices.Internal.Publish
$dlls = @( dir "$installPath\lib\*\*.dll" | select -ExpandProperty FullName ) + 
        @( dir "$(Split-Path $installPath)\Codeless.Core.*\lib\*\*.dll" | select -ExpandProperty FullName )
        
$dlls | % {
    Write-Verbose "Install $($_.Name) to GAC..."
    $publish.GacInstall($_.Name)
}

Write-Verbose "Installing PowerShell module to user folder..."

$psd1dir = "$env:UserProfile\WindowsPowerShell\Modules\Codeless.SharePoint"
mkdir $psd1dir -ErrorAction SilentlyContinue
cp "$toolsPath\Codeless.SharePoint.psd1" $psd1dir