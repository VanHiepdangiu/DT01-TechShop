"""Dependency-free asset contract checks; these are not browser integration tests."""
import hashlib
import json
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
WEB = ROOT / 'wwwroot'

class AssetContracts(unittest.TestCase):
    def test_css_responsive_contract(self):
        css = (WEB / 'css/shop.css').read_text()
        classes = 'site-header brand nav-links user-menu shell hero hero-copy hero-art eyebrow catalog-toolbar category-pills product-grid product-card product-image product-body price old-price stock btn btn-primary btn-dark btn-outline badge notice error page-head panel table-wrap field-grid form-control auth-shell auth-art auth-panel auth-tabs auth-pane summary checkout-grid stat-grid stat footer spec-table order-status timeline empty compare-grid'.split()
        for name in classes:
            self.assertRegex(css, r'\.' + re.escape(name) + r'(?![\w-])')
        self.assertIn('.auth-pane.active', css)
        self.assertIn('1200px', css)
        self.assertIn('repeat(4,', css)
        self.assertIn('@media', css)
        self.assertNotIn('@import', css)

    def test_svg_valid_and_self_contained(self):
        for name in ['laptop', 'phone', 'accessories']:
            root = ET.parse(WEB / f'images/{name}.svg').getroot()
            self.assertEqual(root.tag, '{http://www.w3.org/2000/svg}svg')
            self.assertIn('viewBox', root.attrib)
            self.assertIsNotNone(root.find('{http://www.w3.org/2000/svg}title'))
            self.assertNotIn('http', ''.join(e.attrib.get('href', '') for e in root.iter()))

    def test_admin_contract_and_transitions(self):
        js = (WEB / 'js/admin.js').read_text()
        for endpoint in ['/api/reports/revenue', '/api/reports/top-products', '/api/reports/low-stock', '/api/orders']:
            self.assertIn(endpoint, js)
        for token in ['createElement', 'createRoot', 'csrf-token', 'X-CSRF-TOKEN', 'rowVersion', 'credentials', 'AbortController', 'response.ok']:
            self.assertIn(token, js)
        transitions = json.loads(re.search(r'var NEXT_STATUS = (\{[^;]+\});', js).group(1))
        self.assertEqual(transitions, {'PendingPayment':['Cancelled'], 'New':['Packing','Cancelled'], 'Packing':['Shipping'], 'Shipping':['Completed'], 'Completed':[], 'Cancelled':[], 'Returned':[]})
        self.assertIn("note: ''", js)
        self.assertIn('method: \'PUT\'', js)
        self.assertNotIn('dangerouslySetInnerHTML', js)

    def test_vite_equivalent_to_umd(self):
        umd = (WEB / 'js/admin.js').read_text()
        body = umd.split('/* APP_START */\n', 1)[1].split('\n/* APP_END */', 1)[0]
        main = (ROOT / 'ClientApp/src/main.js').read_text()
        self.assertIn(body, main)
        pkg = json.loads((ROOT / 'ClientApp/package.json').read_text())
        self.assertEqual(pkg['dependencies']['react'], '18.3.1')
        self.assertEqual(pkg['dependencies']['react-dom'], '18.3.1')

    def test_vendor_downloads_match_http_manifest(self):
        manifest = json.loads((WEB / 'lib/react/downloads.json').read_text())
        for item in manifest:
            data = (WEB / 'lib/react' / item['file']).read_bytes()
            self.assertEqual(item['httpStatus'], 200)
            self.assertEqual(hashlib.sha256(data).hexdigest(), item['sha256'])
            self.assertEqual(len(data), item['bytes'])
            self.assertIn('18.3.1', item['url'])
            self.assertIn('cdn.jsdelivr.net', item['url'])
        self.assertIn('MIT License', (WEB / 'lib/react/LICENSE.txt').read_text())

if __name__ == '__main__':
    unittest.main(verbosity=2)
