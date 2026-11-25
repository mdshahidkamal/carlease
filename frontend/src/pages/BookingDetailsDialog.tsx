import { useEffect, useRef, useState } from 'react'
import { authApiBaseUrl } from '../auth/api'

interface BookingDetails {
  bookingReference: string
  pickup: string
  via: string | null
  dropoff: string
  pickupDateTime: string
  status: string
  vehicleType: string
  passengerCount: number
  kidsCount: number
  baggageCount: number
  estimatedDistanceMiles: number | null
  estimatedAmountDollars: number
  preferences: string | null
}

type DetailsState = { type: 'loading' } | { type: 'error'; message: string } | { type: 'loaded'; booking: BookingDetails }

type RideIconName = 'car' | 'calendar' | 'clock' | 'pin' | 'person' | 'bag' | 'distance'
const iconPaths: Record<RideIconName, string> = {
  car: 'M4 16H2v-4l2-2 2-5h12l2 5 2 2v4h-2M4 16h16M4 16v2m16-2v2M4 10h16M7 13h.01M17 13h.01',
  calendar: 'M4 5h16v16H4zM8 3v4m8-4v4M4 10h16',
  clock: 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20ZM12 6v6l4 2',
  pin: 'M12 22s7-6.5 7-13a7 7 0 1 0-14 0c0 6.5 7 13 7 13ZM12 11a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z',
  person: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM4 21a8 8 0 0 1 16 0',
  bag: 'M5 7h14v14H5zM9 7V5a3 3 0 0 1 6 0v2',
  distance: 'M12 3 3 13v8h18v-8L12 3Zm0 0v10m-3 0h6',
}

function RideIcon({ name }: { name: RideIconName }) {
  return <svg className="ride-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={iconPaths[name]} /></svg>
}

export default function BookingDetailsDialog({ reference, onClose }: { reference: string; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const [state, setState] = useState<DetailsState>({ type: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!dialog.current?.open) dialog.current?.showModal()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void fetch(`${authApiBaseUrl}/api/bookings/${encodeURIComponent(reference)}`, { credentials: 'include', signal: controller.signal, cache: 'no-store' })
      .then(async (response) => {
        if (response.status === 401) throw new Error('Your session has expired. Please sign in again to view this ride.')
        if (!response.ok) throw new Error(`Could not load ride details (HTTP ${response.status}).`)
        const booking: BookingDetails = await response.json()
        if (!controller.signal.aborted) setState({ type: 'loaded', booking })
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setState({ type: 'error', message: error instanceof Error ? error.message : 'Could not load ride details.' })
      })
    return () => controller.abort()
  }, [reference, attempt])

  return <dialog ref={dialog} className="ride-dialog" aria-labelledby="ride-dialog-title" onClose={onClose}>
    <header className="ride-dialog-heading">
      <span className="ride-heading-icon"><RideIcon name="car" /></span>
      <div className="ride-heading-text"><h2 id="ride-dialog-title">Ride details</h2><p>Booking reference <strong>{reference}</strong></p></div>
      {state.type === 'loaded' && <span className="ride-status">{state.booking.status.replaceAll('_', ' ')}</span>}
      <button className="ride-close-icon" type="button" onClick={() => dialog.current?.close()} aria-label="Close ride details">×</button>
    </header>
    <div className="ride-dialog-content">
      {state.type === 'loading' && <p role="status">Loading ride details...</p>}
      {state.type === 'error' && <p role="alert">{state.message} <button className="ride-retry" type="button" onClick={() => { setState({ type: 'loading' }); setAttempt((value) => value + 1) }}>Try again</button></p>}
      {state.type === 'loaded' && <dl className="ride-details">
        <div className="ride-panel ride-schedule">
          <div className="ride-schedule-item"><span className="ride-panel-icon"><RideIcon name="calendar" /></span><div><dt>Pickup date</dt><dd><time dateTime={state.booking.pickupDateTime}>{new Date(state.booking.pickupDateTime).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}</time></dd></div></div>
          <div className="ride-schedule-item"><span className="ride-panel-icon"><RideIcon name="clock" /></span><div><dt>Pickup time</dt><dd><time dateTime={state.booking.pickupDateTime}>{new Date(state.booking.pickupDateTime).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })}</time></dd></div></div>
        </div>
        <div className="ride-panel ride-journey"><h3><RideIcon name="pin" /> Journey</h3><div className="ride-route">
          <div className="ride-route-stop ride-route-from"><dt>From</dt><dd>{state.booking.pickup}</dd></div>
          {state.booking.via && <div className="ride-route-stop ride-route-via"><dt>Via</dt><dd>{state.booking.via}</dd></div>}
          <div className="ride-route-stop ride-route-to"><dt>To</dt><dd>{state.booking.dropoff}</dd></div>
        </div></div>
        <div className="ride-panel ride-vehicle"><span className="ride-panel-icon"><RideIcon name="car" /></span><div><dt>Vehicle</dt><dd>{state.booking.vehicleType}</dd></div></div>
        <div className="ride-stats">
          <div className="ride-panel ride-stat"><RideIcon name="person" /><div><dt>Passengers</dt><dd>{state.booking.passengerCount}</dd></div></div>
          <div className="ride-panel ride-stat"><RideIcon name="person" /><div><dt>Children</dt><dd>{state.booking.kidsCount}</dd></div></div>
          <div className="ride-panel ride-stat"><RideIcon name="bag" /><div><dt>Baggage</dt><dd>{state.booking.baggageCount}</dd></div></div>
          <div className="ride-panel ride-stat"><RideIcon name="distance" /><div><dt>Est. distance</dt><dd>{state.booking.estimatedDistanceMiles === null ? 'Not available' : <>{state.booking.estimatedDistanceMiles.toLocaleString()} <small>miles</small></>}</dd></div></div>
        </div>
        <div className="ride-panel ride-amount"><span className="ride-amount-icon" aria-hidden="true">$</span><div><dt>Estimated amount</dt><dd>{state.booking.estimatedAmountDollars.toLocaleString('en-US', { style: 'currency', currency: 'USD' })}</dd></div></div>
        {state.booking.preferences && <div className="ride-panel ride-preferences"><dt>Preferences</dt><dd>{state.booking.preferences}</dd></div>}
      </dl>}
    </div>
    <footer className="ride-dialog-footer"><button type="button" onClick={() => dialog.current?.close()}>Close</button></footer>
  </dialog>
}
