$host.ui.RawUI.WindowTitle = "Listener"

cd $PSScriptRoot

Import-Module .\Hexagon.Automation.psd1

$script = {
param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

"Message: $($message.InnerXml)"
}

$patterns = @(
[pscustomobject]@{
    Pattern =  @('*')

    Script = {
        param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

        "Message: $($message.InnerXml)"
    }

    Id = 'listener'

    Secondary = $true
}
)

cls
Start-XmlMessageSystem -NodeConfig listener.xml -MessagePatterns $patterns
