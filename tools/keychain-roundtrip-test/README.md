# keychain-roundtrip-test

A standalone check for `MacOSKeychain` (`src/Native/MacOSKeychain.cs`), the macOS
backend that stores AI service API keys in the login Keychain instead of in
preference.json.

It stores, reads back, updates, and deletes a generic-password item under the
throwaway service name `SourceGit-kctest`, asserting the value round-trips
exactly (including quotes, backslashes, and non-ASCII characters). It cleans up
the item it creates.

macOS only. It touches the current user's login Keychain.

## Run

```sh
dotnet run -c Release
```

Exit code 0 on success, 1 otherwise.
