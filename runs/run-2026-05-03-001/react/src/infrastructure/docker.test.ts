import { describe, it, expect } from 'vitest'
import * as fs from 'fs'
import * as path from 'path'

// Path helpers — resolved relative to this test file's location:
//   __dirname = .../react/src/infrastructure
//   reactRoot = .../react
//   runRoot   = .../run-2026-05-03-001
const reactRoot = path.resolve(__dirname, '../..')
const runRoot = path.resolve(__dirname, '../../..')

const dockerfilePath = path.join(reactRoot, 'frontend', 'Dockerfile')
const nginxConfPath = path.join(reactRoot, 'frontend', 'nginx.conf')
const composeFilePath = path.join(runRoot, 'dotnet', 'docker-compose.yml')

// ---------------------------------------------------------------------------
// AC1 — Dockerfile inspection
// ---------------------------------------------------------------------------
describe('AC1: Dockerfile', () => {
  it('Dockerfile exists at frontend/Dockerfile', () => {
    expect(fs.existsSync(dockerfilePath)).toBe(true)
  })

  it('contains a node: base image (stage 1 builder)', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    expect(content).toMatch(/FROM node:/)
  })

  it('contains an nginx: base image (stage 2 runtime)', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    expect(content).toMatch(/FROM nginx:/)
  })

  it('is a two-stage build (both node: and nginx: FROM lines present)', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    const fromLines = content.split('\n').filter((l: string) => /^\s*FROM\s+/i.test(l))
    expect(fromLines.length).toBeGreaterThanOrEqual(2)
    const hasNode = fromLines.some((l: string) => /node:/i.test(l))
    const hasNginx = fromLines.some((l: string) => /nginx:/i.test(l))
    expect(hasNode).toBe(true)
    expect(hasNginx).toBe(true)
  })

  it('declares ARG VITE_API_BASE_URL before the build step', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    expect(content).toContain('ARG VITE_API_BASE_URL')
    // ARG must appear before npm run build
    const argIndex = content.indexOf('ARG VITE_API_BASE_URL')
    const buildIndex = content.indexOf('npm run build')
    expect(argIndex).toBeLessThan(buildIndex)
  })

  it('promotes ARG to ENV with ENV VITE_API_BASE_URL so Vite picks it up', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    expect(content).toMatch(/ENV VITE_API_BASE_URL/)
  })

  it('contains EXPOSE 80', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    expect(content).toContain('EXPOSE 80')
  })

  it('final stage uses nginx:alpine, not node', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    const fromLines = content.split('\n').filter((l: string) => /^\s*FROM\s+/i.test(l))
    const lastFrom = fromLines[fromLines.length - 1]
    expect(lastFrom).toMatch(/nginx/)
    expect(lastFrom).not.toMatch(/^FROM node:/i)
  })

  it('contains npm run build (the Vite build command)', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    expect(content).toContain('npm run build')
  })

  // Edge cases
  it('does not expose a Node.js development port (e.g. 3000 or 5173) — only port 80', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    const exposeLines = content.split('\n').filter((l) => /^\s*EXPOSE\s+/i.test(l))
    expect(exposeLines).toHaveLength(1)
    expect(exposeLines[0]).toMatch(/80/)
  })

  it('ARG VITE_API_BASE_URL appears in stage 1 (before the nginx FROM line)', () => {
    const content = fs.readFileSync(dockerfilePath, 'utf-8')
    const nginxFromIndex = content.search(/FROM nginx:/i)
    const argIndex = content.indexOf('ARG VITE_API_BASE_URL')
    expect(argIndex).toBeGreaterThanOrEqual(0)
    expect(argIndex).toBeLessThan(nginxFromIndex)
  })
})

