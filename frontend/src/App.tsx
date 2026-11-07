import { BrowserRouter, Link, Route, Routes } from 'react-router-dom'
import BookingPage from './pages/BookingPage'
import './App.css'

function HomePage() {
  return (
    <main className="home-page">
      <section className="home-hero">
        <div className="home-overlay" />
        <nav className="home-nav">
          <Link className="brand" to="/">
            <span className="brand-mark">R</span>
            <span>
              <b>Royal Limo Service</b>
              <small>Luxury Chauffeur Services</small>
            </span>
          </Link>
          <Link className="nav-book" to="/booking">
            Book a ride
          </Link>
        </nav>
        <div className="hero-copy">
          <span className="eyebrow">Atlanta, Georgia</span>
          <h1>
            Your journey,
            <br />
            <em>elevated.</em>
          </h1>
          <p>
            Premium chauffeured transportation for airport transfers, special
            occasions, business travel and memorable nights out.
          </p>
          <Link className="hero-button" to="/booking">
            Reserve your ride <span>→</span>
          </Link>
        </div>
      </section>
      <section className="services-section">
        <p className="section-label">Our services</p>
        <div className="services-grid">
          {[
            'Airport transfers',
            'Hourly chauffeur',
            'Special occasions',
            'Corporate travel',
            'Events & celebrations',
            'Professional chauffeurs',
          ].map((service) => (
            <div className="service-card" key={service}>
              <span>✦</span>
              <strong>{service}</strong>
            </div>
          ))}
        </div>
      </section>
      <footer className="home-footer">
        © {new Date().getFullYear()} Royal Limo Service · Atlanta, Georgia
      </footer>
    </main>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/booking" element={<BookingPage />} />
      </Routes>
    </BrowserRouter>
  )
}
