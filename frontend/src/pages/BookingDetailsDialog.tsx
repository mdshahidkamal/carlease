import { useEffect, useRef, useState } from 'react'
import { authApiBaseUrl } from '../auth/api'

interface BookingDetails {
  bookingReference: string
  pickup: string
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
    <div className="ride-dialog-heading"><h2 id="ride-dialog-title">Ride details: {reference}</h2><button type="button" onClick={() => dialog.current?.close()} aria-label="Close ride details">×</button></div>
    {state.type === 'loading' && <p role="status">Loading ride details...</p>}
    {state.type === 'error' && <p role="alert">{state.message} <button type="button" onClick={() => { setState({ type: 'loading' }); setAttempt((value) => value + 1) }}>Try again</button></p>}
    {state.type === 'loaded' && <dl className="ride-details">
      <div><dt>Status</dt><dd>{state.booking.status}</dd></div>
      <div><dt>Pickup date and time</dt><dd><time dateTime={state.booking.pickupDateTime}>{new Date(state.booking.pickupDateTime).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}</time></dd></div>
      <div><dt>From</dt><dd>{state.booking.pickup}</dd></div>
      <div><dt>To</dt><dd>{state.booking.dropoff}</dd></div>
      <div><dt>Vehicle</dt><dd>{state.booking.vehicleType}</dd></div>
      <div><dt>Passengers</dt><dd>{state.booking.passengerCount}</dd></div>
      <div><dt>Children</dt><dd>{state.booking.kidsCount}</dd></div>
      <div><dt>Baggage</dt><dd>{state.booking.baggageCount}</dd></div>
      <div><dt>Estimated distance</dt><dd>{state.booking.estimatedDistanceMiles === null ? 'Not available' : `${state.booking.estimatedDistanceMiles.toLocaleString()} miles`}</dd></div>
      <div><dt>Estimated amount</dt><dd>{state.booking.estimatedAmountDollars.toLocaleString('en-US', { style: 'currency', currency: 'USD' })}</dd></div>
      {state.booking.preferences && <div><dt>Preferences</dt><dd>{state.booking.preferences}</dd></div>}
    </dl>}
  </dialog>
}
