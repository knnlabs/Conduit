#\!/bin/bash

# Script to rename client names in test files

# Working directory: ConduitLLM.Tests/Providers
cd ConduitLLM.Tests/Providers || { echo "Failed to change directory"; exit 1; }

# Process each *ClientRevisedTests.cs file
for file in *ClientRevisedTests.cs; do
  # Skip if no files match
  [ -e "$file" ] || continue
  
  # Generate the new filename
  newfile="${file/Revised/}"
  
  # Extract the client name (without Revised)
  clientname="${file%RevisedTests.cs}"
  
  echo "Processing $file -> $newfile"
  
  # Update class names in the file
  sed -i "s/public class ${clientname}RevisedTests/public class ${clientname}Tests/g" "$file"
  sed -i "s/public ${clientname}RevisedTests(/public ${clientname}Tests(/g" "$file"
  
  # Update logger type in the file
  sed -i "s/ILogger<${clientname}Revised>/ILogger<${clientname}>/g" "$file"
  
  # Update Mock<ILogger> creation
  sed -i "s/new Mock<ILogger<${clientname}Revised>>/new Mock<ILogger<${clientname}>>/g" "$file"
  
  # Update client instantiation
  sed -i "s/new ${clientname}Revised(/new ${clientname}(/g" "$file"
  
  # Update reflection references
  sed -i "s/typeof(${clientname}Revised)/typeof(${clientname})/g" "$file"
  
  # If the target file already exists, make a backup
  if [ -f "$newfile" ]; then
    echo "Backing up existing $newfile to ${newfile}.bak"
    mv "$newfile" "${newfile}.bak"
  fi
  
  # Rename the file 
  mv "$file" "$newfile"
  
  echo "Completed: $file -> $newfile"
done

echo "All files processed"
