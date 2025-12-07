// ============================================================
// Leaflet + Mapbox Map Interop for Blazor
// Production-ready implementation with secure API token handling
// ============================================================

// Global map instance
window._leafletMap = null;
window._leafletMarkers = [];
window._userMarker = null;
window._selectionMarker = null;
window._mapClickHandler = null;

// ============================================================
// Map Initialization
// ============================================================

/**
 * Initialize Leaflet map with Mapbox tiles
 * @param {string} mapContainerId - DOM element ID for map container
 * @param {string} accessToken - Mapbox public access token (pk.ey...)
 * @param {number} centerLat - Initial center latitude
 * @param {number} centerLng - Initial center longitude
 * @param {number} zoom - Initial zoom level (1-18)
 */
function initializeMap(mapContainerId, accessToken, centerLat = -26.3167, centerLng = 31.1333, zoom = 8) {
    console.log('🗺️ Initializing map...', { mapContainerId, hasToken: !!accessToken, centerLat, centerLng, zoom });
    
    // Check if Leaflet is loaded
    if (typeof L === 'undefined') {
        console.error('❌ Leaflet library not loaded! Make sure leaflet.js is included before mapInterop.js');
        return;
    }
    
    // Check if map container exists
    const container = document.getElementById(mapContainerId);
    if (!container) {
        console.error(`❌ Map container '#${mapContainerId}' not found in DOM`);
        return;
    }
    
    console.log('✓ Container found:', container, 'Size:', container.offsetWidth, 'x', container.offsetHeight);
    
    // Clean up existing map if reinitializing
    if (window._leafletMap) {
        console.log('Removing existing map instance');
        window._leafletMap.remove();
        window._leafletMap = null;
    }

    try {
        // Initialize Leaflet map
        console.log('Creating Leaflet map instance...');
        window._leafletMap = L.map(mapContainerId).setView([centerLat, centerLng], zoom);
        console.log('✓ Map instance created');

        // Add Mapbox tile layer with access token
        console.log('Adding Mapbox tile layer...');
        const tileUrl = `https://api.mapbox.com/styles/v1/mapbox/streets-v12/tiles/{z}/{x}/{y}?access_token=${accessToken}`;
        console.log('Tile URL:', tileUrl.substring(0, 100) + '...');
        
        const tileLayer = L.tileLayer(tileUrl, {
            attribution: '© <a href="https://www.mapbox.com/about/maps/">Mapbox</a> © <a href="http://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 18,
            tileSize: 512,
            zoomOffset: -1
        });
        
        // Add error handling for tile loading
        tileLayer.on('tileerror', function(error, tile) {
            console.error('❌ Tile loading error:', error, tile);
        });
        
        tileLayer.on('tileload', function() {
            console.log('✓ Tile loaded successfully');
        });
        
        tileLayer.addTo(window._leafletMap);

        console.log('✓ Leaflet map initialized with Mapbox tiles');
        
        // Force map to refresh size
        setTimeout(() => {
            if (window._leafletMap) {
                window._leafletMap.invalidateSize();
                console.log('✓ Map size invalidated');
            }
        }, 100);
        
    } catch (error) {
        console.error('❌ Error initializing map:', error);
        throw error;
    }
}

/**
 * Set markers on the map with different types and styling
 * @param {Array} markers - Array of marker objects: { lat, lng, type, label, details }
 * @param {string} accessToken - Mapbox access token for geocoding if needed
 */
