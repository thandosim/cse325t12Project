// SignalR connection for real-time load tracking
import * as signalR from "@microsoft/signalr";

let connection = null;
let isConnected = false;

/**
 * Initialize SignalR connection to LoadTrackingHub
 */
export async function initializeConnection() {
    if (connection && isConnected) {
        console.log("SignalR already connected");
        return connection;
    }

    connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/loadtracking")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Connection event handlers
    connection.onreconnecting((error) => {
        console.warn("SignalR reconnecting:", error);
        isConnected = false;
    });

    connection.onreconnected((connectionId) => {
        console.log("SignalR reconnected:", connectionId);
        isConnected = true;
    });

    connection.onclose((error) => {
        console.error("SignalR connection closed:", error);
        isConnected = false;
    });

    try {
        await connection.start();
        isConnected = true;
        console.log("SignalR connected successfully");
        return connection;
    } catch (err) {
        console.error("SignalR connection failed:", err);
        isConnected = false;
        throw err;
    }
}

/**
 * Subscribe to real-time updates for a specific load
 */
export async function subscribeToLoad(loadId) {
    if (!connection || !isConnected) {
        await initializeConnection();
    }
    
    try {
        await connection.invoke("SubscribeToLoad", loadId);
        console.log(`Subscribed to load ${loadId}`);
    } catch (err) {
        console.error(`Failed to subscribe to load ${loadId}:`, err);
        throw err;
    }
}

/**
 * Unsubscribe from load updates
 */
export async function unsubscribeFromLoad(loadId) {
    if (!connection || !isConnected) {
        return;
    }
    
    try {
        await connection.invoke("UnsubscribeFromLoad", loadId);
        console.log(`Unsubscribed from load ${loadId}`);
    } catch (err) {
        console.error(`Failed to unsubscribe from load ${loadId}:`, err);
    }
}

/**
 * Update driver's current location for a load
 */
export async function updateDriverLocation(loadId, latitude, longitude) {
    if (!connection || !isConnected) {
        await initializeConnection();
    }
    
    try {
        await connection.invoke("UpdateDriverLocation", loadId, latitude, longitude);
        console.log(`Location updated for load ${loadId}: (${latitude}, ${longitude})`);
    } catch (err) {
        console.error(`Failed to update location for load ${loadId}:`, err);
        throw err;
    }
}

/**
 * Listen for location updates
 */
export function onLocationUpdate(callback) {
    if (!connection) {
        console.warn("SignalR connection not initialized");
        return;
    }
    
    connection.on("ReceiveLocationUpdate", (data) => {
        console.log("Location update received:", data);
        callback(data);
    });
}

/**
 * Listen for load status changes
 */
export function onLoadStatusChanged(callback) {
    if (!connection) {
        console.warn("SignalR connection not initialized");
        return;
    }
    
    connection.on("LoadStatusChanged", (data) => {
        console.log("Load status changed:", data);
        callback(data);
    });
}

/**
 * Listen for ETA updates
 */
export function onETAUpdate(callback) {
    if (!connection) {
        console.warn("SignalR connection not initialized");
        return;
    }
    
    connection.on("ReceiveETAUpdate", (data) => {
        console.log("ETA update received:", data);
        callback(data);
    });
}

/**
 * Listen for notifications
 */
export function onNotification(callback) {
    if (!connection) {
        console.warn("SignalR connection not initialized");
        return;
    }
    
    connection.on("ReceiveNotification", (data) => {
        console.log("Notification received:", data);
        callback(data);
    });
}

/**
 * Remove all event listeners and disconnect
 */
export async function disconnect() {
    if (!connection) {
        return;
    }
    
    try {
        connection.off("ReceiveLocationUpdate");
        connection.off("LoadStatusChanged");
        connection.off("ReceiveETAUpdate");
        connection.off("ReceiveNotification");
        
        await connection.stop();
        isConnected = false;
        console.log("SignalR disconnected");
    } catch (err) {
        console.error("Error disconnecting SignalR:", err);
    }
}

/**
 * Check if connection is established
 */
export function isConnectionActive() {
    return connection && isConnected;
}

/**
 * Get connection state
 */
export function getConnectionState() {
    if (!connection) {
        return "Disconnected";
    }
    
    switch (connection.state) {
        case signalR.HubConnectionState.Connected:
            return "Connected";
        case signalR.HubConnectionState.Connecting:
            return "Connecting";
        case signalR.HubConnectionState.Reconnecting:
            return "Reconnecting";
        case signalR.HubConnectionState.Disconnected:
            return "Disconnected";
        default:
            return "Unknown";
    }
}
