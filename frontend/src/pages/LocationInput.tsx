/// <reference types="google.maps" />

import { useEffect, useRef, useState } from 'react'
import { useMapsLibrary } from '@vis.gl/react-google-maps'
import { emptyPlace } from './placeValue'
import type { PlaceValue } from './placeValue'

interface LocationInputProps {
    id: string
    name: string
    label: string
    placeholder: string
    value: PlaceValue
    onChange: (value: PlaceValue) => void
}

export function ManualLocationInput({ id, name, label, placeholder, value, onChange }: LocationInputProps) {
    return <div className="field full"><label htmlFor={id}>{label}</label><input id={id} name={name} type="text" autoComplete="off" required placeholder={placeholder} value={value.address} onChange={(event) => onChange({ ...emptyPlace, address: event.target.value })} /></div>
}

export function GoogleLocationInput(props: LocationInputProps) {
    const places = useMapsLibrary('places')
    const container = useRef<HTMLDivElement>(null)
    const currentValue = useRef(props.value)
    const onChange = useRef(props.onChange)
    const [ready, setReady] = useState(false)
    const [error, setError] = useState('')

    useEffect(() => {
        currentValue.current = props.value
        onChange.current = props.onChange
    }, [props.value, props.onChange])

    useEffect(() => {
        if (!places || !container.current) return
        const widget = new places.PlaceAutocompleteElement({ placeholder: props.placeholder, description: props.label })
        widget.id = props.id
        widget.setAttribute('aria-label', props.label)
        widget.value = currentValue.current.address
        container.current.appendChild(widget)
        setReady(true)
        let active = true
        let selection = 0
        const handleInput = () => {
            selection++
            setError('')
            onChange.current({ ...emptyPlace, address: widget.value })
        }
        const handleSelect = async (event: google.maps.places.PlacePredictionSelectEvent) => {
            const currentSelection = ++selection
            setError('')
            onChange.current({ ...emptyPlace, address: widget.value, resolving: true })
            const place = event.placePrediction.toPlace()
            try {
                await place.fetchFields({ fields: ['id', 'formattedAddress', 'location'] })
                if (!active || currentSelection !== selection) return
                const address = place.formattedAddress ?? widget.value
                widget.value = address
                onChange.current({ address, placeId: place.id ?? '', latitude: place.location?.lat() ?? null, longitude: place.location?.lng() ?? null })
            } catch (reason) {
                if (!active || currentSelection !== selection) return
                console.error('Google Places could not retrieve the selected address.', reason)
                onChange.current({ ...emptyPlace, address: widget.value })
                setError('Could not retrieve the selected address. Please try again or enter it manually.')
            }
        }
        const handleError = () => setError('Google location search was denied. Check Places API (New), billing, and API key restrictions, or enter the address manually.')
        widget.addEventListener('input', handleInput)
        widget.addEventListener('gmp-select', handleSelect)
        widget.addEventListener('gmp-error', handleError)
        return () => {
            active = false
            widget.removeEventListener('input', handleInput)
            widget.removeEventListener('gmp-select', handleSelect)
            widget.removeEventListener('gmp-error', handleError)
            widget.remove()
        }
    }, [places, props.id, props.label, props.placeholder])

    return <div className="field full">
        <label htmlFor={props.id}>{props.label}</label>
        <div ref={container} className="place-autocomplete" />
        {!ready && <input id={props.id} type="text" autoComplete="off" required placeholder={props.placeholder} value={props.value.address} onChange={(event) => props.onChange({ ...emptyPlace, address: event.target.value })} />}
        <input type="hidden" name={props.name} value={props.value.address} />
        {error && <p className="map-search-error" role="alert">{error}</p>}
    </div>
}
