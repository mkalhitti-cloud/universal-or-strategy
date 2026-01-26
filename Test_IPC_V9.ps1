$port = 5000
$server = "127.0.0.1"
$message = "LONG|ES"

try {
    $client = New-Object System.Net.Sockets.TcpClient($server, $port)
    $stream = $client.GetStream()
    $writer = New-Object System.IO.StreamWriter($stream)
    
    $writer.Write($message)
    $writer.Flush()
    
    Write-Host "Sent: $message to $server:$port"
    
    $client.Close()
} catch {
    Write-Error "Failed to connect to V9 Hub at $server:$port. Is the strategy running?"
}
