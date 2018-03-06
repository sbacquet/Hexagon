$host.ui.RawUI.WindowTitle = "Order service FIXED"

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
'@ -f $message.orderAction.requestId, $resources.orderIdSeq++

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

Start-XmlMessageSystem -NodeConfig orderService-FIXED.xml -MessagePatterns $patterns -Resources $resources
