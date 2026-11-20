import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveApiUrl } from './resolve-api-url.mjs';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const outputPath = resolve(scriptDirectory, '../src/environments/environment.generated.ts');
const apiUrl = resolveApiUrl(process.env);
const googleClientId = process.env.GOOGLE_CLIENT_ID || '';

mkdirSync(dirname(outputPath), { recursive: true });
writeFileSync(outputPath, `export const environment = {
  production: true,
  apiUrl: ${JSON.stringify(apiUrl)},
  googleClientId: ${JSON.stringify(googleClientId)}
};
`);

console.log(`Generated production environment (Google sign-in: ${googleClientId ? 'enabled' : 'disabled'}).`);
