import { useCallback, useState } from 'react'
import type { FormEvent } from 'react'
import { APIProvider } from '@vis.gl/react-google-maps'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { authApiBaseUrl } from '../auth/api'
import { GoogleLocationInput, ManualLocationInput } from './LocationInput'
import { emptyPlace } from './placeValue'
import type { PlaceValue } from './placeValue'

const vehicles = [
    { name: 'Executive Sedan', capacity: 3, image: 'https://images.unsplash.com/photo-1552519507-da3b142c6e3d?auto=format&fit=crop&w=640&q=80' },
    { name: 'Luxury SUV', capacity: 6, image: 'https://images.unsplash.com/photo-1519641471654-76ce0107ad1b?auto=format&fit=crop&w=640&q=80' },
    { name: 'Stretch Limousine', capacity: 8, image: 'https://images.unsplash.com/photo-1563720223185-11003d516935?auto=format&fit=crop&w=640&q=80' },
    { name: 'Luxury Van', capacity: 12, image: 'https://images.unsplash.com/photo-1511919884226-fd3cad34687c?auto=format&fit=crop&w=640&q=80' },
]

const TODAY = new Date().toLocaleDateString('en-CA')
const CURRENT_YEAR = new Date().getFullYear()
const MAPS_UNAVAILABLE_MESSAGE = 'Google location search is not configured. Add VITE_GOOGLE_MAPS_API_KEY to frontend/.env.local and restart the frontend.'
const MAPS_LOAD_ERROR_MESSAGE = 'Google location search could not load. Check your connection, API key, enabled APIs, billing, and website referrer restrictions.'
const MAPS_LIBRARIES = ['places']

export default function BookingPage() {
    const key = import.meta.env.VITE_GOOGLE_MAPS_API_KEY?.trim()
    const [mapsError, setMapsError] = useState(key ? '' : MAPS_UNAVAILABLE_MESSAGE)
    const handleMapsError = useCallback((error: unknown) => {
        console.error('Google Maps failed to load.', error)
        setMapsError(MAPS_LOAD_ERROR_MESSAGE)
    }, [])
    const form = <BookingForm useGoogle={Boolean(key) && !mapsError} mapsError={mapsError} />

    if (!key) return form
    return <APIProvider apiKey={key} libraries={MAPS_LIBRARIES} onError={handleMapsError}>{form}</APIProvider>
}

