/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/


import { useEffect, useRef, useState, useCallback } from 'react'
import { Message } from '../types'

interface RealtimeOptions {
  agentId?: string | null
  onMessage?: (msg: any) => void
  onAudioDelta?: (delta: string) => void
  onTranscript?: (role: 'user' | 'assistant', text: string) => void
}

export function useRealtime(options: RealtimeOptions) {
  const [connected, setConnected] = useState(false)
  const [messages, setMessages] = useState<Message[]>([])
  const wsRef = useRef<WebSocket | null>(null)
  const audioRecording = useRef<any[]>([])
  const conversationRecording = useRef<any[]>([])

  const connect = useCallback(async () => {
    const config = await fetch('/api/config').then(r => r.json())
    console.log('[WebSocket] Config received:', config)
    
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
    const wsUrl = `${wsProtocol}//${window.location.host}${config.ws_endpoint || '/ws/voice'}`
    
    console.log('[WebSocket] Connecting to:', wsUrl)
    const ws = new WebSocket(wsUrl)

    ws.onopen = () => {
      console.log('[WebSocket] Connection opened')
      
      // Send initial session configuration with agent ID
      if (options.agentId) {
        const sessionConfig = {
          type: 'session.update',
          session: {
            agent_id: options.agentId
          }
        }
        console.log('[WebSocket] Sending session config:', sessionConfig)
        ws.send(JSON.stringify(sessionConfig))
      }
    }

    ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data)
        console.log('[WebSocket] Message received:', msg.type)
        
        if (msg.type === 'proxy.connected') {
          setConnected(true)
          console.log('[WebSocket] Proxy connected to Azure')
        }
        
        options.onMessage?.(msg)

        // Handle specific message types
        if (msg.type === 'response.audio.delta' && msg.delta) {
          options.onAudioDelta?.(msg.delta)
          audioRecording.current.push({
            type: 'assistant',
            data: msg.delta,
            timestamp: new Date().toISOString(),
          })
        }

        if (msg.type === 'conversation.item.input_audio_transcription.completed' && msg.transcript) {
          const message: Message = {
            id: crypto.randomUUID(),
            role: 'user',
            content: msg.transcript,
            timestamp: new Date(),
          }
          setMessages(prev => [...prev, message])
          conversationRecording.current.push({
            role: 'user',
            content: msg.transcript,
          })
          options.onTranscript?.('user', msg.transcript)
        }

        if (msg.type === 'response.audio_transcript.done' && msg.transcript) {
          const message: Message = {
            id: crypto.randomUUID(),
            role: 'assistant',
            content: msg.transcript,
            timestamp: new Date(),
          }
          setMessages(prev => [...prev, message])
          conversationRecording.current.push({
            role: 'assistant',
            content: msg.transcript,
          })
          options.onTranscript?.('assistant', msg.transcript)
        }
      } catch (error) {
        console.error('[WebSocket] Error parsing message:', error)
      }
    }

    ws.onerror = (error) => {
      console.error('[WebSocket] Error:', error)
      setConnected(false)
    }

    ws.onclose = (event) => {
      console.log('[WebSocket] Connection closed:', event.code, event.reason)
      setConnected(false)
    }

    wsRef.current = ws
  }, [options.agentId])

  const send = useCallback((message: any) => {
    if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
      const messageStr = typeof message === 'string' ? message : JSON.stringify(message)
      console.log('[WebSocket] Sending:', messageStr.substring(0, 100))
      wsRef.current.send(messageStr)
    } else {
      console.warn('[WebSocket] Cannot send, connection not open')
    }
  }, [])

  const clearMessages = useCallback(() => {
    setMessages([])
    conversationRecording.current = []
    audioRecording.current = []
  }, [])

  const getRecordings = useCallback(
    () => ({
      conversation: conversationRecording.current,
      audio: audioRecording.current,
    }),
    []
  )

  useEffect(() => {
    // Only connect if we have an agent ID
    if (!options.agentId) {
      console.log('[WebSocket] No agent ID, skipping connection')
      return
    }

    connect()
    
    return () => {
      if (wsRef.current) {
        console.log('[WebSocket] Cleaning up connection')
        wsRef.current.close()
      }
    }
  }, [connect, options.agentId])

  return {
    connected,
    messages,
    send,
    clearMessages,
    getRecordings,
  }
}
