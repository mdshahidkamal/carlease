/// <reference types="google.maps" />

import { useEffect, useState } from 'react'
import { useApiIsLoaded } from '@vis.gl/react-google-maps'
import { routeKey, routeLocation } from './placeValue'
import type { PlaceValue } from './placeValue'

const METERS_PER_MILE = 1609.344
const ROUTE_DEBOUNCE_MS = 1000

type DistanceResult =
    | { key: string; status: 'loading' }
    | { key: string; status: 'success'; miles: number }
    | { key: string; status: 'error'; message: string }

interface RouteDistanceProps {
    pickup: PlaceValue
    dropoff: PlaceValue
    via: PlaceValue | null
    useGoogle: boolean
    onDistance: (result: { key: string; miles: number }) => void
}

export function RouteDistance({ pickup, dropoff, via, useGoogle, onDistance }: RouteDistanceProps) {
    const hasAddresses = Boolean(pickup.address.trim() && dropoff.address.trim() && (!via || via.address.trim()))
    if (pickup.resolving || dropoff.resolving || via?.resolving) return <p className="route-distance" role="status">Loading selected location...</p>
    if (!hasAddresses) return null
    if (!useGoogle) return <p className="route-distance error" role="status">Driving distance is unavailable while Google Maps is not configured or could not load.</p>
    return <GoogleRouteDistance pickup={pickup} dropoff={dropoff} via={via} onDistance={onDistance} />
}

function GoogleRouteDistance({ pickup, dropoff, via, onDistance }: Omit<RouteDistanceProps, 'useGoogle'>) {
    const isLoaded = useApiIsLoaded()
    const [result, setResult] = useState<DistanceResult | null>(null)
    const origin = routeLocation(pickup)
    const destination = routeLocation(dropoff)
    const intermediate = via ? routeLocation(via) : null
    const key = routeKey(pickup, dropoff, via)

    useEffect(() => {
        if (!isLoaded) return
        let active = true
        const timer = window.setTimeout(() => {
            setResult({ key, status: 'loading' })
            void (async () => {
                try {
                    const { Route } = await google.maps.importLibrary('routes')
                    const { routes } = await Route.computeRoutes({ origin, destination, ...(intermediate ? { intermediates: [{ location: intermediate }] } : {}), travelMode: 'DRIVING', fields: ['distanceMeters'] })
                    if (!active) return
                    const meters = routes?.[0]?.distanceMeters
                    if (meters === undefined || !Number.isFinite(meters) || meters < 0) {
                        setResult({ key, status: 'error', message: 'No driving distance was found for these locations. Check the addresses and try again.' })
                        return
                    }
                    const miles = Math.round((meters / METERS_PER_MILE) * 100) / 100
                    setResult({ key, status: 'success', miles })
                    onDistance({ key, miles })
                } catch (error) {
                    if (!active) return
                    console.error('Google Maps could not calculate the driving distance.', error)
                    setResult({ key, status: 'error', message: 'Could not calculate driving distance. Check your connection, Routes API access, and addresses.' })
                }
            })()
        }, ROUTE_DEBOUNCE_MS)
        return () => {
            active = false
            window.clearTimeout(timer)
        }
    }, [isLoaded, origin, destination, intermediate, key, onDistance])

    if (!result || result.key !== key || result.status === 'loading') {
        return <p className="route-distance" role="status">Calculating driving distance…</p>
    }
    if (result.status === 'error') return <p className="route-distance error" role="status">{result.message}</p>
    return <p className="route-distance" role="status">Driving distance: <strong>{result.miles.toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} miles</strong></p>
}
