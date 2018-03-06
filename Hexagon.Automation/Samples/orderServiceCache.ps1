$host.ui.RawUI.WindowTitle = "OrderService-cache"

cd $PSScriptRoot

Import-Module .\Hexagon.Automation.psd1

$script = {
    param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

    if (!$resources) { Write-Host "resources is null !" }
    if (!$messageSystem) { Write-Host "messageSystem is null !" }

    Import-Module .\Hexagon.Automation.psd1
    
    $id = $message.orderAction.orderId

    $cache = $resources.cache
    if (!$cache.ContainsKey($id)) { 
        $result = $messageSystem | Send-XmlMessageAndAwaitResponse -Message ([xml]('<orderAction type="get" orderId="{0}" fromCache="true" />' -f $id))
        if ($result) { $cache[$id] = $result }
        else { return $null }
    }
    $cache[$id]
}

$patterns = @(
[pscustomobject]@{
    Id = 'orderServiceCache'
    Pattern =  @('/orderAction[@type = "get"]', '/orderAction[not(@fromCache = "true")]')
    Script = $script
})

$resources = @(
[pscustomobject]@{
    Id = 'orderServiceCache'
    Constructor = { 
        write-host "Creating cache resource..."
        @{ cache = @{} }
    }
    Destructor = {
        param([Hashtable]$resources)
        write-host "Clearing cache resource..."
        $resources.Clear()
    }
})

cls
Start-XmlMessageSystem -NodeConfig orderServiceCache.xml -MessagePatterns $patterns -Resources $resources


