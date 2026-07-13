import * as signalR from '@microsoft/signalr'
import { API_BASE_URL } from './apiClient'

let connection = null
let isConnected = false

function getHubUrl() {
  return API_BASE_URL.replace('/api', '') + '/hubs/ticket'
}

export function createConnection(token) {
  if (connection) return connection

  const url = getHubUrl()
  connection = new signalR.HubConnectionBuilder()
    .withUrl(url, { accessTokenFactory: () => token })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  connection.onreconnecting(() => { isConnected = false })
  connection.onreconnected(() => { isConnected = true })
  connection.onclose(() => { isConnected = false })

  return connection
}

export async function startConnection(token) {
  const conn = createConnection(token)
  if (isConnected) return
  await conn.start()
  isConnected = true
}

export async function stopConnection() {
  if (connection) {
    await connection.stop()
    connection = null
    isConnected = false
  }
}

export async function joinUserGroup(userId) {
  if (connection && isConnected) {
    await connection.invoke('JoinUserGroup', userId)
  }
}

export function onEvent(event, handler) {
  if (connection) {
    connection.off(event)
    connection.on(event, handler)
  }
}

export function offEvent(event) {
  if (connection) {
    connection.off(event)
  }
}