function BookingForm({ useGoogle, mapsError }: { useGoogle: boolean; mapsError: string }) {
    const { customer, setCustomer } = useAuth()
    const navigate = useNavigate()
    const [vehicle, setVehicle] = useState('Executive Sedan')
    const [passengers, setPassengers] = useState(1)
    const [pickupHour, setPickupHour] = useState('')
    const [pickupMinute, setPickupMinute] = useState('00')
    const [pickupPeriod, setPickupPeriod] = useState<'AM' | 'PM'>('AM')
    const [status, setStatus] = useState<{ type: 'idle' | 'success' | 'error'; text: string }>({ type: 'idle', text: '' })
    const [pickupPlace, setPickupPlace] = useState<PlaceValue>(emptyPlace)
    const [dropoffPlace, setDropoffPlace] = useState<PlaceValue>(emptyPlace)
    const [locationReset, setLocationReset] = useState(0)
    const selectedVehicle = vehicles.find((item) => item.name === vehicle) ?? vehicles[0]
    const LocationInput = useGoogle ? GoogleLocationInput : ManualLocationInput

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        const formElement = event.currentTarget
        setStatus({ type: 'idle', text: '' })
        if (!pickupPlace.address.trim() || !dropoffPlace.address.trim()) {
            setStatus({ type: 'error', text: 'Enter both a pickup and destination location before submitting.' })
            return
        }
        const hour = Number(pickupHour)
        const minute = Number(pickupMinute)
        if (!Number.isInteger(hour) || hour < 1 || hour > 12 || !Number.isInteger(minute) || minute < 0 || minute > 59) {
            setStatus({ type: 'error', text: 'Enter a valid pickup time: hour 1–12 and minutes 0–59.' })
            return
        }
        const hour24 = (hour % 12) + (pickupPeriod === 'PM' ? 12 : 0)
        const formattedPickupTime = `${String(hour24).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00`
        const form = new FormData(formElement)
        const payload = Object.fromEntries(form.entries())
        payload.pickupTime = formattedPickupTime
        try {
            const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5292').replace(/\/+$/, '')
            const response = await fetch(`${apiBaseUrl}/api/bookings`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    ...payload,
                    pickup: pickupPlace.address,
                    dropoff: dropoffPlace.address,
                    pickupPlaceId: pickupPlace.placeId || null,
                    pickupLatitude: pickupPlace.latitude,
                    pickupLongitude: pickupPlace.longitude,
                    dropoffPlaceId: dropoffPlace.placeId || null,
                    dropoffLatitude: dropoffPlace.latitude,
                    dropoffLongitude: dropoffPlace.longitude,
                    pickupDate: payload.pickupDate,
                    pickupTime: payload.pickupTime,
                    vehicleType: vehicle,
                    passengerCount: Number(payload.passengerCount),
                    kidsCount: Number(payload.kidsCount || 0),
                    baggageCount: Number(payload.baggageCount || 0),
                }),
                credentials: 'include',
            })
            const result = await response.json().catch(() => ({})) as { message?: string; detail?: string; title?: string; errors?: Record<string, string[] | string>; bookingReference?: string; status?: string }
            const validationErrors = Object.values(result.errors ?? {}).flatMap((messages) => Array.isArray(messages) ? messages : [messages])
            const apiError = result.message || result.detail || validationErrors.join(' ') || result.title
            if (!response.ok) throw new Error(apiError ?? `We could not submit your booking (HTTP ${response.status}).`)
            if (!result.bookingReference || !result.status) throw new Error('Booking was submitted, but confirmation details are unavailable.')
            setStatus({ type: 'success', text: `Booking reference: ${result.bookingReference} | Status: ${result.status}` })
            formElement.reset()
            setVehicle('Executive Sedan')
            setPassengers(1)
            setPickupHour('')
            setPickupMinute('00')
            setPickupPeriod('AM')
            setPickupPlace(emptyPlace)
            setDropoffPlace(emptyPlace)
            setLocationReset((current) => current + 1)
        } catch (error) {
            setStatus({ type: 'error', text: error instanceof Error ? error.message : 'Booking submission failed.' })
        }
    }

    return (
        <main className="booking-shell">
            <section className="booking-hero">
                <nav className="booking-top"><Link className="brand" to="/"><span className="brand-mark">R</span><span><b>Royal Limo Service</b><small>Luxury Chauffeur Services</small></span></Link><span className="booking-account">{customer?.name}</span><button className="back-link booking-signout" type="button" onClick={async () => {
                    try {
                        const response = await fetch(`${authApiBaseUrl}/api/auth/logout`, { method: 'POST', credentials: 'include' })
                        if (!response.ok) throw new Error(`Could not sign out (HTTP ${response.status}).`)
                        setCustomer(null)
                        navigate('/', { replace: true })
                    } catch (error) {
                        setStatus({ type: 'error', text: error instanceof Error ? error.message : 'Could not sign out.' })
                    }
                }}>Sign out</button></nav>
            </section>
            <form className="booking-card" onSubmit={handleSubmit}>
                <section><h2><span>1</span>Journey details</h2>
                    <div className="grid"><LocationInput key={`pickup-${locationReset}`} id="pickup-location" name="pickup" label="From / Pickup location *" placeholder="Search pickup address or place" value={pickupPlace} onChange={setPickupPlace} />
                        <LocationInput key={`dropoff-${locationReset}`} id="dropoff-location" name="dropoff" label="To / Drop-off location *" placeholder="Search destination address or place" value={dropoffPlace} onChange={setDropoffPlace} />
                        <div className="field"><label htmlFor="pickup-date">Pickup date *</label><input className="booking-date-input" id="pickup-date" name="pickupDate" type="date" min={TODAY} required /></div>
                        <div className="field"><label htmlFor="pickup-hour">Pickup time *</label><div className="pickup-time-control"><input id="pickup-hour" type="number" min="1" max="12" step="1" inputMode="numeric" placeholder="HH" aria-label="Pickup hour, 1 to 12" value={pickupHour} onChange={(event) => {
                            const value = event.target.value
                            if (Number(value) > 12) {
                                window.alert('Hour cannot be more than 12 hours.')
                                return
                            }
                            setPickupHour(value)
                        }} required /><span aria-hidden="true">:</span><input id="pickup-minute" type="number" min="0" max="59" step="1" inputMode="numeric" placeholder="MM" aria-label="Pickup minutes, 0 to 59" value={pickupMinute} onChange={(event) => {
                            const value = event.target.value
                            if (Number(value) > 59) {
                                window.alert('Min cannot be more than 59 mins.')
                                return
                            }
                            setPickupMinute(value)
                        }} required /><select id="pickup-period" aria-label="AM or PM" value={pickupPeriod} onChange={(event) => setPickupPeriod(event.target.value as 'AM' | 'PM')}><option value="AM">AM</option><option value="PM">PM</option></select></div></div></div>
                    {mapsError && <p className="map-search-error" role="alert">{mapsError}</p>}
                </section>
                <section><h2><span>2</span>Select your vehicle</h2><div className="vehicle-grid">{vehicles.map((item) => <label className={`vehicle-card ${vehicle === item.name ? 'selected' : ''}`} key={item.name}><input type="radio" name="vehicleType" value={item.name} checked={vehicle === item.name} onChange={() => {
                    setVehicle(item.name)
                    setPassengers((current) => Math.min(current, item.capacity))
                }} /><img src={item.image} alt={item.name} /><div><strong>{item.name}</strong><small>Maximum {item.capacity} passengers</small>{vehicle === item.name && <b>✓ Selected</b>}</div></label>)}</div></section>
                <section><h2><span>3</span>Passengers & preferences</h2><div className="count-grid"><div><label>Passenger count *</label><input name="passengerCount" type="number" min="1" max={selectedVehicle.capacity} value={passengers} onChange={(event) => setPassengers(Number(event.target.value))} required /></div><div><label>Children</label><input name="kidsCount" type="number" min="0" max="12" defaultValue="0" /></div><div><label>Baggage count</label><input name="baggageCount" type="number" min="0" max="30" defaultValue="0" /></div></div><div className="field preference"><label>Preference, if any</label><textarea name="preferences" maxLength={1500} placeholder="Child seat, luggage notes, preferred route or additional stop..." /></div></section>
                <p className="booking-customer-note">Booking for {customer?.name} · {customer?.email}</p>
                <div className="submit"><button type="submit">Book My Cab →</button><p>Submitting creates a booking request. Confirmation is provided by Royal Limo Service.</p></div>
                {status.type !== 'idle' && <div className={`notice ${status.type}`}>{status.text}</div>}
            </form>
            <footer>© {CURRENT_YEAR} Royal Limo Service · Atlanta, Georgia</footer>
        </main>
    )
}