function setMarkers(markers, accessToken) {
    console.log('🗺️ setMarkers called with:', markers);
    
    if (!window._leafletMap) {
        console.error('❌ Map not initialized. Call initializeMap() first.');
        return;
    }

    // Clear existing markers
    console.log(`Clearing ${window._leafletMarkers.length} existing markers`);
    window._leafletMarkers.forEach(marker => marker.remove());
    window._leafletMarkers = [];

    if (!markers || markers.length === 0) {
        console.log('ℹ️ No markers to display');
        return;
    }

    console.log(`📍 Adding ${markers.length} markers to map`);

    // Define marker colors based on type
    const markerColors = {
        'driver': '#14B8A6',    // Teal (matches existing legend)
        'load': '#F59E0B',      // Amber
        'user': '#3B82F6',      // Blue
        'pickup': '#EAB308',    // Yellow
        'dropoff': '#EF4444',   // Red
        'default': '#6B7280'    // Gray
    };

    // Create bounds for auto-fitting
    const bounds = L.latLngBounds();

    // Add markers to map
    markers.forEach((pin, index) => {
        console.log(`  Marker ${index + 1}:`, pin);
        
        const color = markerColors[pin.type] || markerColors['default'];
        
        // Create custom marker icon
        const markerIcon = L.divIcon({
            className: 'custom-marker',
            html: `<div style="
                background-color: ${color};
                width: 24px;
                height: 24px;
                border-radius: 50% 50% 50% 0;
                transform: rotate(-45deg);
                border: 3px solid #fff;
                box-shadow: 0 2px 5px rgba(0,0,0,0.3);
            "></div>`,
            iconSize: [24, 24],
            iconAnchor: [12, 24],
            popupAnchor: [0, -24]
        });

        // Create marker
        const marker = L.marker([pin.lat, pin.lng], { icon: markerIcon })
            .addTo(window._leafletMap);

        // Add popup with info
        const popupContent = `
            <div style="font-family: sans-serif; min-width: 150px;">
                <strong style="font-size: 14px;">${pin.label || 'Location'}</strong>
                ${pin.details ? `<br><span style="color: #666; font-size: 12px;">${pin.details}</span>` : ''}
            </div>
        `;
        marker.bindPopup(popupContent);

        window._leafletMarkers.push(marker);
        bounds.extend([pin.lat, pin.lng]);
    });

    // Fit map to show all markers
    if (markers.length > 0) {
        console.log('📐 Fitting map bounds to show all markers');
        window._leafletMap.fitBounds(bounds, { padding: [50, 50] });
        
        // Don't zoom in too much for single marker
        if (markers.length === 1 && window._leafletMap.getZoom() > 12) {
            window._leafletMap.setZoom(12);
        }
    }

    console.log(`✅ ${markers.length} markers successfully added to map`);
}

// ============================================================
// Geolocation Functions
// ============================================================

/**
 * Get user's current location using browser geolocation API
 * Returns: { latitude, longitude, accuracy }
 */
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

/**
 * Watch user's location for continuous updates
 */
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

/**
 * Update or create user location marker on map
 * @param {number} lat - Latitude
 * @param {number} lng - Longitude
 * @param {string} label - Marker label
 */
function updateUserMarker(lat, lng, label) {
    if (!window._leafletMap) return;

    // Remove existing user marker
    if (window._userMarker) {
        window._userMarker.remove();
    }

    // Create custom blue pulsing marker for user location
    const userIcon = L.divIcon({
        className: 'user-location-marker',
        html: `
            <div style="position: relative;">
                <div style="
                    background-color: rgba(59, 130, 246, 0.2);
                    width: 40px;
                    height: 40px;
                    border-radius: 50%;
                    position: absolute;
                    top: -20px;
                    left: -20px;
                    animation: pulse 2s infinite;
                "></div>
                <div style="
                    background-color: #3B82F6;
                    width: 16px;
                    height: 16px;
                    border-radius: 50%;
                    border: 3px solid #fff;
                    box-shadow: 0 2px 5px rgba(0,0,0,0.3);
                    position: absolute;
                    top: -8px;
                    left: -8px;
                "></div>
            </div>
            <style>
                @keyframes pulse {
                    0% { transform: scale(1); opacity: 1; }
                    100% { transform: scale(1.5); opacity: 0; }
                }
            </style>
        `,
        iconSize: [16, 16],
        iconAnchor: [8, 8]
    });

    window._userMarker = L.marker([lat, lng], { icon: userIcon })
        .addTo(window._leafletMap)
        .bindPopup(label || 'Your Location');

    // Pan to user location
    window._leafletMap.panTo([lat, lng]);
}

/**
 * Pan map to specific location
 */
