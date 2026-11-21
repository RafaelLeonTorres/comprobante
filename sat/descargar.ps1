<#
Descarga los archivos oficiales del SAT que usa Comprobante y los deja en esta carpeta (sat/):

  esquemas/    cfdv40.xsd y los esquemas que importa (catCFDI.xsd, tdCFDI.xsd), más el del Timbre Fiscal Digital
  xslt/        cadenaoriginal_4_0.xslt y todo lo que incluye, más el del Timbre Fiscal Digital
  origen/      lo que pesa o se transforma y no va a git: el Excel de catálogos (de aquí salen sat/catalogos),
               la matriz de errores (sat/matriz), los certificados de prueba completos y las guías en PDF
  fuentes.json de dónde salió cada archivo, cuándo se descargó y su SHA-256

sat/csd-prueba/ (los certificados de prueba que usa la aplicación) es una selección hecha a mano de
origen/Certificados_de_Prueba: este script no la toca.

Uso (Windows PowerShell o PowerShell 7), desde la raíz del repositorio:

  powershell -NoProfile -ExecutionPolicy Bypass -File sat\descargar.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File sat\descargar.ps1 -Catalogos catCFDI_V_4_20261120.xls

Los nombres de los Excel cambian con cada publicación; los vigentes se ven en
http://omawww.sat.gob.mx/tramitesyservicios/Paginas/anexo_20.htm
#>
param(
  [string]$Catalogos = 'catCFDI_V_4_20261120.xls',
  [string]$Matriz = 'MatrizDeErrores_CFDI_v40_20260325.xls'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36'
$sat = $PSScriptRoot
$portal = 'http://omawww.sat.gob.mx/tramitesyservicios/Paginas/documentos/'
$registro = New-Object System.Collections.ArrayList
$fallos = New-Object System.Collections.ArrayList

function Descargar([string]$url, [string]$destino) {
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destino) | Out-Null
  for ($i = 1; $i -le 3; $i++) {
    try {
      Invoke-WebRequest -Uri $url -OutFile $destino -UseBasicParsing -TimeoutSec 300 -UserAgent $ua
      $archivo = Get-Item -LiteralPath $destino
      $hash = (Get-FileHash -LiteralPath $destino -Algorithm SHA256).Hash.ToLowerInvariant()
      $relativo = $destino.Substring($sat.Length + 1).Replace('\', '/')
      [void]$registro.Add([ordered]@{
          archivo    = $relativo
          url        = $url
          descargado = (Get-Date).ToString('yyyy-MM-dd')
          bytes      = $archivo.Length
          sha256     = $hash
        })
      Write-Host ('OK    {0}  ({1:N0} bytes)' -f $url, $archivo.Length)
      return $true
    } catch {
      Write-Host ('falla {0} (intento {1}): {2}' -f $url, $i, $_.Exception.Message)
      Start-Sleep -Seconds 2
    }
  }
  [void]$fallos.Add($url)
  return $false
}

# Baja un archivo y todo lo que referencia (xs:import y xs:include en los esquemas; xsl:include y
# xsl:import en las hojas XSLT), guardándolos todos en la misma carpeta.
function DescargarConReferencias([string]$urlRaiz, [string]$carpeta, [string]$etiquetas, [string]$atributo) {
  $pendientes = New-Object System.Collections.Queue
  $vistos = @{}
  $nombres = @{}
  $pendientes.Enqueue($urlRaiz)
  while ($pendientes.Count -gt 0) {
    $url = $pendientes.Dequeue()
    if ($vistos.ContainsKey($url)) { continue }
    $vistos[$url] = $true
    $nombre = [IO.Path]::GetFileName(([Uri]$url).AbsolutePath)
    if ($nombres.ContainsKey($nombre)) {
      Write-Host ('AVISO: {0} y {1} tienen el mismo nombre; el segundo no se descarga' -f $nombres[$nombre], $url)
      continue
    }
    $nombres[$nombre] = $url
    $destino = Join-Path $carpeta $nombre
    if (-not (Descargar $url $destino)) { continue }
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($destino)
    $consulta = "//*[($etiquetas) and @$atributo]"
    foreach ($nodo in $doc.SelectNodes($consulta)) {
      $ref = $nodo.GetAttribute($atributo)
      $absoluta = ([Uri]::new([Uri]$url, $ref)).AbsoluteUri
      if (-not $vistos.ContainsKey($absoluta)) { $pendientes.Enqueue($absoluta) }
    }
  }
}

Write-Host '== Esquemas (XSD)'
DescargarConReferencias 'http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd' (Join-Path $sat 'esquemas') "local-name()='import' or local-name()='include'" 'schemaLocation'
[void](Descargar 'https://www.sat.gob.mx/sitio_internet/cfd/TimbreFiscalDigital/TimbreFiscalDigitalv11.xsd' (Join-Path $sat 'esquemas\TimbreFiscalDigitalv11.xsd'))

Write-Host '== Cadena original (XSLT)'
DescargarConReferencias 'http://www.sat.gob.mx/sitio_internet/cfd/4/cadenaoriginal_4_0/cadenaoriginal_4_0.xslt' (Join-Path $sat 'xslt') "local-name()='include' or local-name()='import'" 'href'
[void](Descargar 'https://www.sat.gob.mx/sitio_internet/cfd/TimbreFiscalDigital/cadenaoriginal_TFD_1_1.xslt' (Join-Path $sat 'xslt\cadenaoriginal_TFD_1_1.xslt'))

Write-Host '== Certificados de prueba'
$zip = Join-Path $sat 'origen\Certificados_de_Prueba.zip'
if (Descargar ($portal + 'Certificados_de_Prueba.zip') $zip) {
  $destinoCsd = Join-Path $sat 'origen\Certificados_de_Prueba'
  if (Test-Path -LiteralPath $destinoCsd) { Remove-Item -LiteralPath $destinoCsd -Recurse -Force }
  Expand-Archive -LiteralPath $zip -DestinationPath $destinoCsd -Force
}

Write-Host '== Catálogos y matriz de errores'
[void](Descargar ($portal + $Catalogos) (Join-Path $sat ('origen\' + $Catalogos)))
[void](Descargar ($portal + $Matriz) (Join-Path $sat ('origen\' + $Matriz)))

Write-Host '== Guías (de ellas salen las reglas de redondeo y de llenado)'
[void](Descargar ($portal + 'Anexo_20_Guia_de_llenado_CFDI.pdf') (Join-Path $sat 'origen\Anexo_20_Guia_de_llenado_CFDI.pdf'))
[void](Descargar ($portal + 'PregFrecCFDIVer4_0.pdf') (Join-Path $sat 'origen\PregFrecCFDIVer4_0.pdf'))

$json = $registro | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $sat 'fuentes.json'), $json + "`n", (New-Object System.Text.UTF8Encoding $false))

Write-Host ''
Write-Host ('Descargados: {0}. Fallos: {1}.' -f $registro.Count, $fallos.Count)
foreach ($f in $fallos) { Write-Host ('  FALLÓ ' + $f) }
if ($fallos.Count -gt 0) { exit 1 }
