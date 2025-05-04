#!/bin/bash

# Update all Model-related return types in provider implementations
for file in $(find /home/nick/Conduit/ConduitLLM.Providers -name "*ClientRevised.cs"); do
    echo "Processing $file"
    
    # Fix GetModelsAsync return type
    sed -i 's/Task<List<ModelInfo>> GetModelsAsync/Task<List<ExtendedModelInfo>> GetModelsAsync/g' "$file"
    
    # Fix GetFallbackModels return type
    sed -i 's/List<ModelInfo> GetFallbackModels/List<ExtendedModelInfo> GetFallbackModels/g' "$file"
    
    # Fix ModelInfo instantiations within methods
    sed -i 's/new ModelInfo/new ExtendedModelInfo/g' "$file"
    
    # Fix return new List<ModelInfo> statements
    sed -i 's/return new List<ModelInfo>/return new List<ExtendedModelInfo>/g' "$file"
    
    # Fix .Select(m => new ModelInfo
    sed -i 's/\.Select(m => new ModelInfo/\.Select(m => new ExtendedModelInfo/g' "$file"
done

echo "All files processed"