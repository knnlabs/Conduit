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