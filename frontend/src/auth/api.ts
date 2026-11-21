import { localApiBaseUrl } from './apiBaseUrl'

export const authApiBaseUrl = localApiBaseUrl(
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5292',
  window.location.hostname,
  import.meta.env.DEV,
)

export interface AuthApiResponse {
  message?: string
  title?: string
  detail?: string
  traceId?: string
  customer?: {
    id: string
    name: string
    email: string
    phone: string
  }
}

export async function readAuthResponse(response: Response): Promise<AuthApiResponse> {
  const body = await response.text()
  if (!body.trim()) return {}

  try {
    return JSON.parse(body) as AuthApiResponse
  } catch {
    if (!response.ok) {
      return { message: `The server could not process the request (HTTP ${response.status}). Please check the API logs.` }
    }
    throw new Error('The server returned an invalid response. Please try again.')
  }
}