function panToLocation(lat, lng, zoom) {
    if (!window._leafletMap) return;
    
    window._leafletMap.panTo([lat, lng]);
    if (zoom) {
        window._leafletMap.setZoom(zoom);
    }
}

// ============================================================
// Location Selection Functions
// ============================================================

/**
 * Enable click-to-select location mode
 */
function enableLocationSelection(dotNetHelper) {
    if (!window._leafletMap) return;

    // Change cursor to crosshair
    document.getElementById('map').style.cursor = 'crosshair';

    // Add click handler
    window._mapClickHandler = function(e) {
        const lat = e.latlng.lat;
        const lng = e.latlng.lng;

        // Remove existing selection marker
        if (window._selectionMarker) {
            window._selectionMarker.remove();
        }

        // Create selection marker (green/emerald)
        const selectionIcon = L.divIcon({
            className: 'selection-marker',
            html: `<div style="
                background-color: #10B981;
                width: 20px;
                height: 20px;
                border-radius: 50%;
                border: 3px solid #fff;
                box-shadow: 0 2px 5px rgba(0,0,0,0.3);
            "></div>`,
            iconSize: [20, 20],
            iconAnchor: [10, 10]
        });

        window._selectionMarker = L.marker([lat, lng], { 
            icon: selectionIcon,
            draggable: true
        }).addTo(window._leafletMap);

        // Notify Blazor of selection
        dotNetHelper.invokeMethodAsync('OnLocationSelected', lat, lng);

        // Update on drag
        window._selectionMarker.on('dragend', function(e) {
            const pos = e.target.getLatLng();
            dotNetHelper.invokeMethodAsync('OnLocationSelected', pos.lat, pos.lng);
        });
    };

    window._leafletMap.on('click', window._mapClickHandler);
    console.log('✓ Location selection enabled');
}

/**
 * Disable click-to-select location mode
 */
function disableLocationSelection() {
    if (!window._leafletMap) return;

    // Reset cursor
    document.getElementById('map').style.cursor = '';

    // Remove click handler
    if (window._mapClickHandler) {
        window._leafletMap.off('click', window._mapClickHandler);
        window._mapClickHandler = null;
    }

    // Remove selection marker
    if (window._selectionMarker) {
        window._selectionMarker.remove();
        window._selectionMarker = null;
    }

    console.log('✓ Location selection disabled');
}

// ============================================================
// Mapbox Geocoding API Integration
// ============================================================

/**
 * Reverse geocode: Get address from coordinates using Mapbox Geocoding API
 * @param {number} lat - Latitude
 * @param {number} lng - Longitude
 * @param {string} accessToken - Mapbox access token
 */
async function getAddressFromCoordinates(lat, lng, accessToken) {
    try {
        const response = await fetch(
            `https://api.mapbox.com/geocoding/v5/mapbox.places/${lng},${lat}.json?access_token=${accessToken}`
        );
        
        if (!response.ok) {
            throw new Error(`Geocoding API error: ${response.status}`);
        }

        const data = await response.json();
        
        if (data.features && data.features.length > 0) {
            return data.features[0].place_name;
        } else {
            throw new Error('No address found for coordinates');
        }
    } catch (error) {
        console.error('Reverse geocoding error:', error);
        throw error;
    }
}

/**
 * Forward geocode: Get coordinates from address using Mapbox Geocoding API
 * @param {string} address - Address string to geocode
 * @param {string} accessToken - Mapbox access token
 */
async function getCoordinatesFromAddress(address, accessToken) {
    try {
        const encodedAddress = encodeURIComponent(address);
        const response = await fetch(
            `https://api.mapbox.com/geocoding/v5/mapbox.places/${encodedAddress}.json?access_token=${accessToken}`
        );
        
        if (!response.ok) {
            throw new Error(`Geocoding API error: ${response.status}`);
        }

        const data = await response.json();
        
        if (data.features && data.features.length > 0) {
            const feature = data.features[0];
            return {
                lat: feature.center[1],
                lng: feature.center[0],
                formattedAddress: feature.place_name
            };
        } else {
            throw new Error('Address not found');
        }
    } catch (error) {
        console.error('Forward geocoding error:', error);
        throw error;
    }
}

