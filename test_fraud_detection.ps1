# ==============================================================================
# SENSEFIN - HACKATHON JURI DEMO SCRIPT'I (V2.0)
# Ozellikler: HMAC Auth, 4-Katmanli Filtre Testleri (Blacklist, AI, Rules, Redis)
# ==============================================================================

$secretKey = "SenseFin_Dev_Secret_2026"
$baseUrl = "http://localhost:5000"

function Send-SenseFinRequest {
    param([string]$Body, [string]$TestName)
    
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " TEST: $TestName" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Middleware'in bekledigi bosluksuz payload formati
    $normalized = $Body -replace '\s', ''
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
        }
        catch {
            Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
        }
        return $null
    }
}

Write-Host "=================================================================" -ForegroundColor Magenta
Write-Host " 🚀 SENSEFIN CANLI JURI DEMOSU BASLIYOR (4 KATMANLI KORUMA)" -ForegroundColor Magenta
Write-Host "=================================================================" -ForegroundColor Magenta

# ------------------------------------------------------------------------------
# TEST 1: YAPAY ZEKA BASARI TESTI (MASUM ALMAN USULU BORC)
# ------------------------------------------------------------------------------
# Amac: AI'in normal bir arkadaslar arasi odeme istegini (PaymentRequest) 
# dolandiriciliktan ayirt edebildigini gostermek.
$body1 = '{"money":{"amount":250,"currency":"TRY"},"transactionType":"PaymentRequest","senderDeviceId":"DEV_AHMET_PHONE","senderAccountId":"PERS_CLEAN_AHMET","receiverAccountId":"PERS_VICTIM_MEHMET","description":"Dun aksamki yemek borcum kanka alman usulu","receiverIban":"TR560001000200030004000501","typingScore":10,"tremorScore":12}'
Send-SenseFinRequest -Body $body1 -TestName "1. [AI Layer] GUVENLI: Alman Usulu Borc Talebi"

Start-Sleep -Seconds 2

# ------------------------------------------------------------------------------
# TEST 2: STATIK KURAL MOTORU (SIRKET TAKLIDI / SEMANTIC MISMATCH)
# ------------------------------------------------------------------------------
# Amac: Alici hesap kurumsal degilken (IsCorporate=false), aciklamaya "Siparis No, Fatura"
# yazilirsa kural motorunun %88 Risk Floor (Taban) uyguladigini gostermek.
$body2 = '{"money":{"amount":12500,"currency":"TRY"},"transactionType":"Transfer","senderDeviceId":"DEV_AHMET_PHONE","senderAccountId":"PERS_CLEAN_AHMET","receiverAccountId":"PERS_VICTIM_MEHMET","description":"Siparis No: 89452 iPhone 15 Pro Fatura Bedeli","receiverIban":"TR560001000200030004000502","typingScore":15,"tremorScore":10}'
Send-SenseFinRequest -Body $body2 -TestName "2. [Rule Engine] DOLANDIRICILIK: Sirket Taklidi Filtresi"

Start-Sleep -Seconds 2

# ------------------------------------------------------------------------------
# TEST 3: AI & KURAL MOTORU KOMBOSI (OLTALAMA - PAYMENT REQUEST FRAUD)
# ------------------------------------------------------------------------------
# Amac: Oltalama (Phishing) kelimeleri iceren bir odeme istegini engellemek.
$body3 = '{"money":{"amount":4500,"currency":"TRY"},"transactionType":"PaymentRequest","senderDeviceId":"DEV_HACKER_PC","senderAccountId":"PERS_UNKNOWN_SCAMMER","receiverAccountId":"PERS_VICTIM_AYSE","description":"Tebrikler kazandiniz! Ucret iadesi icin islemi onaylayiniz.","receiverIban":"TR330000000000000000000033","typingScore":85,"tremorScore":90}'
Send-SenseFinRequest -Body $body3 -TestName "3. [AI & Rules] DOLANDIRICILIK: Odeme Istegiyle Oltalama"

Start-Sleep -Seconds 2

# ------------------------------------------------------------------------------
# TEST 4: BLACKLIST ISTIHBARATI (ZEYNEP'IN BLOKE IBAN'I)
# ------------------------------------------------------------------------------
# Amac: Seeder tarafindan "FraudConfirmed" isaretlenen TR99...99 IBAN'ina
# para gonderilmeye calisildiginda sistemin direkt %100 riskle blokladigini gostermek.
$body4 = '{"money":{"amount":100,"currency":"TRY"},"transactionType":"Transfer","senderDeviceId":"DEV_AHMET_PHONE","senderAccountId":"PERS_CLEAN_AHMET","receiverAccountId":"PERS_FRAUD_ZEYNEP","description":"Normal aciklama, fark etmez.","receiverIban":"TR990000000000000000000099","typingScore":10,"tremorScore":10}'
Send-SenseFinRequest -Body $body4 -TestName "4. [DB Blacklist] KESIN ENGEL: Kara Listeye Alinmis IBAN/Hesap"

Start-Sleep -Seconds 2

# ------------------------------------------------------------------------------
# TEST 5: REDIS VELOCITY CHECK (DDOS / BOT SALDIRISI)
# ------------------------------------------------------------------------------
# Amac: AI maliyetini korumak icin saniyede gelen ardilsik bot taleplerinin 
# (60 saniyede 5+ istek) Redis tarafindan %95 riskle aninda kesildigini gostermek.
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " TEST 5: [Redis Layer] VELOCITY LIMIT (Bot/Spam Saldirisi)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
$body5 = '{"money":{"amount":10,"currency":"TRY"},"transactionType":"Transfer","senderDeviceId":"DEV_BOT","senderAccountId":"PERS_BOT_ACC","receiverAccountId":"PERS_CLEAN_AHMET","description":"spam test","receiverIban":"TR000000000000000000000000","typingScore":99,"tremorScore":99}'

for ($i = 1; $i -le 6; $i++) {
    Write-Host " Istek $i atiliyor..." -NoNewline
    $resp = Send-SenseFinRequest -Body $body5 -TestName "Redis Hiz Limiti - Istek $i"
    # 6. istekte Redis limitine takilip yuksek risk donmeli.
}

Write-Host "`n============================================================" -ForegroundColor Magenta
Write-Host " DEMO BASARIYLA TAMAMLANDI - SENSEFIN GOES TO FINALS!" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host " Beklenen Sonuclar:" -ForegroundColor White
Write-Host "   Test 1 (AI Masum Borc)    : Risk ~%25 (Green/Yellow)" -ForegroundColor Green
Write-Host "   Test 2 (Sirket Taklidi)   : Risk %88+ (Red)" -ForegroundColor Red
Write-Host "   Test 3 (Oltalama/Phishing): Risk %95+ (Red)" -ForegroundColor Red
Write-Host "   Test 4 (Kara Liste - DB)  : Risk %100 (Red, AI'a gitmeden engellendi)" -ForegroundColor Red
Write-Host "   Test 5 (Redis Velocity)   : 6. Istekte Risk %95 (Red, Cost-Optimized)" -ForegroundColor Red
Write-Host ""