// Remal — dashboard service worker for Web Push notifications.
// Registered by remal-dashboard.html after admin/partner login.

const ICON = 'https://remal-perfume.runasp.net/freshSummer.webp';

self.addEventListener('install', event => {
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(self.clients.claim());
});

self.addEventListener('push', event => {
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch (e) {
        // Non-JSON payload fallback
        data = { title: 'رمال', body: event.data ? event.data.text() : 'إشعار جديد' };
    }

    const title = data.title || 'رمال';
    const options = {
        body: data.body || 'إشعار جديد',
        icon: data.icon || ICON,
        badge: data.badge || ICON,
        dir: 'rtl',
        lang: 'ar',
        vibrate: [200, 100, 200],
        tag: data.tag || 'remal-notification',
        renotify: true,
        requireInteraction: false,
        data: { url: data.url || '/remal-dashboard.html' }
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const targetUrl = (event.notification.data && event.notification.data.url) || '/remal-dashboard.html';

    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(clientList => {
            // If a dashboard tab is already open, focus it.
            for (const client of clientList) {
                if (client.url.includes('remal-dashboard') && 'focus' in client) {
                    return client.focus();
                }
            }
            // Otherwise open a new tab.
            if (self.clients.openWindow) return self.clients.openWindow(targetUrl);
        })
    );
});
