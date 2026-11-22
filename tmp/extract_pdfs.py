import sys
from pypdf import PdfReader
paths = [r'C:\Users\marco\Downloads\dbe6bd25-f644-4744-bcc2-d646430034c4.pdf', r'C:\Users\marco\Downloads\Tabela de armas magicas guilda.pdf']
for path in paths:
    reader = PdfReader(path)
    print(f'=== {path} ({len(reader.pages)} pages) ===')
    for i, page in enumerate(reader.pages, 1):
        text = page.extract_text() or ''
        print(f'--- PAGE {i} ---')
        print(text[:12000])
