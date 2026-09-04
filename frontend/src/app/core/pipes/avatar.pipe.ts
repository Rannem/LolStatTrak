import { Pipe, PipeTransform } from '@angular/core';

export const FALLBACK_AVATAR = 'https://cdn.discordapp.com/embed/avatars/0.png';

/**
 * Discord's CDN serves avatars at 1024px by default; we render them at 24–96px.
 * Appends `?size=N` (power of two, 16–4096) so the browser downloads ~5% of the bytes.
 */
@Pipe({ name: 'avatar', standalone: true })
export class AvatarPipe implements PipeTransform {
  transform(url: string | null | undefined, size: 32 | 64 | 128 | 256 = 64): string {
    const base = url || FALLBACK_AVATAR;
    if (!base.includes('cdn.discordapp.com')) return base;
    // Request 2x for crisp rendering on HiDPI screens.
    const px = Math.min(4096, size * 2);
    return `${base}${base.includes('?') ? '&' : '?'}size=${px}`;
  }
}
