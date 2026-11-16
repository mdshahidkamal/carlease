import { BrowserRouter, Route, Routes } from 'react-router-dom'
import BookingPage from './pages/BookingPage'
import HomePage from './pages/HomePage'
import SdlcDemoPage from './pages/SdlcDemoPage'
import './App.css'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/booking" element={<BookingPage />} />
        <Route path="/sdlc-demo" element={<SdlcDemoPage />} />
      </Routes>
    </BrowserRouter>
  )
}
