<#
================================================================================
 Secret-Santa browser driver (Chrome DevTools Protocol over PowerShell)
================================================================================
 WHY THIS EXISTS
   The `playwright` MCP server was down (CONNECTION_CLOSED) and there is no Node
   runtime on this machine, so neither the playwright MCP nor `chromium-cli`
   worked. This drives real headless Chrome directly over CDP from Windows
   PowerShell (System.Net.WebSockets) — no extra dependencies beyond Chrome + .NET.

   This is a raw, validated tooling script. To wrap it into a proper project
   skill, run  /run-skill-generator  (that command is reserved for you to run;
   it cannot be invoked by the assistant).

 1) START CHROME (once). Desktop, for the account-free app over http:
      & "C:\Program Files\Google\Chrome\Application\chrome.exe" `
        --headless=new --remote-debugging-port=9222 --remote-allow-origins=* `
        --user-data-dir="<scratch>\cp" --window-size=1280,900 `
        --no-first-run --no-default-browser-check --disable-gpu about:blank
    For the HTTPS Aspire stack add:  --ignore-certificate-errors --allow-insecure-localhost
    For a phone pass use:            --window-size=400,850
    Each --user-data-dir is an isolated "device" (own localStorage) — use
    separate dirs/ports (9222/9223/9224/9225) to simulate creator vs joiner.

 2) START THE APP
    Standalone (account-free, quick):
      dotnet run --project src/Apps/SecretSanta/CircleTools.SecretSanta.WebApp --launch-profile http   # http://localhost:5081
    Full stack (account, sync, shared-by-link — needs Docker for Postgres+Mailpit):
      dotnet run --project src/Aspire/CircleTools.AppHost --launch-profile https
      # secretsanta-web https://localhost:7081 | main-api https://localhost:7180
      # main-web https://localhost:7080 | dashboard https://localhost:17073 | Mailpit http://localhost:8025
      # (the http AppHost profile is rejected unless ASPIRE_ALLOW_UNSECURED_TRANSPORT=true)

 3) RUN A FLOW:  .\cdpflow.ps1 -StepsFile flow.json [-Port 9222]
    flow.json is a JSON array of steps, run in order over one CDP connection:
      { "navigate": "http://localhost:5081/" }   full page load (fragment-only
                                                 changes do NOT reload — add a
                                                 dummy ?query to force a reload)
      { "toolkit": true }                        (re)inject window.__t helpers
      { "wait": 1500 }                           sleep ms (WASM boot ~6s; the
                                                 first cold API call can take ~8-10s)
      { "eval": "<js, awaited, returnByValue>" } run JS; use __t.setId/setSel/
                                                 click/clickText/txt/count/url
      { "clickxy": "<css selector>" }            REAL mouse click at element
                                                 centre — required for MudSelect,
                                                 MudRadio, MudDatePicker (they
                                                 ignore synthetic .click())
      { "shot": "C:\\abs\\path.png" }            screenshot — ALWAYS look at it
      { "console": true }                        dump console warnings/errors
                                                 (INFO noise is counted, not shown)
      { "label": "text" }                        marker in the output

 GOTCHAS LEARNED (do not re-discover):
   - Read the steps file as UTF-8 (done below). PowerShell 5.1 Get-Content reads
     UTF-8-without-BOM as ANSI and turns "ü" into "Ã¼" — a FALSE Unicode "bug".
   - MudBlazor buttons uppercase text via CSS; match with textContent + case-
     insensitive (clickText already does). MudSelect/MudRadio/MudMenu items need
     clickxy, not .click(). MudTextField ids map to the inner <input>, but a
     MudSelect's real <input> is zero-size — clickxy its .mud-input-control-input-container.
   - Blazor <form> submit: use  document.querySelector('form').requestSubmit()  —
     clicking the submit button via .click() does not trigger @onsubmit.
   - Reveal/date format follows the browser REGION, not the UI language (ADR-0056);
     e.g. dd/MM/yyyy here. UI language is set in Settings.
   - When copying tokens/links, extract them programmatically from the DOM — never
     from a screenshot (l/I/1 look identical and cause phantom 404s).
   - The app logs at INFO on every action incl. "Authorization failed
     (DenyAnonymousAuthorizationRequirement)" for anonymous calls — that is noise,
     not an error. The console step filters it out.
================================================================================
#>
param(
  [Parameter(Mandatory=$true)][string]$StepsFile,  # JSON array of steps
  [int]$Port = 9222
)

$ErrorActionPreference = 'Stop'

$list = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/json" -TimeoutSec 10
$page = $list | Where-Object { $_.type -eq 'page' } | Select-Object -First 1
if (-not $page) { throw "No page target" }

$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ws.Options.KeepAliveInterval = [TimeSpan]::FromSeconds(20)
$ct = [System.Threading.CancellationToken]::None
$ws.ConnectAsync([Uri]$page.webSocketDebuggerUrl, $ct).Wait()

$script:msgId = 0
$script:consoleMsgs = New-Object System.Collections.ArrayList

function Recv-One {
  param($TimeoutSec = 30)
  $buffer = New-Object byte[] 65536
  $sb = New-Object System.Text.StringBuilder
  do {
    $rseg = New-Object System.ArraySegment[byte] (,$buffer)
    $res = $ws.ReceiveAsync($rseg, $ct); $res.Wait($TimeoutSec*1000) | Out-Null
    if (-not $res.IsCompleted) { throw "recv timeout" }
    $r = $res.Result
    [void]$sb.Append([System.Text.Encoding]::UTF8.GetString($buffer, 0, $r.Count))
  } while (-not $r.EndOfMessage)
  return $sb.ToString()
}

function Send-Cdp {
  param([string]$Method, [hashtable]$Params = @{}, [int]$TimeoutSec = 30)
  $script:msgId++; $id = $script:msgId
  $payload = @{ id = $id; method = $Method; params = $Params } | ConvertTo-Json -Depth 20 -Compress
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
  $seg = New-Object System.ArraySegment[byte] (,$bytes)
  $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $ct).Wait()
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  while ((Get-Date) -lt $deadline) {
    $text = Recv-One -TimeoutSec $TimeoutSec
    try { $obj = $text | ConvertFrom-Json } catch { continue }
    if ($obj.method -eq 'Runtime.consoleAPICalled') {
      $t = $obj.params.type
      $a = ($obj.params.args | ForEach-Object { if ($null -ne $_.value) { $_.value } else { $_.description } }) -join ' '
      [void]$script:consoleMsgs.Add("[$t] $a")
      continue
    }
    if ($obj.method -eq 'Runtime.exceptionThrown') {
      $d = $obj.params.exceptionDetails.exception.description
      if (-not $d) { $d = $obj.params.exceptionDetails.text }
      [void]$script:consoleMsgs.Add("[pageerror] $d")
      continue
    }
    if ($obj.id -eq $id) { return $obj }
  }
  throw "CDP timeout waiting for $Method"
}

[void](Send-Cdp -Method 'Page.enable')
[void](Send-Cdp -Method 'Runtime.enable')

$toolkitJs = @"
window.__t=(function(){function setNative(el,v){var p=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype;var s=Object.getOwnPropertyDescriptor(p,'value').set;s.call(el,v);el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));}
return{setId:function(id,v){var el=document.getElementById(id);if(!el)return'NO_EL:'+id;setNative(el,v);return'OK';},
setSel:function(sel,v){var el=document.querySelector(sel);if(!el)return'NO_EL:'+sel;setNative(el,v);return'OK';},
click:function(sel){var el=document.querySelector(sel);if(!el)return'NO_EL:'+sel;el.click();return'OK';},
clickText:function(txt){var t=txt.toLowerCase();var els=[].slice.call(document.querySelectorAll('button,a,[role=button],.mud-nav-link,li,summary,.mud-list-item,.mud-menu-item'));var m=els.find(function(e){return (e.textContent||'').toLowerCase().indexOf(t)>=0;});if(!m)return'NO_TEXT:'+txt;m.click();return'OK:'+(m.textContent||'').trim().slice(0,50);},
txt:function(sel){var el=document.querySelector(sel||'body');return el?(el.innerText||'').trim():'NO_EL';},
count:function(sel){return document.querySelectorAll(sel).length;},
url:function(){return location.href;}};})();'TOOLKIT_READY'
"@

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$steps = [System.IO.File]::ReadAllText($StepsFile, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
foreach ($step in $steps) {
  if ($step.label)    { "--- $($step.label) ---" }
  if ($step.navigate) { [void](Send-Cdp -Method 'Page.navigate' -Params @{ url = $step.navigate }); "NAV: $($step.navigate)" }
  if ($step.toolkit)  { $r = Send-Cdp -Method 'Runtime.evaluate' -Params @{ expression = $toolkitJs; returnByValue = $true }; "TOOLKIT: $($r.result.result.value)" }
  if ($step.clickxy) {
    # A real mouse click at the element's centre — needed for controls (MudSelect) that ignore synthetic .click().
    $expr = "(function(){var el=document.querySelector('" + ($step.clickxy -replace "'","\'") + "');if(!el)return null;var r=el.getBoundingClientRect();return JSON.stringify({x:Math.round(r.left+r.width/2),y:Math.round(r.top+r.height/2)});})()"
    $r = Send-Cdp -Method 'Runtime.evaluate' -Params @{ expression = $expr; returnByValue = $true }
    if ($r.result.result.value) {
      $pt = $r.result.result.value | ConvertFrom-Json
      [void](Send-Cdp -Method 'Input.dispatchMouseEvent' -Params @{ type='mouseMoved'; x=$pt.x; y=$pt.y })
      [void](Send-Cdp -Method 'Input.dispatchMouseEvent' -Params @{ type='mousePressed'; x=$pt.x; y=$pt.y; button='left'; clickCount=1 })
      [void](Send-Cdp -Method 'Input.dispatchMouseEvent' -Params @{ type='mouseReleased'; x=$pt.x; y=$pt.y; button='left'; clickCount=1 })
      "CLICKXY: ($($pt.x),$($pt.y)) $($step.clickxy)"
    } else { "CLICKXY: NO_EL $($step.clickxy)" }
  }
  if ($null -ne $step.wait -and $step.wait -gt 0) { Start-Sleep -Milliseconds ([int]$step.wait) }
  if ($step.eval) {
    $r = Send-Cdp -Method 'Runtime.evaluate' -Params @{ expression = $step.eval; returnByValue = $true; awaitPromise = $true; userGesture = $true }
    if ($r.result.exceptionDetails) {
      "EVAL_EXCEPTION: " + ($r.result.exceptionDetails.exception.description)
    } else {
      $val = $r.result.result.value
      if ($null -ne $val) {
        if ($val -is [string]) { "EVAL: $val" } else { "EVAL: " + ($val | ConvertTo-Json -Depth 12 -Compress) }
      } else { "EVAL: (type=$($r.result.result.type))" }
    }
  }
  if ($step.shot) {
    $r = Send-Cdp -Method 'Page.captureScreenshot' -Params @{ format = 'png'; captureBeyondViewport = $true }
    [System.IO.File]::WriteAllBytes($step.shot, [System.Convert]::FromBase64String($r.result.data))
    "SHOT: $($step.shot)"
  }
  if ($step.console) {
    # Filter out the app's INFO/log noise (it logs every query + an authz-failed line per anonymous call);
    # surface only warnings, errors and page exceptions, which is what "console should stay clean" means.
    $important = $script:consoleMsgs | Where-Object { $_ -notmatch '^\[(info|log|debug|verbose)\]' }
    $infoCount = $script:consoleMsgs.Count - @($important).Count
    if (@($important).Count -eq 0) { "CONSOLE: clean of warnings/errors ($infoCount info/log lines)" }
    else { "CONSOLE: $infoCount info/log lines; notable:"; $important | ForEach-Object { "  $_" } }
    $script:consoleMsgs.Clear()
  }
}

try { $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", $ct).Wait(2000) } catch {}
$ws.Dispose()
