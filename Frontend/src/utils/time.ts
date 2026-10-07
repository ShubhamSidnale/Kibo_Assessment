/** Clamp expired countdowns to zero; the API owns expiration status. */
export function remaining(expiresAt: string, now: number): string {
    const seconds = Math.max(0, Math.ceil((Date.parse(expiresAt) - now) / 1000));
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}
