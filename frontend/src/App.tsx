import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './hooks/useAuth'
import { AppShell } from './components/layout/AppShell'
import { LoginPage } from './pages/Login/LoginPage'
import { DashboardPage } from './pages/Dashboard/DashboardPage'
import { InvestigationsPage } from './pages/Investigations/InvestigationsPage'
import { CameraManagementPage } from './pages/CameraManagement/CameraManagementPage'
import { SystemStatusPage } from './pages/SystemStatus/SystemStatusPage'
import { SettingsPage } from './pages/Settings/SettingsPage'

function Protected({ children }: { children: ReactNode }) {
  const { user, ready } = useAuth()
  if (!ready) {
    return <p className="p-8 text-mist">Loading session…</p>
  }
  if (!user) {
    return <Navigate to="/login" replace />
  }
  return <AppShell>{children}</AppShell>
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/dashboard" element={<Protected><DashboardPage /></Protected>} />
      <Route path="/investigations" element={<Protected><InvestigationsPage /></Protected>} />
      <Route path="/investigations/:incidentId" element={<Protected><InvestigationsPage /></Protected>} />
      <Route path="/cameras" element={<Protected><CameraManagementPage /></Protected>} />
      <Route path="/status" element={<Protected><SystemStatusPage /></Protected>} />
      <Route path="/settings" element={<Protected><SettingsPage /></Protected>} />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  )
}
