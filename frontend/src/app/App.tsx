/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import React, { useState, useCallback, useEffect } from 'react'
import {
  Dialog,
  DialogSurface,
  DialogBody,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { ScenarioList } from '../components/ScenarioList'
import { VideoPanel } from '../components/VideoPanel'
import { ChatPanel } from '../components/ChatPanel'
import { AssessmentPanel } from '../components/AssessmentPanel'
import { useScenarios } from '../hooks/useScenarios'
import { useRealtime } from '../hooks/useRealtime'
import { useWebRTC } from '../hooks/useWebRTC'
import { useRecorder } from '../hooks/useRecorder'
import { useAudioPlayer } from '../hooks/useAudioPlayer'
import { api } from '../services/api'
import { Assessment } from '../types'

const useStyles = makeStyles({
  container: {
    width: '100%',
    height: '100vh',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: tokens.colorNeutralBackground3,
    padding: tokens.spacingVerticalL,
  },
  mainLayout: {
    width: '95%',
    maxWidth: '1400px',
    height: '90vh',
    display: 'flex',
    gap: tokens.spacingHorizontalL,
  },
  setupDialog: {
    maxWidth: '600px',
    width: '90vw',
  },
  loadingContent: {
    gridColumn: '1 / -1',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    textAlign: 'center',
    width: '100%',
  },
})

export default function App() {
  const styles = useStyles()
  const [showSetup, setShowSetup] = useState(true)
  const [showLoading, setShowLoading] = useState(false)
  const [showAssessment, setShowAssessment] = useState(false)
  const [currentAgent, setCurrentAgent] = useState<string | null>(null)
  const [assessment, setAssessment] = useState<Assessment | null>(null)
  const [selectedScenarioData, setSelectedScenarioData] = useState<any>(null)

  const { scenarios, selectedScenario, setSelectedScenario, loading } =
    useScenarios()
  const { playAudio } = useAudioPlayer()
  const activeScenario =
    selectedScenarioData ||
    scenarios.find(s => s.id === selectedScenario) ||
    null

  const handleWebRTCMessage = useCallback((msg: any) => {
    if (msg.type === 'session.updated') {
      const session = msg.session
      const servers =
        session?.avatar?.ice_servers ||
        session?.rtc?.ice_servers ||
        session?.ice_servers
      const username =
        session?.avatar?.username ||
        session?.avatar?.ice_username ||
        session?.rtc?.ice_username ||
        session?.ice_username
      const credential =
        session?.avatar?.credential ||
        session?.avatar?.ice_credential ||
        session?.rtc?.ice_credential ||
        session?.ice_credential

      if (servers) {
        setupWebRTC(servers, username, credential)
      }
    } else if (
      (msg.server_sdp || msg.sdp || msg.answer) &&
      msg.type !== 'session.update'
    ) {
      handleAnswer(msg)
    }
  }, [])

  const { connected, messages, send, clearMessages, getRecordings } =
    useRealtime({
      agentId: currentAgent,
      onMessage: handleWebRTCMessage,
      onAudioDelta: playAudio,
    })

  const sendOffer = useCallback(
    (sdp: string) => {
      send({ type: 'session.avatar.connect', client_sdp: sdp })
    },
    [send]
  )

  const { setupWebRTC, handleAnswer, videoRef } = useWebRTC(sendOffer)

  const sendAudioChunk = useCallback(
    (base64: string) => {
      send({ type: 'input_audio_buffer.append', audio: base64 })
    },
    [send]
  )

  const { recording, toggleRecording, getAudioRecording } =
    useRecorder(sendAudioChunk)

  const handleToggleRecording = useCallback(() => {
    console.log('[App] Toggle recording called. Current recording state:', recording)
    console.log('[App] Current agent:', currentAgent)
    console.log('[App] Connected:', connected)
    toggleRecording()
  }, [recording, currentAgent, connected, toggleRecording])

  // Monitor currentAgent changes
  useEffect(() => {
    console.log('[App] currentAgent changed to:', currentAgent)
  }, [currentAgent])

  // Monitor connected state changes
  useEffect(() => {
    console.log('[App] connected state changed to:', connected)
  }, [connected])

  const handleStart = async () => {
    if (!selectedScenario) return

    try {
      console.log('[App] Creating agent for scenario:', selectedScenario)
      const { agent_id } = await api.createAgent(selectedScenario)
      console.log('[App] Agent created:', agent_id)
      setCurrentAgent(agent_id)
      console.log('[App] Current agent state set to:', agent_id)
      setShowSetup(false)
    } catch (error) {
      console.error('Failed to create agent:', error)
    }
  }

  const handleAnalyze = async () => {
    console.log('[App] handleAnalyze called')
    console.log('[App] selectedScenario:', selectedScenario)
    
    if (!selectedScenario) {
      console.warn('[App] No scenario selected, aborting analysis')
      alert('No scenario selected')
      return
    }

    const recordings = getRecordings()
    const audioData = getAudioRecording()

    console.log('[App] Recordings:', {
      conversationLength: recordings.conversation.length,
      audioLength: recordings.audio.length,
      audioDataLength: audioData.length
    })

    if (!recordings.conversation.length) {
      console.warn('[App] No conversation recordings found, aborting analysis')
      alert('No conversation to analyze. Please have a conversation first.')
      return
    }

    console.log('[App] Starting analysis...')
    setShowLoading(true)

    try {
      const transcript = recordings.conversation
        .map((m: any) => `${m.role}: ${m.content}`)
        .join('\n')

      console.log('[App] Transcript:', transcript)
      console.log('[App] Calling API analyzeConversation...')

      const result = await api.analyzeConversation(
        selectedScenario,
        transcript,
        [...audioData, ...recordings.audio],
        recordings.conversation
      )

      console.log('[App] Analysis result:', result)
      setAssessment(result)
      setShowAssessment(true)
    } catch (error) {
      console.error('[App] Analysis failed:', error)
      const errorMessage = error instanceof Error ? error.message : 'Unknown error'
      console.error('[App] Error details:', errorMessage)
      alert(`Analysis failed:\n\n${errorMessage}\n\nCheck browser console for details.`)
    } finally {
      setShowLoading(false)
    }
  }

  const handleScenarioGenerated = useCallback((scenario: any) => {
    setSelectedScenarioData(scenario)
  }, [])

  return (
    <div className={styles.container}>
      <Dialog
        open={showSetup}
        onOpenChange={(_, data) => setShowSetup(data.open)}
      >
        <DialogSurface className={styles.setupDialog}>
          <DialogBody>
            {loading ? (
              <Spinner label="Loading scenarios..." />
            ) : (
              <ScenarioList
                scenarios={scenarios}
                selectedScenario={selectedScenario}
                onSelect={setSelectedScenario}
                onStart={handleStart}
                onScenarioGenerated={handleScenarioGenerated}
              />
            )}
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={showLoading}>
        <DialogSurface>
          <DialogBody>
            <div className={styles.loadingContent}>
              <Spinner size="large" />
              <Text
                size={400}
                weight="semibold"
                block
                style={{ marginTop: tokens.spacingVerticalL }}
              >
                Analyzing Performance...
              </Text>
              <Text
                size={200}
                block
                style={{ marginTop: tokens.spacingVerticalS }}
              >
                This may take up to 30 seconds
              </Text>
            </div>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <AssessmentPanel
        open={showAssessment}
        assessment={assessment}
        onClose={() => setShowAssessment(false)}
      />

      {!showSetup && (
        <div className={styles.mainLayout}>
          <VideoPanel videoRef={videoRef} />
          <ChatPanel
            messages={messages}
            recording={recording}
            connected={connected}
            canAnalyze={messages.length > 0}
            onToggleRecording={handleToggleRecording}
            onClear={clearMessages}
            onAnalyze={handleAnalyze}
            scenario={activeScenario}
          />
        </div>
      )}
    </div>
  )
}
