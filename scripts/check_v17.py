import sys, openpyxl
sys.stdout.reconfigure(encoding='utf-8')
wb = openpyxl.load_workbook(r'C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\ATM-Grid-v19.xlsx')
for sn in ['MCL ATM Grid $400', 'MCL ATM Grid $200']:
    ws = wb[sn]
    print(f'\n=== {sn} | {ws.dimensions} ===')
    for i, row in enumerate(ws.iter_rows(values_only=True), 1):
        vals = [v for v in row if v is not None]
        if vals:
            print(f'  R{i:02d}: {list(row)[:16]}')
