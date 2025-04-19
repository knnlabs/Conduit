/**
 * Function to download a file from a string
 * @param {string} fileName - The name of the file to download
 * @param {string} contentType - The MIME type of the file content
 * @param {string} content - The content of the file as a string
 */
function downloadFile(fileName, contentType, content) {
    const blob = new Blob([content], { type: contentType });
    const url = URL.createObjectURL(blob);
    
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    
    // Cleanup
    setTimeout(() => {
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }, 100);
}
