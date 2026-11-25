import { useEffect, useRef, useState } from 'react'
import { authApiBaseUrl } from '../auth/api'
import './AdminPage.css'

type Status = 'PENDING_REVIEW' | 'CONFIRMED' | 'ACTIVE' | 'REJECTED' | 'COMPLETED'
type CountStatus = 'PENDING_REVIEW' | 'CONFIRMED' | 'ACTIVE'
type BookingCounts = Record<CountStatus, number>
type OperationalSummary = { bookings: number; pickups: number; estimatedRevenueDollars: number }
type Booking = { bookingReference: string; customerName: string; phone: string; pickupDateTime: string; pickup: string; dropoff: string; vehicleType: string; status: string; passengerCount: number; baggageCount: number; preferences: string | null }
type BookingAction = { status: 'CONFIRMED' | 'REJECTED' | 'ACTIVE' | 'COMPLETED'; label: string; icon: IconName; color: string }
type IconName = 'menu' | 'dashboard' | 'car' | 'chart' | 'user' | 'users' | 'clock' | 'settings' | 'calendar' | 'checkCalendar' | 'play' | 'pin' | 'money' | 'search' | 'reset' | 'eye' | 'close' | 'check' | 'layers' | 'chevron'

const paths: Record<Exclude<IconName, 'check'>, string> = {
  menu: 'M4 6h16M4 12h16M4 18h16', dashboard: 'M3 11 12 3l9 8M5 10v10h14V10M9 20v-6h6v6', car: 'M5 16H3v-4l2-2 2-5h10l2 5 2 2v4h-2M5 16h14M5 16v2m14-2v2M5 10h14M7 13h.01M17 13h.01', chart: 'M4 20V12m5 8V8m5 12V10m5 10V4M3 7l5-3 5 2 6-4', user: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM4 21a8 8 0 0 1 16 0', users: 'M9 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM2 21a7 7 0 0 1 14 0M16 5a4 4 0 0 1 0 7m2 3a6 6 0 0 1 4 6', clock: 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20ZM12 6v6l4 2', settings: 'M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8ZM12 2v2m0 16v2M2 12h2m16 0h2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4M19.1 4.9l-1.4 1.4M6.3 17.7l-1.4 1.4', calendar: 'M4 5h16v16H4zM8 3v4m8-4v4M4 10h16', checkCalendar: 'M4 5h16v16H4zM8 3v4m8-4v4M4 10h16m-11 5 2 2 4-4', play: 'M7 3v18l14-9z', pin: 'M12 22s7-6.5 7-13a7 7 0 1 0-14 0c0 6.5 7 13 7 13ZM12 11a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z', money: 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20ZM15 8H10a2 2 0 0 0 0 4h4a2 2 0 0 1 0 4H9m3-10v12', search: 'M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm6-2 4 4', reset: 'M20 7v5h-5M4 17v-5h5M5 9a7 7 0 0 1 12-3l3 3M19 15a7 7 0 0 1-12 3l-3-3', eye: 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12Zm10 3a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z', close: 'M5 5l14 14M19 5 5 19', layers: 'm12 2 10 5-10 5L2 7l10-5ZM2 12l10 5 10-5M2 17l10 5 10-5', chevron: 'm9 5 7 7-7 7',
}
function Icon({ name }: { name: IconName }) { return <svg className="admin-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={name === 'check' ? 'M4 12l5 5L20 6' : paths[name]} /></svg> }

function actionsForStatus(status: string): BookingAction[] {
  if (status === 'PENDING_REVIEW') return [{ status: 'CONFIRMED', label: 'Confirm', icon: 'check', color: 'confirm' }, { status: 'REJECTED', label: 'Decline', icon: 'close', color: 'decline' }]
  if (status === 'CONFIRMED') return [{ status: 'ACTIVE', label: 'Activate', icon: 'play', color: 'activate' }, { status: 'REJECTED', label: 'Decline', icon: 'close', color: 'decline' }]
  if (status === 'ACTIVE') return [{ status: 'COMPLETED', label: 'Complete', icon: 'check', color: 'complete' }]
  return []
}
const statusLabel = (status: string) => status.replaceAll('_', ' ')
const formatDate = (date: string) => new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
const formatTime = (date: string) => new Date(date).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })
const localDate = (date: string) => { const d = new Date(date); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` }
function isBooking(value: unknown): value is Booking {
  if (!value || typeof value !== 'object') return false
  return 'bookingReference' in value && typeof value.bookingReference === 'string' &&
    'customerName' in value && typeof value.customerName === 'string' && 'phone' in value && typeof value.phone === 'string' &&
    'pickupDateTime' in value && typeof value.pickupDateTime === 'string' && !Number.isNaN(Date.parse(value.pickupDateTime)) &&
    'pickup' in value && typeof value.pickup === 'string' && 'dropoff' in value && typeof value.dropoff === 'string' &&
    'vehicleType' in value && typeof value.vehicleType === 'string' && 'status' in value && typeof value.status === 'string' &&
    'passengerCount' in value && typeof value.passengerCount === 'number' && 'baggageCount' in value && typeof value.baggageCount === 'number' &&
    'preferences' in value && (typeof value.preferences === 'string' || value.preferences === null)
}
const statusLinks: { label: string; status: Status | 'ALL' }[] = [
  { label: 'All Bookings', status: 'ALL' }, { label: 'Pending Review', status: 'PENDING_REVIEW' }, { label: 'Confirmed', status: 'CONFIRMED' }, { label: 'Active', status: 'ACTIVE' }, { label: 'Rejected', status: 'REJECTED' },
]

export default function AdminPage() {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [status, setStatus] = useState<Status | 'ALL'>('ALL')
  const [mobileStatus, setMobileStatus] = useState<Status | 'ALL'>('ALL')
  const [mobileMenu, setMobileMenu] = useState<'rides' | 'more' | null>(null)
  const [today, setToday] = useState(() => new Date())
  const [completedBookings, setCompletedBookings] = useState<Booking[] | null>(null)
  const [completedError, setCompletedError] = useState('')
  const [completedAttempt, setCompletedAttempt] = useState(0)
  const [search, setSearch] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [vehicle, setVehicle] = useState('')
  const [filters, setFilters] = useState({ search: '', fromDate: '', toDate: '', vehicle: '' })
  const [selected, setSelected] = useState<Booking | null>(null)
  const [counts, setCounts] = useState<BookingCounts | null>(null)
  const [countError, setCountError] = useState('')
  const [countAttempt, setCountAttempt] = useState(0)
  const [summary, setSummary] = useState<OperationalSummary | null>(null)
  const [summaryError, setSummaryError] = useState('')
  const [summaryAttempt, setSummaryAttempt] = useState(0)
  const [allBookings, setAllBookings] = useState<Booking[] | null>(null)
  const [bookingError, setBookingError] = useState('')
  const [bookingAttempt, setBookingAttempt] = useState(0)
  const [actionError, setActionError] = useState('')
  const [actionMessage, setActionMessage] = useState('')
  const [updating, setUpdating] = useState(false)
  const actionInFlight = useRef(false)
  const dialog = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const controller = new AbortController()
    void fetch(`${authApiBaseUrl}/api/admin/dashboard/booking-counts`, { cache: 'no-store', signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error(`Could not load booking counts (HTTP ${response.status}).`)
        const data: unknown = await response.json()
        const validCount = (value: unknown): value is number => typeof value === 'number' && Number.isSafeInteger(value) && value >= 0
        if (!data || typeof data !== 'object' || !('pendingReview' in data) || !('confirmed' in data) || !('active' in data) ||
            !validCount(data.pendingReview) || !validCount(data.confirmed) || !validCount(data.active))
          throw new Error('The server returned invalid booking counts.')
        if (!controller.signal.aborted) setCounts({ PENDING_REVIEW: data.pendingReview, CONFIRMED: data.confirmed, ACTIVE: data.active })
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setCounts(null)
        setCountError(error instanceof Error ? error.message : 'Could not load booking counts.')
      })
    return () => controller.abort()
  }, [countAttempt])

  useEffect(() => {
    const controller = new AbortController()
    const now = new Date()
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate())
    const end = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1)
    const params = new URLSearchParams({ startUtc: start.toISOString(), endUtc: end.toISOString() })
    const refresh = () => { setToday(new Date()); setSummary(null); setSummaryError(''); setSummaryAttempt((attempt) => attempt + 1) }
    const midnight = window.setTimeout(refresh, Math.max(0, end.getTime() - now.getTime()) + 100)
    const onVisibilityChange = () => { if (document.visibilityState === 'visible' && new Date() >= end) refresh() }
    document.addEventListener('visibilitychange', onVisibilityChange)
    void fetch(`${authApiBaseUrl}/api/admin/dashboard/operational-summary?${params}`, { cache: 'no-store', signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error(`Could not load operational summary (HTTP ${response.status}).`)
        const data: unknown = await response.json()
        if (!data || typeof data !== 'object' || !('bookings' in data) || !('pickups' in data) || !('estimatedRevenueDollars' in data) ||
            typeof data.bookings !== 'number' || !Number.isSafeInteger(data.bookings) || data.bookings < 0 ||
            typeof data.pickups !== 'number' || !Number.isSafeInteger(data.pickups) || data.pickups < 0 ||
            typeof data.estimatedRevenueDollars !== 'number' || !Number.isFinite(data.estimatedRevenueDollars) || data.estimatedRevenueDollars < 0)
          throw new Error('The server returned an invalid operational summary.')
        if (!controller.signal.aborted) setSummary({ bookings: data.bookings, pickups: data.pickups, estimatedRevenueDollars: data.estimatedRevenueDollars })
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setSummaryError(error instanceof Error ? error.message : 'Could not load operational summary.')
      })
    return () => { controller.abort(); window.clearTimeout(midnight); document.removeEventListener('visibilitychange', onVisibilityChange) }
  }, [summaryAttempt])

  useEffect(() => {
    const controller = new AbortController()
    void fetch(`${authApiBaseUrl}/api/admin/dashboard/bookings`, { cache: 'no-store', signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error(`Could not load bookings (HTTP ${response.status}).`)
        const data: unknown = await response.json()
        if (!Array.isArray(data) || !data.every(isBooking)) throw new Error('The server returned invalid booking data.')
        if (!controller.signal.aborted) setAllBookings(data.filter((booking) => booking.status !== 'COMPLETED'))
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setBookingError(error instanceof Error ? error.message : 'Could not load bookings.')
      })
    return () => controller.abort()
  }, [bookingAttempt])

  useEffect(() => {
    if (mobileStatus !== 'COMPLETED') return
    const controller = new AbortController()
    void fetch(`${authApiBaseUrl}/api/admin/dashboard/completed-bookings`, { cache: 'no-store', signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error(`Could not load completed bookings (HTTP ${response.status}).`)
        const data: unknown = await response.json()
        if (!Array.isArray(data) || !data.every(isBooking) || data.some((booking) => booking.status !== 'COMPLETED')) throw new Error('The server returned invalid completed booking data.')
        if (!controller.signal.aborted) setCompletedBookings(data)
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setCompletedError(error instanceof Error ? error.message : 'Could not load completed bookings.')
      })
    return () => controller.abort()
  }, [mobileStatus, completedAttempt])

  const bookings = (allBookings ?? []).filter((booking) => (status === 'ALL' || booking.status === status) && (!filters.search || `${booking.bookingReference} ${booking.customerName} ${booking.phone}`.toLowerCase().includes(filters.search.toLowerCase())) && (!filters.fromDate || localDate(booking.pickupDateTime) >= filters.fromDate) && (!filters.toDate || localDate(booking.pickupDateTime) <= filters.toDate) && (!filters.vehicle || booking.vehicleType === filters.vehicle))
  const vehicles = [...new Set((allBookings ?? []).map((booking) => booking.vehicleType).filter(Boolean))].sort()
  const mobileBookings = mobileStatus === 'COMPLETED' ? completedBookings : allBookings?.filter((booking) => mobileStatus === 'ALL' || booking.status === mobileStatus) ?? null
  function clearFilters() { setSearch(''); setFromDate(''); setToDate(''); setVehicle(''); setFilters({ search: '', fromDate: '', toDate: '', vehicle: '' }) }
  function reset() { clearFilters(); setStatus('ALL'); setMobileStatus('ALL'); setMobileMenu(null); setSidebarOpen(false) }
  function selectStatus(next: Status | 'ALL') { clearFilters(); setStatus(next); setMobileStatus(next); setMobileMenu(null); setSidebarOpen(false) }
  function selectMobileStatus(next: Status | 'ALL') { clearFilters(); setMobileStatus(next); setCompletedBookings(null); setCompletedError(''); if (next === 'COMPLETED' && mobileStatus === next) setCompletedAttempt((attempt) => attempt + 1); setMobileMenu(null) }
  function showDetails(booking: Booking) { setSelected(booking); dialog.current?.showModal() }
  async function updateStatus(booking: Booking, action: BookingAction) {
    if (actionInFlight.current || !window.confirm(`${action.label} booking ${booking.bookingReference}? This changes its status in the database.`)) return
    actionInFlight.current = true
    setUpdating(true)
    setActionError('')
    setActionMessage('')
    try {
      const response = await fetch(`${authApiBaseUrl}/api/admin/dashboard/bookings/${encodeURIComponent(booking.bookingReference)}/status`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json', 'X-Admin-Request': 'booking-status' },
        body: JSON.stringify({ expectedStatus: booking.status, status: action.status }),
      })
      if (!response.ok) {
        if (response.status === 409) throw new Error('The booking status changed elsewhere. The grid has been refreshed.')
        throw new Error(`Could not update booking ${booking.bookingReference} (HTTP ${response.status}).`)
      }
      setActionMessage(`Booking ${booking.bookingReference} is now ${statusLabel(action.status)}.`)
    } catch (error: unknown) {
      setActionError(error instanceof Error ? `${error.message} Check the refreshed grid before retrying.` : 'Could not update the booking. Check the refreshed grid before retrying.')
    } finally {
      setAllBookings(null); setBookingError(''); setBookingAttempt((attempt) => attempt + 1)
      setCounts(null); setCountError(''); setCountAttempt((attempt) => attempt + 1)
      setSummary(null); setSummaryError(''); setSummaryAttempt((attempt) => attempt + 1)
      setCompletedBookings(null); setCompletedError(''); setCompletedAttempt((attempt) => attempt + 1)
      actionInFlight.current = false
      setUpdating(false)
    }
  }
  const navigation: { name: string; icon: IconName }[] = [{ name: 'Reports', icon: 'chart' }, { name: 'Drivers', icon: 'user' }, { name: 'Vehicles', icon: 'car' }, { name: 'Customers', icon: 'users' }, { name: 'Audit Logs', icon: 'clock' }, { name: 'Settings', icon: 'settings' }]
  return <div className="admin-shell">
    <aside className={`admin-sidebar${sidebarOpen ? ' is-open' : ''}`} id="admin-navigation" aria-label="Admin navigation">
      <div className="admin-brand"><span className="admin-crown" aria-hidden="true">♛</span><strong>ROYAL LIMO</strong><span>SERVICE</span></div>
      <p className="admin-console-label">Admin Console</p>
      <nav aria-label="Admin sections">
        <button className="admin-nav-main is-current" type="button" onClick={reset}><Icon name="dashboard" />Dashboard</button>
        <p className="admin-nav-heading"><Icon name="car" /> Ride Management</p>
        <div className="admin-status-nav">{statusLinks.map((item) => <button key={item.status} type="button" className={status === item.status && status !== 'ALL' ? 'is-selected' : ''} onClick={() => selectStatus(item.status)}>{item.label}{(item.status === 'PENDING_REVIEW' || item.status === 'CONFIRMED' || item.status === 'ACTIVE') && <span>{counts?.[item.status] ?? '—'}</span>}</button>)}</div>
        <div className="admin-nav-secondary">{navigation.map((item) => <button key={item.name} type="button" disabled title="Coming soon"><Icon name={item.icon} />{item.name}<small>Soon</small></button>)}</div>
      </nav>
      <div className="admin-sidebar-image" aria-hidden="true" />
    </aside>
    <div className="admin-workspace">
      <header className="admin-header"><button className="admin-menu-button" type="button" aria-label="Toggle navigation" aria-expanded={sidebarOpen} aria-controls="admin-navigation" onClick={() => setSidebarOpen(!sidebarOpen)}><Icon name="menu" /></button><h1>Admin Dashboard</h1><div className="admin-profile"><span className="admin-avatar">A</span><span><strong>Admin</strong><small>Administrator</small></span></div></header>
      <header className="admin-mobile-header"><div><strong>♛ ROYAL LIMO</strong><small>ADMIN CONSOLE</small><h1>Dashboard</h1><time>{new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' }).format(today)}</time></div><button type="button" aria-label="Open menu" aria-expanded={mobileMenu === 'more'} aria-controls="admin-mobile-more" onClick={() => setMobileMenu(mobileMenu === 'more' ? null : 'more')}><Icon name="menu" /></button></header>
      <main className="admin-content">
        {actionError && <p className="admin-count-error" role="alert">{actionError}</p>}
        {actionMessage && <p className="admin-action-message" role="status">{actionMessage}</p>}
        {countError && <p className="admin-count-error" role="alert">{countError} Check that the local Development API and database are available. <button type="button" onClick={() => { setCountError(''); setCountAttempt((attempt) => attempt + 1) }}>Retry</button></p>}
        <section className="admin-stats" aria-label="Booking status overview" aria-busy={!counts && !countError}>{([{ status: 'PENDING_REVIEW', icon: 'calendar', description: 'Awaiting admin decision', color: 'pending' }, { status: 'CONFIRMED', icon: 'checkCalendar', description: 'Ready for activation', color: 'confirmed' }, { status: 'ACTIVE', icon: 'play', description: 'Rides currently in progress', color: 'active' }] as const).map((item) => <button key={item.status} type="button" className={`admin-stat ${item.color}`} onClick={() => selectStatus(item.status)} aria-label={`${statusLabel(item.status)}: ${counts ? counts[item.status] : countError ? 'unavailable' : 'loading'}. Filter bookings.`}><span className="admin-stat-icon"><Icon name={item.icon} /></span><span className="admin-stat-text"><strong>{statusLabel(item.status)}</strong><b>{counts?.[item.status] ?? '—'}</b><span>{item.description}</span></span><Icon name="chevron" /></button>)}</section>
        <section className="admin-panel admin-bookings"><h2><Icon name="car" /> {status === 'ALL' ? 'All Bookings' : `${statusLabel(status)} Bookings`}</h2><form className="admin-filters" onSubmit={(event) => { event.preventDefault(); setFilters({ search: search.trim(), fromDate, toDate, vehicle }) }}>
          <label className="admin-search"><span className="admin-sr-only">Search booking ID, customer or phone</span><Icon name="search" /><input placeholder="Search by Booking ID, Customer, Phone..." value={search} onChange={(event) => setSearch(event.target.value)} /></label>
          <label><span className="admin-sr-only">From date</span><input aria-label="From date" type="date" value={fromDate} max={toDate || undefined} onChange={(event) => setFromDate(event.target.value)} /></label>
          <label><span className="admin-sr-only">To date</span><input aria-label="To date" type="date" value={toDate} min={fromDate || undefined} onChange={(event) => setToDate(event.target.value)} /></label>
          <label><span className="admin-sr-only">Vehicle type</span><select value={vehicle} onChange={(event) => setVehicle(event.target.value)}><option value="">All Vehicle Types</option>{vehicles.map((type) => <option key={type}>{type}</option>)}</select></label>
          <button type="submit" className="admin-search-button"><Icon name="search" /> Search</button><button type="button" className="admin-reset" onClick={reset}><Icon name="reset" /> Reset</button>
        </form>
        {bookingError && <p className="admin-count-error" role="alert">{bookingError} Check that the local Development API and database are available. <button type="button" onClick={() => { setBookingError(''); setBookingAttempt((attempt) => attempt + 1) }}>Retry</button></p>}
        {!allBookings && !bookingError && <p className="admin-empty" role="status">Loading bookings...</p>}
        {allBookings && <><div className="admin-table-wrap"><table className="admin-table"><thead><tr><th>Booking ID</th><th>Customer</th><th>Ride Date &amp; Time</th><th>Route (From → To)</th><th>Vehicle</th><th>Status</th><th>Actions</th></tr></thead><tbody>{bookings.map((booking) => <tr key={booking.bookingReference}><td><button className="admin-booking-link" type="button" onClick={() => showDetails(booking)}>{booking.bookingReference}</button></td><td><strong>{booking.customerName}</strong><br />{booking.phone}</td><td><strong>{formatDate(booking.pickupDateTime)}</strong><br />{formatTime(booking.pickupDateTime)}</td><td><span className="admin-route"><Icon name="pin" /> {booking.pickup}</span><span className="admin-route"><span aria-hidden="true">→</span> {booking.dropoff}</span></td><td><strong>{booking.vehicleType}</strong></td><td><span className={`admin-status status-${booking.status.toLowerCase()}`}>{statusLabel(booking.status)}</span></td><td><div className="admin-actions">{actionsForStatus(booking.status).map((action) => <button key={action.status} className={`admin-action ${action.color}`} type="button" title={action.label} aria-label={`${action.label} ${booking.bookingReference}`} disabled={updating} onClick={() => void updateStatus(booking, action)}><Icon name={action.icon} /></button>)}</div></td></tr>)}</tbody></table>{bookings.length === 0 && <p className="admin-empty">No bookings match your filters.</p>}</div>
        <p className="admin-table-footer">Showing {bookings.length} of {allBookings.length} bookings.</p></>}</section>
        <section className="admin-mobile-bookings" aria-label="Mobile bookings"><h2>{mobileStatus === 'ALL' ? 'All Bookings' : `${statusLabel(mobileStatus)} Bookings`}</h2>
          {(mobileStatus === 'COMPLETED' ? completedError : bookingError) && <p className="admin-count-error" role="alert">{mobileStatus === 'COMPLETED' ? completedError : bookingError} <button type="button" onClick={() => { if (mobileStatus === 'COMPLETED') { setCompletedError(''); setCompletedAttempt((attempt) => attempt + 1) } else { setBookingError(''); setBookingAttempt((attempt) => attempt + 1) } }}>Retry</button></p>}
          {!mobileBookings && !(mobileStatus === 'COMPLETED' ? completedError : bookingError) && <p className="admin-empty" role="status">Loading bookings...</p>}
          {mobileBookings?.length === 0 && <p className="admin-empty">No bookings in this category.</p>}
          {mobileBookings?.map((booking) => <article className="admin-booking-card" key={booking.bookingReference}><div className="admin-card-heading"><button className="admin-booking-link" type="button" onClick={() => showDetails(booking)}>{booking.bookingReference}</button><span className={`admin-status status-${booking.status.toLowerCase()}`}>{statusLabel(booking.status)}</span></div><p className="admin-card-customer"><strong>{booking.customerName}</strong><br />{booking.phone}</p><div className="admin-card-details"><p><Icon name="calendar" /> {formatDate(booking.pickupDateTime)} · {formatTime(booking.pickupDateTime)}</p><p><Icon name="pin" /> <span><strong>Pickup:</strong> {booking.pickup}<br /><strong>Drop-off:</strong> {booking.dropoff}</span></p><p><Icon name="car" /> {booking.vehicleType}</p></div>{actionsForStatus(booking.status).length > 0 && <div className="admin-card-actions">{actionsForStatus(booking.status).map((action) => <button key={action.status} type="button" className={`admin-card-action ${action.color}`} disabled={updating} onClick={() => void updateStatus(booking, action)}>{action.status === 'REJECTED' ? 'Reject' : action.status === 'COMPLETED' ? 'Complete Ride' : action.label}</button>)}</div>}</article>)}
        </section>
        <div className="admin-bottom"><section className="admin-panel" aria-busy={!summary && !summaryError}><div className="admin-panel-title"><h2><Icon name="calendar" /> Today's Operational Summary</h2></div><div className="admin-summary"><div><Icon name="car" /><span>Today's Bookings<strong>{summary?.bookings ?? '—'}</strong></span></div><div><Icon name="pin" /><span>Today's Pickups<strong>{summary?.pickups ?? '—'}</strong></span></div><div><Icon name="money" /><span>Estimated Revenue<strong>{summary ? new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(summary.estimatedRevenueDollars) : '—'}</strong></span></div></div><p className="admin-helper">Local day: bookings created; pickups scheduled excluding rejected rides; revenue estimated for confirmed, active and completed pickups.</p>{summaryError && <p className="admin-count-error" role="alert">{summaryError} <button type="button" onClick={() => { setSummaryError(''); setSummaryAttempt((attempt) => attempt + 1) }}>Retry</button></p>}</section></div>
      </main>
      <div className="admin-mobile-navigation">
        {mobileMenu === 'rides' && <nav className="admin-mobile-popup" id="admin-mobile-rides" aria-label="Ride categories">{[...statusLinks, { label: 'Completed', status: 'COMPLETED' as const }].map((item) => <button key={item.status} type="button" aria-pressed={mobileStatus === item.status} onClick={() => selectMobileStatus(item.status)}>{item.label}</button>)}</nav>}
        {mobileMenu === 'more' && <nav className="admin-mobile-popup" id="admin-mobile-more" aria-label="More navigation">{navigation.filter((item) => item.name !== 'Drivers').map((item) => <button key={item.name} type="button" disabled>{item.name} <small>Coming soon</small></button>)}</nav>}
        <nav className="admin-mobile-bottom" aria-label="Mobile navigation"><button type="button" onClick={reset}><Icon name="dashboard" />Dashboard</button><button type="button" aria-expanded={mobileMenu === 'rides'} aria-controls="admin-mobile-rides" onClick={() => setMobileMenu(mobileMenu === 'rides' ? null : 'rides')}><Icon name="car" />Rides</button><button type="button" disabled title="Drivers page coming soon"><Icon name="users" />Drivers</button><button type="button" aria-expanded={mobileMenu === 'more'} aria-controls="admin-mobile-more" onClick={() => setMobileMenu(mobileMenu === 'more' ? null : 'more')}><Icon name="menu" />More</button></nav>
      </div>
    </div>
    <dialog ref={dialog} className="admin-dialog" aria-labelledby="admin-dialog-title" onClose={() => setSelected(null)}>{selected && <><div className="admin-dialog-heading"><h2 id="admin-dialog-title">Booking {selected.bookingReference}</h2><button type="button" aria-label="Close booking details" onClick={() => dialog.current?.close()}><Icon name="close" /></button></div><dl><div><dt>Customer</dt><dd>{selected.customerName} · {selected.phone}</dd></div><div><dt>Pickup</dt><dd>{formatDate(selected.pickupDateTime)} at {formatTime(selected.pickupDateTime)}</dd></div><div><dt>From</dt><dd>{selected.pickup}</dd></div><div><dt>To</dt><dd>{selected.dropoff}</dd></div><div><dt>Vehicle</dt><dd>{selected.vehicleType}</dd></div><div><dt>Passengers</dt><dd>{selected.passengerCount}</dd></div><div><dt>Bags</dt><dd>{selected.baggageCount}</dd></div><div><dt>Preferences</dt><dd>{selected.preferences || 'None'}</dd></div><div><dt>Status</dt><dd>{statusLabel(selected.status)}</dd></div></dl><button type="button" className="admin-reset" onClick={() => dialog.current?.close()}>Close</button></>}</dialog>
  </div>
}
