// // Called automatically by Google Maps API
function initMap() {
    const center = { lat: -26.3167, lng: 31.1333 };
    new google.maps.Map(document.getElementById("map"), {
        zoom: 8,
        center: center
    });
}

// Called explicitly from Blazor with pins
function initMapWithPins(pins) {
    const center = { lat: -26.3167, lng: 31.1333 };
    const map = new google.maps.Map(document.getElementById("map"), {
        zoom: 8,
        center: center
    });

    pins.forEach(p => {
        new google.maps.Marker({
            position: { lat: p.lat, lng: p.lng },
            map,
            title: p.label
        });
    });
}
