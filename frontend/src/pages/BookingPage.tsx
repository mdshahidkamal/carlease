import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'

interface PlaceValue { placeId: string; latitude: number | null; longitude: number | null }

declare global {
  interface Window { google?: any }
}

const vehicles = [
  { name: 'Executive Sedan', capacity: 3, image: 'https://images.unsplash.com/photo-1552519507-da3b142c6e3d?auto=format&fit=crop&w=640&q=80' },
  { name: 'Luxury SUV', capacity: 6, image: 'https://images.unsplash.com/photo-1519641471654-76ce0107ad1b?auto=format&fit=crop&w=640&q=80' },
  { name: 'Stretch Limousine', capacity: 8, image: 'https://images.unsplash.com/photo-1563720223185-11003d516935?auto=format&fit=crop&w=640&q=80' },
  { name: 'Luxury Van', capacity: 12, image: 'https://images.unsplash.com/photo-1511919884226-fd3cad34687c?auto=format&fit=crop&w=640&q=80' },
]

export default function BookingPage() {
  const [vehicle, setVehicle] = useState('Executive Sedan')
  const [passengers, setPassengers] = useState(1)
  const [status, setStatus] = useState<{ type: 'idle' | 'success' | 'error'; text: string }>({ type: 'idle', text: '' })
  const [pickupPlace, setPickupPlace] = useState<PlaceValue>({ placeId: '', latitude: null, longitude: null })
  const [dropoffPlace, setDropoffPlace] = useState<PlaceValue>({ placeId: '', latitude: null, longitude: null })
  const pickupRef = useRef<HTMLDivElement>(null)
  const dropoffRef = useRef<HTMLDivElement>(null)
  const selectedVehicle = vehicles.find((item) => item.name === vehicle) ?? vehicles[0]
  const today = new Date().toLocaleDateString('en-CA')

  useEffect(() => {
    if (passengers > selectedVehicle.capacity) setPassengers(selectedVehicle.capacity)
  }, [selectedVehicle.capacity, passengers])

  useEffect(() => {
    const key = import.meta.env.VITE_GOOGLE_MAPS_API_KEY
    if (!key) return
    const id = 'royal-limo-maps'
    if (document.getElementById(id)) return
    const script = document.createElement('script')
    script.id = id
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}&libraries=places&loading=async`
    script.async = true
    script.onload = () => setupPlaceAutocomplete()
    document.head.appendChild(script)
  }, [])

  async function setupPlaceAutocomplete() {
    if (!window.google?.maps?.places) return
    await window.google.maps.importLibrary('places')
    // The web component is dynamically mounted so it remains usable on mobile browsers.
    setupPlace(pickupRef.current, setPickupPlace, 'Search pickup address or place')
    setupPlace(dropoffRef.current, setDropoffPlace, 'Search destination address or place')
  }

  async function setupPlace(container: HTMLDivElement | null, setPlace: (value: PlaceValue) => void, placeholder: string) {
    if (!container || !window.google) return
    container.innerHTML = ''
    const element = new window.google.maps.places.PlaceAutocompleteElement({}) as unknown as HTMLElement
    element.setAttribute('placeholder', placeholder)
    element.className = 'place-autocomplete'
    container.appendChild(element)
    element.addEventListener('gmp-placeselect', async (event: Event) => {
      const placeEvent = event as Event & { place?: any }
      if (!placeEvent.place) return
      await placeEvent.place.fetchFields({ fields: ['displayName', 'formattedAddress', 'location', 'id'] })
      setPlace({ placeId: placeEvent.place.id ?? '', latitude: placeEvent.place.location?.lat() ?? null, longitude: placeEvent.place.location?.lng() ?? null })
    })
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setStatus({ type: 'idle', text: '' })
    const form = new FormData(event.currentTarget)
    const payload = Object.fromEntries(form.entries())
    try {
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5292'}/api/bookings`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...payload,
          pickup: payload.pickup,
          dropoff: payload.dropoff,
          pickupPlaceId: pickupPlace.placeId,
          pickupLatitude: pickupPlace.latitude,
          pickupLongitude: pickupPlace.longitude,
          dropoffPlaceId: dropoffPlace.placeId,
          dropoffLatitude: dropoffPlace.latitude,
          dropoffLongitude: dropoffPlace.longitude,
          pickupDate: payload.pickupDate,
          pickupTime: payload.pickupTime,
          vehicleType: vehicle,
          passengerCount: Number(payload.passengerCount),
          kidsCount: Number(payload.kidsCount || 0),
          baggageCount: Number(payload.baggageCount || 0),
        }),
      })
      const result = await response.json().catch(() => ({})) as { message?: string; bookingReference?: string }
      if (!response.ok) throw new Error(result.message ?? 'We could not submit your booking.')
      setStatus({ type: 'success', text: `Booking registered${result.bookingReference ? ` — ${result.bookingReference}` : '.'}` })
      event.currentTarget.reset()
      setVehicle('Executive Sedan')
      setPassengers(1)
      pickupRef.current?.replaceChildren()
      dropoffRef.current?.replaceChildren()
      void setupPlaceAutocomplete()
    } catch (error) {
      setStatus({ type: 'error', text: error instanceof Error ? error.message : 'Booking submission failed.' })
    }
  }

  return (
    <main className="booking-shell">
      <nav className="booking-top"><Link className="brand" to="/"><span className="brand-mark">R</span><span><b>Royal Limo Service</b><small>Luxury Chauffeur Services</small></span></Link><Link className="back-link" to="/">← Home</Link></nav>
      <header className="booking-heading"><span className="eyebrow">Atlanta, Georgia</span><h1>Book your ride</h1><p>Enter your trip details, choose your vehicle and send your booking request.</p></header>
      <form className="booking-card" onSubmit={handleSubmit}>
        <section><h2><span>1</span>Journey details</h2>
          <div className="grid"><div className="field full"><label>From / Pickup location *</label><div ref={pickupRef} className="place-field"><input name="pickup" required placeholder="Search pickup address or place" /></div></div>
          <div className="field full"><label>To / Drop-off location *</label><div ref={dropoffRef} className="place-field"><input name="dropoff" required placeholder="Search destination address or place" /></div></div>
          <div className="field"><label>Date *</label><input name="pickupDate" type="date" min={today} required /></div><div className="field"><label>Time *</label><input name="pickupTime" type="time" required /></div></div>
        </section>
        <section><h2><span>2</span>Select your vehicle</h2><div className="vehicle-grid">{vehicles.map((item) => <label className={`vehicle-card ${vehicle === item.name ? 'selected' : ''}`} key={item.name}><input type="radio" name="vehicleType" value={item.name} checked={vehicle === item.name} onChange={() => setVehicle(item.name)} /><img src={item.image} alt={item.name} /><div><strong>{item.name}</strong><small>Maximum {item.capacity} passengers</small>{vehicle === item.name && <b>✓ Selected</b>}</div></label>)}</div></section>
        <section><h2><span>3</span>Passengers & preferences</h2><div className="count-grid"><div><label>Passenger count *</label><input name="passengerCount" type="number" min="1" max={selectedVehicle.capacity} value={passengers} onChange={(event) => setPassengers(Number(event.target.value))} required /></div><div><label>Children</label><input name="kidsCount" type="number" min="0" max="12" defaultValue="0" /></div><div><label>Baggage count</label><input name="baggageCount" type="number" min="0" max="30" defaultValue="0" /></div></div><div className="field preference"><label>Preference, if any</label><textarea name="preferences" maxLength={1500} placeholder="Child seat, luggage notes, preferred route or additional stop..." /></div></section>
        <section><h2><span>4</span>Client details</h2><div className="grid"><div className="field"><label>Name *</label><input name="customerName" required autoComplete="name" maxLength={120} placeholder="Your full name" /></div><div className="field"><label>Phone number *</label><input name="phone" type="tel" required autoComplete="tel" maxLength={30} placeholder="+1 (___) ___-____" /></div><div className="field full"><label>Email address <small>(optional)</small></label><input name="email" type="email" autoComplete="email" placeholder="you@example.com" /></div></div></section>
        <div className="submit"><button type="submit">Book My Cab →</button><p>Submitting creates a booking request. Confirmation is provided by Royal Limo Service.</p></div>
        {status.type !== 'idle' && <div className={`notice ${status.type}`}>{status.text}</div>}
      </form>
      <footer>© {new Date().getFullYear()} Royal Limo Service · Atlanta, Georgia</footer>
    </main>
  )
}
