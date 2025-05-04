#\!/bin/bash

# Script to rename client files and update class names

# Working directory: ConduitLLM.Providers
cd ConduitLLM.Providers || { echo "Failed to change directory"; exit 1; }

# Process each *ClientRevised.cs file
for file in *ClientRevised.cs; do
  # Skip if no files match
  [ -e "$file" ] || continue
  
  # Generate the new filename
  newfile="${file/Revised/}"
  
  # Extract the class name (without Revised)
  classname="${file%Revised.cs}"
  
  echo "Processing $file -> $newfile"
  
  # Update class name in the file
  sed -i "s/public class ${classname}Revised/public class ${classname}/g" "$file"
  
  # Update constructor name in the file
  sed -i "s/public ${classname}Revised/public ${classname}/g" "$file"
  
  # Update logger type in the file
  sed -i "s/ILogger<${classname}Revised>/ILogger<${classname}>/g" "$file"
  
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
