import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { AuthCustomer } from './context'
import { authApiBaseUrl } from './api'

type AuthMode = 'login' | 'register'

export default function AuthModal({ onClose, onSuccess }: { onClose: () => void; onSuccess: (customer: AuthCustomer) => void }) {
  const [mode, setMode] = useState<AuthMode>('login')
  const [step, setStep] = useState<'details' | 'verify'>('details')
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [consent, setConsent] = useState(false)
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [resendSeconds, setResendSeconds] = useState(60)

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && !busy) onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [busy, onClose])

  useEffect(() => {
    if (step !== 'verify' || resendSeconds <= 0) return
    const timer = window.setTimeout(() => setResendSeconds((seconds) => seconds - 1), 1000)
    return () => window.clearTimeout(timer)
  }, [resendSeconds, step])

  async function submitDetails(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const response = await fetch(`${authApiBaseUrl}/api/auth/${mode}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(mode === 'register'
          ? { name: name.trim(), email: email.trim(), phone: phone.trim(), consentAccepted: consent }
          : { email: email.trim() }),
      })
      const result = await response.json() as { message?: string; title?: string }
      if (!response.ok) throw new Error(result.message || result.title || `Could not send a code (HTTP ${response.status}).`)
      setStep('verify')
      setResendSeconds(60)
      setNotice(result.message || 'A six-digit code has been sent to your email address.')
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not send the verification code.')
    } finally {
      setBusy(false)
    }
  }

  async function verifyCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const response = await fetch(`${authApiBaseUrl}/api/auth/verify-otp`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ email: email.trim(), code }),
      })
      const result = await response.json() as { message?: string; title?: string; customer?: AuthCustomer }
      if (!response.ok) throw new Error(result.message || result.title || `Verification failed (HTTP ${response.status}).`)
      if (!result.customer) throw new Error('Sign-in succeeded, but customer details were not returned.')
      onSuccess(result.customer)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not verify the code.')
    } finally {
      setBusy(false)
    }
  }

  async function resendCode() {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const response = await fetch(`${authApiBaseUrl}/api/auth/resend-otp`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ email: email.trim() }),
      })
      const result = await response.json() as { message?: string; title?: string }
      if (!response.ok) throw new Error(result.message || result.title || `Could not resend the code (HTTP ${response.status}).`)
      setResendSeconds(60)
      setNotice(result.message || 'A new code has been sent.')
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not resend the code.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget && !busy) onClose() }}>
      <section className="auth-modal" role="dialog" aria-modal="true" aria-labelledby="auth-title">
        <button className="auth-close" type="button" aria-label="Close sign-in" onClick={onClose} disabled={busy}>×</button>
        <p className="auth-eyebrow">ROYAL LIMO SERVICE</p>
        <h2 id="auth-title">{step === 'verify' ? 'Check your email' : 'Welcome'}</h2>
        <p className="auth-description">{step === 'verify' ? `Enter the six-digit code sent to ${email}.` : 'Sign in or create an account to book your ride.'}</p>

        {step === 'details' ? <>
          <div className="auth-tabs" role="tablist" aria-label="Sign-in method">
            <button type="button" role="tab" aria-selected={mode === 'login'} className={mode === 'login' ? 'active' : ''} onClick={() => { setMode('login'); setError('') }}>Login</button>
            <button type="button" role="tab" aria-selected={mode === 'register'} className={mode === 'register' ? 'active' : ''} onClick={() => { setMode('register'); setError('') }}>Register</button>
          </div>
          <form className="auth-form" onSubmit={submitDetails}>
            {mode === 'register' && <>
              <label>Full name<input autoComplete="name" maxLength={120} required value={name} onChange={(event) => setName(event.target.value)} /></label>
              <label>Mobile number<input type="tel" autoComplete="tel" maxLength={30} minLength={5} placeholder="Include country code" required value={phone} onChange={(event) => setPhone(event.target.value)} /></label>
            </>}
            <label>Email address<input type="email" autoComplete="email" maxLength={160} required value={email} onChange={(event) => setEmail(event.target.value)} /></label>
            {mode === 'register' && <label className="auth-consent"><input type="checkbox" required checked={consent} onChange={(event) => setConsent(event.target.checked)} /><span>I agree to the Terms of Service and Privacy Policy.</span></label>}
            {error && <p className="auth-message error" role="alert">{error}</p>}
            <button className="auth-submit" type="submit" disabled={busy}>{busy ? 'Please wait…' : mode === 'login' ? 'Get Login Code' : 'Register'}</button>
          </form>
        </> : <form className="auth-form" onSubmit={verifyCode}>
          <label>Six-digit code<input autoComplete="one-time-code" inputMode="numeric" pattern="[0-9]{6}" maxLength={6} required value={code} onChange={(event) => setCode(event.target.value.replace(/\D/g, '').slice(0, 6))} /></label>
          {notice && <p className="auth-message" role="status">{notice}</p>}
          {error && <p className="auth-message error" role="alert">{error}</p>}
          <button className="auth-submit" type="submit" disabled={busy || code.length !== 6}>{busy ? 'Verifying…' : mode === 'login' ? 'Login' : 'Verify and create account'}</button>
          <button className="auth-resend" type="button" onClick={resendCode} disabled={busy || resendSeconds > 0}>{resendSeconds > 0 ? `Resend code in ${resendSeconds}s` : 'Resend code'}</button>
          <button className="auth-back" type="button" onClick={() => { setStep('details'); setCode(''); setError(''); setNotice('') }} disabled={busy}>Use a different email</button>
        </form>}
        <p className="auth-footnote">We never ask for or store your password. Verification codes expire after five minutes.</p>
      </section>
    </div>
  )
}
