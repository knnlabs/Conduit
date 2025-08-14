export async function register() {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    // Only run on server
    // Server starting - instrumentation initialized
    
    // SignalR has been disabled - we're using simple fetch() calls instead
    // All real-time functionality has been removed in favor of dead-simple REST APIs
    
    // Server ready - instrumentation complete
  }
}