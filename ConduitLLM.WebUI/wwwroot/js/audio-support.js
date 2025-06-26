// Audio support functions for ConduitLLM WebUI

window.audioSupport = {
    mediaRecorder: null,
    audioChunks: [],
    audioContext: null,
    stream: null,
    
    // Initialize audio context (call once when page loads)
    initialize: function() {
        if (!this.audioContext) {
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }
    },
    
    // Check if browser supports required audio APIs
    checkSupport: function() {
        return {
            mediaRecorder: typeof MediaRecorder !== 'undefined',
            getUserMedia: navigator.mediaDevices && navigator.mediaDevices.getUserMedia,
            audioContext: typeof AudioContext !== 'undefined' || typeof webkitAudioContext !== 'undefined'
        };
    },
    
    // Start recording audio from microphone
    startRecording: async function() {
        try {
            // Request microphone access
            this.stream = await navigator.mediaDevices.getUserMedia({ 
                audio: {
                    echoCancellation: true,
                    noiseSuppression: true,
                    sampleRate: 44100
                } 
            });
            
            // Reset chunks
            this.audioChunks = [];
            
            // Create MediaRecorder with appropriate MIME type
            const mimeType = this.getSupportedMimeType();
            const options = mimeType ? { mimeType } : {};
            this.mediaRecorder = new MediaRecorder(this.stream, options);
            
            // Handle data available event
            this.mediaRecorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    this.audioChunks.push(event.data);
                }
            };
            
            // Handle errors
            this.mediaRecorder.onerror = (event) => {
                console.error('MediaRecorder error:', event.error);
                this.stopRecording();
            };
            
            // Start recording
            this.mediaRecorder.start(100); // Collect data every 100ms
            
            return true;
        } catch (error) {
            console.error('Error starting recording:', error);
            throw error;
        }
    },
    
    // Stop recording and return audio data
    stopRecording: function() {
        return new Promise((resolve, reject) => {
            if (!this.mediaRecorder || this.mediaRecorder.state === 'inactive') {
                reject(new Error('No active recording'));
                return;
            }
            
            this.mediaRecorder.onstop = async () => {
                try {
                    // Create blob from chunks
                    const mimeType = this.mediaRecorder.mimeType || 'audio/webm';
                    const audioBlob = new Blob(this.audioChunks, { type: mimeType });
                    
                    // Convert blob to byte array
                    const arrayBuffer = await audioBlob.arrayBuffer();
                    const byteArray = new Uint8Array(arrayBuffer);
                    
                    // Stop all tracks
                    if (this.stream) {
                        this.stream.getTracks().forEach(track => track.stop());
                        this.stream = null;
                    }
                    
                    // Clean up
                    this.mediaRecorder = null;
                    this.audioChunks = [];
                    
                    resolve(Array.from(byteArray));
                } catch (error) {
                    reject(error);
                }
            };
            
            this.mediaRecorder.stop();
        });
    },
    
    // Get supported MIME type for recording
    getSupportedMimeType: function() {
        const types = [
            'audio/webm;codecs=opus',
            'audio/webm',
            'audio/ogg;codecs=opus',
            'audio/ogg',
            'audio/mp4',
            'audio/wav'
        ];
        
        for (const type of types) {
            if (MediaRecorder.isTypeSupported(type)) {
                return type;
            }
        }
        
        return null;
    },
    
    // Play audio from base64 or blob
    playAudio: function(audioData, isBase64 = true) {
        return new Promise((resolve, reject) => {
            try {
                const audio = new Audio();
                
                if (isBase64) {
                    audio.src = audioData;
                } else {
                    // If audioData is a blob
                    audio.src = URL.createObjectURL(audioData);
                }
                
                audio.onended = () => {
                    if (!isBase64 && audio.src.startsWith('blob:')) {
                        URL.revokeObjectURL(audio.src);
                    }
                    resolve();
                };
                
                audio.onerror = (error) => {
                    if (!isBase64 && audio.src.startsWith('blob:')) {
                        URL.revokeObjectURL(audio.src);
                    }
                    reject(error);
                };
                
                audio.play().catch(reject);
            } catch (error) {
                reject(error);
            }
        });
    },
    
    // Convert audio file to suitable format for API
    processAudioFile: async function(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            
            reader.onload = async (event) => {
                try {
                    const arrayBuffer = event.target.result;
                    const byteArray = new Uint8Array(arrayBuffer);
                    resolve({
                        data: Array.from(byteArray),
                        mimeType: file.type,
                        fileName: file.name
                    });
                } catch (error) {
                    reject(error);
                }
            };
            
            reader.onerror = () => {
                reject(new Error('Failed to read file'));
            };
            
            reader.readAsArrayBuffer(file);
        });
    },
    
    // Get audio duration from file or blob
    getAudioDuration: function(audioSource) {
        return new Promise((resolve, reject) => {
            const audio = new Audio();
            
            audio.onloadedmetadata = () => {
                resolve(audio.duration * 1000); // Return duration in milliseconds
            };
            
            audio.onerror = () => {
                reject(new Error('Failed to load audio metadata'));
            };
            
            if (audioSource instanceof Blob) {
                audio.src = URL.createObjectURL(audioSource);
            } else if (typeof audioSource === 'string') {
                audio.src = audioSource;
            } else {
                reject(new Error('Invalid audio source'));
            }
        });
    },
    
    // Visualize audio waveform (for future use)
    createWaveformVisualization: function(canvasId, audioData) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        
        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;
        
        // Clear canvas
        ctx.clearRect(0, 0, width, height);
        
        // Draw waveform
        ctx.beginPath();
        ctx.moveTo(0, height / 2);
        
        const step = Math.ceil(audioData.length / width);
        for (let i = 0; i < width; i++) {
            const min = 1.0;
            const max = -1.0;
            
            for (let j = 0; j < step; j++) {
                const datum = audioData[(i * step) + j];
                if (datum < min) min = datum;
                if (datum > max) max = datum;
            }
            
            const y1 = ((1 + min) / 2) * height;
            const y2 = ((1 + max) / 2) * height;
            
            ctx.lineTo(i, y1);
            ctx.lineTo(i, y2);
        }
        
        ctx.stroke();
    },
    
    // WebRTC support for real-time audio (placeholder for future implementation)
    realtimeAudio: {
        peerConnection: null,
        localStream: null,
        
        // Initialize WebRTC connection
        initialize: async function(configuration) {
            // This would be implemented when connecting to real-time audio endpoints
            // console.log('Real-time audio initialization placeholder');
        },
        
        // Clean up WebRTC resources
        cleanup: function() {
            if (this.peerConnection) {
                this.peerConnection.close();
                this.peerConnection = null;
            }
            if (this.localStream) {
                this.localStream.getTracks().forEach(track => track.stop());
                this.localStream = null;
            }
        }
    }
};

// Initialize when the script loads
window.audioSupport.initialize();