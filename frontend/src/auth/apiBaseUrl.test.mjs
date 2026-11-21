import assert from 'node:assert/strict'
import { test } from 'node:test'
import { localApiBaseUrl } from './apiBaseUrl.ts'

test('matches development loopback API host to the frontend host', () => {
  assert.equal(localApiBaseUrl('http://localhost:5292/', '127.0.0.1', true), 'http://127.0.0.1:5292')
  assert.equal(localApiBaseUrl('http://127.0.0.1:5292', 'localhost', true), 'http://localhost:5292')
  assert.equal(localApiBaseUrl('http://localhost:5292', 'localhost', true), 'http://localhost:5292')
})

test('does not rewrite production, non-loopback, or relative endpoints', () => {
  assert.equal(localApiBaseUrl('http://localhost:5292', '127.0.0.1', false), 'http://localhost:5292')
  assert.equal(localApiBaseUrl('https://api.example.test/', 'localhost', true), 'https://api.example.test')
  assert.equal(localApiBaseUrl('http://localhost:5292', 'example.test', true), 'http://localhost:5292')
  assert.equal(localApiBaseUrl('/proxy/', 'localhost', true), '/proxy')
})
