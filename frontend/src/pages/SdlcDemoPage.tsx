import { useState } from 'react'
import { Link } from 'react-router-dom'
import { authApiBaseUrl } from '../auth/api'

interface SdlcDemoRecord {
  id: number
  message: string
  environment: string
  createdAtUtc: string
}

function isSdlcDemoRecord(value: unknown): value is SdlcDemoRecord {
  if (typeof value !== 'object' || value === null) return false
  const record = value as Record<string, unknown>
  return typeof record.id === 'number'
    && typeof record.message === 'string'
    && typeof record.environment === 'string'
    && typeof record.createdAtUtc === 'string'
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return `${new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  }).format(date)} UTC`
}

export default function SdlcDemoPage() {
  const [records, setRecords] = useState<SdlcDemoRecord[]>([])
  const [hasLoaded, setHasLoaded] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  async function loadRecords() {
    setIsLoading(true)
    setError('')

    try {
      const response = await fetch(`${authApiBaseUrl}/api/sdlc-demo`)
      if (!response.ok) {
        throw new Error(`The API returned ${response.status} ${response.statusText}.`)
      }

      const result: unknown = await response.json()
      if (!Array.isArray(result) || !result.every(isSdlcDemoRecord)) {
        throw new Error('The API returned data in an unexpected format.')
      }

      setRecords(result)
      setHasLoaded(true)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not load SDLC demo records.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <main className="booking-shell demo-shell">
      <header className="booking-top">
        <Link className="brand" to="/">
          <span className="brand-mark">R</span>
          <span>
            <b>Royal Limo Service</b>
            <small>SDLC API proof of concept</small>
          </span>
        </Link>
        <Link className="back-link" to="/">Back to home</Link>
      </header>

      <section className="booking-heading">
        <span className="eyebrow">Backend integration</span>
        <h1>SDLC demo records</h1>
        <p>Request records from the SDLC demo API and view the response below.</p>
      </section>

      <section className="demo-card" aria-labelledby="demo-table-heading">
        <div className="demo-card-header">
          <div>
            <h2 id="demo-table-heading">API response</h2>
            <p>GET /api/sdlc-demo</p>
          </div>
          <button className="demo-load-button" type="button" onClick={loadRecords} disabled={isLoading}>
            {isLoading ? 'Loading…' : hasLoaded ? 'Refresh records' : 'Load records'}
          </button>
        </div>

        {error && <p className="demo-error" role="alert">{error}</p>}
        {isLoading && <p className="demo-status" role="status">Loading records from the API…</p>}

        {!hasLoaded && !isLoading && !error && (
          <p className="demo-status">Click “Load records” to call the SDLC demo API.</p>
        )}

        {hasLoaded && records.length === 0 && !isLoading && (
          <p className="demo-status" role="status">The API returned no records.</p>
        )}

        {records.length > 0 && (
          <div className="demo-table-wrap">
            <table className="demo-table">
              <thead>
                <tr>
                  <th scope="col">ID</th>
                  <th scope="col">Message</th>
                  <th scope="col">Environment</th>
                  <th scope="col">Created (UTC)</th>
                </tr>
              </thead>
              <tbody>
                {records.map((record) => (
                  <tr key={record.id}>
                    <td>{record.id}</td>
                    <td>{record.message}</td>
                    <td>{record.environment}</td>
                    <td>{formatDate(record.createdAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  )
}
