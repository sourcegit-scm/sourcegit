from pathlib import Path

path = Path("src/Views/Preferences.axaml")
text = path.read_text(encoding="utf-8-sig")
needle = '                        <ComboBoxItem Content="CUDA"/>\n'

if needle not in text:
    raise SystemExit(0)

path.write_text(text.replace(needle, "", 1), encoding="utf-8")
