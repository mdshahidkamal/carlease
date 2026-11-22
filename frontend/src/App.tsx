import { useEffect } from 'react'
import { BrowserRouter, Route, Routes, useNavigate } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider'
import { useAuth } from './auth/useAuth'
import AdminPage from './pages/AdminPage'
import BookingPage from './pages/BookingPage'
import HomePage from './pages/HomePage'
import SdlcDemoPage from './pages/SdlcDemoPage'
import './App.css'

function ProtectedBookingPage() {
  const { customer, loading } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (!loading && !customer)
      navigate('/', { replace: true, state: { openAuth: true, returnTo: '/booking' } })
  }, [customer, loading, navigate])

  if (loading || !customer) return <main className="booking-shell"><p role="status">Checking your sign-in…</p></main>
  return <BookingPage />
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/booking" element={<ProtectedBookingPage />} />
          <Route path="/admin" element={<AdminPage />} />
          <Route path="/sdlc-demo" element={<SdlcDemoPage />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
