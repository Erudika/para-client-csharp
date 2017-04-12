#!/bin/bash
read -e -p "Tag: " ver
git add -A && git commit -m "Release v$ver."
git tag "v$ver"
git push origin master && git push --tags
