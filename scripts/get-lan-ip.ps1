function Test-UsableIpv4([string]$ipAddress) {
    if ($ipAddress -notmatch '^\d{1,3}(\.\d{1,3}){3}$') {
        return $false
    }

    $octets = $ipAddress.Split('.') | ForEach-Object { [int]$_ }
    return $octets.Count -eq 4 -and
        ($octets | Where-Object { $_ -lt 0 -or $_ -gt 255 }).Count -eq 0 -and
        $octets[0] -ne 0 -and
        $octets[0] -ne 127 -and
        -not ($octets[0] -eq 169 -and $octets[1] -eq 254)
}

$candidate = $null

foreach ($line in (ipconfig)) {
    if ($line -match 'IPv4.*?(?<ip>\d{1,3}(?:\.\d{1,3}){3})') {
        $candidate = $Matches.ip
        continue
    }

    if ($candidate -and $line -match 'Default Gateway.*?(?<gateway>\d{1,3}(?:\.\d{1,3}){3})') {
        if (Test-UsableIpv4 $candidate) {
            $candidate
            break
        }
    }
}
