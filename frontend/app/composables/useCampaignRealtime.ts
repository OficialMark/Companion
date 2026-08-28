import * as signalR from '@microsoft/signalr'
import type { CampaignEvent, Participant } from '~/types/campaign'

let connection: signalR.HubConnection | null = null

export function useCampaignRealtime() {
  const store = useCampaignStore()
  const config = useRuntimeConfig()

  async function joinAndRefresh() {
    if (!connection || !store.session) return
    await connection.invoke('JoinCampaign', store.session.campaignId, store.session.participantToken)
    await store.refresh()
    store.connection = 'online'
  }

  async function connect() {
    if (!store.session) return
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await joinAndRefresh()
      return
    }
    store.connection = 'connecting'
    if (!connection) {
      connection = new signalR.HubConnectionBuilder()
        .withUrl(config.public.hubUrl)
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build()
      connection.on('CampaignEvent', (event: CampaignEvent) => { void store.applyEvent(event) })
      connection.on('ParticipantsChanged', (event: CampaignEvent<Participant[]>) => store.applyParticipants(event))
      connection.onreconnecting(() => { store.connection = 'connecting' })
      connection.onreconnected(() => { void joinAndRefresh() })
      connection.onclose(() => { store.connection = 'offline' })
    }

    try {
      await connection.start()
      await joinAndRefresh()
    } catch {
      store.connection = 'offline'
    }
  }

  async function disconnect() {
    if (connection) await connection.stop()
    connection = null
    store.connection = 'offline'
  }

  return { connect, disconnect }
}
