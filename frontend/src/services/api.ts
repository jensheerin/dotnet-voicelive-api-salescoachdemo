/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import { Scenario, Assessment } from '../types'

function extractUserText(conversationMessages: any[]): string {
  return conversationMessages
    .filter(msg => msg.role === 'user')
    .map(msg => msg.content)
    .join(' ')
    .trim()
}

export const api = {
  async getConfig() {
    const res = await fetch('/api/config')
    return res.json()
  },

  async getScenarios(): Promise<Scenario[]> {
    const res = await fetch('/api/scenarios')
    return res.json()
  },

  async createAgent(scenarioId: string) {
    const res = await fetch('/api/agents/create', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ scenario_id: scenarioId }),
    })
    if (!res.ok) throw new Error('Failed to create agent')
    return res.json()
  },

  async analyzeConversation(
    scenarioId: string,
    transcript: string,
    audioData: any[],
    conversationMessages: any[]
  ): Promise<Assessment> {
    const referenceText = extractUserText(conversationMessages)

    const payload = {
      scenario_id: scenarioId,
      transcript,
      audio_data: audioData,
      reference_text: referenceText,
    }

    console.log('[API] analyzeConversation called at:', new Date().toISOString())
    console.log('[API] analyzeConversation payload:', {
      scenario_id: scenarioId,
      transcriptLength: transcript.length,
      audioDataLength: audioData.length,
      reference_text: referenceText.substring(0, 50) + '...',
    })

    try {
      // Create AbortController for timeout
      const controller = new AbortController()
      const timeoutId = setTimeout(() => {
        console.warn('[API] Request taking longer than 45 seconds...')
      }, 45000)

      const res = await fetch('/api/analyze', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
        signal: controller.signal,
      })

      clearTimeout(timeoutId)
      console.log('[API] Response received at:', new Date().toISOString())
      console.log('[API] Response status:', res.status, res.statusText)

      if (!res.ok) {
        const errorText = await res.text()
        console.error('[API] Error response:', errorText)
        throw new Error(`Analysis failed: ${res.status} ${res.statusText}`)
      }

      const result = await res.json()
      console.log('[API] Analysis result received successfully')
      return result
    } catch (error) {
      console.error('[API] Fetch error:', error)

      // Check if it's a network error
      if (error instanceof TypeError && error.message.includes('fetch')) {
        throw new Error(
          'Cannot connect to backend. Please ensure:\n' +
            '1. Backend is running on port 5000 (cd VoiceLive.Api && dotnet run)\n' +
            '2. Frontend dev server is running (npm run dev)\n' +
            '3. Check terminal for any errors'
        )
      }

      // Check if it's an abort error
      if (error instanceof Error && error.name === 'AbortError') {
        throw new Error('Request timeout - analysis taking too long')
      }

      throw error
    }
  },

  async generateGraphScenario(): Promise<Scenario> {
    const res = await fetch('/api/scenarios/graph', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
    })
    if (!res.ok) throw new Error('Failed to generate Graph scenario')
    return res.json()
  },
}
