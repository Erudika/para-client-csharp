#!/bin/bash
lastver=$(git describe --abbrev=0 --tags)
echo "Last tag was: $lastver"
echo "---"
read -e -p "Tag: " ver

sed -i -e "s/>$lastver</>$ver</g" para-client-csharp/para-client-csharp.csproj

git add -A && git commit -m "Release v$ver."
git tag "v$ver"
git push origin master && git push --tags
