var map;

function initMap(lat, lng) {
    if (map) {
        map.remove(); // reset if already initialized
    }

    map = L.map('map').setView([lat, lng], 6);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);
}

function addMarker(lat, lng, label, iconUrl) {
    var icon = L.icon({
        iconUrl: iconUrl || 'https://unpkg.com/leaflet/dist/images/marker-icon.png',
        iconSize: [32, 32],
        iconAnchor: [16, 32],
        popupAnchor: [0, -32]
    });

    L.marker([lat, lng], { icon: icon })
        .addTo(map)
        .bindPopup(label);
}
