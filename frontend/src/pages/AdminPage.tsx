import { useCallback, useEffect, useState } from 'react'
import { authApiBaseUrl } from '../auth/api'

type Booking = {
  id: string; bookingReference: string; status: string; createdAtUtc: string; pickupDateTime: string
  pickup: string; dropoff: string; vehicleType: string; passengerCount: number; kidsCount: number
  baggageCount: number; estimatedDistanceMiles: number | null; estimatedAmountDollars: number
  customerName: string; customerPhone: string; customerEmail: string | null
}
type Report = {
  period: string; startDate: string; endDateExclusive: string; totalBookings: number
  pendingReview: number; confirmed: number; rejected: number; active: number; completed: number
  completedRideRevenue: number; averageCompletedRideValue: number
}
const api = authApiBaseUrl
const money = (value: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value)
const dateTime = (value: string) => new Date(value).toLocaleString()

export default function AdminPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [adminEmail, setAdminEmail] = useState('')
  const [authenticated, setAuthenticated] = useState(false)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [bookings, setBookings] = useState<Booking[]>([])
  const [status, setStatus] = useState('ALL')
  const [search, setSearch] = useState('')
  const [period, setPeriod] = useState('daily')
  const [reportDate, setReportDate] = useState(new Date().toISOString().slice(0, 10))
  const [report, setReport] = useState<Report | null>(null)
  const [detail, setDetail] = useState<Record<string, unknown> | null>(null)
  const [reason, setReason] = useState('')

  const loadBookings = useCallback(async () => {
    const params = new URLSearchParams({ page: '1', pageSize: '100' })
    if (status !== 'ALL') params.set('status', status)
    if (search.trim()) params.set('search', search.trim())
    const response = await fetch(`${api}/api/admin/bookings?${params}`, { credentials: 'include' })
    if (response.status === 401) { setAuthenticated(false); return }
    const body = await response.json()
    if (!response.ok) throw new Error(body.message || 'Could not load bookings.')
    setBookings(body.items ?? [])
  }, [status, search])

  const loadReport = useCallback(async () => {
    const params = new URLSearchParams({ period, date: reportDate })
    const response = await fetch(`${api}/api/admin/reports?${params}`, { credentials: 'include' })
    if (response.status === 401) { setAuthenticated(false); return }
    const body = await response.json()
    if (!response.ok) throw new Error(body.message || 'Could not load report.')
    setReport(body)
  }, [period, reportDate])

  useEffect(() => {
    const controller = new AbortController()
    fetch(`${api}/api/admin/auth/me`, { credentials: 'include', signal: controller.signal })
      .then(async response => {
        if (response.ok) {
          const body = await response.json()
          setAdminEmail(body.email || '')
          setAuthenticated(true)
        }
      })
      .catch(() => undefined)
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (!authenticated) return
    void Promise.all([loadBookings(), loadReport()]).catch(e => setError(e instanceof Error ? e.message : 'Could not load admin data.'))
  }, [authenticated, loadBookings, loadReport])

  async function login(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      const response = await fetch(`${api}/api/admin/auth/login`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        credentials: 'include', body: JSON.stringify({ email, password })
      })
      const body = await response.json()
      if (!response.ok) throw new Error(body.message || 'Administrator sign-in failed.')
      setAdminEmail(body.email || email); setAuthenticated(true); setPassword('')
    } catch (e) { setError(e instanceof Error ? e.message : 'Sign-in failed.') }
    finally { setBusy(false) }
  }

  async function changeStatus(booking: Booking, nextStatus: string) {
    let rejectionReason: string | undefined
    if (nextStatus === 'REJECTED') {
      rejectionReason = reason.trim()
      if (!rejectionReason) { setError('Enter a rejection reason before rejecting this booking.'); return }
    }
    setBusy(true); setError('')
    try {
      const response = await fetch(`${api}/api/admin/bookings/${booking.id}/status`, {
        method: 'PATCH', headers: { 'Content-Type': 'application/json' }, credentials: 'include',
        body: JSON.stringify({ status: nextStatus, reason: rejectionReason })
      })
      const body = await response.json()
      if (!response.ok) throw new Error(body.message || 'Status update failed.')
      setReason('')
      await Promise.all([loadBookings(), loadReport()])
    } catch (e) { setError(e instanceof Error ? e.message : 'Status update failed.') }
    finally { setBusy(false) }
  }

  async function viewDetails(booking: Booking) {
    setError('')
    try {
      const response = await fetch(`${api}/api/admin/bookings/${booking.id}`, { credentials: 'include' })
      const body = await response.json()
      if (!response.ok) throw new Error(body.message || 'Could not load booking details.')
      setDetail(body)
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not load booking details.') }
  }

  async function logout() {
    await fetch(`${api}/api/admin/auth/logout`, { method: 'POST', credentials: 'include' })
    setAuthenticated(false); setBookings([]); setReport(null); setDetail(null)
  }

  if (loading) return <main className="admin-shell"><p>Checking administrator session…</p></main>
  if (!authenticated) return (
    <main className="admin-login-shell">
      <form className="admin-login-card" onSubmit={login}>
        <div className="admin-brand">ROYAL LIMO SERVICE <span>ADMINISTRATION</span></div>
        <h1>Admin sign in</h1><p>Sign in to manage bookings and business reports.</p>
        <label htmlFor="admin-email">Administrator email</label>
        <input id="admin-email" type="email" autoComplete="username" value={email} onChange={e => setEmail(e.target.value)} required />
        <label htmlFor="admin-password">Password</label>
        <input id="admin-password" type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required />
        {error && <p className="admin-error" role="alert">{error}</p>}
        <button className="admin-primary" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
    </main>
  )

  return (
    <main className="admin-shell">
      <header className="admin-header">
        <div><div className="admin-brand">ROYAL LIMO SERVICE <span>ADMIN CONSOLE</span></div><h1>Operations overview</h1></div>
        <div className="admin-user">{adminEmail}<button onClick={logout}>Sign out</button></div>
      </header>
      {error && <p className="admin-error" role="alert">{error}</p>}
      <section className="admin-report">
        <div className="admin-section-heading"><div><h2>Reports & analytics</h2><p>Calendar-year reporting · January to December</p></div>
          <div className="admin-report-controls">
            <select aria-label="Report period" value={period} onChange={e => setPeriod(e.target.value)}>
              <option value="daily">Daily</option><option value="monthly">Monthly</option><option value="ytd">YTD</option><option value="fy">FY</option>
            </select>
            <input aria-label="Report reference date" type="date" value={reportDate} onChange={e => setReportDate(e.target.value)} />
          </div>
        </div>
        {report && <div className="admin-metrics">
          <Metric label="Total bookings" value={report.totalBookings} />
          <Metric label="Pending review" value={report.pendingReview} />
          <Metric label="Confirmed" value={report.confirmed} />
          <Metric label="Active" value={report.active} />
          <Metric label="Completed" value={report.completed} />
          <Metric label="Rejected" value={report.rejected} />
          <Metric label="Completed ride revenue" value={money(report.completedRideRevenue)} />
          <Metric label="Average completed ride" value={money(report.averageCompletedRideValue)} />
        </div>}
      </section>
      <section className="admin-bookings">
        <div className="admin-section-heading"><div><h2>Ride management</h2><p>Review bookings and manage ride status.</p></div>
          <button className="admin-secondary" onClick={() => void Promise.all([loadBookings(), loadReport()])}>Refresh</button>
        </div>
        <div className="admin-filters">
          <input aria-label="Search bookings" placeholder="Search booking ID, customer, phone or email" value={search} onChange={e => setSearch(e.target.value)} />
          <select aria-label="Booking status" value={status} onChange={e => setStatus(e.target.value)}>
            <option value="ALL">All statuses</option><option value="PENDING_REVIEW">Pending review</option>
            <option value="CONFIRMED">Confirmed</option><option value="ACTIVE">Active</option>
            <option value="COMPLETED">Completed</option><option value="REJECTED">Rejected</option>
          </select>
        </div>
        <div className="admin-table-wrap"><table className="admin-table">
          <thead><tr><th>Booking</th><th>Customer</th><th>Journey</th><th>Pickup</th><th>Status</th><th>Actions</th></tr></thead>
          <tbody>{bookings.map(booking => <tr key={booking.id}>
            <td><strong>{booking.bookingReference}</strong><small>Created {dateTime(booking.createdAtUtc)}</small></td>
            <td>{booking.customerName}<small>{booking.customerPhone}</small><small>{booking.customerEmail}</small></td>
            <td>{booking.pickup}<small>to {booking.dropoff}</small><small>{booking.vehicleType}</small></td>
            <td>{dateTime(booking.pickupDateTime)}</td>
            <td><span className={`admin-status ${booking.status.toLowerCase()}`}>{booking.status.replaceAll('_', ' ')}</span></td>
            <td><div className="admin-actions">
              <button className="admin-secondary" onClick={() => void viewDetails(booking)}>Details</button>
              {booking.status === 'PENDING_REVIEW' && <><button className="admin-primary small" disabled={busy} onClick={() => void changeStatus(booking, 'CONFIRMED')}>Confirm</button><button className="admin-danger" disabled={busy} onClick={() => void changeStatus(booking, 'REJECTED')}>Reject</button></>}
              {booking.status === 'CONFIRMED' && <><button className="admin-primary small" disabled={busy} onClick={() => void changeStatus(booking, 'ACTIVE')}>Activate</button><button className="admin-danger" disabled={busy} onClick={() => void changeStatus(booking, 'REJECTED')}>Reject</button></>}
              {booking.status === 'ACTIVE' && <button className="admin-primary small" disabled={busy} onClick={() => void changeStatus(booking, 'COMPLETED')}>Complete</button>}
            </div></td>
          </tr>)}</tbody>
        </table>{bookings.length === 0 && <p className="admin-empty">No bookings match the selected filters.</p>}</div>
        <div className="admin-reject"><label htmlFor="reject-reason">Rejection reason (required when rejecting)</label><input id="reject-reason" value={reason} onChange={e => setReason(e.target.value)} maxLength={1000} placeholder="Enter reason before selecting Reject" /></div>
      </section>
      {detail && <div className="admin-modal-backdrop" role="presentation" onClick={() => setDetail(null)}><section className="admin-detail" role="dialog" aria-modal="true" aria-label="Booking details" onClick={e => e.stopPropagation()}>
        <button className="admin-close" onClick={() => setDetail(null)}>Close</button><h2>Booking details</h2>
        <pre>{JSON.stringify(detail, null, 2)}</pre>
      </section></div>}
    </main>
  )
}

function Metric({ label, value }: { label: string; value: string | number }) {
  return <div className="admin-metric"><span>{label}</span><strong>{value}</strong></div>
}
