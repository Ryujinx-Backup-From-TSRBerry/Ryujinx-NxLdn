#!/bin/bash

script_dir=$(dirname $(realpath ${BASH_SOURCE[0]}))
nxldn_dir=$(realpath $script_dir/../)

echo $nxldn_dir

git stash -u

cd $1

git pull
dotnet build -c Release

rsync -avP --delete \
    --exclude ".git" \
    --exclude ".github" \
    --exclude "README.md" \
    --exclude "LICENSE.txt" \
    --exclude "**/bin" \
    --exclude "**/obj" \
    ./ $nxldn_dir

cd -

git restore .
git stash apply
