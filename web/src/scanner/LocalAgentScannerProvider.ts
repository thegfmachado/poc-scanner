import type { ProviderStatus, ScanOptions, ScannerDevice, ScannerDriver, ScannerProvider, ScanSession } from './types'

const DEFAULT_AGENT_URL = 'http://127.0.0.1:17841'

export class LocalAgentScannerProvider implements ScannerProvider {
  readonly id = 'local-agent' as const
  readonly name = 'Agente local (.NET + NAPS2)'
  private token = sessionStorage.getItem('scanner-agent-token') ?? ''
  private readonly baseUrl: string

  constructor(baseUrl = DEFAULT_AGENT_URL) {
    this.baseUrl = baseUrl
  }

  async getStatus(): Promise<ProviderStatus> {
    try {
      const response = await fetch(`${this.baseUrl}/api/health`, { headers: this.headers() })
      if (!response.ok) throw new Error('Agente indisponivel')
      return this.readJson<ProviderStatus>(response)
    } catch {
      return { available: false, paired: false, message: 'Inicie o executavel do agente nesta maquina.' }
    }
  }

  async pair(code: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/pair`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code }),
    })
    const result = await this.readJson<{ token: string }>(response)
    this.token = result.token
    sessionStorage.setItem('scanner-agent-token', result.token)
  }

  listDevices(driver: ScannerDriver): Promise<ScannerDevice[]> {
    return this.request(`/api/devices?driver=${driver}`)
  }

  startScan(options: ScanOptions): Promise<ScanSession> {
    return this.request('/api/scans', { method: 'POST', body: JSON.stringify(options) })
  }

  getSession(sessionId: string): Promise<ScanSession> {
    return this.request(`/api/scans/${sessionId}`)
  }

  async cancelScan(sessionId: string): Promise<void> {
    await this.request(`/api/scans/${sessionId}/cancel`, { method: 'POST' })
  }

  pageUrl(sessionId: string, pageId: string): string {
    return `${this.baseUrl}/api/scans/${sessionId}/pages/${pageId}?access_token=${encodeURIComponent(this.token)}`
  }

  downloadUrl(sessionId: string, format: 'pdf' | 'images'): string {
    return `${this.baseUrl}/api/scans/${sessionId}/export?format=${format}&access_token=${encodeURIComponent(this.token)}`
  }

  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, { ...init, headers: this.headers(init?.headers) })
    return this.readJson<T>(response)
  }

  private headers(additional?: HeadersInit): HeadersInit {
    return {
      'Content-Type': 'application/json',
      ...(this.token ? { Authorization: `Bearer ${this.token}` } : {}),
      ...additional,
    }
  }

  private async readJson<T>(response: Response): Promise<T> {
    if (!response.ok) {
      const body = await response.json().catch(() => null) as { message?: string } | null
      throw new Error(body?.message ?? `Falha no agente (${response.status})`)
    }
    return response.json() as Promise<T>
  }
}