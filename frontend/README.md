# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## Local booking location search

Set `VITE_GOOGLE_MAPS_API_KEY` in `frontend/.env.local` and restart Vite. In the Google Cloud project associated with the key, enable Maps JavaScript API and Places API (New), activate billing, and allow the local website origin in the key's HTTP referrer restrictions. API restrictions must also allow both services. Keep the key restricted to the intended sites; Vite embeds it in browser JavaScript.

The booking page uses Google's Place Autocomplete widget for pickup and drop-off. Users can enter addresses manually when search is unavailable. A 403 `ACCESS_DENIED` response from Places requires checking the Google Cloud project, billing, and key restrictions; it is not resolved by restarting Vite.

Driving distance is calculated in miles using the Maps JavaScript Routes library. Enable the Routes API in the same Google Cloud project and include it in the browser key's API restrictions. Route requests can incur Google Maps Platform charges. A successful current route calculation is required to submit; manual addresses can still be used when routing is available. The submitted distance is rounded to two decimal places. The API saves the estimated amount as zero, and saves each location's formatted address, place ID, latitude and longitude when provided by Google; manually entered addresses do not have Google metadata.

Existing databases need `backend/Data/migrate-bookings-estimates.sql` if the estimate columns do not exist. If they already exist, check their numeric types, precision and nullability before deployment; the migration does not alter existing columns. Fresh databases use numeric(12,2) for both estimates, with nullable distance for historical bookings and a default of zero for amount. The API database role needs INSERT access to these columns.

The booking form loads active vehicles from `GET /api/vehicles`. Existing databases need the vehicle master and booking foreign key in `backend/Data/migrate-bookings-vehicles.sql`; the API database role needs SELECT access to `Vehicles`. Booking requests now send `vehicleId` and `pickupAtUtc` (an ISO 8601 timestamp with an offset) rather than `vehicleType`, `pickupDate` and `pickupTime`. The selected local pickup minute may be the current minute; earlier minutes are rejected. Passenger and baggage limits are the lower of the vehicle capacities and the existing booking limits of 12 and 30.

After sign-in, the homepage requests `GET /api/bookings` with the session cookie. The API returns only that customer's rides across all statuses, ordered by pickup time (most recent first). Selecting a booking requests `GET /api/bookings/{bookingReference}` for its details; both endpoints enforce ownership. Pickup dates and times are displayed in the browser's local time zone. The API role needs SELECT access to `Bookings` and `CustomerSessions`.

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend enabling type-aware lint rules by installing `oxlint-tsgolint` and editing `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

See the [Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules) for the full list of rules and categories.
