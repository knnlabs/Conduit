#!/usr/bin/env python3
import os
import re
import glob

def remove_auth_from_file(file_path):
    """Remove authentication imports and checks from a TypeScript file."""
    try:
        with open(file_path, 'r') as f:
            content = f.read()
        
        original_content = content
        
        # Remove auth imports
        auth_imports = [
            r"import\s*\{\s*requireAuth\s*\}\s*from\s*['\"]@/lib/auth/simple-auth['\"];\s*\n?",
            r"import\s*\{\s*requireAdmin\s*\}\s*from\s*['\"]@/lib/auth/simple-auth['\"];\s*\n?",
            r"import\s*\{\s*requireAuth,\s*requireAdmin\s*\}\s*from\s*['\"]@/lib/auth/simple-auth['\"];\s*\n?",
            r"import\s*\{\s*requireAdmin,\s*requireAuth\s*\}\s*from\s*['\"]@/lib/auth/simple-auth['\"];\s*\n?",
        ]
        
        for pattern in auth_imports:
            content = re.sub(pattern, '', content, flags=re.MULTILINE)
        
        # Remove auth check blocks from function starts
        # Pattern: function_declaration { auth_check_block
        auth_check_patterns = [
            r'(export\s+async\s+function\s+\w+\([^)]*\)\s*\{\s*)\s*const\s+auth\s*=\s*requireAuth\([^)]*\);\s*if\s*\(\s*!auth\.isValid\s*\)\s*\{\s*return\s+auth\.response!\s*;\s*\}',
            r'(export\s+async\s+function\s+\w+\([^)]*\)\s*\{\s*)\s*const\s+auth\s*=\s*requireAdmin\([^)]*\);\s*if\s*\(\s*!auth\.isValid\s*\)\s*\{\s*return\s+auth\.response!\s*;\s*\}',
        ]
        
        for pattern in auth_check_patterns:
            content = re.sub(pattern, r'\1', content, flags=re.MULTILINE | re.DOTALL)
        
        # Clean up extra whitespace
        content = re.sub(r'\n\s*\n\s*\n', '\n\n', content)
        
        if content != original_content:
            with open(file_path, 'w') as f:
                f.write(content)
            print(f"✓ Removed auth from {file_path}")
            return True
        else:
            return False
    except Exception as e:
        print(f"✗ Error processing {file_path}: {e}")
        return False

def main():
    # Find all TypeScript files in the API directory
    api_dir = "/home/nbn/Code/Conduit/ConduitLLM.WebUI/src/app/api"
    ts_files = glob.glob(f"{api_dir}/**/*.ts", recursive=True)
    
    print(f"Found {len(ts_files)} TypeScript files in API directory")
    
    modified_count = 0
    for file_path in ts_files:
        if remove_auth_from_file(file_path):
            modified_count += 1
    
    print(f"\nModified {modified_count} files")

if __name__ == "__main__":
    main()