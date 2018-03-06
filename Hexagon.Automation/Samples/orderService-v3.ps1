$host.ui.RawUI.WindowTitle = "Order service v3"

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
},

[pscustomobject]@{
    Pattern = @('/orderAction[@type = "get"]')

    Script = {
        param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

        "Received a request to get order with id = {0}" -f $message.orderAction.orderId | Write-Host
        "Order to get : {0}" -f $message.orderAction.orderId | Write-Host

        $order = $resources.orders | where Id -eq $message.orderAction.orderId
        if (!$order) { return $null }

        sleep -Seconds 1

@'
            <order id="{0}">
                <instrument>{1}</instrument>
                <quantity>{2}</quantity>
                <side>{3}</side>
            </order>
'@ -f $order.id, $order.instrument, $order.quantity, $order.side
    }

    Id = 'getOrder'
},

[pscustomobject]@{
    Pattern = @('/orderAction[@type = "getCompetitiveQuotes"]')

    Script = {
        param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

        Import-Module .\Hexagon.Automation.psd1
    
        1..10 | % { $messageSystem | send-xmlmessage -Message ([xml]'<requestCompetitiveQuotes />') -Sender $self }
    }

    Id = 'getCompetitiveQuotes'
},

[pscustomobject]@{
    Pattern = @('/competitiveQuotes')

    Script = {
        param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

        $message.competitiveQuotes.competitiveQuote | % { "Competitive quote received from {2} : {0} => {1}" -f $_.from, $_.price, $sender.Path }
    }

    Id = 'getCompetitiveQuotes'

    Secondary = $true
},

[pscustomobject]@{
    Pattern = @('/requestCompetitiveQuotes')

    Script = {
        param([xml]$message, $sender, $self, [Hashtable]$resources, $messageSystem)

        sleep -Seconds 1

@'
            <competitiveQuotes from="{0}">
                <competitiveQuote from="x" price="10.1" />
                <competitiveQuote from="y" price="10.3" />
                <competitiveQuote from="z" price="9.5" />
            </competitiveQuotes>
'@ -f $self.Path
    }

    Id = 'competitiveQuotes'
}
)

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
},
[pscustomobject]@{
    Id = 'getOrder'
    Constructor = { 
        write-host "Reading persisted orders..."
        $orders = Get-Content -Path .\orders.csv | ConvertFrom-CSV
        @{ orders = $orders }
    }
    Destructor = {
        param([Hashtable]$resources)
        $resources.Clear()
    }
}
)


cls

Start-XmlMessageSystem -NodeConfig orderService-v3.xml -MessagePatterns $patterns -Resources $resources
