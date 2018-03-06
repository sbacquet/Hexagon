$host.ui.RawUI.WindowTitle = "Client"

cd $PSScriptRoot

Import-Module .\Hexagon.Automation.psd1

cls

$sys = Start-XmlMessageSystemClient -NodeConfig $PSScriptRoot\client.xml

function Create-Order-message
{
    param([int]$quantity, [string]$instrument)

@'
    <orderAction type="create">
        <requestId>{0}</requestId>
        <order>
            <instrument>{2}</instrument>
            <quantity>{1}</quantity>
        </order>
    </orderAction>
'@ -f (New-Guid), $quantity, $instrument
}

function Create-Order-with-side-message
{
    param([int]$quantity, [string]$instrument, [string]$side)

@'
    <orderAction type="create">
        <requestId>{0}</requestId>
        <order>
            <side>{3}</side>
            <instrument>{2}</instrument>
            <quantity>{1}</quantity>
        </order>
    </orderAction>
'@ -f (New-Guid), $quantity, $instrument, $side
}

function Create-Order-v2-message
{
    param([int]$quantity, [string]$instrument, [string]$side)

    if ($side) { $sideXml = "<side>$side</side>" }

@'
    <orderAction type="create" version="2">
        <requestId>{0}</requestId>
        <order>
            {3}
            <instrument>{2}</instrument>
            <quantity>{1}</quantity>
        </order>
    </orderAction>
'@ -f (New-Guid), $quantity, $instrument, $sideXml
}

filter Get-Order
{
    param([Parameter(Mandatory = $true, ValueFromPipeline = $True)][int]$id)

    [xml]$message = '<orderAction type="get" orderId="{0}" />' -f $id

    Send-XmlMessageAndAwaitResponse -System $sys -Message $message | select -ExpandProperty order
}

function Get-AllOrders
{
    1..10 | % { Send-XmlMessageAndAwaitResponse -System $sys -Message ([xml]('<orderAction type="get" orderId="{0}" />' -f $_)) | select -ExpandProperty order }
}

function Ask-CompetitiveQuotes
{
    [xml]$message = '<orderAction type="getCompetitiveQuotes" />'

    Send-XmlMessage -System $sys -Message $message
}

function Stop
{
    $sys | Stop-XmlMessageSystem
    exit
}
