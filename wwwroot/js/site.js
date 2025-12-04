// Global map instance
window._map = null;

// Called automatically by Google Maps API
function initMap() {
    const center = { lat: -26.3167, lng: 31.1333 }; // Mbabane
    window._map = new google.maps.Map(document.getElementById("map"), {
        zoom: 8,
        center: center
    });
}

// Called explicitly from Blazor with pins
function initMapWithPins(pins) {
    // Ensure map exists
    if (!window._map) {
        initMap();
    }

    // Clear existing markers if needed
    if (!window._markers) {
        window._markers = [];
    }
    window._markers.forEach(m => m.setMap(null));
    window._markers = [];

    // Add new markers
    pins.forEach(p => {
        const marker = new google.maps.Marker({
            position: { lat: p.lat, lng: p.lng },
            map: window._map,
            title: p.label
        });
        window._markers.push(marker);
    });

    // Optionally re-center map around first pin
    if (pins.length > 0) {
        window._map.setCenter({ lat: pins[0].lat, lng: pins[0].lng });
    }

    console.log("Pins received from Blazor:", pins);

}