// ============================================================
// Mapbox Search Box / Autocomplete
// ============================================================

// Store autocomplete instances by element ID
window._autocompleteInstances = {};

/**
 * Initialize Mapbox address autocomplete on an input element
 * @param {string} elementId - Input element ID
 * @param {string} accessToken - Mapbox access token
 * @param {object} dotNetHelper - DotNetObjectReference for callbacks
 * @param {number} minLength - Minimum characters before showing suggestions
 */
function initAddressAutocomplete(elementId, accessToken, dotNetHelper, minLength = 3) {
    const inputElement = document.getElementById(elementId);
    if (!inputElement) {
        console.error(`Element with ID '${elementId}' not found`);
        return;
    }

    // Clean up existing autocomplete
    if (window._autocompleteInstances[elementId]) {
        destroyAddressAutocomplete(elementId);
    }

    // Create suggestion container
    const suggestionsId = `${elementId}-suggestions`;
    let suggestionsContainer = document.getElementById(suggestionsId);
    
    if (!suggestionsContainer) {
        suggestionsContainer = document.createElement('div');
        suggestionsContainer.id = suggestionsId;
        suggestionsContainer.style.cssText = `
            position: absolute;
            background: white;
            border: 1px solid #ddd;
            border-radius: 4px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.15);
            max-height: 300px;
            overflow-y: auto;
            z-index: 1000;
            display: none;
            min-width: 300px;
        `;
        inputElement.parentElement.style.position = 'relative';
        inputElement.parentElement.appendChild(suggestionsContainer);
    }

    // Debounce timer
    let debounceTimer = null;
    let selectedIndex = -1;

    // Input handler for autocomplete
    const handleInput = async function(e) {
        const query = e.target.value.trim();
        
        clearTimeout(debounceTimer);
        
        if (query.length < minLength) {
            suggestionsContainer.style.display = 'none';
            return;
        }

        debounceTimer = setTimeout(async () => {
            try {
                const response = await fetch(
                    `https://api.mapbox.com/geocoding/v5/mapbox.places/${encodeURIComponent(query)}.json?access_token=${accessToken}&autocomplete=true&limit=5`
                );

                const data = await response.json();
                
                if (data.features && data.features.length > 0) {
                    displaySuggestions(data.features);
                } else {
                    suggestionsContainer.style.display = 'none';
                }
            } catch (error) {
                console.error('Autocomplete error:', error);
                suggestionsContainer.style.display = 'none';
            }
        }, 300);
    };

    // Display suggestions
    const displaySuggestions = function(features) {
        suggestionsContainer.innerHTML = '';
        selectedIndex = -1;

        features.forEach((feature, index) => {
            const div = document.createElement('div');
            div.style.cssText = `
                padding: 10px 15px;
                cursor: pointer;
                border-bottom: 1px solid #f0f0f0;
                font-size: 14px;
            `;
            div.textContent = feature.place_name;
            div.dataset.index = index;

            div.addEventListener('mouseenter', () => {
                div.style.backgroundColor = '#f5f5f5';
            });

            div.addEventListener('mouseleave', () => {
                div.style.backgroundColor = 'white';
            });

            div.addEventListener('click', () => {
                selectSuggestion(feature);
            });

            suggestionsContainer.appendChild(div);
        });

        suggestionsContainer.style.display = 'block';
    };

    // Select suggestion
    const selectSuggestion = function(feature) {
        inputElement.value = feature.place_name;
        suggestionsContainer.style.display = 'none';

        // Parse address components
        const result = {
            address: feature.place_name,
            latitude: feature.center[1],
            longitude: feature.center[0],
            name: feature.text || '',
            components: {}
        };

        // Extract context information
        if (feature.context) {
            feature.context.forEach(ctx => {
                if (ctx.id.startsWith('place')) {
                    result.components.city = ctx.text;
                } else if (ctx.id.startsWith('region')) {
                    result.components.state = ctx.text;
                } else if (ctx.id.startsWith('country')) {
                    result.components.country = ctx.text;
                } else if (ctx.id.startsWith('postcode')) {
                    result.components.postalCode = ctx.text;
                }
            });
        }

        // Notify Blazor
        dotNetHelper.invokeMethodAsync('OnPlaceSelected', result);
    };

    // Keyboard navigation
    const handleKeydown = function(e) {
        const items = suggestionsContainer.children;
        
        if (items.length === 0) return;

        if (e.key === 'ArrowDown') {
            e.preventDefault();
            selectedIndex = Math.min(selectedIndex + 1, items.length - 1);
            updateSelection(items);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            selectedIndex = Math.max(selectedIndex - 1, 0);
            updateSelection(items);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (selectedIndex >= 0 && items[selectedIndex]) {
                items[selectedIndex].click();
            }
        } else if (e.key === 'Escape') {
            suggestionsContainer.style.display = 'none';
        }
    };

    const updateSelection = function(items) {
        Array.from(items).forEach((item, index) => {
            item.style.backgroundColor = index === selectedIndex ? '#f5f5f5' : 'white';
        });
    };

    // Close on outside click
    const handleClickOutside = function(e) {
        if (!inputElement.contains(e.target) && !suggestionsContainer.contains(e.target)) {
            suggestionsContainer.style.display = 'none';
        }
    };

    // Attach event listeners
    inputElement.addEventListener('input', handleInput);
    inputElement.addEventListener('keydown', handleKeydown);
    document.addEventListener('click', handleClickOutside);

    // Store instance for cleanup
    window._autocompleteInstances[elementId] = {
        inputElement,
        suggestionsContainer,
        handleInput,
        handleKeydown,
        handleClickOutside
    };

    console.log(`✓ Mapbox autocomplete initialized for '${elementId}' with min ${minLength} chars`);
}

