import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const outputPath = resolve(scriptDirectory, '../src/environments/environment.generated.ts');
const apiUrl = process.env.API_URL || 'https://insulinandcoffee.onrender.com/api';
const googleClientId = process.env.GOOGLE_CLIENT_ID || '';

mkdirSync(dirname(outputPath), { recursive: true });
writeFileSync(outputPath, `export const environment = {
  production: true,
  apiUrl: ${JSON.stringify(apiUrl)},
  googleClientId: ${JSON.stringify(googleClientId)}
};
`);

console.log(`Generated production environment (Google sign-in: ${googleClientId ? 'enabled' : 'disabled'}).`);
