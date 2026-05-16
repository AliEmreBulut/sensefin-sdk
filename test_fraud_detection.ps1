# SenseFin Fraud Detection Test Script
# Fixes: Uses UTC timestamp, body without extra whitespace

$secretKey = "SenseFin_Dev_Secret_2026"
$baseUrl = "http://localhost:5000"

function Send-SenseFinRequest {
    param([string]$Body, [string]$TestName)
    
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " TEST: $TestName" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    $normalized = $Body -replace '\s',''
    $ts = [int64](([DateTimeOffset]::UtcNow).ToUnixTimeSeconds())
    $payload = "$normalized.$ts"
    
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secretKey)
    $hashBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($payload))
    $sig = [Convert]::ToBase64String($hashBytes)
    
    $headers = @{
        "Content-Type"         = "application/json"
        "X-SenseFin-Signature" = $sig
        "X-SenseFin-Timestamp" = "$ts"
    }
    
    try {
        $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($Body)
        $resp = Invoke-RestMethod -Uri "$baseUrl/api/transactions/analyze" -Method Post -Body $bodyBytes -Headers $headers
        
        $color = if ($resp.riskScore -ge 80) { "Red" } elseif ($resp.riskScore -ge 40) { "Yellow" } else { "Green" }
        Write-Host "  Risk Score  : $($resp.riskScore)" -ForegroundColor $color
        Write-Host "  Risk Level  : $($resp.riskLevel)" -ForegroundColor $color
        Write-Host "  Is High Risk: $($resp.isHighRisk)"
        Write-Host "  AI Reason   : $($resp.aiReason)"
        Write-Host "  Tx ID       : $($resp.transactionId)"
        return $resp
    }
    catch {
        try {
            $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            Write-Host "  ERROR: $($sr.ReadToEnd())" -ForegroundColor Red
        } catch {
            Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
        }
        return $null
    }
}

Write-Host "============================================================" -ForegroundColor Magenta
Write-Host " SenseFin - Odeme Istegi Dolandiricilik Korumasi Test Suite" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta

# Test 1: PaymentRequest + yaniltici aciklama = DOLANDIRICILIK
$body1 = '{"money":{"amount":500,"currency":"TRY"},"transactionType":"PaymentRequest","senderDeviceId":"device-victim-001","senderAccountId":"victim-account-222","receiverAccountId":"scammer-account-111","description":"Onayladiginizda hesabiniza para yatacaktir. Lutfen onaylayin.","receiverIban":"TR330006100519786457841111","typingScore":72,"tremorScore":65}'
Send-SenseFinRequest -Body $body1 -TestName "DOLANDIRICILIK - PaymentRequest + Yaniltici Aciklama"

Start-Sleep -Seconds 2

# Test 2: Guvenli normal transfer
$body2 = '{"money":{"amount":150,"currency":"TRY"},"transactionType":"P2PTransfer","senderDeviceId":"device-user-004","senderAccountId":"safe-account-111","receiverAccountId":"friend-account-222","description":"Yemek borcu","receiverIban":"TR330006100519786457841000"}'
Send-SenseFinRequest -Body $body2 -TestName "GUVENLI - Normal Dusuk Tutarli Transfer"

Start-Sleep -Seconds 2

# Test 3: Normal Transfer + coklu supheli aciklama
$body3 = '{"money":{"amount":1500,"currency":"TRY"},"transactionType":"WireTransfer","senderDeviceId":"device-user-003","senderAccountId":"user-account-333","receiverAccountId":"suspicious-account-666","description":"Kazandiniz! Odulunuz hesabiniza yatacak, onaylayin.","receiverIban":"TR330006100519786457841555"}'
Send-SenseFinRequest -Body $body3 -TestName "SUPHELI ACIKLAMA - Normal Transfer + Coklu Kalip"

Start-Sleep -Seconds 2

# Test 4: 2. dolandiricilik girisimi (ayni scammer - risk profili artmis olmali)
$body4 = '{"money":{"amount":2000,"currency":"TRY"},"transactionType":"PaymentRequest","senderDeviceId":"device-victim-005","senderAccountId":"victim-account-999","receiverAccountId":"scammer-account-111","description":"Geri odeme yapilacaktir, onaylamaniz halinde hesabiniza para yatacaktir.","receiverIban":"TR330006100519786457841111","typingScore":80,"tremorScore":75}'
Send-SenseFinRequest -Body $body4 -TestName "2. DOLANDIRICILIK - Ayni Scammer Tekrar (scammer-account-111)"

Write-Host "`n============================================================" -ForegroundColor Magenta
Write-Host " TESTLER TAMAMLANDI" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host " Beklenen:" -ForegroundColor White
Write-Host "   Test 1: Risk >= 90 (PaymentRequest + fraud desc)" -ForegroundColor Yellow
Write-Host "   Test 2: Risk ~10 (guvenli, dusuk tutar)" -ForegroundColor Green
Write-Host "   Test 3: Risk >= 80 (coklu supheli aciklama)" -ForegroundColor Yellow
Write-Host "   Test 4: Risk >= 90 (ayni scammer, profili yukselir)" -ForegroundColor Yellow
Write-Host ""
Write-Host " ONEMLI: scammer-account-789 risk profili guncellenmeli," -ForegroundColor Red
Write-Host "         victim hesaplari ETKiLENMEMELi." -ForegroundColor Red
