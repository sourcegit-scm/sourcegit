from pathlib import Path

path = Path("src/Views/Preferences.axaml")
text = path.read_text(encoding="utf-8-sig")

if 'Text="Backend"' in text and 'LocalBackendIndex' in text:
    raise SystemExit(0)

local_marker = '<StackPanel IsVisible="{Binding IsLocalLlm}">'
temperature = '<TextBlock Margin="0,12,0,0" Text="Temperature"/>'
local_start = text.find(local_marker)
if local_start < 0:
    raise SystemExit("LocalLLM settings panel not found")

anchor = text.find(temperature, local_start)
if anchor < 0:
    raise SystemExit("LocalLLM temperature control not found")

line_start = text.rfind("\n", 0, anchor) + 1
indent = text[line_start:anchor]
controls = [
    '<TextBlock Margin="0,12,0,0" Text="Backend"/>',
    '<ComboBox Margin="0,4,0,0" Height="28" SelectedIndex="{Binding LocalBackendIndex, Mode=TwoWay}">',
    '  <ComboBoxItem Content="Auto"/>',
    '  <ComboBoxItem Content="CPU / Metal"/>',
    '  <ComboBoxItem Content="CUDA"/>',
    '  <ComboBoxItem Content="Vulkan"/>',
    '</ComboBox>',
    '',
    '<TextBlock Margin="0,12,0,0" Text="GPU Layers (-1 = all)"/>',
    '<NumericUpDown Margin="0,4,0,0" Height="28" Minimum="-1" Maximum="999" Increment="1" Value="{Binding GpuLayerCount, Mode=TwoWay}"/>',
    '',
    '<TextBlock Margin="0,12,0,0" Text="Threads"/>',
    '<NumericUpDown Margin="0,4,0,0" Height="28" Minimum="1" Maximum="256" Increment="1" Value="{Binding LocalThreads, Mode=TwoWay}"/>',
    '',
    '<TextBlock Margin="0,12,0,0" Text="Batch Size"/>',
    '<NumericUpDown Margin="0,4,0,0" Height="28" Minimum="1" Maximum="4096" Increment="64" Value="{Binding LocalBatchSize, Mode=TwoWay}"/>',
    '',
]
block = "\n".join(indent + line if line else "" for line in controls)
text = text[:line_start] + block + "\n" + text[line_start:]
path.write_text(text, encoding="utf-8")
