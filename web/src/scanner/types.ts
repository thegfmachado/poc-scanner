export type ScannerDriver = 'twain' | 'wia'
export type ScanMode = 'native-ui' | 'silent'
export type PaperSource = 'flatbed' | 'feeder' | 'duplex'
export type ColorMode = 'color' | 'grayscale' | 'black-white'

export interface ProviderStatus {
  available: boolean
  paired: boolean
  version?: string
  message: string
}

export interface ScannerDevice {
  id: string
  name: string
  driver: ScannerDriver
}

export interface ScanOptions {
  deviceId: string
  driver: ScannerDriver
  mode: ScanMode
  paperSource: PaperSource
  colorMode: ColorMode
  dpi: number
}

export interface ScanPage {
  id: string
  number: number
  contentType: string
  width: number
  height: number
}

export interface ScanSession {
  id: string
  status: 'pending' | 'scanning' | 'completed' | 'cancelled' | 'failed'
  pages: ScanPage[]
  error?: string
}

export interface ScannerProvider {
  readonly id: 'local-agent'
  readonly name: string
  getStatus(): Promise<ProviderStatus>
  pair(code: string): Promise<void>
  listDevices(driver: ScannerDriver): Promise<ScannerDevice[]>
  startScan(options: ScanOptions): Promise<ScanSession>
  getSession(sessionId: string): Promise<ScanSession>
  cancelScan(sessionId: string): Promise<void>
  pageUrl(sessionId: string, pageId: string): string
  downloadUrl(sessionId: string, format: 'pdf' | 'images'): string
}