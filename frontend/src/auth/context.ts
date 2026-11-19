import { createContext } from 'react'
import type { Dispatch, SetStateAction } from 'react'

export interface AuthCustomer {
  id: string
  name: string
  email: string
  phone: string
}

export interface AuthContextValue {
  customer: AuthCustomer | null
  loading: boolean
  sessionError: string | null
  setCustomer: Dispatch<SetStateAction<AuthCustomer | null>>
  setSessionError: Dispatch<SetStateAction<string | null>>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
