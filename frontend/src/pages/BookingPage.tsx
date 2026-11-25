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

function isVehicle(value: unknown): value is Vehicle {
    if (!value || typeof value !== 'object') return false
    return 'id' in value && typeof value.id === 'string' && 'vehicleName' in value && typeof value.vehicleName === 'string' &&
        'passengerCapacity' in value && typeof value.passengerCapacity === 'number' && Number.isSafeInteger(value.passengerCapacity) && value.passengerCapacity > 0 &&
        'luggageCapacity' in value && typeof value.luggageCapacity === 'number' && Number.isSafeInteger(value.luggageCapacity) && value.luggageCapacity >= 0
}

function canAccommodate(vehicle: Vehicle, passengers: number, baggage: number) {
    return vehicle.passengerCapacity >= passengers && vehicle.luggageCapacity >= baggage
}

function localDate(date: Date) {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function currentTime(date: Date) {
    return { hour: String(date.getHours() % 12 || 12), minute: String(date.getMinutes()), period: date.getHours() < 12 ? 'AM' as const : 'PM' as const }
}

const MIN_LEAD_HOURS = 24

function earliestPickup(now: Date) {
    const earliest = new Date(now.getTime() + MIN_LEAD_HOURS * 60 * 60 * 1000)
    earliest.setSeconds(0, 0)
    return earliest
}

function formatEarliest(date: Date) {
    return date.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })
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
    const [passengers, setPassengers] = useState('')
    const [baggage, setBaggage] = useState('')
    const [preferences, setPreferences] = useState('')
    const [selectionCleared, setSelectionCleared] = useState(false)
    const [clock, setClock] = useState(() => new Date())
    const [pickupDate, setPickupDate] = useState(() => localDate(earliestPickup(new Date())))
    const [initialTime] = useState(() => currentTime(earliestPickup(new Date())))
    const [pickupHour, setPickupHour] = useState(initialTime.hour)
    const [pickupMinute, setPickupMinute] = useState(initialTime.minute)
    const [pickupPeriod, setPickupPeriod] = useState<'AM' | 'PM'>(initialTime.period)
    const [status, setStatus] = useState<{ type: 'idle' | 'success' | 'error'; text: string }>({ type: 'idle', text: '' })
    const [pickupPlace, setPickupPlace] = useState<PlaceValue>(emptyPlace)
    const [dropoffPlace, setDropoffPlace] = useState<PlaceValue>(emptyPlace)
    const [distance, setDistance] = useState<{ key: string; miles: number } | null>(null)
    const [locationReset, setLocationReset] = useState(0)
    const passengerCount = passengers === '' ? null : Number(passengers)
    const baggageCount = baggage === '' ? null : Number(baggage)
    const validCounts = passengerCount !== null && Number.isInteger(passengerCount) && passengerCount >= 1 && passengerCount <= 12 && baggageCount !== null && Number.isInteger(baggageCount) && baggageCount >= 0 && baggageCount <= 30
    const eligibleVehicles = validCounts ? vehicles.filter((vehicle) => canAccommodate(vehicle, passengerCount, baggageCount)) : []
    const preferredVehicles = vehicles.filter((vehicle) => preferences.toLocaleLowerCase().includes(vehicle.vehicleName.toLocaleLowerCase()))
    const prioritizedVehicles = [...eligibleVehicles].sort((a, b) => Number(preferredVehicles.includes(b)) - Number(preferredVehicles.includes(a)))
    const unavailablePreferences = validCounts ? preferredVehicles.filter((vehicle) => !eligibleVehicles.includes(vehicle)) : []
    const selectedVehicle = eligibleVehicles.find((item) => item.id === vehicleId)
    const noPassengerCapacity = validCounts && !vehicles.some((vehicle) => vehicle.passengerCapacity >= passengerCount)
    const LocationInput = useGoogle ? GoogleLocationInput : ManualLocationInput
    const hour24 = (Number(pickupHour) % 12) + (pickupPeriod === 'PM' ? 12 : 0)
    const selectedTime = new Date(`${pickupDate}T${String(hour24).padStart(2, '0')}:${String(Number(pickupMinute)).padStart(2, '0')}:00`)
    const earliestTime = earliestPickup(clock)
    const invalidPickup = !pickupDate || Number.isNaN(selectedTime.getTime()) || localDate(selectedTime) !== pickupDate || selectedTime.getHours() !== hour24 || selectedTime.getMinutes() !== Number(pickupMinute) || selectedTime < earliestTime


    useEffect(() => {
        const timer = window.setInterval(() => setClock(new Date()), 30_000)
        return () => window.clearInterval(timer)
    }, [])

    useEffect(() => {
        const controller = new AbortController()
        void fetch(`${authApiBaseUrl}/api/vehicles`, { credentials: 'include', signal: controller.signal })
            .then(async (response) => {
                if (!response.ok) throw new Error(`Could not load vehicles (HTTP ${response.status}).`)
                const data: unknown = await response.json()
                if (!Array.isArray(data) || !data.every(isVehicle)) throw new Error('Invalid vehicle configuration returned by the server.')
                if (controller.signal.aborted) return
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
        const earliest = earliestPickup(new Date())
        if (!pickupDate || Number.isNaN(selectedTime.getTime()) || localDate(selectedTime) !== pickupDate || selectedTime.getHours() !== hour24 || selectedTime.getMinutes() !== minute || selectedTime < earliest) {
            setStatus({ type: 'error', text: `Pickups must be booked at least ${MIN_LEAD_HOURS} hours in advance. Earliest available: ${formatEarliest(earliest)}.` })
            return
        }
        if (passengerCount === null) {
            setStatus({ type: 'error', text: 'Please enter the passenger count' })
            return
        }
        if (baggageCount === null) {
            setStatus({ type: 'error', text: 'Please enter the baggage count' })
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
            setPassengers('')
            setBaggage('')
            setPreferences('')
            setSelectionCleared(false)
            const resetTime = new Date()
            const resetEarliest = earliestPickup(resetTime)
            const time = currentTime(resetEarliest)
            setClock(resetTime)
            setPickupDate(localDate(resetEarliest))
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
                        <div className="field full"><label htmlFor="pickup-date">Pickup date *</label><input className="booking-date-input" id="pickup-date" name="pickupDate" type="date" min={localDate(earliestTime)} value={pickupDate} onFocus={() => setClock(new Date())} onChange={(event) => setPickupDate(event.target.value)} required /></div>
                        <div className="field full"><label htmlFor="pickup-hour">Pickup time *</label><div className="pickup-time-control"><select id="pickup-hour" aria-label="Pickup hour" value={pickupHour} onChange={(event) => setPickupHour(event.target.value)}>{Array.from({ length: 12 }, (_, index) => <option key={index + 1} value={index + 1}>{index + 1}</option>)}</select><span aria-hidden="true">:</span><select id="pickup-minute" aria-label="Pickup minute" value={pickupMinute} onChange={(event) => setPickupMinute(event.target.value)}>{Array.from({ length: 60 }, (_, index) => <option key={index} value={index}>{String(index).padStart(2, '0')}</option>)}</select><select id="pickup-period" aria-label="AM or PM" value={pickupPeriod} onChange={(event) => setPickupPeriod(event.target.value as 'AM' | 'PM')}><option value="AM">AM</option><option value="PM">PM</option></select></div>{invalidPickup && <p className="map-search-error" role="alert">Pickups must be booked at least {MIN_LEAD_HOURS} hours in advance. Earliest available: {formatEarliest(earliestTime)}.</p>}</div></div>
                    {mapsError && <p className="map-search-error" role="alert">{mapsError}</p>}
                </section>
                <section><h2><span>2</span>Passengers & preferences</h2><div className="count-grid"><div><label htmlFor="passenger-count">Passenger count *</label><input id="passenger-count" name="passengerCount" type="number" min="1" max="12" value={passengers} onChange={(event) => { const value = event.target.value; setPassengers(value); if (vehicleId && (!value || Number(value) < 1 || Number(value) > 12 || !Number.isInteger(Number(value)) || Number(value) > (selectedVehicle?.passengerCapacity ?? 0))) { setVehicleId(''); setSelectionCleared(true) } }} required />{passengers === '' && <p className="vehicle-capacity">Please enter the passenger count</p>}</div><div><label htmlFor="kids-count">Children</label><input id="kids-count" name="kidsCount" type="number" min="0" max="12" defaultValue="0" /></div><div><label htmlFor="baggage-count">Baggage count *</label><input id="baggage-count" name="baggageCount" type="number" min="0" max="30" value={baggage} onChange={(event) => { const value = event.target.value; setBaggage(value); if (vehicleId && (!value || Number(value) < 0 || Number(value) > 30 || !Number.isInteger(Number(value)) || Number(value) > (selectedVehicle?.luggageCapacity ?? -1))) { setVehicleId(''); setSelectionCleared(true) } }} required />{baggage === '' && <p className="vehicle-capacity">Please enter the baggage count</p>}</div></div><div className="field preference"><label htmlFor="preferences">Preference, if any</label><textarea id="preferences" name="preferences" value={preferences} onChange={(event) => setPreferences(event.target.value)} maxLength={1500} placeholder="Child seat, extra luggage, preferred vehicle or other requests..." /><p className="vehicle-capacity">Mention a vehicle's full name to prioritize it when available.</p></div></section>
                <section><h2><span>3</span>Select your vehicle</h2><div className="field"><label htmlFor="vehicle">Vehicle *</label><select id="vehicle" value={selectedVehicle?.id ?? ''} onChange={(event) => { setVehicleId(event.target.value); setSelectionCleared(false) }} required disabled={vehiclesLoading || !validCounts || eligibleVehicles.length === 0}><option value="">{vehiclesLoading ? 'Loading vehicles…' : !validCounts ? 'Select passenger and baggage count' : eligibleVehicles.length === 0 ? 'No vehicle available' : 'Select a vehicle'}</option>{prioritizedVehicles.map((item) => <option key={item.id} value={item.id}>{item.vehicleName}{preferredVehicles.includes(item) ? ' (preferred)' : ''}</option>)}</select>{selectedVehicle && <p className="vehicle-capacity">Maximum {selectedVehicle.passengerCapacity} passengers · {selectedVehicle.luggageCapacity} bags</p>}{selectionCleared && <p className="map-search-error" role="alert">Please select another available vehicle</p>}{validCounts && !vehiclesLoading && !vehicleError && eligibleVehicles.length === 0 && <p className="map-search-error" role="alert">No vehicle available for the selected passenger and baggage requirements. {noPassengerCapacity ? 'No vehicle available for the selected passenger count' : 'No vehicle available for the selected baggage count'}</p>}{unavailablePreferences.length > 0 && <p className="map-search-error" role="status">Preferred vehicle {unavailablePreferences.map((item) => item.vehicleName).join(', ')} cannot accommodate the selected passengers or baggage.</p>}{vehicleError && <p className="map-search-error" role="alert">{vehicleError}</p>}</div></section>
                <p className="booking-customer-note">Booking for {customer?.name} · {customer?.email}</p>
                <div className="submit"><button type="submit" disabled={vehiclesLoading || vehicles.length === 0}>Book My Cab →</button><p>Submitting creates a booking request. Confirmation is provided by Royal Limo Service.</p></div>
                {status.type !== 'idle' && <div className={`notice ${status.type}`}>{status.text}</div>}
            </form>
            <footer>© {CURRENT_YEAR} Royal Limo Service · Atlanta, Georgia</footer>
        </main>
    )
}
