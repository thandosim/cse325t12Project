// Location tracking functionality for real-time driver tracking
let watchId = null;
let currentPosition = null;
let trackingCallback = null;
let trackingInterval = null;

/**
 * Start tracking user's location with high accuracy
 * @param {number} updateIntervalMs - How often to update location (in milliseconds)
 * @param {DotNetObjectReference} dotNetRef - .NET reference to call back
 */
window.startLocationTracking = function (updateIntervalMs, dotNetRef) {
    if (!navigator.geolocation) {
        console.error("Geolocation is not supported by this browser");
        return false;
    }

    // Store callback reference
    trackingCallback = dotNetRef;

    // Options for high accuracy tracking
    const options = {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 0
    };

    // Success callback
    const success = (position) => {
        currentPosition = {
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracy: position.coords.accuracy,
            timestamp: new Date(position.timestamp).toISOString()
        };

        console.log("Location updated:", currentPosition);

        // Call back to .NET
        if (trackingCallback) {
            trackingCallback.invokeMethodAsync('OnLocationUpdated', 
                currentPosition.latitude, 
                currentPosition.longitude,
                currentPosition.accuracy);
        }
    };

    // Error callback
    const error = (err) => {
        console.error("Location error:", err);
        if (trackingCallback) {
            trackingCallback.invokeMethodAsync('OnLocationError', err.message);
        }
    };

    // Start watching position
    watchId = navigator.geolocation.watchPosition(success, error, options);

    // Also set up periodic updates (backup mechanism)
    if (updateIntervalMs > 0) {
        trackingInterval = setInterval(() => {
            navigator.geolocation.getCurrentPosition(success, error, options);
        }, updateIntervalMs);
    }

    console.log("Location tracking started");
    return true;
};

/**
 * Stop tracking user's location
 */
window.stopLocationTracking = function () {
    if (watchId !== null) {
        navigator.geolocation.clearWatch(watchId);
        watchId = null;
    }

    if (trackingInterval !== null) {
        clearInterval(trackingInterval);
        trackingInterval = null;
    }

    trackingCallback = null;
    console.log("Location tracking stopped");
};

/**
 * Get current position once
 * @returns {Promise<{latitude: number, longitude: number, accuracy: number}>}
 */
window.getCurrentPosition = function () {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject(new Error("Geolocation is not supported"));
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (position) => {
                resolve({
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude,
                    accuracy: position.coords.accuracy,
                    timestamp: new Date(position.timestamp).toISOString()
                });
            },
            (error) => {
                reject(error);
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 0
            }
        );
    });
};

/**
 * Calculate distance between two points (Haversine formula)
 * @param {number} lat1 - Latitude of point 1
 * @param {number} lon1 - Longitude of point 1
 * @param {number} lat2 - Latitude of point 2
 * @param {number} lon2 - Longitude of point 2
 * @returns {number} Distance in kilometers
 */
window.calculateDistance = function (lat1, lon1, lat2, lon2) {
    const R = 6371; // Earth's radius in kilometers
    const dLat = toRadians(lat2 - lat1);
    const dLon = toRadians(lon2 - lon1);

    const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
              Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) *
              Math.sin(dLon / 2) * Math.sin(dLon / 2);

    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return R * c;
};

/**
 * Calculate bearing between two points
 * @param {number} lat1 - Latitude of point 1
 * @param {number} lon1 - Longitude of point 1
 * @param {number} lat2 - Latitude of point 2
 * @param {number} lon2 - Longitude of point 2
 * @returns {number} Bearing in degrees
 */
window.calculateBearing = function (lat1, lon1, lat2, lon2) {
    const dLon = toRadians(lon2 - lon1);
    const y = Math.sin(dLon) * Math.cos(toRadians(lat2));
    const x = Math.cos(toRadians(lat1)) * Math.sin(toRadians(lat2)) -
              Math.sin(toRadians(lat1)) * Math.cos(toRadians(lat2)) * Math.cos(dLon);
    
    let bearing = Math.atan2(y, x);
    bearing = toDegrees(bearing);
    return (bearing + 360) % 360;
};

/**
 * Request permission for location access
 * @returns {Promise<string>} Permission state
 */
window.requestLocationPermission = async function () {
    if (!navigator.permissions) {
        return "unavailable";
    }

    try {
        const result = await navigator.permissions.query({ name: 'geolocation' });
        console.log("Location permission:", result.state);
        return result.state; // "granted", "denied", or "prompt"
    } catch (error) {
        console.error("Permission query failed:", error);
        return "error";
    }
};

/**
 * Check if location services are available
 * @returns {boolean}
 */
window.isLocationAvailable = function () {
    return 'geolocation' in navigator;
};

// Helper functions
function toRadians(degrees) {
    return degrees * Math.PI / 180;
}

function toDegrees(radians) {
    return radians * 180 / Math.PI;
}
