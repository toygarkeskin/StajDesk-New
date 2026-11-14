#!/usr/bin/env node
// StajDesk yerel gizli değer kurulumu.
//
//   node scripts/setup-secrets.mjs            # yoksa oluşturur, varsa dokunmaz
//   node scripts/setup-secrets.mjs --force    # yeniden üretir (mevcut veritabanı parolası değişeceği için volume'leri silmeniz gerekir)
//
// Üretilenler (hepsi .gitignore kapsamındadır, depoya girmez):
//   .env                                              → docker compose
//   backend/src/StajDesk.Api/appsettings.Local.json   → API'yi yerelde (dotnet run) çalıştırırken
//   backend/src/StajDesk.Worker/appsettings.Local.json→ Worker'ı yerelde çalıştırırken
//
// Parolalar ve JWT anahtarı kriptografik rastgele üretilir. Demo hesaplarının parolası yalnızca bir kez ekrana yazdırılır.
import { randomBytes, randomInt } from 'node:crypto'
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const force = process.argv.includes('--force')
const envPath = join(root, '.env')

const token = (bytes) => randomBytes(bytes).toString('base64url')
/** Parola politikasına uyan (≥10, büyük/küçük harf, rakam, sembol) okunabilir parola */
function demoPassword() {
  const words = ['Mavi', 'Kuzey', 'Atlas', 'Delta', 'Nova', 'Vega', 'Orion', 'Lima', 'Tango', 'Zeyna']
  return `${words[randomInt(words.length)]}-${words[randomInt(words.length)].toLowerCase()}-${randomInt(1000, 9999)}!`
}

function parseEnv(text) {
  const out = {}
  for (const line of text.split(/\r?\n/)) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/)
    if (m) out[m[1]] = m[2]
  }
  return out
}

let env
if (existsSync(envPath) && !force) {
  env = parseEnv(readFileSync(envPath, 'utf8'))
  console.log('• .env zaten var, değerleri korunuyor (yeniden üretmek için --force).')
} else {
  const example = readFileSync(join(root, '.env.example'), 'utf8')
  env = parseEnv(example)
  Object.assign(env, {
    POSTGRES_PASSWORD: token(24),
    RABBITMQ_PASSWORD: token(24),
    JWT_KEY: token(48),
    SEED_DEFAULT_PASSWORD: demoPassword(),
  })
  const text = example.replace(/^([A-Z0-9_]+)=.*$/gm, (_, k) => `${k}=${env[k] ?? ''}`)
  writeFileSync(envPath, text)
  console.log('✓ .env oluşturuldu (rastgele parolalar ve JWT anahtarı).')
  console.log(`  Demo hesaplarının parolası: ${env.SEED_DEFAULT_PASSWORD}`)
  console.log('  (Bu parola yalnızca .env dosyanızda durur; depoya girmez.)')
}

// Yerel (docker dışı) çalıştırma için: compose'un dışarı açtığı portlar
const api = {
  ConnectionStrings: { Postgres: `Host=localhost;Port=5434;Database=${env.POSTGRES_DB};Username=${env.POSTGRES_USER};Password=${env.POSTGRES_PASSWORD}` },
  RabbitMq: { User: env.RABBITMQ_USER, Password: env.RABBITMQ_PASSWORD },
  Jwt: { Key: env.JWT_KEY },
  Seed: { DefaultPassword: env.SEED_DEFAULT_PASSWORD },
}
const worker = { ConnectionStrings: api.ConnectionStrings, RabbitMq: api.RabbitMq }
for (const [project, content] of [['StajDesk.Api', api], ['StajDesk.Worker', worker]]) {
  const p = join(root, 'backend', 'src', project, 'appsettings.Local.json')
  if (existsSync(p) && !force) { console.log(`• ${project}/appsettings.Local.json zaten var, korunuyor.`); continue }
  writeFileSync(p, JSON.stringify({ '//': 'Yerel gizli değerler — depoya girmez (.gitignore). scripts/setup-secrets.mjs üretir.', ...content }, null, 2) + '\n')
  console.log(`✓ ${project}/appsettings.Local.json oluşturuldu.`)
}
