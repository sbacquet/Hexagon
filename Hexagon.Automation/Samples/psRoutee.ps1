param([int]$Num)

$host.ui.RawUI.WindowTitle = "Routee $Num"

cd $PSScriptRoot

Import-Module .\Hexagon.Automation.psd1

cls
Start-XmlMessageSystem -NodeConfig "psRoutee$Num.xml"