/**
 * Clean up autocomplete instance
 */
function destroyAddressAutocomplete(elementId) {
    const instance = window._autocompleteInstances[elementId];
    if (instance) {
        instance.inputElement.removeEventListener('input', instance.handleInput);
        instance.inputElement.removeEventListener('keydown', instance.handleKeydown);
        document.removeEventListener('click', instance.handleClickOutside);
        
        if (instance.suggestionsContainer && instance.suggestionsContainer.parentElement) {
            instance.suggestionsContainer.remove();
        }

        delete window._autocompleteInstances[elementId];
        console.log(`✓ Autocomplete destroyed for '${elementId}'`);
    }
}

/**
 * Set the value of an autocomplete input programmatically
 */
function setAutocompleteValue(elementId, value) {
    const inputElement = document.getElementById(elementId);
    if (inputElement) {
        inputElement.value = value;
    }
}

// ============================================================
// Real-Time Driver Tracking
// ============================================================

let _driverMarker = null;
let _driverTrail = null;
let _destinationMarker = null;

/**
 * Update driver marker position on the map
 * @param {number} latitude - Driver's current latitude
 * @param {number} longitude - Driver's current longitude
 * @param {string} driverName - Driver's name for popup
 * @param {number} bearing - Optional bearing/heading in degrees
 */
function updateDriverMarker(latitude, longitude, driverName = "Driver", bearing = null) {
    if (!window._leafletMap) {
        console.warn("Map not initialized");
        return;
    }

    const position = [latitude, longitude];

    // Create custom icon for driver (car icon)
    const driverIcon = L.divIcon({
        className: 'driver-marker',
        html: `<div class="driver-icon" style="transform: rotate(${bearing || 0}deg);">
                   <svg width="32" height="32" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                       <circle cx="12" cy="12" r="10" fill="#2563eb" stroke="white" stroke-width="2"/>
                       <path d="M12 8 L12 12 L15 15" stroke="white" stroke-width="2" stroke-linecap="round"/>
                   </svg>
               </div>`,
        iconSize: [32, 32],
        iconAnchor: [16, 16]
    });

    if (_driverMarker) {
        // Update existing marker
        _driverMarker.setLatLng(position);
        if (bearing !== null) {
            _driverMarker.setIcon(driverIcon);
        }
    } else {
        // Create new marker
        _driverMarker = L.marker(position, { icon: driverIcon })
            .addTo(window._leafletMap)
            .bindPopup(`<b>${driverName}</b><br>Current Location`);
    }

    // Update trail
    if (_driverTrail) {
        _driverTrail.addLatLng(position);
    } else {
        _driverTrail = L.polyline([position], {
            color: '#2563eb',
            weight: 3,
            opacity: 0.7,
            smoothFactor: 1
        }).addTo(window._leafletMap);
    }

    // Pan map to follow driver
    window._leafletMap.panTo(position);

    console.log(`Driver marker updated: (${latitude}, ${longitude})`);
}

