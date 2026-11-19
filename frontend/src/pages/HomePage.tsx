import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { authApiBaseUrl } from '../auth/api'
import AuthModal from '../auth/AuthModal'
import './HomePage.css'

const CURRENT_YEAR = 2026

const services = [
  { icon: '✦', label: 'Airport pick-up & drop-off' },
  { icon: '◷', label: 'Hourly chauffeur service' },
  { icon: '♛', label: 'Special occasions' },
  { icon: '▣', label: 'Corporate transportation' },
  { icon: '✧', label: 'Events & celebrations' },
  { icon: '✓', label: 'Professional chauffeurs' },
]

export default function HomePage() {
  const { customer, loading, sessionError, setCustomer, setSessionError } = useAuth()
  const location = useLocation()
  const [authOpen, setAuthOpen] = useState(() => Boolean(location.state?.openAuth))
  const [signOutError, setSignOutError] = useState('')
  const navigate = useNavigate()

  function bookRide() {
    if (customer) navigate('/booking')
    else setAuthOpen(true)
  }

  async function signOut() {
    setSignOutError('')
    try {
      const response = await fetch(`${authApiBaseUrl}/api/auth/logout`, { method: 'POST', credentials: 'include' })
      if (!response.ok) throw new Error(`Could not sign out (HTTP ${response.status}).`)
      setCustomer(null)
    } catch (error) {
      setSignOutError(error instanceof Error ? error.message : 'Could not sign out.')
    }
  }

  return (
    <>
    <main className="public-home">
      <div className="public-home-hero" role="img" aria-label="Luxury stretch limousine in a city at night" />
      <header className="public-home-intro">
        <h1>Royal Limo Service</h1>
        <p className="public-home-tagline">Luxury Chauffeur &amp; Limousine Services</p>
        <p className="public-home-description">
          Travel in comfort and style. Royal Limo Service provides premium chauffeured transportation
          for airport transfers, special occasions, business travel and memorable nights out.
        </p>
        {customer && <div className="public-home-account"><span>Signed in as {customer.name}</span><button type="button" onClick={signOut}>Sign out</button></div>}
        <div className="public-home-actions public-home-hero-actions">
          <a className="public-home-button public-home-call" href="tel:+14049187449">☎ Call</a>
          <button className="public-home-button public-home-primary" type="button" onClick={bookRide} disabled={loading}>{loading ? 'Checking sign-in…' : 'Book a Ride'}</button>
          <a className="public-home-button public-home-secondary" href="/assets/red-line-luxe-contact.vcf" download="Red-Line-Luxe-Contact.vcf">♙ Save Contact</a>
        </div>
        {sessionError && <p className="auth-message error" role="alert">{sessionError} You can still try to sign in.</p>}
        {signOutError && <p className="auth-message error" role="alert">{signOutError}</p>}
      </header>

      <section className="public-home-section" aria-labelledby="public-services-title">
        <h2 className="public-home-section-title" id="public-services-title">Our services</h2>
        <div className="public-home-services">
          {services.map((service) => (
            <div className="public-home-service" key={service.label}>
              <span className="public-home-mark" aria-hidden="true">{service.icon}</span>
              <span>{service.label}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="public-home-contact" aria-labelledby="public-contact-title">
        <h2 className="public-home-section-title" id="public-contact-title">Contact Royal Limo Service</h2>
        <div className="public-home-contact-row">
          <span className="public-home-icon" aria-hidden="true">♙</span>
          <span><small>Contact</small>Akbar Ajani (Alex)</span>
        </div>
        <div className="public-home-contact-row">
          <span className="public-home-icon" aria-hidden="true">☎</span>
          <span><small>Phone</small><a href="tel:+14049187449">+1 (404) 918-7449</a></span>
        </div>
        <div className="public-home-contact-row">
          <span className="public-home-icon" aria-hidden="true">◉</span>
          <span><small>WhatsApp</small><a href="https://wa.me/14046107893" target="_blank" rel="noopener noreferrer">+1 (404) 610-7893</a></span>
        </div>
        <div className="public-home-contact-row">
          <span className="public-home-icon" aria-hidden="true">✉</span>
          <span><small>Email</small><a href="mailto:ajaniakbar@gmail.com">ajaniakbar@gmail.com</a></span>
        </div>
        <div className="public-home-contact-row">
          <span className="public-home-icon" aria-hidden="true">⌖</span>
          <span><small>Service area</small>Atlanta, Georgia</span>
        </div>
      </section>

      <footer className="public-home-footer">
        © {CURRENT_YEAR} Royal Limo Service · Luxury Chauffeur &amp; Limousine Services
      </footer>
    </main>
    {authOpen && <AuthModal onClose={() => setAuthOpen(false)} onSuccess={(authenticatedCustomer) => {
      setCustomer(authenticatedCustomer)
      setSessionError(null)
      setAuthOpen(false)
      navigate(location.state?.returnTo ?? '/booking', { replace: true })
    }} />}
    </>
  )
}
