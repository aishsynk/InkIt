import re

with open('src/ScreenCanvas/UI/ToolbarWindow.xaml', 'r', encoding='utf-8') as f:
    content = f.read()

# I will just write a regex or a quick string replacement to reorder the XML blocks.
# Actually it is easier to extract each block by Name and then stitch them together.

blocks = {}
button_names = ['CursorButton', 'PenButton', 'HighlighterButton', 'EraserButton', 'EyedropperButton', 'ShapesButton', 'TextButton', 'PresentButton', 'SpotlightButton', 'ZoomButton', 'CaptureButton', 'RecordButton', 'BlurPixelateButton', 'UndoButton', 'RedoButton', 'ColorChipButton', 'ClearButton', 'MoreButton', 'CollapseButton']

for name in button_names:
    pattern = r'<!--[^>]*-->\s*<Button x:Name="' + name + r'"[\s\S]*?</Button>'
    match = re.search(pattern, content)
    if match:
        blocks[name] = match.group(0)
    else:
        # Fallback without comment
        pattern = r'<Button x:Name="' + name + r'"[\s\S]*?</Button>'
        match = re.search(pattern, content)
        if match:
            blocks[name] = match.group(0)

# Build the new sequence
new_sequence = [
    blocks['CursorButton'],
    '<Border x:Name="Sep1" Width="1" Height="13" VerticalAlignment="Center" Background="{DynamicResource SeparatorBrush}" Margin="2,0,2,0"/>',
    blocks['PenButton'],
    blocks['HighlighterButton'],
    blocks['ShapesButton'],
    blocks['TextButton'],
    blocks['ColorChipButton'],
    '<Border x:Name="Sep2" Width="1" Height="13" VerticalAlignment="Center" Background="{DynamicResource SeparatorBrush}" Margin="2,0,2,0"/>',
    blocks['EraserButton'],
    blocks['UndoButton'],
    blocks['RedoButton'],
    blocks['ClearButton'],
    '<Border x:Name="Sep3" Width="1" Height="13" VerticalAlignment="Center" Background="{DynamicResource SeparatorBrush}" Margin="2,0,2,0"/>',
    blocks['PresentButton'],
    blocks['SpotlightButton'],
    blocks['ZoomButton'],
    '<Border x:Name="Sep4" Width="1" Height="13" VerticalAlignment="Center" Background="{DynamicResource SeparatorBrush}" Margin="2,0,2,0"/>',
    blocks['CaptureButton'],
    blocks['RecordButton'],
    blocks['BlurPixelateButton'],
    blocks['EyedropperButton'],
    '<Border x:Name="Sep5" Width="1" Height="13" VerticalAlignment="Center" Background="{DynamicResource SeparatorBrush}" Margin="2,0,2,0"/>',
    blocks['MoreButton'],
    blocks['CollapseButton']
]

# Find where to replace
start_marker = '<!-- ═══ GROUP: Navigation ═══ -->\n          <Border x:Name="Sep0" Width="1" Height="13" VerticalAlignment="Center" Background="{DynamicResource SeparatorBrush}" Margin="2,0,2,0"/>'
end_marker = '</StackPanel>'

start_idx = content.find(start_marker)
end_idx = content.rfind(end_marker)

if start_idx != -1 and end_idx != -1:
    new_content = content[:start_idx] + start_marker + '\n\n' + '\n\n'.join(new_sequence) + '\n        ' + content[end_idx:]
    # Now fix the RecordIcon icon data
    new_content = new_content.replace('Data="{StaticResource Fluent.Circle.Filled}" Fill="#E5484D"', 'Data="{StaticResource Fluent.Video.Regular}"')
    
    with open('src/ScreenCanvas/UI/ToolbarWindow.xaml', 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Reordered successfully.")
else:
    print("Could not find markers.")
