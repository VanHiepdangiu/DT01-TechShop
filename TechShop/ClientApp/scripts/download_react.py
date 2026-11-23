"""Download pinned React UMD assets with requests; no package installation."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'wwwroot/lib/react'
DEST.mkdir(parents=True, exist_ok=True)
ASSETS = {
    'react.production.min.js': 'https://cdn.jsdelivr.net/npm/react@18.3.1/umd/react.production.min.js',
    'react-dom.production.min.js': 'https://cdn.jsdelivr.net/npm/react-dom@18.3.1/umd/react-dom.production.min.js',
}
session = requests.Session()
session.mount('https://', HTTPAdapter(max_retries=Retry(total=3, backoff_factor=0.5, status_forcelist=[429, 500, 502, 503, 504])))
records = []
for filename, url in ASSETS.items():
    response = session.get(url, timeout=60)
    response.raise_for_status()
    data = response.content
    if response.status_code != 200 or len(data) < 5000 or b'18.3.1' not in data or b'<html' in data[:100].lower():
        raise RuntimeError('Unexpected JavaScript response: ' + url)
    (DEST / filename).write_bytes(data)
    record = {'file': filename, 'url': url, 'httpStatus': response.status_code, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'verifiedAtUtc': datetime.now(timezone.utc).isoformat()}
    records.append(record)
    print(json.dumps(record))
license_url = 'https://cdn.jsdelivr.net/npm/react@18.3.1/LICENSE'
license_response = session.get(license_url, timeout=60)
license_response.raise_for_status()
if 'MIT License' not in license_response.text:
    raise RuntimeError('Unexpected upstream license')
license_text = '\n'.join([
    'React and ReactDOM 18.3.1 — locally vendored UMD production builds.',
    'Source: jsDelivr npm CDN. Downloaded using Python requests over verified HTTPS.',
    'HTTP 200 and version markers checked; exact byte counts and SHA-256 hashes are in downloads.json.',
    'Both React and ReactDOM are licensed under MIT by Meta Platforms, Inc. and affiliates.',
    'No CDN connection is required at application runtime.',
    *[f"{r['file']}: {r['url']} (HTTP {r['httpStatus']}, {r['bytes']} bytes)" for r in records],
    f'License source: {license_url} (HTTP {license_response.status_code})',
    '', license_response.text,
])
(DEST / 'LICENSE.txt').write_text(license_text, encoding='utf-8')
(DEST / 'downloads.json').write_text(json.dumps(records, indent=2) + '\n', encoding='utf-8')
