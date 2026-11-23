"""Keep optional Vite entry identical to the shipped UMD dashboard body."""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
umd = (ROOT / 'wwwroot/js/admin.js').read_text(encoding='utf-8')
body = umd.split('/* APP_START */\n', 1)[1].split('\n/* APP_END */', 1)[0]
entry = "import React from 'react';\nimport * as ReactDOM from 'react-dom/client';\nimport '../../wwwroot/css/shop.css';\n\nconst host = document.getElementById('admin-app');\nif (host) {\n" + body + '\n}\n'
(ROOT / 'ClientApp/src').mkdir(parents=True, exist_ok=True)
(ROOT / 'ClientApp/src/main.js').write_text(entry, encoding='utf-8')
print('Synchronized ClientApp/src/main.js from wwwroot/js/admin.js')
