// Spec 011 validation server: serves the generated frontend (.output/public) and forwards /api, /hubs
// and /health (WebSocket upgrades included) to a validation API. Same routing as nginx.conf, with no
// dependencies, so the e2e can run next to the stack that serves the table without touching it.
//   node tests/e2e/serve-validation.mjs [port=3611] [api=http://127.0.0.1:5311]
import { createServer, request } from 'node:http'
import { connect } from 'node:net'
import { readFile, stat } from 'node:fs/promises'
import { extname, join, normalize } from 'node:path'
import { fileURLToPath } from 'node:url'

const port = Number(process.argv[2] ?? 3611)
const api = new URL(process.argv[3] ?? 'http://127.0.0.1:5311')
const root = fileURLToPath(new URL('../../.output/public/', import.meta.url))
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.webmanifest': 'application/manifest+json', '.ico': 'image/x-icon', '.woff2': 'font/woff2' }
const proxied = path => path.startsWith('/api/') || path.startsWith('/hubs/') || path === '/health'

const server = createServer(async (req, res) => {
  const path = new URL(req.url, 'http://local').pathname
  if (proxied(path)) {
    const upstream = request({ host: api.hostname, port: api.port, method: req.method, path: req.url, headers: { ...req.headers, host: api.host } }, reply => {
      res.writeHead(reply.statusCode ?? 502, reply.headers)
      reply.pipe(res)
    })
    upstream.on('error', () => { if (!res.headersSent) res.writeHead(502); res.end() })
    req.pipe(upstream)
    return
  }
  let file = join(root, normalize(decodeURIComponent(path)).replace(/^([/\\])+/, ''))
  if (!file.startsWith(root)) { res.writeHead(403); res.end(); return }
  try {
    if ((await stat(file)).isDirectory()) file = join(file, 'index.html')
  } catch {
    file = join(root, 'index.html') // SPA fallback, like try_files in nginx.conf
  }
  try {
    const body = await readFile(file)
    res.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' })
    res.end(body)
  } catch {
    res.writeHead(404); res.end()
  }
})

// SignalR WebSocket: replay the upgrade request upstream and splice the two sockets.
server.on('upgrade', (req, socket, head) => {
  const upstream = connect(Number(api.port), api.hostname, () => {
    const lines = [`${req.method} ${req.url} HTTP/1.1`, ...Object.entries({ ...req.headers, host: api.host }).map(([key, value]) => `${key}: ${value}`), '', '']
    upstream.write(lines.join('\r\n'))
    if (head?.length) upstream.write(head)
    upstream.pipe(socket)
    socket.pipe(upstream)
  })
  const close = () => { upstream.destroy(); socket.destroy() }
  upstream.on('error', close)
  socket.on('error', close)
})

server.listen(port, '127.0.0.1', () => console.log(`validation frontend on http://127.0.0.1:${port} -> ${api.origin}`))
