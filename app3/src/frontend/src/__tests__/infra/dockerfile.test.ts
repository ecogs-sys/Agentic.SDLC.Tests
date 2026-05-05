/// <reference types="node" />
/**
 * STORY-010 — Frontend Docker image (multi-stage Node + Nginx)
 *
 * Acceptance criteria covered:
 *   AC1 — Dockerfile has a node:20-alpine build stage and nginx:alpine runtime stage.
 *   AC2 — Dockerfile declares ARG VITE_API_BASE_URL with default /api.
 *   AC3 — nginx.conf contains `listen 80`.
 *   AC4 — nginx.conf contains `try_files $uri /index.html` (SPA fallback).
 *   AC5 — nginx.conf contains a proxy_pass to http://backend:8080/api/.
 *
 * Files are read as plain text using Node's `fs` module so that no bundling
 * or transformation is applied — we validate the raw configuration text.
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { resolve, dirname } from 'node:path'

// Resolve paths relative to this test file.
// Test file: src/frontend/src/__tests__/infra/dockerfile.test.ts
// Dockerfile:  src/frontend/Dockerfile  (3 levels up + filename)
// nginx.conf:  src/frontend/nginx.conf  (3 levels up + filename)
const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)

const dockerfilePath = resolve(__dirname, '../../..', 'Dockerfile')
const nginxConfPath = resolve(__dirname, '../../..', 'nginx.conf')

function readConfig(filePath: string): string {
  return readFileSync(filePath, 'utf-8')
}

// ─────────────────────────────────────────────────────────────────────────────
// Dockerfile tests
// ─────────────────────────────────────────────────────────────────────────────

describe('Dockerfile (STORY-010)', () => {

  let dockerfile: string

  beforeAll(() => {
    dockerfile = readConfig(dockerfilePath)
  })

  // AC1 — happy path: node:20-alpine build stage present
  it('has a node:20-alpine build stage', () => {
    expect(dockerfile).toMatch(/FROM\s+node:20-alpine/)
  })

  // AC1 — happy path: nginx:alpine runtime stage present
  it('has an nginx:alpine runtime stage', () => {
    expect(dockerfile).toMatch(/FROM\s+nginx:alpine/)
  })

  // AC1 — edge case: both stages are distinct (multi-stage build, not a single stage)
  it('defines two distinct FROM instructions (multi-stage build)', () => {
    const fromLines = dockerfile
      .split('\n')
      .filter(line => /^\s*FROM\s+/i.test(line))
    expect(fromLines.length).toBeGreaterThanOrEqual(2)
  })

  // AC2 — happy path: ARG VITE_API_BASE_URL declared
  it('declares ARG VITE_API_BASE_URL', () => {
    expect(dockerfile).toMatch(/ARG\s+VITE_API_BASE_URL/)
  })

  // AC2 — happy path: default value is /api
  it('sets default value /api for VITE_API_BASE_URL', () => {
    expect(dockerfile).toMatch(/ARG\s+VITE_API_BASE_URL\s*=\s*\/api/)
  })

  // AC2 — edge case: ARG is in the build stage (before the nginx FROM), not elsewhere only
  it('declares VITE_API_BASE_URL ARG before the nginx runtime stage', () => {
    const nginxFromIndex = dockerfile.indexOf('FROM nginx:alpine')
    const argIndex = dockerfile.indexOf('ARG VITE_API_BASE_URL')
    expect(argIndex).toBeGreaterThanOrEqual(0)
    expect(argIndex).toBeLessThan(nginxFromIndex)
  })

  // AC1 — negative: Dockerfile must NOT use a non-alpine node base
  it('does not use a non-alpine node base image', () => {
    // e.g. FROM node:20 (without -alpine) should not appear
    const nonAlpineNode = dockerfile.match(/FROM\s+node:\d+\b(?!-alpine)/g)
    expect(nonAlpineNode).toBeNull()
  })

})

// ─────────────────────────────────────────────────────────────────────────────
// nginx.conf tests
// ─────────────────────────────────────────────────────────────────────────────

describe('nginx.conf (STORY-010)', () => {

  let nginxConf: string

  beforeAll(() => {
    nginxConf = readConfig(nginxConfPath)
  })

  // AC3 — happy path: listen 80 directive present
  it('configures Nginx to listen on port 80', () => {
    expect(nginxConf).toMatch(/listen\s+80/)
  })

  // AC3 — edge case: listen directive is not set to a different port only
  it('does not exclusively listen on a port other than 80', () => {
    // Extract all listen directives and ensure at least one is port 80
    const listenDirectives = nginxConf.match(/listen\s+\d+/g) ?? []
    const listens80 = listenDirectives.some(d => /listen\s+80$/.test(d.trim()))
    expect(listens80).toBe(true)
  })

  // AC4 — happy path: try_files SPA fallback present
  it('contains try_files $uri /index.html for SPA fallback', () => {
    expect(nginxConf).toMatch(/try_files\s+\$uri\s+\/index\.html/)
  })

  // AC4 — edge case: try_files fallback is inside a location block (not commented out)
  it('SPA fallback is not commented out', () => {
    const lines = nginxConf.split('\n')
    const activeFallbackLine = lines.find(
      line => /try_files\s+\$uri\s+\/index\.html/.test(line) && !line.trim().startsWith('#')
    )
    expect(activeFallbackLine).toBeDefined()
  })

  // AC5 — happy path: proxy_pass to backend:8080/api/ present
  it('contains proxy_pass to http://backend:8080/api/', () => {
    expect(nginxConf).toMatch(/proxy_pass\s+http:\/\/backend:8080\/api\//)
  })

  // AC5 — happy path: the proxy is inside a /api/ location block
  it('proxies /api/ location to the backend', () => {
    expect(nginxConf).toMatch(/location\s+\/api\/\s*\{[^}]*proxy_pass\s+http:\/\/backend:8080\/api\//s)
  })

  // AC5 — edge case: proxy_pass directive is not commented out
  it('proxy_pass to backend is not commented out', () => {
    const lines = nginxConf.split('\n')
    const activeProxyLine = lines.find(
      line => /proxy_pass\s+http:\/\/backend:8080\/api\//.test(line) && !line.trim().startsWith('#')
    )
    expect(activeProxyLine).toBeDefined()
  })

})