/**
 * Set destination marker for the driver
 * @param {number} latitude - Destination latitude
 * @param {number} longitude - Destination longitude
 * @param {string} label - Destination label
 */
function setDestinationMarker(latitude, longitude, label = "Destination") {
    if (!window._leafletMap) {
        console.warn("Map not initialized");
        return;
    }

    const position = [latitude, longitude];

    // Create destination icon
    const destinationIcon = L.divIcon({
        className: 'destination-marker',
        html: `<div class="destination-icon">
                   <svg width="32" height="40" viewBox="0 0 24 30" xmlns="http://www.w3.org/2000/svg">
                       <path d="M12 0C7.589 0 4 3.589 4 8c0 6.5 8 14 8 14s8-7.5 8-14c0-4.411-3.589-8-8-8z" 
                             fill="#dc2626" stroke="white" stroke-width="1.5"/>
                       <circle cx="12" cy="8" r="3" fill="white"/>
                   </svg>
               </div>`,
        iconSize: [32, 40],
        iconAnchor: [16, 40]
    });

    if (_destinationMarker) {
        _destinationMarker.setLatLng(position);
    } else {
        _destinationMarker = L.marker(position, { icon: destinationIcon })
            .addTo(window._leafletMap)
            .bindPopup(`<b>${label}</b>`);
    }

    console.log(`Destination marker set: (${latitude}, ${longitude})`);
}

/**
 * Draw route between driver and destination
 * @param {Array<{lat: number, lng: number}>} routePoints - Array of lat/lng points
 */
function drawRoute(routePoints) {
    if (!window._leafletMap) {
        console.warn("Map not initialized");
        return;
    }

    // Remove existing route
    if (window._routeLine) {
        window._leafletMap.removeLayer(window._routeLine);
    }

    // Convert to Leaflet format
    const latlngs = routePoints.map(p => [p.lat, p.lng]);

    // Draw route line
    window._routeLine = L.polyline(latlngs, {
        color: '#3b82f6',
        weight: 4,
        opacity: 0.8,
        dashArray: '10, 10'
    }).addTo(window._leafletMap);

    // Fit map to show entire route
    window._leafletMap.fitBounds(window._routeLine.getBounds(), { padding: [50, 50] });

    console.log(`Route drawn with ${routePoints.length} points`);
}

/**
 * Clear driver tracking markers and trail
 */
function clearDriverTracking() {
    if (_driverMarker) {
        window._leafletMap.removeLayer(_driverMarker);
        _driverMarker = null;
    }

    if (_driverTrail) {
        window._leafletMap.removeLayer(_driverTrail);
        _driverTrail = null;
    }

    if (_destinationMarker) {
        window._leafletMap.removeLayer(_destinationMarker);
        _destinationMarker = null;
    }

    if (window._routeLine) {
        window._leafletMap.removeLayer(window._routeLine);
        window._routeLine = null;
    }

    console.log("Driver tracking cleared");
}

/**
 * Fit map to show both driver and destination
 */
function fitMapToDriverAndDestination() {
    if (!window._leafletMap || !_driverMarker || !_destinationMarker) {
        return;
    }

    const bounds = L.latLngBounds([
        _driverMarker.getLatLng(),
        _destinationMarker.getLatLng()
    ]);

    window._leafletMap.fitBounds(bounds, { padding: [100, 100] });
}

