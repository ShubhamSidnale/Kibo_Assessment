import { useEffect, useState } from 'react';
import type { Hold } from '../api';
import { remaining } from '../utils/time';

export function HoldCountdown({ hold }: { hold: Hold }) {
    const [now, setNow] = useState(Date.now());

    // Localize clock updates so the rest of the dashboard does not render every second.
    useEffect(() => {
        if (hold.status !== 'Active') {
            return;
        }
        const timer = window.setInterval(() => setNow(Date.now()), 1000);
        return () => window.clearInterval(timer);
    }, [hold.status]);

    let label: string;
    if (hold.status !== 'Active') {
        label = 'Ended ' + new Date(hold.endedAt || hold.expiresAt).toLocaleTimeString();
    } else if (Date.parse(hold.expiresAt) <= now) {
        // Stock stays reserved until the backend confirms expiration.
        label = 'Awaiting expiration confirmation';
    } else {
        label = remaining(hold.expiresAt, now) + ' remaining';
    }

    return <span className="countdown">{label}</span>;
}
