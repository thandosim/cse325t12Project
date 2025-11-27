window.mapInterop = {
    initMap: function (id, lat, lng) {
        var map = L.map(id).setView([lat, lng], 10);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '© OpenStreetMap'
        }).addTo(map);

        return map;
    },

    addMarker: function (map, lat, lng, popupText) {
        L.marker([lat, lng]).addTo(map).bindPopup(popupText);
    }
};
