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

// Get marker icon based on pin type
function getMarkerIcon(type) {
    const icons = {
        'driver': 'http://maps.google.com/mapfiles/ms/icons/green-dot.png',
        'load': 'http://maps.google.com/mapfiles/ms/icons/orange-dot.png',
        'user': 'http://maps.google.com/mapfiles/ms/icons/blue-dot.png',
        'pickup': 'http://maps.google.com/mapfiles/ms/icons/yellow-dot.png',
        'dropoff': 'http://maps.google.com/mapfiles/ms/icons/red-dot.png'
    };
    return icons[type] || 'http://maps.google.com/mapfiles/ms/icons/red-dot.png';
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

    // Add new markers with different colors based on type
    pins.forEach(p => {
        const marker = new google.maps.Marker({
            position: { lat: p.lat, lng: p.lng },
            map: window._map,
            title: p.label,
            icon: getMarkerIcon(p.type)
        });
        
        // Add info window for each marker
        const infoWindow = new google.maps.InfoWindow({
            content: `<div style="padding: 8px; font-family: sans-serif;">
                <strong>${p.label}</strong>
                ${p.details ? `<br><span style="color: #666;">${p.details}</span>` : ''}
            </div>`
        });
        
        marker.addListener('click', () => {
            infoWindow.open(window._map, marker);
        });
        
        window._markers.push(marker);
    });

    // Fit bounds to show all markers
    if (pins.length > 0) {
        const bounds = new google.maps.LatLngBounds();
        pins.forEach(p => bounds.extend({ lat: p.lat, lng: p.lng }));
        window._map.fitBounds(bounds);
        
        // Don't zoom in too much for single marker
        if (pins.length === 1) {
            window._map.setZoom(10);
        }
    }

    console.log("Pins received from Blazor:", pins);
}

// Get user's current location using browser geolocation API
async function getCurrentLocation() {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject({ error: 'Geolocation is not supported by this browser.' });
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (position) => {
                resolve({
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude,
                    accuracy: position.coords.accuracy
                });
            },
            (error) => {
                let errorMessage = 'Unknown error occurred.';
                switch (error.code) {
                    case error.PERMISSION_DENIED:
                        errorMessage = 'User denied the request for Geolocation.';
                        break;
                    case error.POSITION_UNAVAILABLE:
                        errorMessage = 'Location information is unavailable.';
                        break;
                    case error.TIMEOUT:
                        errorMessage = 'The request to get user location timed out.';
                        break;
                }
                reject({ error: errorMessage });
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 60000
            }
        );
    });
}

// Watch user's location for continuous updates
let watchId = null;

function startWatchingLocation(dotNetHelper) {
    if (!navigator.geolocation) {
        dotNetHelper.invokeMethodAsync('OnLocationError', 'Geolocation is not supported by this browser.');
        return;
    }

    watchId = navigator.geolocation.watchPosition(
        (position) => {
            dotNetHelper.invokeMethodAsync('OnLocationUpdate', {
                latitude: position.coords.latitude,
                longitude: position.coords.longitude,
                accuracy: position.coords.accuracy
            });
        },
        (error) => {
            let errorMessage = 'Unknown error occurred.';
            switch (error.code) {
                case error.PERMISSION_DENIED:
                    errorMessage = 'Location permission denied.';
                    break;
                case error.POSITION_UNAVAILABLE:
                    errorMessage = 'Location unavailable.';
                    break;
                case error.TIMEOUT:
                    errorMessage = 'Location request timed out.';
                    break;
            }
            dotNetHelper.invokeMethodAsync('OnLocationError', errorMessage);
        },
        {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 30000
        }
    );
}

function stopWatchingLocation() {
    if (watchId !== null) {
        navigator.geolocation.clearWatch(watchId);
        watchId = null;
    }
}

