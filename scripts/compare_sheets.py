import sys
import openpyxl
sys.stdout.reconfigure(encoding='utf-8')

# Get all style details from v14 MES $400 sheet
wb = openpyxl.load_workbook(r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\ATM-Grid-v14.xlsx')
ws = wb['MES ATM Grid $400']

print('=== v14 MES $400 full style inventory ===')
for i, row in enumerate(ws.iter_rows(min_row=1, max_row=22), 1):
    for j, cell in enumerate(row, 1):
        if cell.value is None and (not cell.fill or cell.fill.fgColor.rgb in ('00000000','FF000000')):
            continue
        fill = '?'
        try:
            fill = cell.fill.fgColor.rgb
        except: pass
        font_bold = cell.font.bold if cell.font else False
        font_color = '?'
        try:
            font_color = cell.font.color.rgb
        except: pass
        font_size = cell.font.size if cell.font else None
        align_h = cell.alignment.horizontal if cell.alignment else None
        print(f'  [{i},{j}] val={repr(str(cell.value)[:40]) if cell.value else None}  fill={fill}  bold={font_bold}  fc={font_color}  sz={font_size}  align={align_h}')
