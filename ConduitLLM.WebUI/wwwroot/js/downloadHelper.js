/**
 * Helper function to download a file
 * @param {string} fileName - Name of the file to download
 * @param {string} content - Content of the file
 * @param {string} contentType - MIME type of the file
 */
window.downloadFile = function(fileName, content, contentType) {
    // Create a blob with the content
    const blob = new Blob([content], { type: contentType });
    
    // Create a URL for the blob
    const url = URL.createObjectURL(blob);
    
    // Create a temporary anchor element
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.style.display = 'none';
    
    // Add the anchor to the document
    document.body.appendChild(a);
    
    // Click the anchor to start the download
    a.click();
    
    // Clean up
    setTimeout(() => {
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }, 100);
};

/**
 * Helper function to download a file from a URL
 * @param {string} url - URL to download from
 * @param {string} fileName - Optional filename to use, defaults to the URL's filename
 */
window.downloadFromUrl = function(url, fileName) {
    // Create a temporary anchor element
    const a = document.createElement('a');
    a.href = url;
    
    // Use provided filename or extract from URL
    if (fileName) {
        a.download = fileName;
    }
    
    a.style.display = 'none';
    
    // Add the anchor to the document
    document.body.appendChild(a);
    
    // Click the anchor to start the download
    a.click();
    
    // Clean up
    setTimeout(() => {
        document.body.removeChild(a);
    }, 100);
};

/**
 * Helper function to trigger a file upload dialog
 * @param {string} accept - MIME types to accept, e.g. ".db,.json"
 * @param {Function} dotNetReference - .NET reference to call when a file is selected
 * @param {string} methodName - Name of the method to call on the .NET reference
 */
window.uploadFile = function(accept, dotNetReference, methodName) {
    // Create a file input element
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.style.display = 'none';
    
    // Handle file selection
    input.onchange = async function() {
        if (input.files && input.files[0]) {
            const file = input.files[0];
            
            try {
                // Read file as array buffer
                const arrayBuffer = await file.arrayBuffer();
                
                // Convert to base64
                const base64 = btoa(
                    new Uint8Array(arrayBuffer)
                        .reduce((data, byte) => data + String.fromCharCode(byte), '')
                );
                
                // Call .NET method with file info
                await dotNetReference.invokeMethodAsync(methodName, {
                    fileName: file.name,
                    contentType: file.type,
                    size: file.size,
                    base64Content: base64
                });
            } catch (error) {
                console.error('Error handling file upload:', error);
            }
            
            // Clean up
            document.body.removeChild(input);
        }
    };
    
    // Add the input to the document
    document.body.appendChild(input);
    
    // Trigger the file dialog
    input.click();
};