// Update user marker on map in real-time
function updateUserMarker(lat, lng, label) {
    if (!window._map) return;

    // Remove existing user marker if any
    if (window._userMarker) {
        window._userMarker.setMap(null);
    }

    // Create new user marker with pulsing effect
    window._userMarker = new google.maps.Marker({
        position: { lat: lat, lng: lng },
        map: window._map,
        title: label || 'Your Location',
        icon: {
            path: google.maps.SymbolPath.CIRCLE,
            scale: 12,
            fillColor: '#3B82F6',
            fillOpacity: 1,
            strokeColor: '#ffffff',
            strokeWeight: 3
        },
        animation: google.maps.Animation.DROP
    });

    // Add accuracy circle
    if (window._accuracyCircle) {
        window._accuracyCircle.setMap(null);
    }

    // Center map on user location
    window._map.panTo({ lat: lat, lng: lng });
}

// Pan map to specific location
function panToLocation(lat, lng, zoom) {
    if (!window._map) return;
    window._map.panTo({ lat: lat, lng: lng });
    if (zoom) {
        window._map.setZoom(zoom);
    }
}

// Add a draggable marker for location selection
let selectedLocationCallback = null;

function enableLocationSelection(dotNetHelper) {
    if (!window._map) return;

    // Change cursor to crosshair
    window._map.setOptions({ draggableCursor: 'crosshair' });

    // Add click listener
    const clickListener = window._map.addListener('click', (e) => {
        const lat = e.latLng.lat();
        const lng = e.latLng.lng();

        // Remove existing selection marker
        if (window._selectionMarker) {
            window._selectionMarker.setMap(null);
        }

        // Create selection marker
        window._selectionMarker = new google.maps.Marker({
            position: { lat: lat, lng: lng },
            map: window._map,
            title: 'Selected Location',
            icon: {
                path: google.maps.SymbolPath.CIRCLE,
                scale: 10,
                fillColor: '#10B981',
                fillOpacity: 1,
                strokeColor: '#ffffff',
                strokeWeight: 2
            },
            draggable: true
        });

        // Notify Blazor of selection
        dotNetHelper.invokeMethodAsync('OnLocationSelected', lat, lng);

        // Update on drag
        window._selectionMarker.addListener('dragend', (e) => {
            dotNetHelper.invokeMethodAsync('OnLocationSelected', e.latLng.lat(), e.latLng.lng());
        });
    });

    // Store listener for cleanup
    window._mapClickListener = clickListener;
}

function disableLocationSelection() {
    if (!window._map) return;

    // Reset cursor
    window._map.setOptions({ draggableCursor: null });

    // Remove click listener
    if (window._mapClickListener) {
        google.maps.event.removeListener(window._mapClickListener);
        window._mapClickListener = null;
    }

    // Remove selection marker
    if (window._selectionMarker) {
        window._selectionMarker.setMap(null);
        window._selectionMarker = null;
    }
}

// Reverse geocode to get address from coordinates
async function getAddressFromCoordinates(lat, lng) {
    return new Promise((resolve, reject) => {
        const geocoder = new google.maps.Geocoder();
        geocoder.geocode({ location: { lat: lat, lng: lng } }, (results, status) => {
            if (status === 'OK' && results[0]) {
                resolve(results[0].formatted_address);
            } else {
                reject('Unable to get address');
            }
        });
    });
}

// Geocode address to get coordinates
async function getCoordinatesFromAddress(address) {
    return new Promise((resolve, reject) => {
        const geocoder = new google.maps.Geocoder();
        geocoder.geocode({ address: address }, (results, status) => {
            if (status === 'OK' && results[0]) {
                resolve({
                    lat: results[0].geometry.location.lat(),
                    lng: results[0].geometry.location.lng(),
                    formattedAddress: results[0].formatted_address
                });
            } else {
                reject('Unable to find location');
            }
        });
    });
}

// ============================================================
// Google Places Autocomplete for Address Fields
// ============================================================

// Store autocomplete instances by element ID
window._autocompleteInstances = {};