// ---------------------------------------------------------------------------
// AC3 — nginx.conf SPA fallback
// ---------------------------------------------------------------------------
describe('AC3: nginx.conf SPA fallback', () => {
  it('nginx.conf exists at frontend/nginx.conf', () => {
    expect(fs.existsSync(nginxConfPath)).toBe(true)
  })

  it('contains a try_files directive', () => {
    const content = fs.readFileSync(nginxConfPath, 'utf-8')
    expect(content).toContain('try_files')
  })

  it('try_files directive falls back to /index.html (SPA routing)', () => {
    const content = fs.readFileSync(nginxConfPath, 'utf-8')
    // The fallback must include /index.html so deep links return the SPA shell
    expect(content).toMatch(/try_files[^;]+\/index\.html/)
  })

  it('listens on port 80', () => {
    const content = fs.readFileSync(nginxConfPath, 'utf-8')
    expect(content).toMatch(/listen\s+80/)
  })

  // Edge case — a bare 404 location block would break SPA routing
  it('does not contain a plain 404 return that would override try_files fallback', () => {
    const content = fs.readFileSync(nginxConfPath, 'utf-8')
    // A `return 404` without a try_files nearby would break SPA routing
    // Acceptable: no such line at all, or it is inside a specific error handler
    const has404Return = /^\s*return\s+404\s*;/m.test(content)
    expect(has404Return).toBe(false)
  })

  // Edge case — SPA fallback must be in the catch-all location
  it('try_files is inside a location block that matches all paths', () => {
    const content = fs.readFileSync(nginxConfPath, 'utf-8')
    // e.g.  location / { ... try_files ... }
    expect(content).toMatch(/location\s+\/\s*\{[^}]*try_files/s)
  })
})

// ---------------------------------------------------------------------------
// AC5 — ARG wiring in docker-compose.yml
// ---------------------------------------------------------------------------
describe('AC5: docker-compose.yml ARG wiring', () => {
  it('docker-compose.yml exists', () => {
    expect(fs.existsSync(composeFilePath)).toBe(true)
  })

  it('web service has VITE_API_BASE_URL in build args', () => {
    const content = fs.readFileSync(composeFilePath, 'utf-8')
    expect(content).toContain('VITE_API_BASE_URL')
  })

  it('the ARG name in compose matches the ARG name declared in the Dockerfile', () => {
    const dockerfile = fs.readFileSync(dockerfilePath, 'utf-8')
    const compose = fs.readFileSync(composeFilePath, 'utf-8')

    // Extract the ARG name from the Dockerfile
    const argMatch = dockerfile.match(/ARG\s+(VITE_\w+)/)
    expect(argMatch).not.toBeNull()
    const argName = argMatch![1]

    // That exact name must also appear in the compose file's build args section
    expect(compose).toContain(argName)
  })

  // Edge case — compose should not hardwire a localhost URL that can't be overridden
  it('the compose VITE_API_BASE_URL value targets localhost:8080 (the api service port)', () => {
    const content = fs.readFileSync(composeFilePath, 'utf-8')
    // The default compose value should point at the api service port
    expect(content).toMatch(/VITE_API_BASE_URL.*localhost:8080|localhost:8080.*VITE_API_BASE_URL/)
  })
})

// ---------------------------------------------------------------------------
// AC2 (structural) — web service port mapping and dependencies
// ---------------------------------------------------------------------------
describe('AC2 structural: web service in docker-compose.yml', () => {
  it('web service maps port 5173', () => {
    const content = fs.readFileSync(composeFilePath, 'utf-8')
    expect(content).toContain('5173')
  })

  it('web service depends_on api', () => {
    const content = fs.readFileSync(composeFilePath, 'utf-8')
    // The web service block must reference api as a dependency
    // Simple check: `depends_on` and `api` appear in the compose file
    // and specifically in proximity suggesting web depends on api
    expect(content).toMatch(/depends_on[\s\S]*?api/m)
  })

  it('web service is on the same network as api (appnet)', () => {
    const content = fs.readFileSync(composeFilePath, 'utf-8')
    // Both api and web should reference the same network name
    // Count occurrences of appnet — should appear at least twice (once per service + definition)
    const appnetMatches = content.match(/appnet/g)
    expect(appnetMatches).not.toBeNull()
    expect(appnetMatches!.length).toBeGreaterThanOrEqual(2)
  })

  // Edge case — the host port (5173) maps to container port 80 (nginx)
  it('port mapping routes external 5173 to internal 80', () => {
    const content = fs.readFileSync(composeFilePath, 'utf-8')
    expect(content).toMatch(/5173\s*:\s*80|"5173:80"/)
  })
})
