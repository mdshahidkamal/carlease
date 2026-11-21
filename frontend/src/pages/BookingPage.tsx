import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { APIProvider } from '@vis.gl/react-google-maps'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { authApiBaseUrl } from '../auth/api'
import { GoogleLocationInput, ManualLocationInput } from './LocationInput'
import { RouteDistance } from './RouteDistance'
import { emptyPlace, routeKey } from './placeValue'
import type { PlaceValue } from './placeValue'

interface Vehicle {
    id: string
    vehicleName: string
    passengerCapacity: number
    luggageCapacity: number
}

function localDate(date: Date) {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function currentTime(date: Date) {
    return { hour: String(date.getHours() % 12 || 12), minute: String(date.getMinutes()), period: date.getHours() < 12 ? 'AM' as const : 'PM' as const }
}

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
    const [vehicles, setVehicles] = useState<Vehicle[]>([])
    const [vehicleId, setVehicleId] = useState('')
    const [vehicleError, setVehicleError] = useState('')
    const [vehiclesLoading, setVehiclesLoading] = useState(true)
    const [passengers, setPassengers] = useState(1)
    const [baggage, setBaggage] = useState(0)
    const [clock, setClock] = useState(() => new Date())
    const [pickupDate, setPickupDate] = useState(() => localDate(new Date()))
    const [initialTime] = useState(() => currentTime(new Date()))
    const [pickupHour, setPickupHour] = useState(initialTime.hour)
    const [pickupMinute, setPickupMinute] = useState(initialTime.minute)
    const [pickupPeriod, setPickupPeriod] = useState<'AM' | 'PM'>(initialTime.period)
    const [status, setStatus] = useState<{ type: 'idle' | 'success' | 'error'; text: string }>({ type: 'idle', text: '' })
    const [pickupPlace, setPickupPlace] = useState<PlaceValue>(emptyPlace)
    const [dropoffPlace, setDropoffPlace] = useState<PlaceValue>(emptyPlace)
    const [distance, setDistance] = useState<{ key: string; miles: number } | null>(null)
    const [locationReset, setLocationReset] = useState(0)
    const selectedVehicle = vehicles.find((item) => item.id === vehicleId)
    const LocationInput = useGoogle ? GoogleLocationInput : ManualLocationInput
    const hour24 = (Number(pickupHour) % 12) + (pickupPeriod === 'PM' ? 12 : 0)
    const selectedTime = new Date(`${pickupDate}T${String(hour24).padStart(2, '0')}:${String(Number(pickupMinute)).padStart(2, '0')}:00`)
    const currentMinute = new Date(clock)
    currentMinute.setSeconds(0, 0)
    const invalidPickup = !pickupDate || Number.isNaN(selectedTime.getTime()) || localDate(selectedTime) !== pickupDate || selectedTime.getHours() !== hour24 || selectedTime.getMinutes() !== Number(pickupMinute) || selectedTime < currentMinute

    useEffect(() => {
        const timer = window.setInterval(() => setClock(new Date()), 30_000)
        return () => window.clearInterval(timer)
    }, [])

    useEffect(() => {
        const controller = new AbortController()
        void fetch(`${authApiBaseUrl}/api/vehicles`, { credentials: 'include', signal: controller.signal })
            .then(async (response) => {
                if (!response.ok) throw new Error(`Could not load vehicles (HTTP ${response.status}).`)
                const data: Vehicle[] = await response.json()
                setVehicles(data)
                if (data.length === 0) setVehicleError('No vehicles are currently available.')
            })
            .catch((error: unknown) => {
                if (controller.signal.aborted) return
                console.error('Could not load vehicles.', error)
                setVehicleError('Could not load vehicles. Please refresh the page and try again.')
            })
            .finally(() => { if (!controller.signal.aborted) setVehiclesLoading(false) })
        return () => controller.abort()
    }, [])

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        const formElement = event.currentTarget
        setStatus({ type: 'idle', text: '' })
        if (!pickupPlace.address.trim() || !dropoffPlace.address.trim()) {
            setStatus({ type: 'error', text: 'Enter both a pickup and destination location before submitting.' })
            return
        }
        if (pickupPlace.resolving || dropoffPlace.resolving || !useGoogle || !distance || distance.key !== routeKey(pickupPlace, dropoffPlace)) {
            setStatus({ type: 'error', text: 'A driving distance is required. Wait for the route calculation or check the locations and Google Maps connection.' })
            return
        }
        const hour = Number(pickupHour)
        const minute = Number(pickupMinute)
        if (!Number.isInteger(hour) || hour < 1 || hour > 12 || !Number.isInteger(minute) || minute < 0 || minute > 59) {
            setStatus({ type: 'error', text: 'Enter a valid pickup time: hour 1–12 and minutes 0–59.' })
            return
        }
        const hour24 = (hour % 12) + (pickupPeriod === 'PM' ? 12 : 0)
        const selectedTime = new Date(`${pickupDate}T${String(hour24).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00`)
        const now = new Date()
        now.setSeconds(0, 0)
        if (!pickupDate || Number.isNaN(selectedTime.getTime()) || localDate(selectedTime) !== pickupDate || selectedTime.getHours() !== hour24 || selectedTime.getMinutes() !== minute || selectedTime < now) {
            setStatus({ type: 'error', text: 'Select a valid pickup date and time that is not in the past.' })
            return
        }
        if (!selectedVehicle) {
            setStatus({ type: 'error', text: 'Please select an available vehicle.' })
            return
        }
        const form = new FormData(formElement)
        const payload = Object.fromEntries(form.entries())
        try {
            const response = await fetch(`${authApiBaseUrl}/api/bookings`, {
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
                    estimatedDistanceMiles: distance.miles,
                    pickupAtUtc: selectedTime.toISOString(),
                    vehicleId: selectedVehicle.id,
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
            setVehicleId('')
            setPassengers(1)
            setBaggage(0)
            const resetTime = new Date()
            const time = currentTime(resetTime)
            setClock(resetTime)
            setPickupDate(localDate(resetTime))
            setPickupHour(time.hour)
            setPickupMinute(time.minute)
            setPickupPeriod(time.period)
            setPickupPlace(emptyPlace)
            setDropoffPlace(emptyPlace)
            setDistance(null)
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
                    <div className="grid"><LocationInput key={`pickup-${locationReset}`} id="pickup-location" name="pickup" label="From / Pickup location *" placeholder="Search pickup address or place" value={pickupPlace} onChange={(value) => { if (value.resolving || routeKey(value, dropoffPlace) !== routeKey(pickupPlace, dropoffPlace)) setDistance(null); setPickupPlace(value) }} />
                        <LocationInput key={`dropoff-${locationReset}`} id="dropoff-location" name="dropoff" label="To / Drop-off location *" placeholder="Search destination address or place" value={dropoffPlace} onChange={(value) => { if (value.resolving || routeKey(pickupPlace, value) !== routeKey(pickupPlace, dropoffPlace)) setDistance(null); setDropoffPlace(value) }} />
                        <RouteDistance pickup={pickupPlace} dropoff={dropoffPlace} useGoogle={useGoogle} onDistance={setDistance} />
                        <div className="field full"><label htmlFor="pickup-date">Pickup date *</label><input className="booking-date-input" id="pickup-date" name="pickupDate" type="date" min={localDate(clock)} value={pickupDate} onFocus={() => setClock(new Date())} onChange={(event) => setPickupDate(event.target.value)} required /></div>
                        <div className="field full"><label htmlFor="pickup-hour">Pickup time *</label><div className="pickup-time-control"><select id="pickup-hour" aria-label="Pickup hour" value={pickupHour} onChange={(event) => setPickupHour(event.target.value)}>{Array.from({ length: 12 }, (_, index) => <option key={index + 1} value={index + 1}>{index + 1}</option>)}</select><span aria-hidden="true">:</span><select id="pickup-minute" aria-label="Pickup minute" value={pickupMinute} onChange={(event) => setPickupMinute(event.target.value)}>{Array.from({ length: 60 }, (_, index) => <option key={index} value={index}>{String(index).padStart(2, '0')}</option>)}</select><select id="pickup-period" aria-label="AM or PM" value={pickupPeriod} onChange={(event) => setPickupPeriod(event.target.value as 'AM' | 'PM')}><option value="AM">AM</option><option value="PM">PM</option></select></div>{invalidPickup && <p className="map-search-error" role="alert">Select a valid pickup date and time that is not in the past.</p>}</div></div>
                    {mapsError && <p className="map-search-error" role="alert">{mapsError}</p>}
                </section>
                <section><h2><span>2</span>Select your vehicle</h2><div className="field"><label htmlFor="vehicle">Vehicle *</label><select id="vehicle" value={vehicleId} onChange={(event) => {
                    const id = event.target.value
                    setVehicleId(id)
                    const selected = vehicles.find((item) => item.id === id)
                    if (selected) {
                        setPassengers(selected.passengerCapacity)
                        setBaggage(selected.luggageCapacity)
                    } else {
                        setPassengers(1)
                        setBaggage(0)
                    }
                }} required disabled={vehiclesLoading || vehicles.length === 0}><option value="">{vehiclesLoading ? 'Loading vehicles…' : 'Select a vehicle'}</option>{vehicles.map((item) => <option key={item.id} value={item.id}>{item.vehicleName}</option>)}</select>{selectedVehicle && <p className="vehicle-capacity">Maximum {selectedVehicle.passengerCapacity} passengers · {selectedVehicle.luggageCapacity} bags</p>}{vehicleError && <p className="map-search-error" role="alert">{vehicleError}</p>}</div></section>
                <section><h2><span>3</span>Passengers & preferences</h2><div className="count-grid"><div><label htmlFor="passenger-count">Passenger count *</label><input id="passenger-count" name="passengerCount" type="number" min="1" max={Math.min(selectedVehicle?.passengerCapacity ?? 12, 12)} value={passengers} onChange={(event) => setPassengers(Number(event.target.value))} required /></div><div><label>Children</label><input name="kidsCount" type="number" min="0" max="12" defaultValue="0" /></div><div><label htmlFor="baggage-count">Baggage count</label><input id="baggage-count" name="baggageCount" type="number" min="0" max={Math.min(selectedVehicle?.luggageCapacity ?? 30, 30)} value={baggage} onChange={(event) => setBaggage(Number(event.target.value))} /></div></div><div className="field preference"><label>Preference, if any</label><textarea name="preferences" maxLength={1500} placeholder="Child seat, luggage notes, preferred route or additional stop..." /></div></section>
                <p className="booking-customer-note">Booking for {customer?.name} · {customer?.email}</p>
                <div className="submit"><button type="submit" disabled={vehiclesLoading || vehicles.length === 0}>Book My Cab →</button><p>Submitting creates a booking request. Confirmation is provided by Royal Limo Service.</p></div>
                {status.type !== 'idle' && <div className={`notice ${status.type}`}>{status.text}</div>}
            </form>
            <footer>© {CURRENT_YEAR} Royal Limo Service · Atlanta, Georgia</footer>
        </main>
    )
}