// Initialize Places Autocomplete on an input element
function initAddressAutocomplete(elementId, dotNetHelper, minLength) {
    // Wait for Google Maps to load
    if (typeof google === 'undefined' || !google.maps) {
        console.warn('Google Maps API not loaded yet, retrying...');
        setTimeout(() => initAddressAutocomplete(elementId, dotNetHelper, minLength), 500);
        return;
    }
    
    if (!google.maps.places) {
        console.error('Google Maps Places library not loaded. Make sure &libraries=places is in the script URL.');
        console.error('Check your Google Cloud Console to ensure:');
        console.error('1. Places API is enabled');
        console.error('2. Maps JavaScript API is enabled');
        console.error('3. Billing is enabled');
        console.error('4. API key restrictions allow your domain');
        return;
    }

    const inputElement = document.getElementById(elementId);
    if (!inputElement) {
        console.error(`Element with ID '${elementId}' not found`);
        return;
    }

    // Remove existing autocomplete if any
    if (window._autocompleteInstances[elementId]) {
        google.maps.event.clearInstanceListeners(window._autocompleteInstances[elementId]);
        delete window._autocompleteInstances[elementId];
    }

    // Configure autocomplete options
    const options = {
        types: ['geocode', 'establishment'],
        fields: ['formatted_address', 'geometry', 'name', 'address_components']
    };

    try {
        // Create autocomplete instance
        const autocomplete = new google.maps.places.Autocomplete(inputElement, options);
        window._autocompleteInstances[elementId] = autocomplete;

        // Track input length to only show suggestions after significant input
        const minChars = minLength || 10;
        let lastValue = '';

        // Listen for input changes to control when autocomplete shows
        inputElement.addEventListener('input', function(e) {
            const value = e.target.value;
            
            // Only allow autocomplete to show after minimum characters
            if (value.length < minChars) {
                // Hide the autocomplete dropdown by temporarily disabling it
                autocomplete.setOptions({ types: [] });
            } else if (lastValue.length < minChars && value.length >= minChars) {
                // Re-enable autocomplete when threshold is reached
                autocomplete.setOptions({ types: ['geocode', 'establishment'] });
            }
            lastValue = value;
        });

        // Handle place selection
        autocomplete.addListener('place_changed', function() {
            const place = autocomplete.getPlace();
            
            if (!place.geometry || !place.geometry.location) {
                console.warn('No geometry data for selected place');
                return;
            }

            const result = {
                address: place.formatted_address || place.name || inputElement.value,
                latitude: place.geometry.location.lat(),
                longitude: place.geometry.location.lng(),
                name: place.name || '',
                components: {}
            };

            // Extract address components
            if (place.address_components) {
                place.address_components.forEach(component => {
                    const types = component.types;
                    if (types.includes('street_number')) {
                        result.components.streetNumber = component.long_name;
                    }
                    if (types.includes('route')) {
                        result.components.street = component.long_name;
                    }
                    if (types.includes('locality')) {
                        result.components.city = component.long_name;
                    }
                    if (types.includes('administrative_area_level_1')) {
                        result.components.state = component.long_name;
                    }
                    if (types.includes('country')) {
                        result.components.country = component.long_name;
                    }
                    if (types.includes('postal_code')) {
                        result.components.postalCode = component.long_name;
                    }
                });
            }

            // Update the input value with the formatted address
            inputElement.value = result.address;

            // Notify Blazor of the selection
            dotNetHelper.invokeMethodAsync('OnPlaceSelected', result);
        });

        // Prevent form submission on Enter when autocomplete is open
        inputElement.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                const pacContainer = document.querySelector('.pac-container');
                if (pacContainer && pacContainer.style.display !== 'none') {
                    e.preventDefault();
                }
            }
        });

        console.log(`✓ Address autocomplete initialized for '${elementId}' with min ${minChars} chars`);
    } catch (error) {
        console.error('Failed to initialize autocomplete:', error);
        console.error('This usually means Google Maps API is not properly configured.');
    }
}

// Clean up autocomplete instance
function destroyAddressAutocomplete(elementId) {
    if (window._autocompleteInstances[elementId]) {
        google.maps.event.clearInstanceListeners(window._autocompleteInstances[elementId]);
        delete window._autocompleteInstances[elementId];
        console.log(`Address autocomplete destroyed for '${elementId}'`);
    }
}

// Set the value of an autocomplete input programmatically
function setAutocompleteValue(elementId, value) {
    const inputElement = document.getElementById(elementId);
    if (inputElement) {
        inputElement.value = value;
    }
}
