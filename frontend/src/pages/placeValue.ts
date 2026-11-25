export interface PlaceValue {
    address: string
    placeId: string
    latitude: number | null
    longitude: number | null
    resolving?: boolean
}

export const emptyPlace: PlaceValue = { address: '', placeId: '', latitude: null, longitude: null }

export function routeLocation(place: PlaceValue) {
    return place.placeId ? `places/${place.placeId}` : place.address.trim()
}

export function routeKey(pickup: PlaceValue, dropoff: PlaceValue, via: PlaceValue | null = null) {
    return JSON.stringify([routeLocation(pickup), via ? routeLocation(via) : null, routeLocation(dropoff)])
}
