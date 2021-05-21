$PSVersionTable.PSVersion
$env:computername
#Expand-Archive ..\drop\AI4Bharat.ISLBot.Services.zip -DestinationPath ..\other -Force
#copy ..\other\Content\D_C\a\1\s\src\AI4Bharat.ISLBot.Services\obj\Release\net48\win7-x86\PubTmp\Out\* ..\Release

$isRunning = (Get-Process -Name 'AI4Bharat.ISLBot.Services').Count -gt 0
$isRunning
If(!$isRunning)
{
 Stop-Process -Name 'AI4Bharat.ISLBot.Services'
}
Start-Process -FilePath C:\FTP_BOT\bot\AI4Bharat.ISLBot.Services.exe
