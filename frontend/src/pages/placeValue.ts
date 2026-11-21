export interface PlaceValue {
    address: string
    placeId: string
    latitude: number | null
    longitude: number | null
}

export const emptyPlace: PlaceValue = { address: '', placeId: '', latitude: null, longitude: null }
