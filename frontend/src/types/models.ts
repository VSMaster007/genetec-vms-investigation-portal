export interface AuthenticatedUser {
  id: string
  email: string
  displayName: string
  role: 'Administrator' | 'Investigator'
}

export interface LoginResult {
  token: string
  expiresAtUtc: string
  user: AuthenticatedUser
}

export interface TimeRange {
  startUtc: string
  endUtc: string
}

export interface Incident {
  id: string
  externalIncidentId: string
  title: string
  description: string
  eventType: string
  cameraName: string | null
  externalCameraId: string
  genetecCameraGuid: string | null
  location: string
  eventTimestampUtc: string
  mappingStatus: string
  recordingState: string
  recordingMessage: string | null
  recordingIsMock: boolean
}

export interface IncidentPage {
  items: Incident[]
  page: number
  pageSize: number
  total: number
}

export interface Camera {
  id: string
  genetecCameraGuid: string
  cameraName: string
  location: string
  status: string
  isMock: boolean
  createdAt: string
  updatedAt: string
}

export interface CameraMapping {
  id: string
  externalCameraId: string
  cameraId: string
  genetecCameraGuid: string
  cameraName: string
  location: string
  cameraStatus: string
  isMock: boolean
  createdAt: string
  updatedAt: string
}

export interface MappingConsistency {
  mappings: CameraMapping[]
  unmappedExternalIds: string[]
  issues: string[]
}

export interface RecordingAvailability {
  state: string
  coversEvent: boolean
  isMockArchive: boolean
  message: string
  sequences: TimeRange[]
  gaps: TimeRange[]
}

export interface PlaybackSession {
  mode: 'Mock' | 'Real'
  provider: string
  isGenetecRecording: boolean
  canPlay: boolean
  notice: string
  incidentId: string
  externalIncidentId: string
  title: string
  externalCameraId: string
  cameraName: string | null
  cameraId: string | null
  genetecCameraGuid: string | null
  mediaGatewayEndpoint: string | null
  gwpScriptUrl: string | null
  tokenPath: string | null
  seekUtc: string
  windowStartUtc: string
  windowEndUtc: string
  preEventSeconds: number
  postEventSeconds: number
  availability: RecordingAvailability
  demo: DemoRecording | null
}

export interface DemoRecording {
  mediaUrl: string
  sourceName: string
  sourceUrl: string
  cameraName: string
  externalCameraId: string
  location: string
  cameraType: string
  cameraStatus: string
  integrationMode: string
  recordingStartUtc: string
  recordingEndUtc: string
  durationSeconds: number
  incidentOffsetSeconds: number
  playbackStartOffsetSeconds: number
  playbackEndOffsetSeconds: number
  seekOffsetSeconds: number
  playbackEngine: string
}

export interface ComponentCheck {
  state: string
  detail: string
  attempted: boolean
}

export interface SystemStatus {
  api: ComponentCheck
  database: ComponentCheck
  genetecDirectory: ComponentCheck
  sdkAuthentication: ComponentCheck
  mediaGateway: ComponentCheck
  externalApi: ComponentCheck
  genetecMode: string
  transport: string
  sdkAssembliesPresent: boolean
}

export interface GenetecStatus {
  mode: string
  transport: string
  sdkAssembliesPresent: boolean
  attempted: boolean
  directoryConnected: boolean
  authenticated: boolean
  certificateStatus: string
  mediaGatewayAttempted: boolean
  mediaGatewayReachable: boolean
  mediaGatewayDetail: string | null
  directoryDetail: string | null
  error: string | null
  checkedAtUtc: string
}

export interface AuditRecord {
  id: string
  userEmail: string | null
  externalIncidentId: string | null
  action: string
  details: string
  createdAt: string
}

export interface Dashboard {
  totalIncidents: number
  todaysIncidents: number
  catalogCameras: number
  onlineCameras: number
  mappedCameras: number
  cameraCatalogMode: string
  genetec: GenetecStatus
  recentIncidents: Incident[]
  recentInvestigations: AuditRecord[]
}

export interface Problem {
  title?: string
  detail?: string
  status?: number
}
