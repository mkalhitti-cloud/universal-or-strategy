$port = 5000
$symbol = "MGC"
$command = "LONG|$symbol"

try {
    $client = New-Object System.Net.Sockets.TcpClient("localhost", $port)
    $stream = $client.GetStream()
    $data = [System.Text.Encoding]::UTF8.GetBytes($command)
    $stream.Write($data, 0, $data.Length)
    $client.Close()
    Write-Host "✅ Test Command Sent: $command" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to connect to port $port. Is the strategy enabled?" -ForegroundColor Red
}
