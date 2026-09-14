import { useEffect, useState } from 'react'
import { Download, FileImage, LoaderCircle, MonitorCog, PlugZap, RefreshCw, ScanLine, Square } from 'lucide-react'
import { LocalAgentScannerProvider } from './scanner/LocalAgentScannerProvider'
import type { ColorMode, PaperSource, ProviderStatus, ScanMode, ScannerDevice, ScannerDriver, ScanSession } from './scanner/types'
import './App.css'

const provider = new LocalAgentScannerProvider()

function App() {
  const [status, setStatus] = useState<ProviderStatus | null>(null)
  const [pairingCode, setPairingCode] = useState('')
  const [driver, setDriver] = useState<ScannerDriver>('twain')
  const [devices, setDevices] = useState<ScannerDevice[]>([])
  const [deviceId, setDeviceId] = useState('')
  const [mode, setMode] = useState<ScanMode>('native-ui')
  const [paperSource, setPaperSource] = useState<PaperSource>('flatbed')
  const [colorMode, setColorMode] = useState<ColorMode>('color')
  const [dpi, setDpi] = useState(300)
  const [session, setSession] = useState<ScanSession | null>(null)
  const [selectedPage, setSelectedPage] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const sessionId = session?.id
  const sessionStatus = session?.status

  async function refreshStatus() {
    setStatus(null)
    setError('')
    const nextStatus = await provider.getStatus()
    setStatus(nextStatus)
  }

  async function pair() {
    setBusy(true)
    setError('')
    try {
      await provider.pair(pairingCode.trim())
      setPairingCode('')
      await refreshStatus()
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setBusy(false)
    }
  }

  async function startScan() {
    if (!deviceId) return
    setBusy(true)
    setError('')
    setSession(null)
    try {
      setSession(await provider.startScan({ deviceId, driver, mode, paperSource, colorMode, dpi }))
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setBusy(false)
    }
  }

  async function cancelScan() {
    if (!session) return
    try { await provider.cancelScan(session.id) } catch (caught) { setError(errorMessage(caught)) }
  }

  useEffect(() => {
    let active = true
    provider.getStatus().then((nextStatus) => {
      if (active) setStatus(nextStatus)
    })
    return () => { active = false }
  }, [])
  useEffect(() => {
    if (!status?.available || !status.paired) return
    let active = true
    provider.listDevices(driver).then((nextDevices) => {
      if (!active) return
      setDevices(nextDevices)
      setDeviceId((current) => nextDevices.some((item) => item.id === current) ? current : (nextDevices[0]?.id ?? ''))
    }).catch((caught: unknown) => {
      if (active) setError(errorMessage(caught))
    })
    return () => { active = false }
  }, [driver, status?.available, status?.paired])
  useEffect(() => {
    if (!sessionId || !sessionStatus || !['pending', 'scanning'].includes(sessionStatus)) return
    const timer = window.setInterval(async () => {
      try {
        const nextSession = await provider.getSession(sessionId)
        setSession(nextSession)
        if (nextSession.error) setError(nextSession.error)
      } catch (caught) { setError(errorMessage(caught)) }
    }, 900)
    return () => window.clearInterval(timer)
  }, [sessionId, sessionStatus])

  return (
    <main className="app-shell">
      <header className="topbar"><div className="brand-mark"><ScanLine size={22} /></div><div><strong>Scanner Lab</strong><span>POC de captura web</span></div><div className={`connection ${status?.available ? 'online' : ''}`}><span />{status?.available ? 'Agente online' : 'Agente offline'}</div></header>
      <section className="workspace">
        <aside className="controls">
          <div className="section-heading"><MonitorCog size={18} /><div><h2>Origem da captura</h2><p>Configure o agente local e o scanner.</p></div></div>
          <div className="agent-status"><div><span className={`status-dot ${status?.available ? 'online' : ''}`} /><strong>{status?.message ?? 'Verificando agente...'}</strong></div><button className="icon-button" onClick={() => void refreshStatus()} title="Atualizar status" aria-label="Atualizar status"><RefreshCw size={16} /></button></div>
          {status?.available && !status.paired && <div className="pairing"><label>Codigo exibido pelo agente<input value={pairingCode} onChange={(event) => setPairingCode(event.target.value)} placeholder="000000" maxLength={6} /></label><button className="secondary" disabled={busy || pairingCode.length < 6} onClick={() => void pair()}><PlugZap size={16} /> Parear</button></div>}
          <div className="divider" />
          <div className="segmented" aria-label="Driver"><button className={driver === 'twain' ? 'active' : ''} onClick={() => setDriver('twain')}>TWAIN</button><button className={driver === 'wia' ? 'active' : ''} onClick={() => setDriver('wia')}>WIA</button></div>
          <label>Scanner<select value={deviceId} onChange={(event) => setDeviceId(event.target.value)} disabled={!devices.length}><option value="">{devices.length ? 'Selecione' : 'Nenhum dispositivo encontrado'}</option>{devices.map((device) => <option key={device.id} value={device.id}>{device.name}</option>)}</select></label>
          <div className="field-row"><label>Origem<select value={paperSource} onChange={(event) => setPaperSource(event.target.value as PaperSource)}><option value="flatbed">Mesa</option><option value="feeder">Alimentador</option><option value="duplex">Duplex</option></select></label><label>Resolucao<select value={dpi} onChange={(event) => setDpi(Number(event.target.value))}><option value={150}>150 DPI</option><option value={300}>300 DPI</option><option value={600}>600 DPI</option></select></label></div>
          <label>Cor<select value={colorMode} onChange={(event) => setColorMode(event.target.value as ColorMode)}><option value="color">Colorido</option><option value="grayscale">Tons de cinza</option><option value="black-white">Preto e branco</option></select></label>
          <label className="toggle"><input type="checkbox" checked={mode === 'native-ui'} onChange={(event) => setMode(event.target.checked ? 'native-ui' : 'silent')} /><span /><div><strong>Interface do fabricante</strong><small>{mode === 'native-ui' ? 'O driver abrira sua janela nativa.' : 'Parametros definidos por esta tela.'}</small></div></label>
          {error && <div className="error-banner">{error}</div>}
          <div className="actions">{session && ['pending', 'scanning'].includes(session.status) ? <button className="danger" onClick={() => void cancelScan()}><Square size={15} fill="currentColor" /> Cancelar</button> : <button className="primary" disabled={busy || !status?.paired || !deviceId} onClick={() => void startScan()}>{busy ? <LoaderCircle className="spin" size={18} /> : <ScanLine size={18} />} Digitalizar</button>}</div>
        </aside>
        <section className="preview-area">
          <div className="preview-toolbar"><div><h1>Documento</h1><span>{session?.pages.length ?? 0} pagina(s) · {session?.status ?? 'aguardando'}</span></div>{session?.status === 'completed' && <div className="download-actions"><a className="secondary" href={provider.downloadUrl(session.id, 'images')}><FileImage size={16} /> Imagens</a><a className="primary compact" href={provider.downloadUrl(session.id, 'pdf')}><Download size={16} /> PDF</a></div>}</div>
          <div className="canvas-zone">{session?.pages.length ? <img src={provider.pageUrl(session.id, session.pages[selectedPage]?.id ?? session.pages[0].id)} alt={`Pagina ${selectedPage + 1}`} /> : <div className="empty-state"><div className="scan-frame"><ScanLine size={42} /></div><h2>Nenhuma pagina capturada</h2><p>Conecte o agente, escolha o scanner e inicie a digitalizacao.</p></div>}</div>
          <div className="filmstrip">{session?.pages.map((page, index) => <button key={page.id} className={selectedPage === index ? 'selected' : ''} onClick={() => setSelectedPage(index)}><img src={provider.pageUrl(session.id, page.id)} alt="" /><span>{page.number}</span></button>)}</div>
        </section>
      </section>
    </main>
  )
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Ocorreu uma falha inesperada.'
}

export default App
