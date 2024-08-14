#!/bin/bash

set -e

if [ -z "$VERSION" ]; then
    echo "Provide the version as environment variable VERSION"
    exit 1
fi

if [ -z "$RUNTIME" ]; then
    echo "Provide the runtime as environment variable RUNTIME"
    exit 1
fi

cd build

rm -rf SourceGit/*.pdb

zip "sourcegit_$VERSION.$RUNTIME.zip" -r SourceGit
