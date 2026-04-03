<#
.SYNOPSIS
    Smoke test script for LLMGateway.

.DESCRIPTION
    Reads the admin API key from appsettings.json, exercises every endpoint,
    and reports a pass/fail summary. Cleans up all resources it creates.

.PARAMETER BaseUrl
    Base URL of the running gateway. Defaults to http://localhost:5273

.PARAMETER AppSettings
    Path to appsettings.json. Defaults to src/LLMGateway/appsettings.json

.EXAMPLE
    .\smoke-test.ps1
    .\smoke-test.ps1 -BaseUrl http://localhost:8080
    .\smoke-test.ps1 -TestLlm   # also exercise real LLM calls (requires valid provider API keys)
#>
param(
    [string]$BaseUrl     = "http://localhost:5273",
    [string]$AppSettings = "src/LLMGateway/appsettings.json",
    [switch]$TestLlm                       # run actual upstream LLM calls
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Helpers ──────────────────────────────────────────────────────────────────

$passCount = 0
$failCount = 0

function Write-Pass([string]$label) {
    Write-Host "  [PASS] $label" -ForegroundColor Green
    $script:passCount++
}

function Write-Fail([string]$label, [string]$detail = "") {
    $msg = "  [FAIL] $label"
    if ($detail) { $msg += " — $detail" }
    Write-Host $msg -ForegroundColor Red
    $script:failCount++
}

function Invoke-Api {
    param(
        [string]$Method   = "GET",
        [string]$Path,
        [string]$Token    = "",
        [hashtable]$Body  = $null,
        [int]$Expect      = 200
    )

    $uri     = "$BaseUrl$Path"
    $headers = @{ "Content-Type" = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $params = @{
        Method             = $Method
        Uri                = $uri
        Headers            = $headers
        StatusCodeVariable = "statusCode"
        SkipHttpErrorCheck = $true
    }
    if ($Body) { $params["Body"] = ($Body | ConvertTo-Json -Depth 10) }

    $response   = Invoke-RestMethod @params
    $statusCode = [int]$statusCode
    return [pscustomobject]@{ Status = $statusCode; Body = $response }
}

# Sends a raw (potentially malformed) string body – used to test invalid-JSON rejection.
function Invoke-RawPost {
    param(
        [string]$Path,
        [string]$Token = "",
        [string]$RawBody = ""
    )
    $uri     = "$BaseUrl$Path"
    $headers = @{ "Content-Type" = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $resp = Invoke-WebRequest -Method POST -Uri $uri -Headers $headers -Body $RawBody `
        -SkipHttpErrorCheck -ErrorAction Stop
    return [pscustomobject]@{ Status = [int]$resp.StatusCode; Body = $resp.Content }
}

function Test-Endpoint {
    param(
        [string]$Label,
        [string]$Method = "GET",
        [string]$Path,
        [string]$Token  = "",
        [hashtable]$Body = $null,
        [int]$Expect    = 200,
        [scriptblock]$Assert = $null
    )

    try {
        $r = Invoke-Api -Method $Method -Path $Path -Token $Token -Body $Body -Expect $Expect
        if ($r.Status -ne $Expect) {
            Write-Fail $Label "Expected HTTP $Expect, got $($r.Status)"
            $script:LastResponseBody = $null
            return
        }
        if ($Assert) {
            $result = & $Assert $r.Body
            if ($result -eq $false) {
                Write-Fail $Label "Assertion failed"
                $script:LastResponseBody = $null
                return
            }
        }
        Write-Pass "$Label (HTTP $($r.Status))"
        $script:LastResponseBody = $r.Body
    }
    catch {
        Write-Fail $Label $_.Exception.Message
        $script:LastResponseBody = $null
    }
}

# ── Read admin key from appsettings.json ──────────────────────────────────────

Write-Host "`nReading configuration from $AppSettings ..." -ForegroundColor Cyan

if (-not (Test-Path $AppSettings)) {
    Write-Host "ERROR: $AppSettings not found. Run from the repo root." -ForegroundColor Red
    exit 1
}

$config   = Get-Content $AppSettings -Raw | ConvertFrom-Json
$adminKey = $config.Gateway.AdminApiKeys |
    Where-Object { $_.IsActive -eq $true } |
    Select-Object -First 1 -ExpandProperty Key

if (-not $adminKey) {
    Write-Host "ERROR: No active AdminApiKey found in $AppSettings" -ForegroundColor Red
    exit 1
}

Write-Host "  Admin key: $($adminKey.Substring(0, [Math]::Min(12, $adminKey.Length)))..." -ForegroundColor DarkGray

# ── Cleanup tracker ──────────────────────────────────────────────────────────

$createdProviderId = $null
$createdApiKeyId   = $null
$generatedUserKey  = $null

# ── Tests ────────────────────────────────────────────────────────────────────

Write-Host "`n=== Health ===" -ForegroundColor Cyan

Test-Endpoint -Label "GET /health (no auth)" `
    -Path "/health" `
    -Expect 200 `
    -Assert { param($b) $b.status -eq "ok" }


Write-Host "`n=== Auth Enforcement ===" -ForegroundColor Cyan

Test-Endpoint -Label "GET /v1/models without auth → 401" `
    -Path "/v1/models" `
    -Expect 401

Test-Endpoint -Label "GET /admin/providers without auth → 401" `
    -Path "/admin/providers" `
    -Expect 401

Test-Endpoint -Label "GET /admin/providers with bad key → 401" `
    -Path "/admin/providers" `
    -Token "sk-wrong-key" `
    -Expect 401


Write-Host "`n=== Admin: Provider CRUD ===" -ForegroundColor Cyan

Test-Endpoint -Label "GET /admin/providers (admin key)" `
    -Path "/admin/providers" `
    -Token $adminKey `
    -Expect 200 `
    -Assert { param($b) $b -is [array] }

$created = Test-Endpoint -Label "POST /admin/providers → 201" `
    -Method "POST" `
    -Path "/admin/providers" `
    -Token $adminKey `
    -Body @{
        name     = "SmokeTest-Provider"
        baseUrl  = "https://smoke-test.example.com"
        apiKey   = "sk-smoke-test-key"
        models   = @("smoke-model-1", "smoke-model-2")
        isEnabled = $true
    } `
    -Expect 201

if ($script:LastResponseBody) {
    $createdProviderId = $script:LastResponseBody.id
    Write-Host "      Created provider id=$createdProviderId" -ForegroundColor DarkGray

    Test-Endpoint -Label "GET /admin/providers/$createdProviderId" `
        -Path "/admin/providers/$createdProviderId" `
        -Token $adminKey `
        -Expect 200 `
        -Assert { param($b) $b.name -eq "SmokeTest-Provider" }

    Test-Endpoint -Label "PUT /admin/providers/$createdProviderId (rename)" `
        -Method "PUT" `
        -Path "/admin/providers/$createdProviderId" `
        -Token $adminKey `
        -Body @{ name = "SmokeTest-Provider-Updated" } `
        -Expect 200 `
        -Assert { param($b) $b.name -eq "SmokeTest-Provider-Updated" }
}

Test-Endpoint -Label "GET /admin/providers/99999 (not found) → 404" `
    -Path "/admin/providers/99999" `
    -Token $adminKey `
    -Expect 404

Test-Endpoint -Label "POST /admin/providers (duplicate name) → 409" `
    -Method "POST" `
    -Path "/admin/providers" `
    -Token $adminKey `
    -Body @{
        name    = "SmokeTest-Provider-Updated"
        baseUrl = "https://smoke-test.example.com"
        apiKey  = "sk-smoke-dup"
        models  = @()
    } `
    -Expect 409


Write-Host "`n=== Admin: API Key Management ===" -ForegroundColor Cyan

Test-Endpoint -Label "GET /admin/apikeys" `
    -Path "/admin/apikeys" `
    -Token $adminKey `
    -Expect 200 `
    -Assert { param($b) $b -is [array] }

$keyCreated = Test-Endpoint -Label "POST /admin/apikeys → 201" `
    -Method "POST" `
    -Path "/admin/apikeys" `
    -Token $adminKey `
    -Body @{ name = "smoke-test-user-key" } `
    -Expect 201

if ($script:LastResponseBody) {
    $createdApiKeyId  = $script:LastResponseBody.id
    $generatedUserKey = $script:LastResponseBody.key
    Write-Host "      Created api key id=$createdApiKeyId prefix=$($script:LastResponseBody.keyPrefix)" -ForegroundColor DarkGray

    Test-Endpoint -Label "GET /admin/apikeys/$createdApiKeyId" `
        -Path "/admin/apikeys/$createdApiKeyId" `
        -Token $adminKey `
        -Expect 200 `
        -Assert { param($b) $b.name -eq "smoke-test-user-key" -and $b.isActive -eq $true }

    Test-Endpoint -Label "PUT /admin/apikeys/$createdApiKeyId (rename)" `
        -Method "PUT" `
        -Path "/admin/apikeys/$createdApiKeyId" `
        -Token $adminKey `
        -Body @{ name = "smoke-test-user-key-renamed" } `
        -Expect 200 `
        -Assert { param($b) $b.name -eq "smoke-test-user-key-renamed" }
}

Test-Endpoint -Label "POST /admin/apikeys (missing name) → 400" `
    -Method "POST" `
    -Path "/admin/apikeys" `
    -Token $adminKey `
    -Body @{ name = "" } `
    -Expect 400


Write-Host "`n=== User API (with generated key) ===" -ForegroundColor Cyan

if ($generatedUserKey) {
    Test-Endpoint -Label "GET /v1/models (user key)" `
        -Path "/v1/models" `
        -Token $generatedUserKey `
        -Expect 200 `
        -Assert { param($b) $b.data -is [array] -and $b.data.Count -gt 0 }

    Test-Endpoint -Label "GET /admin/providers with user key → 401 (isolation)" `
        -Path "/admin/providers" `
        -Token $generatedUserKey `
        -Expect 401

    # Deactivate the key and verify it is rejected
    Test-Endpoint -Label "PUT /admin/apikeys/$createdApiKeyId (deactivate)" `
        -Method "PUT" `
        -Path "/admin/apikeys/$createdApiKeyId" `
        -Token $adminKey `
        -Body @{ isActive = $false } `
        -Expect 200

    Test-Endpoint -Label "GET /v1/models with deactivated key → 401" `
        -Path "/v1/models" `
        -Token $generatedUserKey `
        -Expect 401
}
else {
    Write-Host "  [SKIP] User API tests skipped (no generated key)" -ForegroundColor Yellow
}


# ── Chat Completions ─────────────────────────────────────────────────────────

Write-Host "`n=== Chat Completions: Validation ==" -ForegroundColor Cyan
# These tests only check gateway-level validation; no real upstream call needed.

# Re-activate the generated key (it was deactivated in the User API section above)
if ($createdApiKeyId -and $generatedUserKey) {
    Invoke-Api -Method "PUT" -Path "/admin/apikeys/$createdApiKeyId" `
        -Token $adminKey -Body @{ isActive = $true } | Out-Null
}

# Auth check
Test-Endpoint -Label "POST /v1/chat/completions without auth → 401" `
    -Method "POST" `
    -Path "/v1/chat/completions" `
    -Expect 401

# Invalid JSON
try {
    $r = Invoke-RawPost -Path "/v1/chat/completions" -Token $generatedUserKey -RawBody '{bad json'
    if ($r.Status -eq 400) { Write-Pass "POST /v1/chat/completions invalid JSON → 400 (HTTP 400)" }
    else { Write-Fail "POST /v1/chat/completions invalid JSON → 400" "Got HTTP $($r.Status)" }
} catch { Write-Fail "POST /v1/chat/completions invalid JSON → 400" $_.Exception.Message }

# Missing model field
Test-Endpoint -Label "POST /v1/chat/completions missing model → 400" `
    -Method "POST" `
    -Path "/v1/chat/completions" `
    -Token $generatedUserKey `
    -Body @{ messages = @(@{ role = "user"; content = "hi" }) } `
    -Expect 400 `
    -Assert { param($b) $b.error.code -eq "missing_model" }

# Unknown model
Test-Endpoint -Label "POST /v1/chat/completions unknown model → 404" `
    -Method "POST" `
    -Path "/v1/chat/completions" `
    -Token $generatedUserKey `
    -Body @{ model = "no-such-model-xyz"; messages = @(@{ role = "user"; content = "hi" }) } `
    -Expect 404 `
    -Assert { param($b) $b.error.code -eq "model_not_found" }

# Upstream unreachable (smoke provider has a fake base URL; gateway should surface 5xx)
if ($createdProviderId -and $generatedUserKey) {
    $r2 = Invoke-Api -Method "POST" -Path "/v1/chat/completions" -Token $generatedUserKey `
        -Body @{ model = "smoke-model-1"; messages = @(@{ role = "user"; content = "hi" }) }
    if ($r2.Status -ge 500 -and $r2.Status -lt 600) {
        Write-Pass "POST /v1/chat/completions unreachable upstream → $($r2.Status) (5xx)"
    } else {
        Write-Fail "POST /v1/chat/completions unreachable upstream" "Expected 5xx, got $($r2.Status)"
    }
}

# ── Anthropic Messages API ────────────────────────────────────────────────────

Write-Host "`n=== Anthropic Messages: Validation ===" -ForegroundColor Cyan

# Auth check (no x-api-key, no Bearer)
Test-Endpoint -Label "POST /v1/messages without auth -> 401" `
    -Method "POST" `
    -Path "/v1/messages" `
    -Expect 401

# Auth with x-api-key header
try {
    $anthropicAuthUri     = "$BaseUrl/v1/messages"
    $anthropicAuthHeaders = @{
        "x-api-key"     = $generatedUserKey
        "Content-Type"  = "application/json"
    }
    $anthropicAuthBody    = @{
        model       = "no-such-model-xyz"
        max_tokens  = 10
        messages    = @(@{ role = "user"; content = "hi" })
    } | ConvertTo-Json -Depth 10

    $authResp = Invoke-WebRequest -Method POST -Uri $anthropicAuthUri `
        -Headers $anthropicAuthHeaders -Body $anthropicAuthBody -SkipHttpErrorCheck -ErrorAction Stop
    if ([int]$authResp.StatusCode -eq 404) {
        Write-Pass "POST /v1/messages with x-api-key auth (HTTP 404 = auth passed, model not found)"
    } else {
        Write-Fail "POST /v1/messages with x-api-key auth" "Expected 404, got $($authResp.StatusCode)"
    }
} catch {
    Write-Fail "POST /v1/messages with x-api-key auth" $_.Exception.Message
}

# Invalid JSON
try {
    $rawR = Invoke-RawPost -Path "/v1/messages" -Token $generatedUserKey -RawBody '{bad json'
    if ($rawR.Status -eq 400) { Write-Pass "POST /v1/messages invalid JSON -> 400 (HTTP 400)" }
    else { Write-Fail "POST /v1/messages invalid JSON -> 400" "Got HTTP $($rawR.Status)" }
} catch { Write-Fail "POST /v1/messages invalid JSON -> 400" $_.Exception.Message }

# Missing model field
Test-Endpoint -Label "POST /v1/messages missing model -> 400" `
    -Method "POST" `
    -Path "/v1/messages" `
    -Token $generatedUserKey `
    -Body @{ max_tokens = 10; messages = @(@{ role = "user"; content = "hi" }) } `
    -Expect 400

# Unknown model
Test-Endpoint -Label "POST /v1/messages unknown model -> 404" `
    -Method "POST" `
    -Path "/v1/messages" `
    -Token $generatedUserKey `
    -Body @{ model = "no-such-model-xyz"; max_tokens = 10; messages = @(@{ role = "user"; content = "hi" }) } `
    -Expect 404

# Upstream unreachable
if ($createdProviderId -and $generatedUserKey) {
    $r3 = Invoke-Api -Method "POST" -Path "/v1/messages" -Token $generatedUserKey `
        -Body @{ model = "smoke-model-1"; max_tokens = 10; messages = @(@{ role = "user"; content = "hi" }) }
    if ($r3.Status -ge 500 -and $r3.Status -lt 600) {
        Write-Pass "POST /v1/messages unreachable upstream -> $($r3.Status) (5xx)"
    } else {
        Write-Fail "POST /v1/messages unreachable upstream" "Expected 5xx, got $($r3.Status)"
    }
}


# ── Real LLM calls (opt-in via -TestLlm) ─────────────────────────────────────

if ($TestLlm) {
    Write-Host "`n=== Chat Completions: Real LLM Calls (-TestLlm) ===" -ForegroundColor Cyan

    $placeholderPattern = 'YOUR_|_KEY_HERE|_HERE$|^sk-YOUR'
    $liveProviders = $config.Gateway.Providers |
        Where-Object { $_.ApiKey -notmatch $placeholderPattern -and $_.Models.Count -gt 0 }

    if (-not $liveProviders) {
        Write-Host "  [SKIP] No providers with real API keys found in $AppSettings" -ForegroundColor Yellow
    } else {
        # Re-activate user key (may have been deactivated above)
        Invoke-Api -Method "PUT" -Path "/admin/apikeys/$createdApiKeyId" `
            -Token $adminKey -Body @{ isActive = $true } | Out-Null

        foreach ($provider in $liveProviders) {
            $model = $provider.Models[0]
            Write-Host "  Testing provider '$($provider.Name)' model '$model' ..." -ForegroundColor DarkGray

            # ── OpenAI: Non-streaming ──────────────────────────────────────────
            $oaiNonStream = Test-Endpoint -Label "[$($provider.Name)] OpenAI non-streaming" `
                -Method "POST" `
                -Path "/v1/chat/completions" `
                -Token $generatedUserKey `
                -Body @{
                    model    = $model
                    messages = @(@{ role = "user"; content = "Reply with exactly one word: pong" })
                    max_tokens = 256
                } `
                -Expect 200 `
                -Assert { param($b) $b.choices -is [array] -and $b.choices.Count -gt 0 }
            if ($script:LastResponseBody) {
                $msg = $script:LastResponseBody.choices[0].message
                if ($msg.PSObject.Properties["reasoning_content"] -and $msg.reasoning_content) {
                    Write-Host "      Reasoning: $($msg.reasoning_content)" -ForegroundColor DarkGray
                }
                Write-Host "      Response: $($msg.content)" -ForegroundColor DarkGray
            }

            # ── OpenAI: Streaming ──────────────────────────────────────────────
            try {
                $streamUri     = "$BaseUrl/v1/chat/completions"
                $streamHeaders = @{
                    "Authorization" = "Bearer $generatedUserKey"
                    "Content-Type"  = "application/json"
                }
                $streamBody = @{
                    model       = $model
                    messages    = @(@{ role = "user"; content = "Reply with exactly one word: pong" })
                    stream      = $true
                    max_tokens  = 256
                } | ConvertTo-Json -Depth 10

                $streamResp = Invoke-WebRequest -Method POST -Uri $streamUri `
                    -Headers $streamHeaders -Body $streamBody -SkipHttpErrorCheck -ErrorAction Stop

                $ct     = $streamResp.Headers["Content-Type"]
                $hasSSE = $ct -and $ct -match "text/event-stream"
                $hasData = $streamResp.Content -match "data:"

                if ([int]$streamResp.StatusCode -eq 200 -and $hasSSE -and $hasData) {
                    Write-Pass "[$($provider.Name)] OpenAI streaming (HTTP 200, SSE)"
                    # Extract content from SSE data lines
                    $oaiStreamText = ($streamResp.Content -split "`n" |
                        Where-Object { $_ -match '^data: \{' } |
                        ForEach-Object {
                            $jsonStr = $_.Substring(6)
                            if ($jsonStr -ne '[DONE]') {
                                try { ($jsonStr | ConvertFrom-Json).choices[0].delta.content } catch {}
                            }
                        }) -join ''
                    Write-Host "      Response: $oaiStreamText" -ForegroundColor DarkGray
                } else {
                    Write-Fail "[$($provider.Name)] OpenAI streaming" `
                        "status=$($streamResp.StatusCode) content-type=$ct hasData=$hasData"
                }
            } catch {
                Write-Fail "[$($provider.Name)] OpenAI streaming" $_.Exception.Message
            }

            # ── Anthropic: Non-streaming ───────────────────────────────────────
            Write-Host "  Testing Anthropic format for provider '$($provider.Name)' model '$model' ..." -ForegroundColor DarkGray
            try {
                $antHeaders = @{
                    "x-api-key"         = $generatedUserKey
                    "anthropic-version" = "2023-06-01"
                    "Content-Type"      = "application/json"
                }
                $antBody = @{
                    model       = $model
                    max_tokens  = 256
                    messages    = @(@{ role = "user"; content = "Reply with exactly one word: pong" })
                } | ConvertTo-Json -Depth 10

                $antResp = Invoke-WebRequest -Method POST -Uri "$BaseUrl/v1/messages" `
                    -Headers $antHeaders -Body $antBody -SkipHttpErrorCheck -ErrorAction Stop

                if ([int]$antResp.StatusCode -eq 200) {
                    $antJson = $antResp.Content | ConvertFrom-Json
                    if ($antJson.type -eq "message" -and $antJson.content -is [array] -and $antJson.content.Count -gt 0) {
                        Write-Pass "[$($provider.Name)] Anthropic non-streaming (HTTP 200, type=message)"
                        $antText = ""
                        $antReasoning = ""
                        foreach ($block in $antJson.content) {
                            if ($null -ne $block.PSObject.Properties["text"]) {
                                $antText += $block.text
                            }
                            if ($null -ne $block.PSObject.Properties["reasoning_content"]) {
                                $antReasoning += $block.reasoning_content
                            }
                        }
                        if ($antReasoning) {
                            Write-Host "      Reasoning: $antReasoning" -ForegroundColor DarkGray
                        }
                        Write-Host "      Response: $antText" -ForegroundColor DarkGray
                    } else {
                        Write-Fail "[$($provider.Name)] Anthropic non-streaming" "Unexpected response structure: $($antResp.Content.Substring(0, [Math]::Min(200, $antResp.Content.Length)))"
                    }
                } else {
                    Write-Fail "[$($provider.Name)] Anthropic non-streaming" "Expected 200, got $($antResp.StatusCode)"
                }
            } catch {
                Write-Fail "[$($provider.Name)] Anthropic non-streaming" $_.Exception.Message
            }

            # ── Anthropic: Streaming ───────────────────────────────────────────
            try {
                $antStreamHeaders = @{
                    "x-api-key"         = $generatedUserKey
                    "anthropic-version" = "2023-06-01"
                    "Content-Type"      = "application/json"
                }
                $antStreamBody = @{
                    model       = $model
                    max_tokens  = 256
                    messages    = @(@{ role = "user"; content = "Reply with exactly one word: pong" })
                    stream      = $true
                } | ConvertTo-Json -Depth 10

                $antStreamResp = Invoke-WebRequest -Method POST -Uri "$BaseUrl/v1/messages" `
                    -Headers $antStreamHeaders -Body $antStreamBody -SkipHttpErrorCheck -ErrorAction Stop

                $antCt     = $antStreamResp.Headers["Content-Type"]
                $antHasSSE = $antCt -and $antCt -match "text/event-stream"
                $antHasEvt = $antStreamResp.Content -match "event: message_start"
                $antHasStop = $antStreamResp.Content -match "event: message_stop"

                if ([int]$antStreamResp.StatusCode -eq 200 -and $antHasSSE -and $antHasEvt -and $antHasStop) {
                    Write-Pass "[$($provider.Name)] Anthropic streaming (HTTP 200, SSE with message_start/message_stop)"
                    # Extract text deltas from Anthropic SSE
                    $antStreamText = ""
                    $antStreamThinking = ""
                    foreach ($line in ($antStreamResp.Content -split "`n")) {
                        if ($line -notmatch '^data: \{') { continue }
                        try {
                            $evt = ($line.Substring(6) | ConvertFrom-Json)
                            if ($null -eq $evt.delta) { continue }
                            if ($null -ne $evt.delta.PSObject.Properties["text"]) {
                                $antStreamText += $evt.delta.text
                            }
                            if ($null -ne $evt.delta.PSObject.Properties["thinking"]) {
                                $antStreamThinking += $evt.delta.thinking
                            }
                        } catch {}
                    }
                    if ($antStreamThinking) {
                        Write-Host "      Thinking: $antStreamThinking" -ForegroundColor DarkGray
                    }
                    Write-Host "      Response: $antStreamText" -ForegroundColor DarkGray
                } else {
                    Write-Fail "[$($provider.Name)] Anthropic streaming" `
                        "status=$($antStreamResp.StatusCode) content-type=$antCt hasStart=$antHasEvt hasStop=$antHasStop"
                }
            } catch {
                Write-Fail "[$($provider.Name)] Anthropic streaming" $_.Exception.Message
            }
        }
    }
} else {
    Write-Host "`n  Tip: run with -TestLlm to also test real upstream LLM calls (OpenAI + Anthropic format)." -ForegroundColor DarkGray
}

# ── Cleanup ───────────────────────────────────────────────────────────────────

Write-Host "`n=== Cleanup ===" -ForegroundColor Cyan

if ($createdApiKeyId) {
    Test-Endpoint -Label "DELETE /admin/apikeys/$createdApiKeyId" `
        -Method "DELETE" `
        -Path "/admin/apikeys/$createdApiKeyId" `
        -Token $adminKey `
        -Expect 204
}

if ($createdProviderId) {
    Test-Endpoint -Label "DELETE /admin/providers/$createdProviderId" `
        -Method "DELETE" `
        -Path "/admin/providers/$createdProviderId" `
        -Token $adminKey `
        -Expect 204

    Test-Endpoint -Label "GET /admin/providers/$createdProviderId after delete → 404" `
        -Path "/admin/providers/$createdProviderId" `
        -Token $adminKey `
        -Expect 404
}


# ── Summary ───────────────────────────────────────────────────────────────────

$total = $passCount + $failCount
Write-Host "`n═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Results: $passCount/$total passed" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })
if ($failCount -gt 0) {
    Write-Host "  $failCount test(s) FAILED" -ForegroundColor Red
}
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

# exit $(if ($failCount -eq 0) { 0 } else { 1 })
