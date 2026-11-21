export interface PlaceValue {
    address: string
    placeId: string
    latitude: number | null
    longitude: number | null
    resolving?: boolean
}

export const emptyPlace: PlaceValue = { address: '', placeId: '', latitude: null, longitude: null }

export function routeKey(pickup: PlaceValue, dropoff: PlaceValue) {
    return JSON.stringify([pickup.placeId ? `places/${pickup.placeId}` : pickup.address.trim(), dropoff.placeId ? `places/${dropoff.placeId}` : dropoff.address.trim()])
}
