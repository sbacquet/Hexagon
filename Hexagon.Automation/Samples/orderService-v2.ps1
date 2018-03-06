$host.ui.RawUI.WindowTitle = "Order service v2"

cd $PSScriptRoot

Import-Module .\Hexagon.Automation.psd1

$patterns = @(
[pscustomobject]@{
    Pattern = @('/orderAction[@type = "create"]', '/orderAction/order/side | /orderAction[@version >= 2]')

    Script = {
        param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

        "Received a request to create order, req id = {0}" -f $message.orderAction.requestId | Write-Host
        "Order to create : {0}" -f $message.orderAction.order.InnerXml | Write-Host

        $side = $message.orderAction.order.side
        if (-not $side) { $side = 'Buy' }

@'
            <orderCreated side="{2}">
                <requestId>{0}</requestId>
                <orderId>{1}</orderId>
            </orderCreated>
'@ -f $message.orderAction.requestId, $resources.orderIdSeq++, $side
    }

    Id = 'createOrder'
})

$resources = @(
[pscustomobject]@{
    Id = 'createOrder'
    Constructor = { 
        write-host "Creating sequence for orders..."
        [int]$seq = Get-Content -Path .\orderSequence.txt
        @{ orderIdSeq = $seq }
    }
    Destructor = {
        param([Hashtable]$resources)
        write-host "Clearing resources for order creation..."
        Set-Content -Value $resources.orderIdSeq -Path .\orderSequence.txt
        $resources.Clear()
    }
})

cls

Start-XmlMessageSystem -NodeConfig orderService-v2.xml -MessagePatterns $patterns -Resources $resources
