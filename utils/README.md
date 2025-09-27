# Utility scripts

> collection of utility scripts for various tasks

## Translate Helper

> A script to help with translations by reading the target language, comparing with the base language, and going through missing keys.

### Usage

```bash
python translate_helper.py pt_BR [--check]
```

- `pt_BR` is the target language code (change as needed)
- `--check` is an optional flag to only check for missing keys without prompting for translations

The script will read the base language file (`en_US.axaml`) and the target language file (e.g., `pt_BR.axaml`), identify missing keys, and prompt you to provide translations for those keys. If the `--check` flag is used, it will only list the missing keys without prompting for translations.
