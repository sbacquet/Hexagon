$host.ui.RawUI.WindowTitle = "Order service"

cd $PSScriptRoot

Import-Module .\Hexagon.Automation.psd1

$patterns = @(
[pscustomobject]@{
    Pattern = @('/orderAction[@type = "create"]')

    Script = {
        param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

        "Received a request to create order, req id = {0}" -f $message.orderAction.requestId | Write-Host
        "Order to create : {0}" -f $message.orderAction.order.InnerXml | Write-Host
@'
            <orderCreated>
                <requestId>{0}</requestId>
                <orderId>{1}</orderId>
            </orderCreated>
'@ -f $message.orderAction.requestId, 0
    }

    Id = 'createOrder'
})

cls

Start-XmlMessageSystem -NodeConfig orderService.xml -MessagePatterns $patterns
