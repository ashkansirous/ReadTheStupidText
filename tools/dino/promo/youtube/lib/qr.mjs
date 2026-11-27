// The small QR code to dinoreader.sirous.uk as inline SVG (node-qrcode, context7 /soldair/node-qrcode: toString with type 'svg').
// Dark modules on white, quiet zone of 2 modules, error correction M. `noQr` (the policy fallback, see docs/plan/notes/2026-11-24-youtube-kids-policy.md) returns null.

import QRCode from 'qrcode';
import { SITE_URL } from './frame.mjs';

export async function qrSvg({ noQr = false } = {}) {
  if (noQr) return null;
  return QRCode.toString(SITE_URL, { type: 'svg', margin: 2, errorCorrectionLevel: 'M', color: { dark: '#2b2540', light: '#ffffff' } });
}
