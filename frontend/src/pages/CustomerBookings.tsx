import { useEffect, useState } from 'react'
import { authApiBaseUrl } from '../auth/api'
import BookingDetailsDialog from './BookingDetailsDialog'

interface BookingSummary {
  bookingReference: string
  pickupDateTime: string
  status: string
}

type LoadState =
  | { type: 'loading' }
  | { type: 'error'; message: string }
  | { type: 'loaded'; bookings: BookingSummary[] }

export default function CustomerBookings() {
  const [state, setState] = useState<LoadState>({ type: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [selectedReference, setSelectedReference] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    void fetch(`${authApiBaseUrl}/api/bookings`, { credentials: 'include', signal: controller.signal, cache: 'no-store' })
      .then(async (response) => {
        if (response.status === 401) throw new Error('Your session has expired. Please sign in again to view your rides.')
        if (!response.ok) throw new Error(`Could not load your rides (HTTP ${response.status}).`)
        const bookings: BookingSummary[] = await response.json()
        if (!controller.signal.aborted) setState({ type: 'loaded', bookings })
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setState({ type: 'error', message: error instanceof Error ? error.message : 'Could not load your rides.' })
      })
    return () => controller.abort()
  }, [attempt])

  if (state.type === 'loading') return <p className="pending-rides-message" role="status">Loading your rides...</p>
  if (state.type === 'error') return <div className="pending-rides-message" role="alert">{state.message} <button type="button" onClick={() => { setState({ type: 'loading' }); setAttempt((value) => value + 1) }}>Try again</button></div>
  if (state.bookings.length === 0) return null

  return <section className="customer-rides" aria-labelledby="customer-rides-title">
    <h2 id="customer-rides-title">Your rides</h2>
    <div className="customer-rides-scroll"><table>
      <thead><tr><th scope="col">Booking ID</th><th scope="col">Pickup Date</th><th scope="col">Status</th></tr></thead>
      <tbody>{state.bookings.map((booking) => <tr key={booking.bookingReference}>
        <td><button className="ride-reference" type="button" onClick={() => setSelectedReference(booking.bookingReference)}>{booking.bookingReference}</button></td>
        <td><time dateTime={booking.pickupDateTime}>{new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: '2-digit' }).format(new Date(booking.pickupDateTime)).replaceAll(' ', '-')}</time></td>
        <td>{booking.status}</td>
      </tr>)}</tbody>
    </table></div>
    {selectedReference && <BookingDetailsDialog key={selectedReference} reference={selectedReference} onClose={() => setSelectedReference(null)} />}
  </section>
}
