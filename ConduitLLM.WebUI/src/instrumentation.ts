export async function register() {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    // Only run on server
    console.log('[Instrumentation] Server starting...');
    
    // SignalR has been disabled - we're using simple fetch() calls instead
    // All real-time functionality has been removed in favor of dead-simple REST APIs
    
    console.log('[Instrumentation] Server ready');
  }
}