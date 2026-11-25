# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## Local booking location search

Set `VITE_GOOGLE_MAPS_API_KEY` in `frontend/.env.local` and restart Vite. In the Google Cloud project associated with the key, enable Maps JavaScript API and Places API (New), activate billing, and allow the local website origin in the key's HTTP referrer restrictions. API restrictions must also allow both services. Keep the key restricted to the intended sites; Vite embeds it in browser JavaScript.

The booking page uses Google's Place Autocomplete widget for pickup, drop-off and one optional stop, added or removed with the button between the locations. An added stop must be filled in or removed before submitting. Users can enter addresses manually when search is unavailable. A 403 `ACCESS_DENIED` response from Places requires checking the Google Cloud project, billing, and key restrictions; it is not resolved by restarting Vite.

Driving distance is calculated in miles using the Maps JavaScript Routes library, through the stop when one is provided. Enable the Routes API in the same Google Cloud project and include it in the browser key's API restrictions. Route requests can incur Google Maps Platform charges. A successful current route calculation is required to submit; manual addresses can still be used when routing is available. The submitted distance is rounded to two decimal places. The API saves the estimated amount as zero, pickup and drop-off addresses and their Google metadata when provided, and the stop's formatted address in `Via`. The stop is shown in customer and admin booking details. Existing databases must apply `backend/Data/migrate-bookings-via.sql` before deploying this API; the API role needs SELECT and INSERT access to the new column.

Existing databases need `backend/Data/migrate-bookings-estimates.sql` if the estimate columns do not exist. If they already exist, check their numeric types, precision and nullability before deployment; the migration does not alter existing columns. Fresh databases use numeric(12,2) for both estimates, with nullable distance for historical bookings and a default of zero for amount. The API database role needs INSERT access to these columns.

The booking form loads active vehicles and their capacities from `GET /api/vehicles`. Enter passenger and baggage counts (including zero bags) before selecting a vehicle; only vehicles meeting both capacities are offered, and the API independently checks the selected active vehicle. Mention a vehicle's full name in preferences to prioritize it if eligible or see a warning if it cannot accommodate the counts. Preferences and counts are saved and shown in admin booking details. Requests remain limited to 12 passengers and 30 bags.

Fresh databases seed Lexus RX350 (3 passengers, 3 bags), Mitsubishi Outlander (4, 3) and Chevrolet Suburban (7, 6). Existing databases need the vehicle master and booking foreign key in `backend/Data/migrate-bookings-vehicles.sql` and, after reviewing the fleet and possible code/name conflicts, `backend/Data/migrate-vehicle-capacities.sql`. The latter retires the original four seed vehicles, preserves historical bookings and custom vehicles, and upserts the three configured vehicles; other active custom vehicles remain eligible. The API database role needs SELECT access to `Vehicles`. Booking requests send `vehicleId` and `pickupAtUtc` (an ISO 8601 timestamp with an offset). The pickup defaults to 24 hours from now (minute precision) and must be at least 24 hours ahead; the date picker, form validation and the API all enforce this lead time.

After sign-in, the homepage requests `GET /api/bookings` with the session cookie. The API returns at most five of that customer's non-completed rides, with pending-review rides first and pickup date and time ascending within each group. Selecting a booking requests `GET /api/bookings/{bookingReference}` for its details; both endpoints enforce ownership. Owned completed rides remain accessible through the details endpoint but do not appear in the homepage list. Pickup dates and times are displayed in the browser's local time zone. The API role needs SELECT access to `Bookings` and `CustomerSessions`.

## Admin dashboard preview

Open `/admin` to see the unauthenticated dashboard. The status tiles and sidebar badges fetch counts of `PENDING_REVIEW`, `CONFIRMED`, and `ACTIVE` rows using `GET /api/admin/dashboard/booking-counts`; the grid fetches bookings and customer names/phones using `GET /api/admin/dashboard/bookings`. Completed bookings are excluded. Rows are grouped by pending review, confirmed, active, then other statuses, and sorted by pickup date and time descending within each group. Tile and sidebar clicks filter the loaded grid by status and clear the other filters. Search, date and vehicle filters operate on the loaded list, with dates shown and filtered in the browser's local time zone. Reload the page to refresh the data. The database role needs SELECT on `Bookings` and `Customers`.

At phone widths the dashboard shows booking cards and bottom navigation instead of the desktop sidebar, filters and table. Confirmed cards offer Activate and Reject. Select the booking ID on either layout to see ride details. Rides provides all, pending, confirmed, active, rejected and completed categories; completed rides are fetched separately via `GET /api/admin/dashboard/completed-bookings` and never appear in the desktop grid or All category. Driver assignment, the Drivers page and the More destinations are not implemented.

The operational summary uses `GET /api/admin/dashboard/operational-summary` with the browser's local midnight-to-midnight interval in UTC. Today's Bookings counts all bookings created during that interval, regardless of status. Today's Pickups counts scheduled pickups in that interval except rejected rides. Estimated Revenue sums stored `EstimatedAmount_Dollars` for those pickups in confirmed, active or completed status; it is not actual revenue, and newly created bookings currently default to zero. The summary reloads at local midnight and after status actions; reload the page to see other changes.

Start the API with the Development launch profile and its configured PostgreSQL connection. The admin endpoints accept loopback connections in Development only, have no admin authentication, and are unavailable in Staging and Production. Do not expose a Development API through a reverse proxy or shared host: loopback is not a substitute for authentication, particularly because booking data includes customer details.

The action buttons update the real database after confirmation: pending review can be confirmed or declined, confirmed can be activated or declined, and active can be completed. Declined bookings are stored as `REJECTED`. The API conditionally updates only the expected status to prevent stale changes. The database role needs UPDATE on `Bookings.Status`. These activation and completion buttons are local-development overrides: driver assignment, authorization, status history and notifications are not implemented. Do not use them for operational rides. Implement the workflow's driver permissions, assignment checks, auditing and notifications before making these actions available beyond local development. Reports and other navigation sections are not connected.

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
