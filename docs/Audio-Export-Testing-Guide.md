# Audio Usage Export Testing Guide

This guide provides instructions for manually testing the new audio usage export functionality.

## Prerequisites

1. Conduit Admin API and WebUI running
2. Some audio usage data in the system (from transcription, TTS, or realtime operations)
3. Valid master key for API access

## Testing the Backend API

### 1. Test CSV Export

```bash
# Export all usage data as CSV
curl -X GET "http://localhost:5000/api/admin/audio/usage/export?format=csv" \
  -H "X-Master-Key: your-master-key" \
  -o audio_usage_export.csv

# Export with date range filter
curl -X GET "http://localhost:5000/api/admin/audio/usage/export?format=csv&startDate=2024-01-01&endDate=2024-12-31" \
  -H "X-Master-Key: your-master-key" \
  -o audio_usage_filtered.csv
```

**Expected Result:** CSV file with headers and usage data rows

### 2. Test JSON Export

```bash
# Export all usage data as JSON
curl -X GET "http://localhost:5000/api/admin/audio/usage/export?format=json" \
  -H "X-Master-Key: your-master-key" \
  -o audio_usage_export.json

# Export with provider filter
curl -X GET "http://localhost:5000/api/admin/audio/usage/export?format=json&provider=openai" \
  -H "X-Master-Key: your-master-key" \
  -o audio_usage_openai.json
```

**Expected Result:** JSON array with usage objects

### 3. Test Error Handling

```bash
# Test unsupported format
curl -X GET "http://localhost:5000/api/admin/audio/usage/export?format=xml" \
  -H "X-Master-Key: your-master-key"
```

**Expected Result:** 400 Bad Request with error message

## Testing the WebUI

### 1. Access Audio Usage Dashboard

1. Navigate to `http://localhost:5001/audio-usage` in your browser
2. Login with valid credentials

### 2. Test CSV Export

1. Set desired date range and filters
2. Click the **"Export CSV"** button
3. Verify file download starts automatically
4. Open the downloaded CSV file and verify data format

### 3. Test JSON Export

1. Set desired date range and filters
2. Click the **"Export JSON"** button  
3. Verify file download starts automatically
4. Open the downloaded JSON file and verify data format

### 4. Test Different Filters

1. Filter by virtual key, provider, or date range
2. Export data in both formats
3. Verify exported data respects the applied filters

## Validation Checklist

- [ ] CSV export downloads with correct filename format: `audio_usage_YYYYMMDD_YYYYMMDD.csv`
- [ ] JSON export downloads with correct filename format: `audio_usage_YYYYMMDD_YYYYMMDD.json`
- [ ] CSV contains proper headers: Timestamp, VirtualKey, Provider, Operation, Model, Duration, Cost, Status, Language, Voice
- [ ] JSON contains properly formatted objects with all fields
- [ ] Date range filters work correctly
- [ ] Provider filters work correctly
- [ ] Virtual key filters work correctly
- [ ] Error messages display for failed exports
- [ ] Success notifications appear for successful exports
- [ ] Both export buttons are visible and functional
- [ ] Backend export is used instead of client-side generation

## Common Issues

1. **Empty Export**: Ensure there is audio usage data for the selected date range and filters
2. **Download Fails**: Check browser console for JavaScript errors
3. **API Errors**: Verify Admin API is running and master key is correct
4. **Format Issues**: Verify the export service implementation supports both CSV and JSON

## File Format Examples

### CSV Format
```
Timestamp,VirtualKey,Provider,Operation,Model,Duration,Cost,Status,Language,Voice
2024-01-01 10:00:00,key_abc123,openai,transcription,whisper-1,120.5,0.0025,200,en,
2024-01-01 10:01:15,key_abc123,openai,tts,tts-1,,0.0015,200,en,alloy
```

### JSON Format
```json
[
  {
    "timestamp": "2024-01-01T10:00:00Z",
    "virtualKey": "key_abc123",
    "provider": "openai",
    "operation": "transcription",
    "model": "whisper-1",
    "duration": 120.5,
    "cost": 0.0025,
    "status": 200,
    "language": "en",
    "voice": null,
    "error": null
  }
]
```