import { Link } from 'react-router-dom'
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
  return (
    <main className="public-home">
      <div className="public-home-hero" role="img" aria-label="Luxury stretch limousine in a city at night" />
      <header className="public-home-intro">
        <span className="public-home-eyebrow">Your Journey, Elevated</span>
        <h1>Royal Limo Service</h1>
        <p className="public-home-tagline">Luxury Chauffeur &amp; Limousine Services</p>
        <p className="public-home-description">
          Travel in comfort and style. Royal Limo Service provides premium chauffeured transportation
          for airport transfers, special occasions, business travel and memorable nights out.
        </p>
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
        <div className="public-home-actions">
          <a className="public-home-button public-home-primary" href="tel:+14049187449">☎ Call</a>
          <Link className="public-home-button public-home-secondary" to="/booking">Book a ride</Link>
          <a className="public-home-button public-home-secondary" href="/assets/red-line-luxe-contact.vcf" download="Red-Line-Luxe-Contact.vcf">♙ Save Contact</a>
        </div>
      </section>

      <footer className="public-home-footer">
        © {CURRENT_YEAR} Royal Limo Service · Luxury Chauffeur &amp; Limousine Services
      </footer>
    </main>
  )
}
